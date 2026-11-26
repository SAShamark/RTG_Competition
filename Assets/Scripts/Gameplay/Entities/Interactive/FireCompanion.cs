using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    public sealed class FireCompanion : MonoBehaviour
    {
        [SerializeField] private FireSource[] _fires;
        [SerializeField] private ParticleSystem[] _particles;
        [SerializeField] private Light[] _lights;
        [SerializeField, Min(0f)] private float _lingerLimit = 3f;

        private float[] _rates;
        private float[] _lightLevels;

        private void Awake()
        {
            _rates = new float[_particles.Length];
            for (int i = 0; i < _rates.Length; i++)
            {
                _rates[i] = _particles[i].emission.rateOverTimeMultiplier;
            }

            _lightLevels = new float[_lights.Length];
            for (int i = 0; i < _lightLevels.Length; i++)
            {
                _lightLevels[i] = _lights[i].intensity;
            }
        }

        private void OnEnable()
        {
            foreach (FireSource fire in _fires)
            {
                fire.IntensityChanged += HandleIntensityChanged;
            }
        }

        private void OnDisable()
        {
            foreach (FireSource fire in _fires)
            {
                fire.IntensityChanged -= HandleIntensityChanged;
            }
        }

        private void HandleIntensityChanged(FireSource changed, float intensity)
        {
            float heat = 0f;
            foreach (FireSource fire in _fires)
            {
                heat += fire.Intensity;
            }

            Apply(heat / _fires.Length);
        }

        private void Apply(float heat)
        {
            for (int i = 0; i < _particles.Length; i++)
            {
                ParticleSystem particles = _particles[i];
                var emission = particles.emission;
                emission.rateOverTimeMultiplier = _rates[i] * heat;
                if (heat > 0f)
                {
                    if (!particles.isEmitting)
                    {
                        particles.Play(false);
                    }

                    continue;
                }

                // Long-lived pieces such as painted flame cards would hang in the air for seconds, so they go at once.
                bool lingers = particles.main.startLifetime.constantMax > _lingerLimit;
                particles.Stop(false, lingers ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
            }

            for (int i = 0; i < _lights.Length; i++)
            {
                _lights[i].intensity = _lightLevels[i] * heat;
            }
        }
    }
}
