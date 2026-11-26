using System;
using Gameplay.Entities.Interactive;
using Services;
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
        private const int DoorPoint = 1;
        private const int BesidePoint = 2;

        [SerializeField] private float[] _keyTimes = { 0f, 0.64f, 1.6f, 2.65f, 3.8f, 5.4f, 7.2f };
        [SerializeField] private float _exitSpeed = 3.6f;
        [SerializeField] private float _groundProbeLift = 0.4f;
        [SerializeField] private float _groundSharpness = 14f;

        [Header("Doorway")]
        [SerializeField] private float _laneOffset = 0.6f;
        [SerializeField] private float _startFront = 2.2f;
        [SerializeField] private float _doorBack = 0.1f;

        [Header("Past the player")]
        [SerializeField] private float _passDistance = 1.1f;
        [SerializeField] private float _besideBlend = 0.85f;
        [SerializeField] private float _besideMaxBack = 4.4f;
        [SerializeField] private float _besideMinBack = 0.6f;
        [SerializeField] private float _besideMaxSide = 0.9f;
        [SerializeField] private float _passSide = 0.5f;
        [SerializeField] private float _passBack = 4.9f;

        [Header("Street")]
        [SerializeField] private float _turnSide = 1.8f;
        [SerializeField] private float _turnBack = 7.5f;
        [SerializeField] private float _streetSide = 7.2f;
        [SerializeField] private float _streetBack = 8.3f;
        [SerializeField] private float _exitSide = 15f;
        [SerializeField] private float _exitBack = 8.4f;

        [Header("Cat lanes")]
        [SerializeField] private float _doorHalfWidth = 0.7f;
        [SerializeField] private float _porchHalfWidth = 1f;

        private Vector3[] _points;
        private float _side;
        private float _doorX;
        private float _groundY;

        private int Last => _points.Length - 1;
        private float EndTime => _keyTimes[Last];

        public void Build(Vector3 doorCenter, Vector3 playerPosition)
        {
            float side = playerPosition.x >= doorCenter.x ? -1f : 1f;
            float lane = doorCenter.x + side * _laneOffset;
            float beside = Mathf.Clamp(playerPosition.x + side * _passDistance, doorCenter.x - _besideMaxSide, doorCenter.x + _besideMaxSide);
            float besideZ = Mathf.Clamp(playerPosition.z, doorCenter.z - _besideMaxBack, doorCenter.z - _besideMinBack);
            float y = doorCenter.y;

            _points = new[]
            {
                new Vector3(lane, y, doorCenter.z + _startFront),
                new Vector3(lane, y, doorCenter.z - _doorBack),
                new Vector3(Mathf.Lerp(lane, beside, _besideBlend), y, besideZ),
                new Vector3(doorCenter.x + side * _passSide, y, doorCenter.z - _passBack),
                new Vector3(doorCenter.x + side * _turnSide, y, doorCenter.z - _turnBack),
                new Vector3(doorCenter.x + side * _streetSide, y, doorCenter.z - _streetBack),
                new Vector3(doorCenter.x + side * _exitSide, y, doorCenter.z - _exitBack),
            };

            _side = side;
            _doorX = doorCenter.x;
            _groundY = y;
        }

        public Vector3 Sample(float time, float deltaTime)
        {
            Vector3 position = Flat(_points, Mathf.Max(time, _keyTimes[0]));
            float ground = GroundY(position, _groundY + _groundProbeLift);
            _groundY = Mathf.Lerp(_groundY, ground, Easing.Damp(_groundSharpness, deltaTime));
            position.y = _groundY;
            return position;
        }

        public Vector3 Direction(float time) =>
            Vector3.ProjectOnPlane(Spline(_points, time + DirectionStep) - Spline(_points, time), Vector3.up).normalized;

        public Vector3[] BuildLane(float[] offsets)
        {
            var lane = new Vector3[_points.Length];
            for (int i = 0; i < lane.Length; i++)
            {
                Vector3 inner = Vector3.Cross(Vector3.up, Direction(_keyTimes[i])) * -_side;
                lane[i] = _points[i] + inner * (i < offsets.Length ? offsets[i] : 0f);
            }

            lane[DoorPoint].x = Mathf.Clamp(lane[DoorPoint].x, _doorX - _doorHalfWidth, _doorX + _doorHalfWidth);
            lane[BesidePoint].x = Mathf.Clamp(lane[BesidePoint].x, _doorX - _porchHalfWidth, _doorX + _porchHalfWidth);
            return lane;
        }

        public Vector3 SampleLane(Vector3[] lane, float time) => Flat(lane, time);

        private Vector3 Flat(Vector3[] points, float time)
        {
            if (time <= _keyTimes[0])
            {
                Vector3 leadIn = points[0] - points[1];
                return points[0] + leadIn * ((_keyTimes[0] - time) / (_keyTimes[1] - _keyTimes[0]));
            }

            if (time < EndTime)
            {
                return Spline(points, time);
            }

            Vector3 exit = Vector3.ProjectOnPlane(points[Last] - points[Last - 1], Vector3.up).normalized;
            return points[Last] + exit * (_exitSpeed * (time - EndTime));
        }

        private Vector3 Spline(Vector3[] points, float time)
        {
            int i = 0;
            while (i < Last - 1 && time > _keyTimes[i + 1])
            {
                i++;
            }

            float u = Mathf.InverseLerp(_keyTimes[i], _keyTimes[i + 1], time);
            Vector3 p0 = Point(points, i - 1);
            Vector3 p1 = Point(points, i);
            Vector3 p2 = Point(points, i + 1);
            Vector3 p3 = Point(points, i + 2);
            float u2 = u * u;
            float u3 = u2 * u;

            // Catmull-Rom: ½·(2p₁ + (p₂ − p₀)u + (2p₀ − 5p₁ + 4p₂ − p₃)u² + (3p₁ − p₀ − 3p₂ + p₃)u³)
            return 0.5f * (2f * p1 + (p2 - p0) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 + (3f * p1 - p0 - 3f * p2 + p3) * u3);
        }

        // ends are mirrored (2·pₑ − pₙ) so the run keeps its speed at the first and the last point
        private Vector3 Point(Vector3[] points, int index)
        {
            if (index < 0)
            {
                return 2f * points[0] - points[1];
            }

            return index > Last ? 2f * points[Last] - points[Last - 1] : points[index];
        }

        private static float GroundY(Vector3 point, float maxY)
        {
            var origin = new Vector3(point.x, maxY + GroundProbeHeight, point.z);
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, GroundProbeDistance, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MinValue;
            foreach (RaycastHit hit in hits)
            {
                if (hit.point.y > maxY || hit.collider.GetComponentInParent<Door>() != null
                    || hit.collider.GetComponentInParent<CharacterController>() != null
                    || hit.collider.GetComponentInParent<IToolReactive>() != null)
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
