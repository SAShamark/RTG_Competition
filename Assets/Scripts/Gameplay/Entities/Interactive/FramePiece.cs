using System;
using Services.Audio;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Gameplay.Entities.Interactive
{
    public sealed class FramePiece : MonoBehaviour, IChoppable
    {
        private const float HalfUnit = 0.5f;

        [SerializeField] private MeshRenderer _renderer;
        [SerializeField] private Collider _collider;
        [SerializeField] private DebrisSet _piecesPrefab;
        [SerializeField] private BreakableGlass _glass;

        [Header("Segments")]
        [SerializeField, Min(2)] private int _segmentCount = 5;
        [SerializeField, Range(0f, 0.45f)] private float _boundaryJitter = 0.3f;
        [SerializeField] private float _segmentGap = 0.006f;
        [SerializeField] private float _knockRadius = 0.08f;
        [SerializeField] private float _supportTolerance = 0.06f;

        [Header("Support")]
        [SerializeField] private bool _startAnchored = true;
        [SerializeField] private bool _endAnchored = true;
        [SerializeField] private FramePiece _startSupport;
        [SerializeField] private FramePiece _endSupport;

        [Header("Debris")]
        [SerializeField] private float _impulse = 1.1f;
        [SerializeField] private float _scatter = 0.5f;
        [SerializeField] private float _torque = 0.05f;
        [SerializeField] private float _dropImpulse = 0.15f;
        [SerializeField] private float _lifetime = 30f;

        [Header("Feedback")]
        [SerializeField] private SoundType _breakSound = SoundType.BlockerSmash;
        [SerializeField] private float _breakPitch = 1.25f;
        [SerializeField] private GameObject _breakEffect;
        [SerializeField] private float _effectLifetime = 3f;

        private DebrisSet _set;
        private DebrisPiece[] _segments;
        private int _axis;

        public bool IsInner => _startSupport != null || _endSupport != null;

        public event Action<FramePiece> Changed;

        private void Awake()
        {
            Vector3 scale = transform.lossyScale;
            _axis = scale.x >= scale.y && scale.x >= scale.z ? 0 : scale.y >= scale.z ? 1 : 2;
        }

        private void OnEnable()
        {
            if (_startSupport != null)
            {
                _startSupport.Changed += HandleSupportChanged;
            }

            if (_endSupport != null)
            {
                _endSupport.Changed += HandleSupportChanged;
            }
        }

        private void OnDisable()
        {
            if (_startSupport != null)
            {
                _startSupport.Changed -= HandleSupportChanged;
            }

            if (_endSupport != null)
            {
                _endSupport.Changed -= HandleSupportChanged;
            }
        }

        public bool IsSolidAt(Vector3 point)
        {
            if (_set == null)
            {
                return _collider.enabled && Contains(_collider.bounds, point);
            }

            foreach (DebrisPiece segment in _segments)
            {
                if (segment != null && Contains(segment.Collider.bounds, point))
                {
                    return true;
                }
            }

            return false;
        }

        public void Chop(Vector3 point, Vector3 direction)
        {
            AudioService.Instance.PlayAt(_breakSound, point, _breakPitch);
            if (_breakEffect != null)
            {
                Destroy(Instantiate(_breakEffect, point, Quaternion.LookRotation(-direction)), _effectLifetime);
            }

            if (_glass != null && _glass.isActiveAndEnabled)
            {
                _glass.Smash(point, direction, !IsInner);
                return;
            }

            BreakAll(point, direction);
        }

        public void Restore()
        {
            if (_set != null)
            {
                Destroy(_set.gameObject);
            }

            _set = null;
            _segments = null;
            _renderer.enabled = true;
            _collider.enabled = true;
        }

        public void BreakAll(Vector3 point, Vector3 direction)
        {
            if (_set == null)
            {
                Segment();
            }

            for (int i = 0; i < _segments.Length; i++)
            {
                if (_segments[i] != null)
                {
                    Free(i, point, direction, _impulse);
                }
            }
        }

        private void Segment()
        {
            Vector3 scale = transform.lossyScale;
            float length = scale[_axis];
            float gap = _segmentGap / length;
            var bounds = new float[_segmentCount + 1];
            bounds[0] = -HalfUnit;
            bounds[_segmentCount] = HalfUnit;
            float step = 1f / _segmentCount;
            for (int i = 1; i < _segmentCount; i++)
            {
                bounds[i] = -HalfUnit + step * (i + Random.Range(-_boundaryJitter, _boundaryJitter));
            }

            _set = Instantiate(_piecesPrefab, transform.position, transform.rotation);
            _set.Bind(this);
            DebrisPiece[] pieces = _set.Pieces;
            _segments = new DebrisPiece[_segmentCount];
            for (int i = 0; i < pieces.Length; i++)
            {
                if (i >= _segmentCount)
                {
                    pieces[i].gameObject.SetActive(false);
                    continue;
                }

                float from = bounds[i] + (i > 0 ? gap : 0f);
                float to = bounds[i + 1] - (i < _segmentCount - 1 ? gap : 0f);
                PlaceSegment(pieces[i], from, to, scale);
                _segments[i] = pieces[i];
            }

            _renderer.enabled = false;
            _collider.enabled = false;
        }

        private void PlaceSegment(DebrisPiece piece, float from, float to, Vector3 scale)
        {
            Vector3 center = Vector3.zero;
            center[_axis] = (from + to) * 0.5f * scale[_axis];
            Vector3 size = scale;
            size[_axis] = (to - from) * scale[_axis];
            piece.transform.localScale = size;
            piece.Place(center, Quaternion.identity);
            piece.Renderer.sharedMaterial = _renderer.sharedMaterial;
            piece.Collider.enabled = true;
        }

        private void Knock(Vector3 point, Vector3 direction)
        {
            int nearest = -1;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < _segments.Length; i++)
            {
                if (_segments[i] == null)
                {
                    continue;
                }

                float distance = Vector3.Distance(_segments[i].Collider.ClosestPoint(point), point);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = i;
                }

                if (distance <= _knockRadius)
                {
                    Free(i, point, direction, _impulse);
                }
            }

            if (nearest >= 0 && _segments[nearest] != null)
            {
                Free(nearest, point, direction, _impulse);
            }
        }

        private void ResolveSupport()
        {
            bool startHeld = IsEndHeld(true);
            bool endHeld = IsEndHeld(false);
            int keepFromStart = startHeld ? CountAlive(0, 1) : 0;
            int keepFromEnd = endHeld ? CountAlive(_segments.Length - 1, -1) : 0;
            for (int i = keepFromStart; i < _segments.Length - keepFromEnd; i++)
            {
                if (_segments[i] != null)
                {
                    Free(i, _segments[i].transform.position, Vector3.down, _dropImpulse);
                }
            }
        }

        private int CountAlive(int from, int step)
        {
            int count = 0;
            for (int i = from; i >= 0 && i < _segments.Length && _segments[i] != null; i += step)
            {
                count++;
            }

            return count;
        }

        private bool IsEndHeld(bool isStart)
        {
            if (!(isStart ? _startAnchored : _endAnchored))
            {
                return false;
            }

            FramePiece support = isStart ? _startSupport : _endSupport;
            if (support == null)
            {
                return true;
            }

            Vector3 local = Vector3.zero;
            local[_axis] = isStart ? -HalfUnit : HalfUnit;
            return support.IsSolidAt(transform.TransformPoint(local));
        }

        private void Free(int index, Vector3 point, Vector3 direction, float impulse)
        {
            DebrisPiece piece = _segments[index];
            _segments[index] = null;
            Vector3 away = (piece.transform.position - point).normalized;
            piece.Free(direction * impulse + away * _scatter + Random.insideUnitSphere * (_scatter * 0.5f), Random.insideUnitSphere * _torque, _lifetime + Random.value);
        }

        private void HandleSupportChanged(FramePiece support)
        {
            if (_set == null)
            {
                if (IsEndHeld(true) || IsEndHeld(false))
                {
                    return;
                }

                Segment();
            }

            ResolveSupport();
            Changed?.Invoke(this);
        }

        private bool Contains(Bounds bounds, Vector3 point)
        {
            bounds.Expand(_supportTolerance * 2f);
            return bounds.Contains(point);
        }
    }
}
