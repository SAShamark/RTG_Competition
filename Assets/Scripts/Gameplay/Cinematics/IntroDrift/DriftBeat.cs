using Services;
using System;
using UnityEngine;

namespace Gameplay.Cinematics.IntroDrift
{
    [Serializable]
    public sealed class DriftBeat
    {
        private const int SampleCount = 1024;

        [SerializeField, Min(1f)] private float _sensitivity = 1.5f;
        [SerializeField, Min(0f)] private float _floor = 0.002f;
        [SerializeField, Min(0.01f)] private float _averageTime = 0.6f;
        [SerializeField, Min(0f)] private float _minInterval = 0.3f;
        [SerializeField, Min(1f)] private float _maxStrength = 2f;

        private readonly float[] _samples = new float[SampleCount];
        private float _average;
        private float _sinceBeat;

        public void Reset()
        {
            _average = 0f;
            _sinceBeat = _minInterval;
        }

        public bool TryDetect(AudioSource source, float deltaTime, out float strength)
        {
            strength = 0f;
            _sinceBeat += deltaTime;
            if (!source.isPlaying)
            {
                return false;
            }

            source.GetOutputData(_samples, 0);
            float energy = 0f;
            foreach (float sample in _samples)
            {
                energy += sample * sample;
            }

            energy /= SampleCount;
            float ratio = _average > 0f ? energy / _average : 0f;
            // exponential moving average, frame-rate independent
            _average = Mathf.Lerp(_average, energy, Easing.Damp(1f / _averageTime, deltaTime));
            if (energy < _floor || ratio < _sensitivity || _sinceBeat < _minInterval)
            {
                return false;
            }

            _sinceBeat = 0f;
            strength = Mathf.Min(ratio / _sensitivity, _maxStrength);
            return true;
        }
    }
}
