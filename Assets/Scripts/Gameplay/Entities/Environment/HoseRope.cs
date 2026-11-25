using System;
using System.Collections.Generic;
using Gameplay.Entities.Interactive;
using UnityEngine;

namespace Gameplay.Entities.Environment
{
    [Serializable]
    public sealed class HoseRope
    {
        private const int MaxHits = 64;
        private const float MinMove = 0.0001f;
        private const float MinFeed = 0.05f;
        private const int PinnedPoints = 1;
        private const float ReferenceRate = 60f;

        [SerializeField] private float _segmentLength = 0.3f;
        [SerializeField] private float _maxLength = 25f;
        [SerializeField] private float _radius = 0.045f;
        [SerializeField, Min(2)] private int _minPoints = 3;
        [SerializeField] private float _teleportDistance = 4f;

        [Header("Simulation")]
        [SerializeField] private float _gravity = 20f;
        [SerializeField, Range(0f, 1f)] private float _drag = 0.06f;
        [SerializeField, Range(0f, 1f)] private float _groundFriction = 0.6f;
        [SerializeField, Min(1)] private int _iterations = 24;
        [SerializeField, Range(0f, 1f)] private float _bendStiffness = 0.8f;
        [SerializeField] private float _minBendRadius = 0.45f;
        [SerializeField, Min(1)] private int _bendSpan = 2;
        [SerializeField, Range(0f, 1f)] private float _endStiffness = 0.5f;
        [SerializeField] private float _stepsPerSecond = 60f;
        [SerializeField, Min(1)] private int _maxStepsPerFrame = 3;

        [Header("Feeding from the truck")]
        [SerializeField] private float _tautMargin = 0.02f;
        [SerializeField] private float _stretchLimit = 0.15f;
        [SerializeField] private float _payoutSpeed = 12f;
        [SerializeField, Min(1)] private int _slackStride = 6;
        [SerializeField] private float _tensionRange = 0.4f;
        [SerializeField] private float _splitStretch = 1.6f;
        [SerializeField] private float _mergeChord = 1.3f;

        [Header("Collision")]
        [SerializeField] private float _skin = 0.01f;
        [SerializeField, Range(0f, 1f)] private float _groundNormal = 0.5f;
        [SerializeField, Min(0)] private int _clearSearchSteps = 4;
        [SerializeField] private float _cornerCut = 0.06f;
        [SerializeField] private LayerMask _mask = ~0;

        private readonly RaycastHit[] _hits = new RaycastHit[MaxHits];
        private readonly Dictionary<Collider, bool> _solid = new();
        private Vector3[] _points;
        private Vector3[] _previous;
        private Vector3[] _stepStart;
        private bool[] _grounded;
        private float _feed;
        private float _time;
        private float _takeUpDebt;
        private Transform _ignored;

        public int Count { get; private set; }
        public int Capacity => _points.Length;
        public float Radius => _radius;
        public float Tension { get; private set; }
        public Vector3 Source { get; private set; }
        public float Length => (Count - 1) * _segmentLength + _feed;
        public Vector3 PullDirection => (_points[Mathf.Min(2, Count - 1)] - _points[0]).normalized;
        public Vector3 this[int index] => _points[index];

        public Vector3 PointAlong(float distance)
        {
            for (int i = 1; i < Count; i++)
            {
                float span = Vector3.Distance(_points[i - 1], _points[i]);
                if (distance <= span)
                {
                    return Vector3.Lerp(_points[i - 1], _points[i], span > 0f ? distance / span : 1f);
                }

                distance -= span;
            }

            return _points[Count - 1];
        }

        public void Init(Vector3 source, Vector3 end, Transform ignored)
        {
            _ignored = ignored;
            int capacity = Mathf.CeilToInt(_maxLength / _segmentLength) + 2;
            _points = new Vector3[capacity];
            _previous = new Vector3[capacity];
            _stepStart = new Vector3[capacity];
            _grounded = new bool[capacity];
            Lay(source, end);
        }

        public void Lay(Vector3 source, Vector3 end)
        {
            Source = source;
            float distance = Vector3.Distance(source, end);
            Count = Mathf.Clamp(Mathf.FloorToInt(distance / _segmentLength) + 1, _minPoints, _points.Length);
            float spacing = Mathf.Min(_segmentLength, distance / Count);
            Vector3 direction = distance > 0f ? (source - end) / distance : Vector3.forward;
            for (int i = 0; i < Count; i++)
            {
                _points[i] = end + direction * (spacing * i);
                _previous[i] = _points[i];
                _grounded[i] = false;
            }

            _feed = Mathf.Max(MinFeed, distance - spacing * (Count - 1));
            Tension = 0f;
        }

