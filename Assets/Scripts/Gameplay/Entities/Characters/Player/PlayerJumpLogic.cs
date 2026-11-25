using System;
using UnityEngine;

namespace Gameplay.Entities.Characters.Player
{
    [Serializable]
    public sealed class PlayerJumpLogic
    {
        private const float GroundedFallSpeed = -2f;
        private const float ProbeSkin = 0.05f;

        [SerializeField] private float _jumpHeight = 1.35f;
        [SerializeField] private float _coyoteTime = 0.12f;
        [SerializeField] private float _jumpBuffer = 0.15f;

        [Header("Arc")]
        [SerializeField] private float _fallGravityMultiplier = 1.8f;
        [SerializeField] private float _releaseGravityMultiplier = 1.5f;
        [SerializeField] private float _apexSpeedThreshold = 1.2f;
        [SerializeField] private float _apexGravityMultiplier = 0.55f;
        [SerializeField] private float _terminalSpeed = -22f;

        [Header("Ground")]
        [SerializeField] private float _groundProbeDistance = 0.35f;
        [SerializeField] private LayerMask _groundMask = ~0;
        [SerializeField] private float _minAirTime = 0.15f;
        [SerializeField] private float _hardLandingSpeed = 9f;

        private CharacterController _controller;
        private float _sinceGrounded;
        private float _sinceJumpPressed = float.MaxValue;
        private bool _wasGrounded = true;
        private bool _isRising;

        public float VerticalSpeed { get; private set; }
        public bool IsGrounded => _controller.isGrounded;
        public bool IsNearGround { get; private set; } = true;
        public bool IsJumpHeld { get; private set; }
        public bool IsRiding { get; private set; }
        public float HardLandingSpeed => _hardLandingSpeed;
        public float HeightBoost { get; set; }

        public event Action Jumped;
        public event Action<float> Landed;

        public void Init(CharacterController controller)
        {
            _controller = controller;
        }

        public void Tick(bool jumpPressed, bool jumpHeld, bool canJump, float gravity, float deltaTime)
        {
            IsJumpHeld = jumpHeld;
            if (IsRiding)
            {
                _sinceJumpPressed = float.MaxValue;
                IsNearGround = true;
                return;
            }

            bool isGrounded = _controller.isGrounded;
            if (isGrounded && !_wasGrounded && _sinceGrounded >= _minAirTime)
            {
                Landed?.Invoke(-VerticalSpeed);
            }

            _wasGrounded = isGrounded;
            _sinceGrounded = isGrounded ? 0f : _sinceGrounded + deltaTime;
            _sinceJumpPressed = jumpPressed ? 0f : _sinceJumpPressed + deltaTime;

            if (canJump && _sinceJumpPressed <= _jumpBuffer && _sinceGrounded <= _coyoteTime)
            {
                // v = sqrt(2 * h * |g|) reaches exactly _jumpHeight at the apex.
                VerticalSpeed = Mathf.Sqrt(2f * _jumpHeight * (1f + HeightBoost) * -gravity);
                _sinceJumpPressed = float.MaxValue;
                _sinceGrounded = float.MaxValue;
                _wasGrounded = false;
                _isRising = true;
                IsNearGround = false;
                Jumped?.Invoke();
                return;
            }

            if (isGrounded && VerticalSpeed <= 0f)
            {
                VerticalSpeed = GroundedFallSpeed;
                _isRising = false;
            }
            else
            {
                VerticalSpeed = Mathf.Max(_terminalSpeed, VerticalSpeed + gravity * GravityMultiplier(jumpHeld) * deltaTime);
            }

            IsNearGround = isGrounded || (VerticalSpeed <= 0f && ProbeGround());
        }

        public void BeginRide()
        {
            IsRiding = true;
            _isRising = false;
        }

        public void Ride(float speed)
        {
            VerticalSpeed = speed;
        }

        public void EndRide(float launchSpeed)
        {
            IsRiding = false;
            VerticalSpeed = launchSpeed;
            _sinceGrounded = float.MaxValue;
            _wasGrounded = false;
            _isRising = true;
            IsNearGround = false;
            Jumped?.Invoke();
        }

        public void HandleCollision(CollisionFlags flags)
        {
            if ((flags & CollisionFlags.Above) != 0 && VerticalSpeed > 0f)
            {
                VerticalSpeed = 0f;
            }
        }

        private float GravityMultiplier(bool jumpHeld)
        {
            if (VerticalSpeed < 0f)
            {
                _isRising = false;
                return _fallGravityMultiplier;
            }

            if (_isRising && !jumpHeld)
            {
                return _releaseGravityMultiplier;
            }

            return VerticalSpeed < _apexSpeedThreshold ? _apexGravityMultiplier : 1f;
        }

        private bool ProbeGround()
        {
            Transform body = _controller.transform;
            float radius = _controller.radius;
            Vector3 origin = body.TransformPoint(_controller.center) + Vector3.down * (_controller.height * 0.5f - radius - ProbeSkin);
            return Physics.SphereCast(origin, radius, Vector3.down, out _, _groundProbeDistance + ProbeSkin, _groundMask, QueryTriggerInteraction.Ignore);
        }
    }
}
