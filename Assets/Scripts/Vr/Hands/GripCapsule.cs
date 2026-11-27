using UnityEngine;

namespace Vr.Hands
{
    public readonly struct GripCapsule
    {
        private readonly Vector3 _start;
        private readonly Vector3 _end;
        private readonly float _radius;

        public GripCapsule(Transform grip, float halfLength, float radius)
        {
            Vector3 axis = grip.right * halfLength;
            _start = grip.position - axis;
            _end = grip.position + axis;
            _radius = radius;
        }

        public bool Contains(Vector3 point)
        {
            Vector3 segment = _end - _start;
            float along = Mathf.Clamp01(Vector3.Dot(point - _start, segment) / segment.sqrMagnitude);
            Vector3 closest = _start + segment * along;
            return (point - closest).sqrMagnitude <= _radius * _radius;
        }
    }
}
