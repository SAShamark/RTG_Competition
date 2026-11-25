using System;
using Gameplay.Entities.Environment;
using UnityEngine;

namespace Gameplay.Cinematics.IntroDrift
{
    [Serializable]
    public sealed class DriftChassis
    {
        private const float Rolling = 1f;

        [SerializeField] private TruckControl _truck;
        [SerializeField] private Transform _pitch;
        [SerializeField, Min(1f)] private float _fullSlip = 28f;
        [SerializeField, Range(0f, 60f)] private float _maxSteer = 35f;
        [SerializeField, Min(1f)] private float _rearSpin = 2.5f;
        [SerializeField] private float _brakeDive = 2.5f;

        private Quaternion _restPitch;
        private bool _isActive;

        public void Begin()
        {
            _restPitch = _pitch.localRotation;
            _isActive = true;
        }

        public void Tick(float slip, float braking)
        {
            // front wheels point where the truck travels, not where its nose points
            _truck.Steer = Mathf.Clamp(-slip, -_maxSteer, _maxSteer);
            _truck.RearSpin = Mathf.Lerp(Rolling, _rearSpin, Mathf.Clamp01(Mathf.Abs(slip) / _fullSlip));
            _pitch.localRotation = _restPitch * Quaternion.Euler(_brakeDive * braking, 0f, 0f);
        }

        public void Stop()
        {
            if (!_isActive)
            {
                return;
            }

            _isActive = false;
            _truck.Steer = 0f;
            _truck.RearSpin = Rolling;
            _pitch.localRotation = _restPitch;
        }
    }
}
