using System;
using Gameplay.Entities.Interactive;
using Services.Audio;
using UnityEngine;
using Vr;
using Vr.Hands;

namespace Gameplay.Entities.Characters.Player
{
    [Serializable]
    public sealed class ForcePullLogic
    {
        private const int IgnoreRaycastLayer = 2;
        private const int MaxSightHits = 16;

        [SerializeField] private Material _highlightMaterial;
        [SerializeField, Min(0.5f)] private float _range = 6f;
        [SerializeField, Min(0f)] private float _minDistance = 0.5f;
        [SerializeField, Range(1f, 45f)] private float _coneAngle = 18f;
        [SerializeField, Min(0.1f)] private float _flickSpeed = 1.3f;
        [SerializeField, Min(0.1f)] private float _flightTime = 0.5f;
        [SerializeField, Range(0f, 1f)] private float _apexShare = 0.25f;
        [SerializeField, Min(0f)] private float _maxApex = 0.8f;
        [SerializeField, Range(0f, 1f)] private float _aimPulse = 0.08f;
        [SerializeField, Range(0f, 1f)] private float _lockPulse = 0.15f;
        [SerializeField, Range(0f, 1f)] private float _launchPulse = 0.45f;
        [SerializeField, Min(0.01f)] private float _pulseDuration = 0.06f;
        [SerializeField] private LayerMask _sightMask = ~0;
        [SerializeField, Min(0f)] private float _sightSlack = 0.35f;
        [SerializeField, Min(0f)] private float _sightLift = 0.1f;

        private readonly RaycastHit[] _sightHits = new RaycastHit[MaxSightHits];
        private readonly HandState _left = new();
        private readonly HandState _right = new();
        private Transform _head;
        private VrInputLogic _input;
        private int _queryMask;

        private sealed class HandState
        {
            public TargetHighlightLogic Highlight;
            public IPullable Aimed;
            public IPullable Locked;
            public IPullable Flying;
            public float FlyTime;
            public Vector3 Start;
            public Vector3 LastPoint;
        }

        public void Init(Transform head, VrInputLogic input)
        {
            _head = head;
            _input = input;
            _queryMask = _sightMask & ~(1 << IgnoreRaycastLayer);
            _left.Highlight = new TargetHighlightLogic(_highlightMaterial);
            _right.Highlight = new TargetHighlightLogic(_highlightMaterial);
        }

        public IPullable Tick(bool isLeft, PhysicalHand hand, bool isFree, bool isGripPressed, bool isGripHeld, float deltaTime)
        {
            HandState state = isLeft ? _left : _right;
            if (state.Flying != null)
            {
                return TickFlight(state, hand, isGripHeld, deltaTime);
            }

            if (!isFree)
            {
                Clear(state);
                return null;
            }

            if (state.Locked != null)
            {
                TickLocked(state, isLeft, hand, isGripHeld);
                return null;
            }

            IPullable aimed = !isGripHeld || isGripPressed ? FindTarget(hand) : null;
            if (aimed != null && !ReferenceEquals(aimed, state.Aimed))
            {
                _input.Pulse(isLeft, _aimPulse, _pulseDuration);
            }

            SetAimed(state, aimed);
            if (isGripPressed && state.Aimed != null)
            {
                state.Locked = state.Aimed;
                _input.Pulse(isLeft, _lockPulse, _pulseDuration);
            }

            return null;
        }

        public void Clear()
        {
            Drop(_left);
            Drop(_right);
            Clear(_left);
            Clear(_right);
        }

        private static void Drop(HandState state)
        {
            if (state.Flying == null)
            {
                return;
            }

            state.Flying.EndPull(Vector3.zero);
            state.Flying = null;
        }

