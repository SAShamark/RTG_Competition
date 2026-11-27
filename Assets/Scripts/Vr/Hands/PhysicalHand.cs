using System.Collections.Generic;
using UnityEngine;

namespace Vr.Hands
{
    public sealed class PhysicalHand : MonoBehaviour
    {
        private const float MinTurnAngle = 0.01f;
        private const float HalfTurn = 180f;
        private const float FullTurn = 360f;

        [SerializeField] private Rigidbody _body;
        [SerializeField] private GameObject _visual;
        [SerializeField] private Collider[] _colliders;
        [SerializeField] private HandFinger[] _fingers;
        [SerializeField] private Transform _grip;
        [SerializeField] private Transform _toolSocket;
        [SerializeField] private Transform _fingerTip;
        [SerializeField, Min(0.005f)] private float _gripRadius = 0.017f;
        [SerializeField, Min(0.01f)] private float _gripHalfLength = 0.05f;
        [SerializeField] private Vector3 _controllerEuler;

        private PhysicalHandSettings _settings;
        private Transform _controller;
        private Vector3 _lastControllerPosition;
        private float _gripValue;
        private float _triggerValue;
        private float _releaseTimer;
        private bool _isVisible = true;
        private bool _isHolding;
        private bool _isIndexFree;
        private float _fittedRadius;

        public IReadOnlyList<Collider> Colliders => _colliders;
        public Transform Grip => _grip;
        public Transform ToolSocket => _toolSocket;
        public Transform FingerTip => _fingerTip;
        public Transform Body => _body.transform;
        public Vector3 Velocity { get; private set; }
        public bool IsVisible => _isVisible;

        private Quaternion TargetRotation => _controller.rotation * Quaternion.Euler(_controllerEuler);
        private Vector3 TargetPosition => _controller.position - TargetRotation * _grip.localPosition;

        public void Initialize(PhysicalHandSettings settings, Transform controller)
        {
            _settings = settings;
            _controller = controller;
            Transform body = _body.transform;
            body.SetParent(null, false);

            foreach (HandFinger finger in _fingers)
            {
                finger.Initialize();
            }

            Fit(_gripRadius);

            SnapToController();
            _lastControllerPosition = controller.position;
        }

        public void IgnoreCollisions(IReadOnlyList<Collider> others)
        {
            foreach (Collider mine in _colliders)
            {
                foreach (Collider other in others)
                {
                    Physics.IgnoreCollision(mine, other, true);
                }
            }
        }

        public void SetVisible(bool isVisible)
        {
            if (_isVisible == isVisible)
            {
                return;
            }

            _isVisible = isVisible;
            _visual.SetActive(isVisible);
            RefreshColliders();
            if (isVisible && !_isHolding)
            {
                SnapToController();
            }
        }

        public void SetGrip(float radius, bool isIndexFree)
        {
            _isIndexFree = isIndexFree;
            if (!Mathf.Approximately(radius, _fittedRadius))
            {
                Fit(radius);
            }
        }

        public void ResetGrip() => SetGrip(_gripRadius, false);

        public void SetHolding(bool isHolding)
        {
            if (_isHolding == isHolding)
            {
                return;
            }

            _isHolding = isHolding;
            _body.isKinematic = isHolding;
            if (!isHolding)
            {
                _releaseTimer = _settings.ReleaseGrace;
            }

            RefreshColliders();
            ApplyHold();
        }

        public void ApplyHold()
        {
            if (!_isHolding)
            {
                return;
            }

            Vector3 position = TargetPosition;
            Quaternion rotation = TargetRotation;
            _body.position = position;
            _body.rotation = rotation;
            _body.transform.SetPositionAndRotation(position, rotation);
        }

        public void FixedTick(float deltaTime)
        {
            if (_isHolding || !_isVisible)
            {
                return;
            }

            Vector3 target = TargetPosition;
            Quaternion targetRotation = TargetRotation;
            if ((target - _body.position).sqrMagnitude > _settings.SnapDistance * _settings.SnapDistance)
            {
                SnapToController();
                return;
            }

            float strength = _settings.FollowStrength;
            _body.linearVelocity = (target - _body.position) * (strength / deltaTime);

            Quaternion delta = targetRotation * Quaternion.Inverse(_body.rotation);
            delta.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > HalfTurn)
            {
                angle -= FullTurn;
            }

            bool isTurning = Mathf.Abs(angle) > MinTurnAngle && !float.IsNaN(axis.x);
            _body.angularVelocity = isTurning ? axis * (angle * Mathf.Deg2Rad * strength / deltaTime) : Vector3.zero;
        }

        public void Tick(float deltaTime, float grip, float trigger)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            if (_releaseTimer > 0f)
            {
                _releaseTimer -= deltaTime;
                if (_releaseTimer <= 0f)
                {
                    RefreshColliders();
                }
            }

            Vector3 controllerVelocity = (_controller.position - _lastControllerPosition) / deltaTime;
            _lastControllerPosition = _controller.position;
            Velocity = Vector3.Lerp(Velocity, controllerVelocity, 1f - Mathf.Exp(-_settings.VelocitySharpness * deltaTime));

            float blend = 1f - Mathf.Exp(-_settings.CurlSharpness * deltaTime);
            _gripValue = Mathf.Lerp(_gripValue, grip, blend);
            _triggerValue = Mathf.Lerp(_triggerValue, trigger, blend);
            foreach (HandFinger finger in _fingers)
            {
                finger.Tick(_isHolding, _isIndexFree, _gripValue, _triggerValue, blend);
            }
        }

        private void Fit(float radius)
        {
            _fittedRadius = radius;
            var capsule = new GripCapsule(_grip, _gripHalfLength, radius + _settings.FingerRadius);
            foreach (HandFinger finger in _fingers)
            {
                finger.FitTo(capsule);
            }
        }

        private void SnapToController()
        {
            if (_isHolding)
            {
                return;
            }

            Vector3 position = TargetPosition;
            Quaternion rotation = TargetRotation;
            _body.position = position;
            _body.rotation = rotation;
            _body.transform.SetPositionAndRotation(position, rotation);
            if (!_body.isKinematic)
            {
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
            }
        }

        private void RefreshColliders()
        {
            bool isEnabled = _isVisible && !_isHolding && _releaseTimer <= 0f;
            foreach (Collider handCollider in _colliders)
            {
                handCollider.enabled = isEnabled;
            }
        }
    }
}
