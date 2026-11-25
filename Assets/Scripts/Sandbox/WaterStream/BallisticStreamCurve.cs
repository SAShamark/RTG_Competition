using System;
using Gameplay.Effects.WaterStream;
using UnityEngine;

namespace Sandbox.WaterStream
{
    [Serializable]
    public sealed class BallisticStreamCurve : IWaterStreamCurve
    {
        [SerializeField] private float _speed = 30f;
        [SerializeField, Range(0f, 1f)] private float _emptySpeedFactor = 0.35f;
        [SerializeField] private float _gravity = 9.81f;
        [SerializeField] private float _airDrag = 0.6f;
        [SerializeField] private float _flightTime = 5f;
        [SerializeField, Min(2)] private int _segments = 80;
        [SerializeField] private LayerMask _obstacleMask = ~0;

        private Vector3[] _points;
        private Vector3[] _velocities;
        private float[] _distances;
        private int _count;

        public float Length { get; private set; }
        public float Speed { get; private set; }
        public bool HasImpact { get; private set; }
        public Vector3 ImpactPoint { get; private set; }
        public Vector3 ImpactNormal { get; private set; }

        public void Init()
        {
            _points = new Vector3[_segments + 1];
            _velocities = new Vector3[_segments + 1];
            _distances = new float[_segments + 1];
            _count = 2;
            Speed = _speed;
        }

        public void Trace(Vector3 origin, Vector3 direction, float pressure)
        {
            float step = _flightTime / _segments;
            Speed = _speed * Mathf.Lerp(_emptySpeedFactor, 1f, pressure);
            Vector3 velocity = direction * Speed;
            HasImpact = false;
            _points[0] = origin;
            _velocities[0] = velocity;
            _distances[0] = 0f;
            _count = 1;
            for (int i = 1; i <= _segments && !HasImpact; i++)
            {
                // v ← (v + g·Δt)·(1 − k·Δt)
                velocity = (velocity + Vector3.down * (_gravity * step)) * Mathf.Max(0f, 1f - _airDrag * step);
                Vector3 from = _points[i - 1];
                Vector3 delta = velocity * step;
                float length = delta.magnitude;
                Vector3 to = from + delta;
                if (Physics.Raycast(from, delta / length, out RaycastHit hit, length, _obstacleMask, QueryTriggerInteraction.Ignore))
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
                _count = i + 1;
            }

            Length = _distances[_count - 1];
        }

        public void Sample(float distance, out Vector3 point, out Vector3 velocity)
        {
            int segment = 0;
            int last = _count - 2;
            while (segment < last && distance >= _distances[segment + 1])
            {
                segment++;
            }

            float span = _distances[segment + 1] - _distances[segment];
            float t = span > 0f ? Mathf.Clamp01((distance - _distances[segment]) / span) : 0f;
            point = Vector3.Lerp(_points[segment], _points[segment + 1], t);
            velocity = Vector3.Lerp(_velocities[segment], _velocities[segment + 1], t);
        }
    }
}
