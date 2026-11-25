using System;
using Gameplay.Entities.Characters.Player;
using Services;
using Unity.Cinemachine;
using UnityEngine;

namespace Gameplay.Cinematics.UncleBurst
{
    [Serializable]
    public sealed class UncleBurstLook
    {
        private const float MinDirectionSqr = 0.01f;
        private const float HalfTurn = 180f;
        private const float FullTurn = 360f;

        [SerializeField] private float _sharpness = 6f;
        [SerializeField] private float _maxTurnSpeed = 220f;
        [SerializeField] private float _maxPitchDown = 25f;
        [SerializeField] private float _maxPitchUp = -40f;

        [Header("FOV kick")]
        [SerializeField] private float _fovKick = 9f;
        [SerializeField] private float _fovKickTime = 0.8f;

        private CinemachineCamera _camera;
        private Transform _target;
        private FirstPersonController _player;
        private float _baseFov;
        private float _time;

        public void Init(CinemachineCamera camera, Transform target, FirstPersonController player)
        {
            _camera = camera;
            _target = target;
            _player = player;
            _baseFov = camera.Lens.FieldOfView;
        }

        public void Begin()
        {
            _time = 0f;
            _camera.transform.SetPositionAndRotation(_player.Head.position, _player.Head.rotation);
            SetFov(_baseFov + _fovKick);
        }

        public void End() => SetFov(_baseFov);

        public void Tick(float deltaTime)
        {
            _time += deltaTime;
            SetFov(_baseFov + _fovKick * (1f - Easing.OutCubic(Mathf.Clamp01(_time / _fovKickTime))));

            Transform cam = _camera.transform;
            Vector3 toTarget = _target.position - cam.position;
            if (toTarget.sqrMagnitude < MinDirectionSqr)
            {
                return;
            }

            Vector3 desiredEuler = Quaternion.LookRotation(toTarget.normalized, Vector3.up).eulerAngles;
            float pitch = desiredEuler.x > HalfTurn ? desiredEuler.x - FullTurn : desiredEuler.x;
            Quaternion desired = Quaternion.Euler(Mathf.Clamp(pitch, _maxPitchUp, _maxPitchDown), desiredEuler.y, 0f);
            Quaternion smoothed = Quaternion.Slerp(cam.rotation, desired, Easing.Damp(_sharpness, deltaTime));
            cam.rotation = Quaternion.RotateTowards(cam.rotation, smoothed, _maxTurnSpeed * deltaTime);

            Vector3 camEuler = cam.rotation.eulerAngles;
            _player.transform.rotation = Quaternion.Euler(0f, camEuler.y, 0f);
            _player.Head.localRotation = Quaternion.Euler(camEuler.x, 0f, 0f);
        }

        private void SetFov(float fov)
        {
            LensSettings lens = _camera.Lens;
            lens.FieldOfView = fov;
            _camera.Lens = lens;
        }
    }
}