        public void Tick(Vector3 source, Vector3 end, Vector3 endDirection, float reelInSpeed, float slack, float deltaTime)
        {
            Source = source;
            if (Vector3.Distance(end, _points[0]) > _teleportDistance)
            {
                Lay(source, end);
            }

            float step = 1f / _stepsPerSecond;
            _time += deltaTime;
            for (int i = 0; i < _maxStepsPerFrame && _time >= step; i++)
            {
                _time -= step;
                Step(end, endDirection, step);
                Feed(reelInSpeed, slack, step);
            }

            _time = Mathf.Min(_time, step);
        }

        private void Step(Vector3 end, Vector3 endDirection, float step)
        {
            float nearChord = BendChord(_bendSpan);
            float farChord = BendChord(_bendSpan * 2);
            // Verlet: xₙ₊₁ = xₙ + (xₙ − xₙ₋₁)·(1 − drag) + g·Δt²
            Vector3 fall = Vector3.down * (_gravity * step * step);
            // Drag and friction are set per sixtieth of a second and rescaled, so they act the same at any step rate.
            float rate = step * ReferenceRate;
            float keep = Mathf.Pow(1f - _drag, rate);
            float slide = Mathf.Pow(1f - _groundFriction, rate);
            _stepStart[0] = _points[0];
            for (int i = PinnedPoints; i < Count; i++)
            {
                _stepStart[i] = _points[i];
                Vector3 velocity = (_points[i] - _previous[i]) * keep;
                if (_grounded[i])
                {
                    velocity.x *= slide;
                    velocity.z *= slide;
                }

                _previous[i] = _points[i];
                _points[i] += velocity + fall;
            }

            for (int iteration = 0; iteration < _iterations; iteration++)
            {
                _points[0] = end;
                for (int i = 0; i < Count - 1; i++)
                {
                    Stretch(i, i + 1, _segmentLength, 1f, false, i < PinnedPoints);
                }

                // The hose leaves the nozzle along its barrel, and nowhere folds tighter than its bend radius allows.
                if (Count > 2 && endDirection != Vector3.zero)
                {
                    _points[1] = Vector3.Lerp(_points[1], end + endDirection * _segmentLength, _endStiffness);
                }

                for (int i = 0; i < Count - _bendSpan; i++)
                {
                    Stretch(i, i + _bendSpan, nearChord, _bendStiffness, true, i < PinnedPoints);
                }

                for (int i = 0; i < Count - _bendSpan * 2; i++)
                {
                    Stretch(i, i + _bendSpan * 2, farChord, _bendStiffness, true, i < PinnedPoints);
                }

                HoldAtSource();
            }

            for (int i = PinnedPoints; i < Count; i++)
            {
                Vector3 swept = Sweep(_stepStart[i], _points[i], out _grounded[i], out Vector3 contact);
                _points[i] = KeepClear(i, swept);
                if (_points[i] != swept)
                {
                    _previous[i] = _points[i];
                    continue;
                }

                // A point resting on a surface keeps no speed into it, or it would bounce on the spot every step.
                Vector3 velocity = _points[i] - _previous[i];
                _previous[i] = _points[i] - (velocity - contact * Mathf.Min(0f, Vector3.Dot(velocity, contact)));
            }
        }

        private void Stretch(int a, int b, float rest, float stiffness, bool onlyWhenFolded, bool isFirstFixed)
        {
            Vector3 delta = _points[b] - _points[a];
            float distance = delta.magnitude;
            if (distance <= 0f || (onlyWhenFolded && distance >= rest))
            {
                return;
            }

            Vector3 correction = delta * ((distance - rest) / distance * stiffness);
            if (isFirstFixed)
            {
                _points[b] -= correction;
                return;
            }

            _points[a] += correction * 0.5f;
            _points[b] -= correction * 0.5f;
        }

        // Points a given number of segments apart on an arc of the tightest allowed radius are this far from each other:
        // chord = 2·R·sin(n·s / 2R). Keeping them at least that far apart is what stops the hose folding sharper.
        private float BendChord(int segments) =>
            2f * _minBendRadius * Mathf.Sin(Mathf.Min(Mathf.PI * 0.5f, segments * _segmentLength / (2f * _minBendRadius)));

        private void HoldAtSource()
        {
            int last = Count - 1;
            Vector3 delta = _points[last] - Source;
            float distance = delta.magnitude;
            if (distance > _feed)
            {
                _points[last] = Source + delta * (_feed / distance);
            }
        }

        // No stretch of hose may ever cut through geometry. A point keeps its neighbour nearer the nozzle in sight: if its own
        // move would hide it, the move is cut short. If the neighbour itself slipped round a corner, the point steps into the
        // nearest place the hose ahead of it has just left that the neighbour can still see: the hose slides round the corner.
        private Vector3 KeepClear(int index, Vector3 target)
        {
            Vector3 before = _points[index - 1];
            if (IsClear(before, target))
            {
                return target;
            }

            Vector3 safe = _stepStart[index];
            if (!IsClear(before, safe))
            {
                return Follow(index, before, safe);
            }

            for (int i = 0; i < _clearSearchSteps; i++)
            {
                Vector3 middle = (safe + target) * 0.5f;
                if (IsClear(before, middle))
                {
                    safe = middle;
                }
                else
                {
                    target = middle;
                }
            }

            return safe;
        }

