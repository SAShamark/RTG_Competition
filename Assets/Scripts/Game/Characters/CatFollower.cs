using UnityEngine;

namespace Game.Characters
{
    public class CatFollower : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private Vector3 _offset = new(0.6f, 0f, -0.8f);
        [SerializeField] private float _followSharpness = 6f;
        [SerializeField] private float _groundY;

        private Vector3 _lastTargetPos;
        private float _footOffset;
        private Vector3 _heading = Vector3.forward;

        public void Setup(Transform target, Vector3 offset, float groundY)
        {
            _target = target;
            _offset = offset;
            _groundY = groundY;
        }

        private Quaternion _tilt = Quaternion.identity;
        private bool _measured;

        private void Awake()
        {
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
            _lastTargetPos = _target.position;
            transform.position = DesiredPosition();
        }

        private void LateUpdate()
        {
            Vector3 move = _target.position - _lastTargetPos;
            move.y = 0f;
            if (move.sqrMagnitude > 0.00001f)
            {
                _heading = Vector3.Slerp(_heading, move.normalized, 0.2f);
            }

            _lastTargetPos = _target.position;
            Vector3 desired = DesiredPosition();
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-_followSharpness * Time.deltaTime));
            transform.rotation = Quaternion.LookRotation(_heading, Vector3.up) * Quaternion.Euler(0f, 90f, 0f) * _tilt;
            MeasureFeet();
            _measured = true;
        }

        private Vector3 DesiredPosition()
        {
            Quaternion frame = Quaternion.LookRotation(_heading, Vector3.up);
            Vector3 p = _target.position + frame * _offset;
            p.y = GroundAt(p, Mathf.Max(_groundY, _target.position.y)) + _footOffset;
            return p;
        }

        private float GroundAt(Vector3 p, float fallback)
        {
            RaycastHit[] hits = Physics.RaycastAll(p + Vector3.up * 1.2f, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MinValue;
            foreach (RaycastHit hit in hits)
            {
                Transform t = hit.collider.transform;
                if (t.IsChildOf(transform) || (t.IsChildOf(_target.root)) || hit.collider is CharacterController
                    || hit.collider.GetComponentInParent<Game.World.Door>() != null)
                {
                    continue;
                }

                best = Mathf.Max(best, hit.point.y);
            }

            return best == float.MinValue ? fallback : best;
        }
    }
}
