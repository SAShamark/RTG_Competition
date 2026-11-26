using Services;
using Gameplay.Entities.Interactive;
using UnityEngine;

namespace Gameplay.Entities.Environment
{
    public sealed class CatFollower : MonoBehaviour
    {
        private const float MinMoveSqr = 0.00001f;
        private const float ModelYaw = 90f;
        private const int MaxHits = 16;
        private const float MinDeltaTime = 0.0001f;
        private const float FullTurn = Mathf.PI * 2f;

        [Header("Route")]
        [SerializeField] private float[] _laneOffsets = { 0f, 0f, 0f, 0f, 0f, 0f, 0f };
        [SerializeField] private float _delay = 0.4f;
        [SerializeField] private float _surgeTime = 0.04f;
        [SerializeField] private float _surgeFrequency = 2.3f;

        [Header("Ground")]
        [SerializeField] private float _headingSharpness = 13f;
        [SerializeField] private float _groundProbeHeight = 1.2f;
        [SerializeField] private float _groundProbeDistance = 3f;
        [SerializeField] private float _stepHeight = 0.2f;
        [SerializeField] private float _heightSharpness = 18f;
        [SerializeField] private float _feetMeasureTime = 0.4f;

        [Header("Gait")]
        [SerializeField] private float _animationSpeed = 1f;
        [SerializeField, Range(0f, 1f)] private float _cycleOffset;
        [SerializeField] private float _referenceSpeed = 3.6f;
        [SerializeField] private float _minGait = 0.55f;
        [SerializeField] private float _maxGait = 1.35f;
        [SerializeField] private float _speedSharpness = 8f;
        [SerializeField] private float _slopeSharpness = 10f;
        [SerializeField] private float _maxSlope = 0.6f;

        private readonly RaycastHit[] _hits = new RaycastHit[MaxHits];
        private Vector3 _goal;
        private float _footOffset;
        private float _speed;
        private float _slope;
        private Vector3 _heading = Vector3.forward;
        private Quaternion _tilt = Quaternion.identity;
        private Animator _animator;
        private bool _measured;
        private float _measuredTime;

        public float[] LaneOffsets => _laneOffsets;

        public float RouteTime(float pathTime) =>
            pathTime - _delay + Mathf.Sin(pathTime * _surgeFrequency + _cycleOffset * FullTurn) * _surgeTime;

        public void Place(Vector3 point, Vector3 heading)
        {
            _goal = point;
            _heading = heading;
            _speed = _referenceSpeed;
            _slope = 0f;
            point.y = GroundAt(point, point.y + _groundProbeHeight, point.y) + _footOffset;
            transform.position = point;
        }

        public void MoveTo(Vector3 point) => _goal = point;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            Quaternion rotation = transform.rotation;
            _tilt = Quaternion.Inverse(Quaternion.Euler(0f, rotation.eulerAngles.y, 0f)) * rotation;
        }

        private void MeasureFeet()
        {
            float minY = float.MaxValue;
            foreach (SkinnedMeshRenderer skinned in GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var baked = new Mesh();
                skinned.BakeMesh(baked, true);
                Matrix4x4 toWorld = Matrix4x4.TRS(skinned.transform.position, skinned.transform.rotation, Vector3.one);
                foreach (Vector3 v in baked.vertices)
                {
                    minY = Mathf.Min(minY, toWorld.MultiplyPoint3x4(v).y);
                }

                Destroy(baked);
            }

            if (minY == float.MaxValue)
            {
                foreach (Renderer r in GetComponentsInChildren<MeshRenderer>())
                {
                    minY = Mathf.Min(minY, r.bounds.min.y);
                }
            }

            if (minY != float.MaxValue)
            {
                float offset = transform.position.y - minY;
                _footOffset = _measured ? Mathf.Max(_footOffset, offset) : offset;
            }
        }

        private void OnEnable()
        {
            _animator.speed = _animationSpeed;
            _animator.Play(0, 0, _cycleOffset);
        }

        private void LateUpdate()
        {
            float deltaTime = Mathf.Max(Time.deltaTime, MinDeltaTime);
            Vector3 current = transform.position;
            Vector3 next = _goal;
            Vector3 move = next - current;
            move.y = 0f;
            float planar = move.magnitude;
            if (move.sqrMagnitude > MinMoveSqr)
            {
                _heading = Vector3.Slerp(_heading, move / planar, Easing.Damp(_headingSharpness, deltaTime));
            }

            float feet = current.y - _footOffset;
            float ground = GroundAt(next, feet + _stepHeight, feet);
            next.y = Mathf.Lerp(current.y, ground + _footOffset, Easing.Damp(_heightSharpness, deltaTime));

            _speed = Mathf.Lerp(_speed, planar / deltaTime, Easing.Damp(_speedSharpness, deltaTime));
            _animator.speed = _animationSpeed * Mathf.Clamp(_speed / _referenceSpeed, _minGait, _maxGait);

            float rise = move.sqrMagnitude > MinMoveSqr ? Mathf.Clamp((next.y - current.y) / planar, -_maxSlope, _maxSlope) : 0f;
            _slope = Mathf.Lerp(_slope, rise, Easing.Damp(_slopeSharpness, deltaTime));

            transform.position = next;
            transform.rotation = Quaternion.LookRotation(_heading + Vector3.up * _slope, Vector3.up) * Quaternion.Euler(0f, ModelYaw, 0f) * _tilt;

            if (_measuredTime < _feetMeasureTime)
            {
                _measuredTime += Time.deltaTime;
                MeasureFeet();
                _measured = true;
            }
        }

        private float GroundAt(Vector3 p, float probeTop, float fallback)
        {
            var origin = new Vector3(p.x, probeTop, p.z);
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, _hits, _groundProbeDistance, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                Transform t = hit.collider.transform;
                if (t.IsChildOf(transform) || t.IsChildOf(transform.parent) || hit.collider is CharacterController
                    || hit.collider.GetComponentInParent<Door>() != null)
                {
                    continue;
                }

                best = Mathf.Max(best, hit.point.y);
            }

            return best == float.MinValue ? fallback : best;
        }
    }
}