        // The stretch the point shared with its neighbour a step ago was clear, so somewhere along it the neighbour comes back
        // into sight: the point moves only that far, which carries it round the corner a little at a time instead of in a leap.
        private Vector3 Follow(int index, Vector3 before, Vector3 hidden)
        {
            Vector3 seen = _stepStart[index - 1];
            if (IsClear(before, seen))
            {
                for (int i = 0; i < _clearSearchSteps; i++)
                {
                    Vector3 middle = (hidden + seen) * 0.5f;
                    if (IsClear(before, middle))
                    {
                        seen = middle;
                    }
                    else
                    {
                        hidden = middle;
                    }
                }

                return seen;
            }

            for (int ahead = index - 2; ahead >= 0; ahead--)
            {
                if (IsClear(before, _stepStart[ahead]))
                {
                    return _stepStart[ahead];
                }
            }

            return before;
        }

        private void Feed(float reelInSpeed, float slack, float step)
        {
            float stretch = ShortcutLength() - Length;
            bool hasSlack = Length > SnugLength() * slack + _segmentLength;
            Tension = Length >= _maxLength - MinFeed ? Mathf.Clamp01(stretch / _tensionRange) : 0f;
            if (stretch > _stretchLimit || (!hasSlack && stretch > -_tautMargin))
            {
                _feed += Mathf.Min(stretch + _tautMargin, _payoutSpeed * step);
            }
            else if (hasSlack)
            {
                TakeUp(reelInSpeed * step);
            }

            LetOut();

            while (_feed > _segmentLength && Count < _points.Length)
            {
                AddPoint();
            }

            while (_feed < 0f && Count > _minPoints)
            {
                Count--;
                _feed += _segmentLength;
            }

            float floor = Count > _minPoints ? 0f : MinFeed;
            _feed = Mathf.Clamp(_feed, floor, Mathf.Max(floor, _maxLength - (Count - 1) * _segmentLength));
        }

        // Hose is let out where it is being pulled thin rather than only at the truck: a stretch grown too long gains a
        // point in its middle. Nothing then has to slide round the corners between there and the truck to make up the length.
        private void LetOut()
        {
            float limit = _segmentLength * _splitStretch;
            for (int i = Count - 1; i >= 1 && Count < _points.Length && Length + _segmentLength <= _maxLength; i--)
            {
                if (Vector3.Distance(_points[i - 1], _points[i]) > limit)
                {
                    Insert(i, (_points[i - 1] + _points[i]) * 0.5f);
                }
            }
        }

        // Slack is taken up where the hose is bunched: the point whose neighbours are closest together and can see each
        // other is removed. Only when there is no such point is the hose pulled in at the truck instead.
        private void TakeUp(float amount)
        {
            _takeUpDebt += amount;
            while (_takeUpDebt >= _segmentLength)
            {
                _takeUpDebt -= _segmentLength;
                int bunched = -1;
                float nearest = _segmentLength * _mergeChord;
                for (int i = PinnedPoints + 1; i < Count - 1; i++)
                {
                    float chord = Vector3.Distance(_points[i - 1], _points[i + 1]);
                    if (chord < nearest && IsClear(_points[i - 1], _points[i + 1]))
                    {
                        nearest = chord;
                        bunched = i;
                    }
                }

                if (bunched < 0 || Count <= _minPoints)
                {
                    _feed -= _segmentLength;
                    continue;
                }

                Remove(bunched);
            }
        }

        private void Insert(int index, Vector3 point)
        {
            int moved = Count - index;
            Array.Copy(_points, index, _points, index + 1, moved);
            Array.Copy(_previous, index, _previous, index + 1, moved);
            Array.Copy(_stepStart, index, _stepStart, index + 1, moved);
            Array.Copy(_grounded, index, _grounded, index + 1, moved);
            _points[index] = point;
            _previous[index] = point;
            _stepStart[index] = point;
            _grounded[index] = false;
            Count++;
        }

        private void Remove(int index)
        {
            int moved = Count - index - 1;
            Array.Copy(_points, index + 1, _points, index, moved);
            Array.Copy(_previous, index + 1, _previous, index, moved);
            Array.Copy(_stepStart, index + 1, _stepStart, index, moved);
            Array.Copy(_grounded, index + 1, _grounded, index, moved);
            Count--;
        }

