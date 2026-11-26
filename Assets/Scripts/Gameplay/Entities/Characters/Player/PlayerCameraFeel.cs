using System;
using Services;
using Unity.Cinemachine;
using UnityEngine;

namespace Gameplay.Entities.Characters.Player
{
    [Serializable]
    public sealed class PlayerCameraFeel
    {
        private const float SpringStep = 1f / 120f;

        [SerializeField] private CinemachineCamera _firstPersonCamera;
        [SerializeField] private CinemachineCamera _thirdPersonCamera;
        [SerializeField] private CinemachineThirdPersonFollow _follow;

        [Header("Field of view")]
        [SerializeField] private float _sprintFovBoost = 6f;
        [SerializeField] private float _aimFovDrop = 5f;
        [SerializeField] private float _fovSharpness = 6f;

        [Header("First person")]
        [SerializeField] private bool _isBobEnabled = true;
        [SerializeField] private Vector2 _bobAmplitude = new(0.007f, 0.011f);
        [SerializeField] private float _runBobMultiplier = 1.4f;
        [SerializeField] private float _bobSharpness = 8f;
        [SerializeField] private float _strafeRoll = 1.2f;
        [SerializeField] private float _rollSharpness = 8f;

        [Header("Landing")]
        [SerializeField] private float _dipPerSpeed = 0.012f;
        [SerializeField] private float _maxDip = 0.12f;
        [SerializeField] private float _springStiffness = 140f;
        [SerializeField] private float _springDamping = 16f;
        [SerializeField, Range(0f, 1f)] private float _thirdPersonDipScale = 0.5f;

        [Header("Third person")]
        [SerializeField] private float _sprintDistance = 0.35f;
        [SerializeField] private float _carryDistance = 0.5f;
        [SerializeField] private float _aimDistance = 1.9f;
        [SerializeField] private Vector3 _aimShoulder = new(0.6f, 0.15f, 0f);
        [SerializeField] private float _distanceSharpness = 4f;
        [SerializeField] private float _hideBodyDistance = 0.55f;

        private PlayerViewLogic _view;
        private Transform _head;
        private Transform _brain;
        private Vector3 _firstPersonRest;
        private float _baseFov;
        private float _baseDistance;
        private Vector3 _baseShoulder;
        private float _fov;
        private float _distanceOffset;
        private float _bobWeight;
        private float _roll;
        private float _dip;
        private float _dipVelocity;

        public void Init(PlayerViewLogic view, Transform head)
        {
            _view = view;
            _head = head;
            _brain = Camera.main.transform;
            _firstPersonRest = _firstPersonCamera.transform.localPosition;
            _baseFov = _firstPersonCamera.Lens.FieldOfView;
            _fov = _baseFov;
            _baseDistance = _follow.CameraDistance;
            _baseShoulder = _follow.ShoulderOffset;
        }

        public void Land(float impact) => Dip(Mathf.Min(impact * _dipPerSpeed, _maxDip));

        public void Dip(float depth)
        {
            // An undamped spring kicked with v₀ peaks at x = v₀ / √k, so v₀ = depth · √k reaches the wanted depth.
            _dipVelocity -= depth * Mathf.Sqrt(_springStiffness);
        }

        public void Tick(Vector3 localVelocity, float runSpeed, float stepPhase, bool isGrounded, bool isRunning, bool isCarrying, float aimWeight, float deltaTime)
        {
            float planarSpeed = new Vector2(localVelocity.x, localVelocity.z).magnitude;
            TickSpring(deltaTime);

            float sprint = isRunning && planarSpeed > 0.1f ? Mathf.Clamp01(planarSpeed / runSpeed) : 0f;
            float targetFov = _baseFov + _sprintFovBoost * sprint - _aimFovDrop * aimWeight;
            _fov = Mathf.Lerp(_fov, targetFov, Easing.Damp(_fovSharpness, deltaTime));
            SetFov(_firstPersonCamera, _fov);
            SetFov(_thirdPersonCamera, _fov);

            TickFirstPerson(localVelocity, planarSpeed, runSpeed, stepPhase, isGrounded, isRunning, deltaTime);
            TickThirdPerson(sprint, isCarrying, aimWeight, deltaTime);
        }

        // Critically-damped-ish spring: a = −k·x − c·v pulls the landing dip back to rest without wobbling.
        // Integrated in small fixed steps: one long frame would overshoot and blow the spring up to infinity.
        private void TickSpring(float deltaTime)
        {
            for (float left = deltaTime; left > 0f; left -= SpringStep)
            {
                float step = Mathf.Min(left, SpringStep);
                float acceleration = -_springStiffness * _dip - _springDamping * _dipVelocity;
                _dipVelocity += acceleration * step;
                _dip += _dipVelocity * step;
            }
        }

        private void TickFirstPerson(Vector3 localVelocity, float planarSpeed, float runSpeed, float stepPhase, bool isGrounded, bool isRunning, float deltaTime)
        {
            float targetWeight = _isBobEnabled && isGrounded ? Mathf.Clamp01(planarSpeed / runSpeed * 2f) : 0f;
            _bobWeight = Mathf.Lerp(_bobWeight, targetWeight, Easing.Damp(_bobSharpness, deltaTime));
            // One full bob cycle covers two steps: the dip bottoms out on every footstep, the sway alternates sides.
            float bobPhase = stepPhase * Mathf.PI;
            float amplitude = isRunning ? _runBobMultiplier : 1f;
            var bob = new Vector3(Mathf.Sin(bobPhase) * _bobAmplitude.x, -Mathf.Abs(Mathf.Cos(bobPhase)) * _bobAmplitude.y, 0f) * (amplitude * _bobWeight);

            float targetRoll = -localVelocity.x / runSpeed * _strafeRoll;
            _roll = Mathf.Lerp(_roll, targetRoll, Easing.Damp(_rollSharpness, deltaTime));

            Transform camera = _firstPersonCamera.transform;
            camera.localPosition = _firstPersonRest + bob + Vector3.up * _dip;
            camera.localRotation = Quaternion.Euler(0f, 0f, _roll);
        }

        private void TickThirdPerson(float sprint, bool isCarrying, float aimWeight, float deltaTime)
        {
            float targetOffset = _sprintDistance * sprint + (isCarrying ? _carryDistance : 0f);
            _distanceOffset = Mathf.Lerp(_distanceOffset, targetOffset, Easing.Damp(_distanceSharpness, deltaTime));
            _follow.CameraDistance = Mathf.Lerp(_baseDistance + _distanceOffset, _aimDistance, aimWeight);
            Vector3 shoulder = Vector3.Lerp(_baseShoulder, _aimShoulder, aimWeight);
            shoulder.y += _dip * _thirdPersonDipScale;
            _follow.ShoulderOffset = shoulder;

            if (_view.IsThirdPerson)
            {
                _view.SetBodyHidden(Vector3.Distance(_brain.position, _head.position) < _hideBodyDistance);
            }
        }

        private static void SetFov(CinemachineCamera camera, float fov)
        {
            LensSettings lens = camera.Lens;
            lens.FieldOfView = fov;
            camera.Lens = lens;
        }
    }
}
