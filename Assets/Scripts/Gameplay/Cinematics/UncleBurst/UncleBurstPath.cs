using System;
using Gameplay.Entities.Interactive;
using UnityEngine;

namespace Gameplay.Cinematics.UncleBurst
{
    [Serializable]
    public sealed class UncleBurstPath
    {
        private const float GroundProbeHeight = 3f;
        private const float GroundProbeDistance = 10f;
        private const float FallbackGroundY = 0.05f;
        private const float DirectionStep = 0.2f;
        private const int FirstGroundedPoint = 2;

        [SerializeField] private float[] _keyTimes = { 0f, 0.64f, 1.6f, 2.65f, 3.8f, 5.4f, 7.2f };
        [SerializeField] private float _exitSpeed = 3.6f;
        [SerializeField] private float _groundProbeLift = 0.4f;

        [Header("Doorway")]
        [SerializeField] private float _laneOffset = 0.6f;
        [SerializeField] private float _startFront = 2.2f;
        [SerializeField] private float _doorBack = 0.1f;

        [Header("Past the player")]
        [SerializeField] private float _passDistance = 1.1f;
        [SerializeField] private float _besideBlend = 0.85f;
        [SerializeField] private float _besideMaxBack = 4.4f;
        [SerializeField] private float _besideMinBack = 0.6f;
        [SerializeField] private float _passSide = 0.5f;
        [SerializeField] private float _passBack = 4.9f;

        [Header("Street")]
        [SerializeField] private float _turnSide = 2.6f;
        [SerializeField] private float _turnBack = 6.8f;
        [SerializeField] private float _streetSide = 8f;
        [SerializeField] private float _streetBack = 7.4f;
        [SerializeField] private float _exitSide = 15f;
        [SerializeField] private float _exitBack = 7.6f;

        private Vector3[] _points;
        private Vector3 _exitDirection;
        private float _exitGroundY;

        private int Last => _points.Length - 1;
        private float EndTime => _keyTimes[Last];

        public void Build(Vector3 doorCenter, Vector3 playerPosition)
        {
            float side = playerPosition.x >= doorCenter.x ? -1f : 1f;
            float lane = doorCenter.x + side * _laneOffset;
            float beside = playerPosition.x + side * _passDistance;
            float besideZ = Mathf.Clamp(playerPosition.z, doorCenter.z - _besideMaxBack, doorCenter.z - _besideMinBack);

            _points = new[]
            {
                new Vector3(lane, doorCenter.y, doorCenter.z + _startFront),
                new Vector3(lane, doorCenter.y, doorCenter.z - _doorBack),
                new Vector3(Mathf.Lerp(lane, beside, _besideBlend), 0f, besideZ),
                new Vector3(doorCenter.x + side * _passSide, 0f, doorCenter.z - _passBack),
                new Vector3(doorCenter.x + side * _turnSide, 0f, doorCenter.z - _turnBack),
                new Vector3(doorCenter.x + side * _streetSide, 0f, doorCenter.z - _streetBack),
                new Vector3(doorCenter.x + side * _exitSide, 0f, doorCenter.z - _exitBack),
            };

            for (int i = FirstGroundedPoint; i < _points.Length; i++)
            {
                _points[i].y = GroundY(_points[i], doorCenter.y + _groundProbeLift);
            }

            _exitDirection = Vector3.ProjectOnPlane(_points[Last] - _points[Last - 1], Vector3.up).normalized;
            _exitGroundY = _points[Last].y;
        }

        public Vector3 Sample(float time)
        {
            if (time <= _keyTimes[0])
            {
                return _points[0];
            }

            return time < EndTime ? Spline(time) : RunOut(time);
        }

        public Vector3 Direction(float time) =>
            Vector3.ProjectOnPlane(Spline(time + DirectionStep) - Spline(time), Vector3.up).normalized;

        private Vector3 RunOut(float time)
        {
            Vector3 position = _points[Last] + _exitDirection * (_exitSpeed * (time - EndTime));
            _exitGroundY = GroundY(position, _exitGroundY + _groundProbeLift);
            position.y = _exitGroundY;
            return position;
        }

        private Vector3 Spline(float time)
        {
            int i = 0;
            while (i < Last - 1 && time > _keyTimes[i + 1])
            {
                i++;
            }

            float u = Mathf.InverseLerp(_keyTimes[i], _keyTimes[i + 1], time);
            Vector3 p0 = Point(i - 1);
            Vector3 p1 = Point(i);
            Vector3 p2 = Point(i + 1);
            Vector3 p3 = Point(i + 2);
            float u2 = u * u;
            float u3 = u2 * u;

            // Catmull-Rom: ½·(2p₁ + (p₂ − p₀)u + (2p₀ − 5p₁ + 4p₂ − p₃)u² + (3p₁ − p₀ − 3p₂ + p₃)u³)
            return 0.5f * (2f * p1 + (p2 - p0) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 + (3f * p1 - p0 - 3f * p2 + p3) * u3);
        }

        // ends are mirrored (2·pₑ − pₙ) so the run keeps its speed at the first and the last point
        private Vector3 Point(int index)
        {
            if (index < 0)
            {
                return 2f * _points[0] - _points[1];
            }

            return index > Last ? 2f * _points[Last] - _points[Last - 1] : _points[index];
        }

        private static float GroundY(Vector3 point, float maxY)
        {
            var origin = new Vector3(point.x, maxY + GroundProbeHeight, point.z);
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, GroundProbeDistance, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MinValue;
            foreach (RaycastHit hit in hits)
            {
                if (hit.point.y > maxY || hit.collider.GetComponentInParent<Door>() != null
                    || hit.collider.GetComponentInParent<CharacterController>() != null)
                {
                    continue;
                }

                if (hit.collider.TryGetComponent(out Renderer hitRenderer) && !hitRenderer.enabled)
                {
                    continue;
                }

                best = Mathf.Max(best, hit.point.y);
            }

            return best == float.MinValue ? FallbackGroundY : best;
        }
    }
}
