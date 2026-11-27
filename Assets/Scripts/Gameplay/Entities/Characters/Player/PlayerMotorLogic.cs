using System;
using UnityEngine;

namespace Gameplay.Entities.Characters.Player
{
    [Serializable]
    public sealed class PlayerMotorLogic
    {
        [SerializeField] private float _walkSpeed = 3.2f;
        [SerializeField] private float _runSpeed = 5.5f;
        [SerializeField, Range(0f, 1f)] private float _backwardRunFactor = 0.76f;
        [SerializeField, Range(0f, 1f)] private float _strafeRunFactor = 0.8f;
        [SerializeField] private float _gravity = -18f;

        [Header("Acceleration")]
        [SerializeField] private float _groundAcceleration = 35f;
        [SerializeField] private float _groundDeceleration = 45f;
        [SerializeField] private float _airAcceleration = 7f;

        [Header("Hard landing")]
        [SerializeField] private float _hardLandingRecovery = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _hardLandingSpeedFactor = 0.35f;

        private CharacterController _controller;
        private Transform _body;
        private PlayerJumpLogic _jump;
        private float _landingRecovery;

        public float WalkSpeed => _walkSpeed;
        public float RunSpeed => _runSpeed;
        public float Gravity => _gravity;
        public bool IsGrounded => _controller.isGrounded;
        public float LandingSpeedFactor => _landingRecovery > 0f ? _hardLandingSpeedFactor : 1f;
        public Vector3 PlanarVelocity { get; set; }

        public void Init(CharacterController controller, Transform body, PlayerJumpLogic jump)
        {
            _controller = controller;
            _body = body;
            _jump = jump;
        }

        public void Tick(float deltaTime) => _landingRecovery = Mathf.Max(0f, _landingRecovery - deltaTime);

        public void Stop() => PlanarVelocity = Vector3.zero;

        public void BeginLandingRecovery() => _landingRecovery = _hardLandingRecovery;

        public float RunSpeedFor(Vector2 input)
        {
            if (input.y > 0f)
            {
                return _runSpeed;
            }

            return _runSpeed * (input.y < 0f ? _backwardRunFactor : _strafeRunFactor);
        }

        public Vector3 Steer(Vector2 input, Vector3 forward, float speed, float deltaTime)
        {
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 target = (forward * input.y + right * input.x).normalized * speed;
            PlanarVelocity = Vector3.MoveTowards(PlanarVelocity, target, Acceleration(target) * deltaTime);
            return target;
        }

        public Vector3 Move(Vector3 push, float deltaTime)
        {
            Vector3 move = PlanarVelocity + push;
            move.y = _jump.VerticalSpeed;
            _jump.HandleCollision(_controller.Move(move * deltaTime));
            return move;
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            _controller.enabled = false;
            _body.SetPositionAndRotation(position, rotation);
            _controller.enabled = true;
            Stop();
        }

        private float Acceleration(Vector3 target)
        {
            if (!_jump.IsNearGround)
            {
                return _airAcceleration;
            }

            return target.sqrMagnitude >= PlanarVelocity.sqrMagnitude ? _groundAcceleration : _groundDeceleration;
        }
    }
}
