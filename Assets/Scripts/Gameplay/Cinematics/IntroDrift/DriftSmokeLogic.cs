using System;
using UnityEngine;

namespace Gameplay.Cinematics.IntroDrift
{
    [Serializable]
    public sealed class DriftSmokeLogic
    {
        [SerializeField] private ParticleSystem[] _smoke;
        [SerializeField] private TrailRenderer[] _skidMarks;
        [SerializeField, Min(1f)] private float _fullSlip = 28f;
        [SerializeField, Min(0f)] private float _boost = 2.6f;
        [SerializeField, Range(0f, 1f)] private float _skidThreshold = 0.2f;

        private float[] _baseRates;

        public void Init()
        {
            _baseRates = new float[_smoke.Length];
            for (int i = 0; i < _smoke.Length; i++)
            {
                _baseRates[i] = _smoke[i].emission.rateOverTimeMultiplier;
            }

            SetSlip(0f);
        }

        public void Clear()
        {
            foreach (ParticleSystem smoke in _smoke)
            {
                smoke.Clear();
            }

            foreach (TrailRenderer mark in _skidMarks)
            {
                mark.Clear();
            }
        }

        public void SetSlip(float slip)
        {
            float strength = Mathf.Clamp01(Mathf.Abs(slip) / _fullSlip);
            for (int i = 0; i < _smoke.Length; i++)
            {
                var emission = _smoke[i].emission;
                emission.rateOverTimeMultiplier = _baseRates[i] * strength * _boost;
            }

            bool skidding = strength > _skidThreshold;
            foreach (TrailRenderer mark in _skidMarks)
            {
                mark.emitting = skidding;
            }
        }
    }
}
