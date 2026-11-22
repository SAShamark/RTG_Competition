using System;
using UnityEngine;

namespace Gameplay.Entities.Environment
{
    [Serializable]
    public sealed class TyreSmokeLogic
    {
        [SerializeField] private Transform _body;
        [SerializeField] private ParticleSystem[] _smoke;
        [SerializeField] private TrailRenderer[] _skidMarks;
        [SerializeField] private float _brakeStart = 6f;
        [SerializeField] private float _brakeFull = 25f;
        [SerializeField] private float _speedFull = 12f;
        [SerializeField] private float _skidThreshold = 0.3f;
        [SerializeField] private float _smoothing = 10f;
        [SerializeField] private float _teleportSpeed = 40f;

        private float[] _baseRates;
        private Vector3 _last;
        private float _speed;
        private float _brake;

        public void Init()
        {
            _baseRates = new float[_smoke.Length];
            for (int i = 0; i < _smoke.Length; i++)
            {
                _baseRates[i] = _smoke[i].emission.rateOverTimeMultiplier;
            }

            _last = _body.position;
            SetStrength(0f);
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            Vector3 position = _body.position;
            float speed = Vector3.Distance(position, _last) / deltaTime;
            _last = position;
            if (speed > _teleportSpeed)
            {
                _speed = 0f;
                _brake = 0f;
                return;
            }

            // Braking = loss of ground speed per second, smoothed because the animation is sampled per frame.
            float brake = (_speed - speed) / deltaTime;
            _brake = Mathf.Lerp(_brake, brake, 1f - Mathf.Exp(-_smoothing * deltaTime));
            _speed = speed;

            float strength = Mathf.InverseLerp(_brakeStart, _brakeFull, _brake) * Mathf.Clamp01(speed / _speedFull);
            SetStrength(strength);
        }

        private void SetStrength(float strength)
        {
            for (int i = 0; i < _smoke.Length; i++)
            {
                var emission = _smoke[i].emission;
                emission.rateOverTimeMultiplier = _baseRates[i] * strength;
            }

            bool skidding = strength > _skidThreshold;
            foreach (TrailRenderer mark in _skidMarks)
            {
                mark.emitting = skidding;
            }
        }
    }
}
