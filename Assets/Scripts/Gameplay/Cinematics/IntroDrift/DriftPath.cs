using System;
using Services;
using UnityEngine;

namespace Gameplay.Cinematics.IntroDrift
{
    [Serializable]
    public sealed class DriftPath
    {
        [SerializeField] private Transform _end;
        [SerializeField, Min(0f)] private float _approachLength = 78f;
        [SerializeField, Min(1f)] private float _turnRadius = 10f;
        [SerializeField, Range(-180f, 180f)] private float _turnAngle = 90f;
        [SerializeField, Min(0f)] private float _exitLength = 21f;

        [Header("Speed")]
        [SerializeField, Min(0f)] private float _approachSpeed = 26f;
        [SerializeField, Min(0f)] private float _driftSpeed = 19f;
        [SerializeField, Min(0f)] private float _exitSpeed = 22f;
        [SerializeField, Min(0f)] private float _brakeDistance = 10f;

        [Header("Slip")]
        [SerializeField] private AnimationCurve _slip = new(
            new Keyframe(-1f, 0f),
            new Keyframe(-0.3f, 30f),
            new Keyframe(0.3f, 40f),
            new Keyframe(0.75f, 34f),
            new Keyframe(1.15f, -7f),
            new Keyframe(1.45f, 0f));
        [SerializeField] private float _rollPerSlip = 0.1f;

        private Vector3 _exitStart;
        private Vector3 _turnIn;
        private Vector3 _center;
        private Vector3 _approachHeading;
        private Vector3 _exitHeading;
        private float _side;
        private float _turnLength;

        public float TurnIn => _approachLength;
        public float TurnLength => _turnLength;
        public float Length => _approachLength + _turnLength + _exitLength;

        public void Init()
        {
            _exitHeading = Vector3.ProjectOnPlane(_end.forward, Vector3.up).normalized;
            _side = Mathf.Sign(_turnAngle);
            _turnLength = _turnRadius * Mathf.Abs(_turnAngle) * Mathf.Deg2Rad;
            _exitStart = _end.position - _exitHeading * _exitLength;
            _center = _exitStart + Quaternion.Euler(0f, 90f * _side, 0f) * _exitHeading * _turnRadius;
            Quaternion back = Quaternion.Euler(0f, -_turnAngle, 0f);
            _turnIn = _center + back * (_exitStart - _center);
            _approachHeading = back * _exitHeading;
        }

        public float ProgressAt(float distance) => (distance - _approachLength) / _turnLength;

        public float SpeedAt(float distance)
        {
            float turnOut = _approachLength + _turnLength;
            if (distance < _approachLength)
            {
                float brake = Easing.Smooth((distance - (_approachLength - _brakeDistance)) / _brakeDistance);
                return Mathf.Lerp(_approachSpeed, _driftSpeed, brake);
            }

            if (distance < turnOut)
            {
                return _driftSpeed;
            }

            return Mathf.Lerp(_driftSpeed, _exitSpeed, (distance - turnOut) / _exitLength);
        }

        public float BrakingAt(float distance)
        {
            float brake = Mathf.Clamp01((distance - (_approachLength - _brakeDistance)) / _brakeDistance);
            // deceleration rises and falls across the brake zone
            return Mathf.Sin(brake * Mathf.PI);
        }

        public DriftPose Sample(float distance)
        {
            Vector3 position;
            Vector3 heading;
            if (distance < _approachLength)
            {
                heading = _approachHeading;
                position = _turnIn - heading * (_approachLength - distance);
            }
            else if (distance < _approachLength + _turnLength)
            {
                float degrees = (distance - _approachLength) / _turnRadius * Mathf.Rad2Deg * _side;
                Quaternion turn = Quaternion.Euler(0f, degrees, 0f);
                heading = turn * _approachHeading;
                position = _center + turn * (_turnIn - _center);
            }
            else
            {
                heading = _exitHeading;
                position = _exitStart + heading * (distance - _approachLength - _turnLength);
            }

            float slip = _slip.Evaluate(ProgressAt(distance)) * _side;
            Quaternion rotation = Quaternion.LookRotation(heading) * Quaternion.Euler(0f, slip, -slip * _rollPerSlip);
            return new DriftPose(position, heading, rotation, slip);
        }
    }
}
