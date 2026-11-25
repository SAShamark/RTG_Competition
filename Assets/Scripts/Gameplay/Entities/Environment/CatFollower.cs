using Services;
using Gameplay.Entities.Interactive;
using UnityEngine;

namespace Gameplay.Entities.Environment
{
    public sealed class CatFollower : MonoBehaviour
    {
        private const float MinMoveSqr = 0.00001f;
        private const float ModelYaw = 90f;
        private const int MaxGroundHits = 16;

        [SerializeField] private Transform _target;
        [SerializeField] private Vector3 _offset = new(0.6f, 0f, -0.8f);
        [SerializeField] private float _followSharpness = 6f;
        [SerializeField] private float _groundY;
        [SerializeField] private float _headingSharpness = 13f;
        [SerializeField] private float _groundProbeHeight = 1.2f;
        [SerializeField] private float _groundProbeDistance = 3f;
        [SerializeField] private float _stepHeight = 0.2f;
        [SerializeField] private float _heightSharpness = 18f;
        [SerializeField] private float _feetMeasureTime = 0.4f;
        [SerializeField] private float _animationSpeed = 1f;
        [SerializeField, Range(0f, 1f)] private float _cycleOffset;

        private readonly RaycastHit[] _groundHits = new RaycastHit[MaxGroundHits];
        private Vector3 _lastTargetPos;
        private float _footOffset;
        private Vector3 _heading = Vector3.forward;
        private Quaternion _tilt = Quaternion.identity;
        private Animator _animator;
        private bool _measured;
        private float _measuredTime;

        public void SetHeading(Vector3 heading) => _heading = heading;

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
            _lastTargetPos = _target.position;
            Vector3 start = DesiredFlat();
            float top = Mathf.Max(_groundY, _target.position.y) + _groundProbeHeight;
            start.y = GroundAt(start, top, Mathf.Max(_groundY, _target.position.y)) + _footOffset;
            transform.position = start;
        }

        private void LateUpdate()
        {
            Vector3 move = _target.position - _lastTargetPos;
            move.y = 0f;
            if (move.sqrMagnitude > MinMoveSqr)
            {
                _heading = Vector3.Slerp(_heading, move.normalized, Easing.Damp(_headingSharpness, Time.deltaTime));
            }

            _lastTargetPos = _target.position;
            Vector3 current = transform.position;
            Vector3 next = Vector3.Lerp(current, DesiredFlat(), Easing.Damp(_followSharpness, Time.deltaTime));
            float feet = current.y - _footOffset;
            float ground = GroundAt(next, feet + _stepHeight, feet);
            next.y = Mathf.Lerp(current.y, ground + _footOffset, Easing.Damp(_heightSharpness, Time.deltaTime));
            transform.position = next;
            transform.rotation = Quaternion.LookRotation(_heading, Vector3.up) * Quaternion.Euler(0f, ModelYaw, 0f) * _tilt;

            if (_measuredTime < _feetMeasureTime)
            {
                _measuredTime += Time.deltaTime;
                MeasureFeet();
                _measured = true;
            }
        }

        private Vector3 DesiredFlat()
        {
            Quaternion frame = Quaternion.LookRotation(_heading, Vector3.up);
            return _target.position + frame * _offset;
        }

        private float GroundAt(Vector3 p, float probeTop, float fallback)
        {
            var origin = new Vector3(p.x, probeTop, p.z);
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, _groundHits, _groundProbeDistance, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _groundHits[i];
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
