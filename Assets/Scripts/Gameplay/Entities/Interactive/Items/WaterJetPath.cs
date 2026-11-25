using System;
using Gameplay.Effects.WaterStream;
using UnityEngine;

namespace Gameplay.Entities.Interactive.Items
{
    [Serializable]
    public sealed class WaterJetPath : IWaterStreamCurve
    {
        private const int MaxRayHits = 8;
        private const float MinSideSqr = 0.0001f;

        [SerializeField] private float _speed = 30f;
        [SerializeField, Range(0f, 1f)] private float _emptySpeedFactor = 0.35f;
        [SerializeField] private float _gravity = 9.81f;
        [SerializeField] private float _airDrag = 0.6f;
        [SerializeField] private float _flightTime = 5f;
        [SerializeField, Min(2)] private int _segments = 80;
        [SerializeField] private LayerMask _obstacleMask = ~0;

        private readonly RaycastHit[] _rayHits = new RaycastHit[MaxRayHits];
        private Vector3[] _points;
        private Vector3[] _velocities;
        private float[] _distances;

        public int Count { get; private set; }
        public float Length { get; private set; }
        public bool HasImpact { get; private set; }
        public Vector3 ImpactPoint { get; private set; }
        public Vector3 ImpactNormal { get; private set; }
        public Vector3 Side { get; private set; } = Vector3.right;
        public float Speed { get; private set; }
        public LayerMask ObstacleMask => _obstacleMask;

        public void Init()
        {
            _points = new Vector3[_segments + 1];
            _velocities = new Vector3[_segments + 1];
            _distances = new float[_segments + 1];
            Speed = _speed;
        }

        public void Trace(Vector3 origin, Vector3 direction, float pressure, Transform owner)
        {
            Vector3 side = Vector3.Cross(Vector3.up, direction);
            if (side.sqrMagnitude > MinSideSqr)
            {
                Side = side.normalized;
            }

            float step = _flightTime / _segments;
            Speed = _speed * Mathf.Lerp(_emptySpeedFactor, 1f, pressure);
            Vector3 velocity = direction * Speed;
            HasImpact = false;
            _points[0] = origin;
            _velocities[0] = velocity;
            _distances[0] = 0f;
            Count = 1;
            for (int i = 1; i <= _segments && !HasImpact; i++)
            {
                // Air drag slows the water every step, so the jet falls steeper than it rises: v ← (v + g·Δt)·(1 − k·Δt)
                velocity = (velocity + Vector3.down * (_gravity * step)) * Mathf.Max(0f, 1f - _airDrag * step);
                Vector3 from = _points[i - 1];
                Vector3 delta = velocity * step;
                float length = delta.magnitude;
                Vector3 to = from + delta;
                if (Cast(from, delta / length, length, owner, out RaycastHit hit))
                {
                    to = hit.point;
                    length = hit.distance;
                    HasImpact = true;
                    ImpactPoint = hit.point;
                    ImpactNormal = hit.normal;
                }

                _points[i] = to;
                _velocities[i] = velocity;
                _distances[i] = _distances[i - 1] + length;
                Count = i + 1;
            }

            Length = _distances[Count - 1];
        }

        public void Sample(float distance, out Vector3 point, out Vector3 velocity)
        {
            int segment = SegmentAt(distance);
            float span = _distances[segment + 1] - _distances[segment];
            float t = span > 0f ? Mathf.Clamp01((distance - _distances[segment]) / span) : 0f;
            Vector3 from = _points[segment];
            Vector3 to = _points[segment + 1];
            Vector3 leave = _velocities[segment].normalized * span;
            Vector3 arrive = _velocities[segment + 1].normalized * span;

            // Cubic Hermite: (2t³−3t²+1)·p₀ + (t³−2t²+t)·m₀ + (−2t³+3t²)·p₁ + (t³−t²)·m₁
            float t2 = t * t;
            float t3 = t2 * t;
            point = (2f * t3 - 3f * t2 + 1f) * from + (t3 - 2f * t2 + t) * leave + (3f * t2 - 2f * t3) * to + (t3 - t2) * arrive;
            velocity = Vector3.Lerp(_velocities[segment], _velocities[segment + 1], t);
        }

        public float DistanceTo(Vector3 point, float reach)
        {
            float nearest = float.MaxValue;
            for (int i = 1; i < Count && _distances[i - 1] < reach; i++)
            {
                Vector3 from = _points[i - 1];
                Vector3 along = _points[i] - from;
                float share = Mathf.Clamp01(Vector3.Dot(point - from, along) / Mathf.Max(along.sqrMagnitude, MinSideSqr));
                nearest = Mathf.Min(nearest, Vector3.Distance(point, from + along * share));
            }

            return nearest;
        }

        private int SegmentAt(float distance)
        {
            int last = Count - 2;
            for (int i = 0; i < last; i++)
            {
                if (distance < _distances[i + 1])
                {
                    return i;
                }
            }

            return last;
        }

        private bool Cast(Vector3 origin, Vector3 direction, float distance, Transform owner, out RaycastHit nearest)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, _rayHits, distance, _obstacleMask, QueryTriggerInteraction.Ignore);
            nearest = default;
            nearest.distance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (_rayHits[i].distance < nearest.distance && !_rayHits[i].transform.IsChildOf(owner))
                {
                    nearest = _rayHits[i];
                }
            }

            return nearest.distance < float.MaxValue;
        }
    }
}