        private void TickLocked(HandState state, bool isLeft, PhysicalHand hand, bool isGripHeld)
        {
            if (!isGripHeld || !state.Locked.CanPull)
            {
                Clear(state);
                return;
            }

            Vector3 toHead = (_head.position - hand.Grip.position).normalized;
            Vector3 velocity = hand.Velocity;
            bool isFlick = Vector3.Dot(velocity, toHead) >= _flickSpeed || velocity.y >= _flickSpeed;
            if (!isFlick)
            {
                return;
            }

            IPullable item = state.Locked;
            Clear(state);
            item.BeginPull();
            AudioService.Instance.Play(SoundType.ItemThrow);
            _input.Pulse(isLeft, _launchPulse, _pulseDuration);
            state.Flying = item;
            state.FlyTime = 0f;
            state.Start = item.PullPoint;
            state.LastPoint = state.Start;
        }

        private IPullable TickFlight(HandState state, PhysicalHand hand, bool isGripHeld, float deltaTime)
        {
            IPullable item = state.Flying;
            if (!isGripHeld)
            {
                state.Flying = null;
                item.EndPull(deltaTime > 0f ? (item.PullPoint - state.LastPoint) / deltaTime : Vector3.zero);
                return null;
            }

            state.FlyTime += deltaTime;
            float progress = Mathf.Clamp01(state.FlyTime / _flightTime);
            Vector3 target = hand.Grip.position;
            float apex = Mathf.Min(_maxApex, Vector3.Distance(state.Start, target) * _apexShare);
            // Arc toward the moving hand: lerp(start, hand, smooth(u)) lifted by 4·apex·u·(1 − u).
            Vector3 point = Vector3.Lerp(state.Start, target, Mathf.SmoothStep(0f, 1f, progress)) + Vector3.up * (4f * apex * progress * (1f - progress));
            state.LastPoint = item.PullPoint;
            item.MovePull(point);
            if (progress < 1f)
            {
                return null;
            }

            state.Flying = null;
            return item;
        }

        private IPullable FindTarget(PhysicalHand hand)
        {
            Vector3 origin = hand.Grip.position;
            Vector3 forward = hand.Body.forward;
            IPullable best = null;
            float bestDot = Mathf.Cos(_coneAngle * Mathf.Deg2Rad);
            foreach (IPullable item in Pullables.All)
            {
                if (!item.CanPull)
                {
                    continue;
                }

                Vector3 offset = item.PullPoint - origin;
                float distance = offset.magnitude;
                if (distance < _minDistance || distance > _range)
                {
                    continue;
                }

                float dot = Vector3.Dot(offset / distance, forward);
                if (dot > bestDot && IsVisible(origin, item))
                {
                    bestDot = dot;
                    best = item;
                }
            }

            return best;
        }

        private bool IsVisible(Vector3 origin, IPullable item)
        {
            return CanSee(origin, item, item.PullPoint) || CanSee(origin, item, item.PullPoint + Vector3.up * _sightLift);
        }

        private bool CanSee(Vector3 origin, IPullable item, Vector3 point)
        {
            Vector3 offset = point - origin;
            float distance = offset.magnitude;
            int count = Physics.RaycastNonAlloc(origin, offset / distance, _sightHits, distance, _queryMask, QueryTriggerInteraction.Ignore);
            float itemDistance = distance;
            float blockerDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _sightHits[i];
                if (ReferenceEquals(hit.collider.GetComponentInParent<IPullable>(), item))
                {
                    itemDistance = Mathf.Min(itemDistance, hit.distance);
                }
                else
                {
                    blockerDistance = Mathf.Min(blockerDistance, hit.distance);
                }
            }

            return blockerDistance >= itemDistance - _sightSlack;
        }

        private static void SetAimed(HandState state, IPullable item)
        {
            if (ReferenceEquals(state.Aimed, item))
            {
                return;
            }

            state.Aimed = item;
            state.Highlight.Set(item as Component);
        }

        private static void Clear(HandState state)
        {
            SetAimed(state, null);
            state.Locked = null;
        }
    }
}
