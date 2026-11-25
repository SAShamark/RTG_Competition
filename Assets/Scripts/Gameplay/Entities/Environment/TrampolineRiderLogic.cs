using System;
using Gameplay.Entities.Characters.Player;
using UnityEngine;

namespace Gameplay.Entities.Environment
{
    [Serializable]
    public sealed class TrampolineRiderLogic
    {
        [SerializeField] private Collider _solid;
        [SerializeField, Min(0f)] private float _surfaceSkin = 0.12f;

        [Header("Impact")]
        [SerializeField, Min(0f)] private float _minBounceSpeed = 3f;
        [SerializeField, Min(0f)] private float _impactScale = 0.45f;
        [SerializeField, Min(0f)] private float _standSag = 0.03f;

        [Header("Launch")]
        [SerializeField, Range(0f, 1f)] private float _restitution = 0.6f;
        [SerializeField, Min(0f)] private float _heldLaunchSpeed = 9.5f;
        [SerializeField, Min(0f)] private float _minLaunchSpeed = 2f;
        [SerializeField, Min(0f)] private float _maxLaunchSpeed = 12f;

        private Transform _surface;
        private float _radius;
        private Collider _body;
        private PlayerJumpLogic _jump;
        private bool _isPassing;
        private float _impactSpeed;

        public bool IsRiding => _body != null && _jump.IsRiding;
        public bool IsStanding { get; private set; }
        public Vector2 Point { get; private set; }
        public float Sag => IsStanding ? _standSag : 0f;

        public event Action<float, float> Impacted;

        public void Init(Transform surface, float radius)
        {
            _surface = surface;
            _radius = radius;
        }

        public void Enter(Collider other)
        {
            if (_body == null && other.TryGetComponent(out FirstPersonController player))
            {
                _body = other;
                _jump = player.Jump;
            }
        }

        public void Exit(Collider other)
        {
            if (other == _body)
            {
                Release();
            }
        }

        public void Release()
        {
            if (_body == null)
            {
                return;
            }

            if (_jump.IsRiding)
            {
                Launch();
            }

            SetPassing(false);
            IsStanding = false;
            _body = null;
            _jump = null;
        }

        public void Tick(float offset, float velocity)
        {
            if (_body == null)
            {
                return;
            }

            if (!_body.enabled)
            {
                Release();
                return;
            }

            Bounds bounds = _body.bounds;
            Vector3 feet = _surface.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
            Vector2 point = new(feet.x, feet.z);
            bool isOver = point.sqrMagnitude <= _radius * _radius;

            if (_jump.IsRiding)
            {
                Point = point;
                if (offset >= 0f && velocity > 0f)
                {
                    Launch();
                }

                return;
            }

            IsStanding = false;
            if (feet.y > _surfaceSkin)
            {
                SetPassing(isOver && _jump.VerticalSpeed <= -_minBounceSpeed);
                return;
            }

            if (_isPassing && feet.y <= 0f)
            {
                _impactSpeed = -_jump.VerticalSpeed;
                _jump.BeginRide();
                Point = point;
                Impacted?.Invoke(_impactSpeed * _impactScale, feet.y);
            }
            else if (!_isPassing && isOver && _jump.IsGrounded)
            {
                IsStanding = true;
                Point = point;
            }
        }

        public void Follow(float velocity)
        {
            if (IsRiding)
            {
                _jump.Ride(velocity);
            }
        }

        private void Launch()
        {
            float speed = _impactSpeed * _restitution;
            if (_jump.IsJumpHeld)
            {
                speed = Mathf.Max(speed, _heldLaunchSpeed);
            }

            _jump.EndRide(Mathf.Clamp(speed, _minLaunchSpeed, _maxLaunchSpeed));
        }

        private void SetPassing(bool isPassing)
        {
            if (_isPassing == isPassing)
            {
                return;
            }

            _isPassing = isPassing;
            Physics.IgnoreCollision(_body, _solid, isPassing);
        }
    }
}
