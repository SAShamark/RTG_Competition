using System;
using Services;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gameplay.Cinematics.IntroDrift
{
    [Serializable]
    public sealed class DriftPostFX
    {
        [SerializeField] private Volume _volume;
        [SerializeField] private Transform _camera;
        [SerializeField] private float _focusHeight = 1.3f;
        [SerializeField, Min(0.01f)] private float _releaseTime = 3f;

        private DepthOfField _depthOfField;
        private float _release = -1f;

        public bool IsReleasing => _release >= 0f;

        public void Init()
        {
            _volume.profile.TryGet(out _depthOfField);
            _volume.weight = 0f;
        }

        public void Begin()
        {
            _release = -1f;
            _volume.weight = 1f;
        }

        public void Tick(Vector3 truckPosition)
        {
            _depthOfField.focusDistance.value = Vector3.Distance(_camera.position, truckPosition + Vector3.up * _focusHeight);
        }

        public void BeginRelease() => _release = 0f;

        public void TickRelease(float deltaTime)
        {
            _release += deltaTime;
            if (_release >= _releaseTime)
            {
                Stop();
                return;
            }

            _volume.weight = 1f - Easing.Smooth(_release / _releaseTime);
        }

        public void Stop()
        {
            _release = -1f;
            _volume.weight = 0f;
        }
    }
}
