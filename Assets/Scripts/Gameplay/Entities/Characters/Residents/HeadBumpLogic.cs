using System;
using Services;
using UnityEngine;

namespace Gameplay.Entities.Characters.Residents
{
    [Serializable]
    public sealed class HeadBumpLogic
    {
        private const int HitBufferSize = 8;
        private const float MinTangentSqr = 0.0001f;

        [SerializeField] private LayerMask _obstacleMask = ~0;
        [SerializeField, Min(0f)] private float _headRadius = 0.13f;
        [SerializeField, Min(0f)] private float _headCenterOffset = 0.1f;
        [SerializeField, Range(0f, 120f)] private float _maxBend = 80f;
        [SerializeField, Min(0f)] private float _pushSharpness = 40f;
        [SerializeField, Min(0f)] private float _releaseSharpness = 5f;

        [Header("Bump")]
        [SerializeField, Min(0f)] private float _bumpSpeed = 0.5f;
        [SerializeField, Min(0f)] private float _bumpCooldown = 1.2f;
        [SerializeField, Min(0f)] private float _recoil = 0.5f;
        [SerializeField, Min(0f)] private float _recoilSharpness = 6f;

        private readonly RaycastHit[] _hits = new RaycastHit[HitBufferSize];

        private Transform _root;
        private Transform _spine;
        private Transform _head;
        private Transform _carrier;
        private Quaternion _spineRest;
        private Quaternion _bend = Quaternion.identity;
        private Vector3 _lastCenter;
        private float _sinceBump;
        private float _recoilLeft;
        private bool _isTouching;
        private bool _isActive;

        public Vector3 HeadCenter => _lastCenter;

        public void Init(Animator animator)
        {
            if (animator == null || !animator.isHuman)
            {
                return;
            }

            _root = animator.transform;
            _spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            _head = animator.GetBoneTransform(HumanBodyBones.Head);
        }

        public void Begin(Transform carrier)
        {
            if (_spine == null || _head == null)
            {
                return;
            }

            _carrier = carrier;
            _spineRest = _spine.localRotation;
            _bend = Quaternion.identity;
            _lastCenter = Center();
            _sinceBump = _bumpCooldown;
            _recoilLeft = 0f;
            _isTouching = false;
            _isActive = true;
        }

        public void End()
        {
            if (!_isActive)
            {
                return;
            }

            _isActive = false;
            _spine.localRotation = _spineRest;
        }

        public bool Tick(float deltaTime)
        {
            if (!_isActive || deltaTime <= 0f)
            {
                return false;
            }

            _spine.localRotation = _spineRest;
            _sinceBump += deltaTime;
            _recoilLeft = Mathf.Lerp(_recoilLeft, 0f, Easing.Damp(_recoilSharpness, deltaTime));

            Vector3 pivot = _spine.position;
            Vector3 center = Center();
            Vector3 toHead = center - pivot;
            float length = toHead.magnitude;
            Vector3 direction = toHead / length;
            Vector3 velocity = (center - _lastCenter) / deltaTime;
            _lastCenter = center;

            Quaternion needed = Quaternion.identity;
            bool isBump = false;
            bool isTouching = TryFindObstacle(pivot, direction, length, out RaycastHit hit);
            if (isTouching)
            {
                needed = Quaternion.RotateTowards(Quaternion.identity,
                    Quaternion.FromToRotation(direction, ClearDirection(pivot, direction, length, hit)), _maxBend);

                if (!_isTouching && _sinceBump >= _bumpCooldown && Vector3.Dot(velocity, -hit.normal) >= _bumpSpeed)
                {
                    isBump = true;
                    _sinceBump = 0f;
                    _recoilLeft = _recoil;
                }
            }

            _isTouching = isTouching;

            Quaternion local = Quaternion.Inverse(_root.rotation) * Quaternion.SlerpUnclamped(Quaternion.identity, needed, 1f + _recoilLeft) * _root.rotation;
            _bend = Quaternion.Slerp(_bend, local, Easing.Damp(isTouching ? _pushSharpness : _releaseSharpness, deltaTime));
            _spine.rotation = _root.rotation * _bend * Quaternion.Inverse(_root.rotation) * _spine.rotation;
            return isBump;
        }

        private Vector3 Center() => _head.position + (_head.position - _spine.position).normalized * _headCenterOffset;

        private bool TryFindObstacle(Vector3 pivot, Vector3 direction, float length, out RaycastHit nearest)
        {
            nearest = default;
            float best = float.MaxValue;
            int count = Physics.SphereCastNonAlloc(pivot, _headRadius, direction, _hits, length, _obstacleMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                Transform other = hit.collider.transform;
                // distance 0 means the cast started inside the collider: no usable contact plane
                if (hit.distance <= 0f || hit.distance >= best || other.IsChildOf(_carrier) || other.IsChildOf(_root))
                {
                    continue;
                }

                best = hit.distance;
                nearest = hit;
            }

            return best < float.MaxValue;
        }

        private Vector3 ClearDirection(Vector3 pivot, Vector3 direction, float length, RaycastHit hit)
        {
            Vector3 normal = hit.normal;
            // the head sphere stays outside the wall plane while dot(d, n) · L + h ≥ r,
            // h being the spine's height above the plane; the closest such d keeps the tangent of the current direction
            float height = Vector3.Dot(pivot - hit.point, normal);
            float minAlongNormal = Mathf.Clamp((_headRadius - height) / length, -1f, 1f);
            if (Vector3.Dot(direction, normal) >= minAlongNormal)
            {
                return direction;
            }

            Vector3 tangent = Vector3.ProjectOnPlane(direction, normal);
            if (tangent.sqrMagnitude < MinTangentSqr)
            {
                tangent = Vector3.ProjectOnPlane(_carrier.up - _carrier.forward, normal);
            }

            return normal * minAlongNormal + tangent.normalized * Mathf.Sqrt(1f - minAlongNormal * minAlongNormal);
        }
    }
}