        private void AddPoint()
        {
            _feed -= _segmentLength;
            Vector3 fromSource = _points[Count - 1] - Source;
            float distance = fromSource.magnitude;
            Vector3 point = distance > 0f ? Source + fromSource * Mathf.Min(1f, _feed / distance) : Source;
            _points[Count] = point;
            _previous[Count] = point;
            _grounded[Count] = false;
            Count++;
        }

        // The hose path with its small wiggles cut off: what it would measure if pulled snug around the same corners.
        private float ShortcutLength()
        {
            float length = 0f;
            int from = 0;
            while (from < Count - 1)
            {
                int to = Mathf.Min(from + _slackStride, Count - 1);
                length += Vector3.Distance(_points[from], _points[to]);
                from = to;
            }

            return length + Vector3.Distance(_points[Count - 1], Source);
        }

        // String pulling: from the nozzle, walk along the hose for as long as every point passed stays in sight, hop there,
        // and repeat until the truck. Walking point by point keeps the hops on the hose's own side of every obstacle,
        // so they add up to the shortest route around the same corners, however the hose itself is lying.
        private float SnugLength()
        {
            float length = 0f;
            int from = 0;
            while (from < Count - 1)
            {
                Vector3 origin = _points[from];
                int to = from + 1;
                while (to < Count - 1 && IsClear(origin, _points[to + 1]))
                {
                    to++;
                }

                length += Vector3.Distance(origin, _points[to]);
                from = to;
            }

            return length + Vector3.Distance(_points[Count - 1], Source);
        }

        private Vector3 Sweep(Vector3 from, Vector3 to, out bool isGrounded, out Vector3 contact)
        {
            isGrounded = false;
            contact = Vector3.zero;
            Vector3 move = to - from;
            // Moves too small to cast must not be taken on trust: they would creep through the skin and into the wall.
            if (move.sqrMagnitude < MinMove * MinMove)
            {
                return from;
            }

            if (!Cast(from, move, out RaycastHit hit))
            {
                return to;
            }

            isGrounded = hit.normal.y >= _groundNormal;
            contact = hit.normal;
            Vector3 stopped = Rest(from, move, hit);
            Vector3 slide = Vector3.ProjectOnPlane(to - stopped, hit.normal);
            if (slide.sqrMagnitude < MinMove * MinMove)
            {
                return stopped;
            }

            if (!Cast(stopped, slide, out RaycastHit second))
            {
                return stopped + slide;
            }

            isGrounded |= second.normal.y >= _groundNormal;
            return Rest(stopped, slide, second);
        }

        // The skin is kept along the surface normal, not along the travel: a grazing slide would otherwise end up touching the wall.
        private Vector3 Rest(Vector3 from, Vector3 move, RaycastHit hit)
        {
            return from + move.normalized * hit.distance + hit.normal * _skin;
        }

        private bool Cast(Vector3 origin, Vector3 move, out RaycastHit nearest)
        {
            nearest = default;
            float distance = move.magnitude;
            if (distance < MinMove)
            {
                return false;
            }

            int count = Physics.SphereCastNonAlloc(origin, _radius, move / distance, _hits, distance + _skin, _mask, QueryTriggerInteraction.Ignore);
            nearest.distance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                if (hit.distance > 0f && hit.distance < nearest.distance && IsSolid(hit.collider))
                {
                    nearest = hit;
                }
            }

            return nearest.distance < float.MaxValue;
        }

        // A stretch counts as clear when nothing solid lies on it, or when it only nicks a corner: the rays from both ends
        // then leave no more than a sliver between their hits, where a real wall would leave its whole thickness.
        private bool IsClear(Vector3 from, Vector3 to)
        {
            Vector3 toTarget = to - from;
            float distance = toTarget.magnitude;
            if (distance < MinMove)
            {
                return true;
            }

            Vector3 direction = toTarget / distance;
            float entry = SolidDistance(from, direction, distance);
            return entry < 0f || distance - entry - SolidDistance(to, -direction, distance) < _cornerCut;
        }

        private float SolidDistance(Vector3 origin, Vector3 direction, float distance)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, _hits, distance, _mask, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].distance < nearest && IsSolid(_hits[i].collider))
                {
                    nearest = _hits[i].distance;
                }
            }

            return nearest < float.MaxValue ? nearest : -1f;
        }

        // Only the fixed world stops a hose: people, flames and loose props are walked through rather than wrapped around.
        private bool IsSolid(Collider collider)
        {
            if (_solid.TryGetValue(collider, out bool isSolid))
            {
                return isSolid;
            }

            isSolid = collider is not CharacterController
                && collider.attachedRigidbody == null
                && collider.GetComponentInParent<Animator>() == null
                && collider.GetComponentInParent<FireSource>() == null
                && !collider.transform.IsChildOf(_ignored);
            _solid[collider] = isSolid;
            return isSolid;
        }
    }
}
