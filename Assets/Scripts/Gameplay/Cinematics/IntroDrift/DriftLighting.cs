using System;
using Services;
using UnityEngine;

namespace Gameplay.Cinematics.IntroDrift
{
    [Serializable]
    public sealed class DriftLighting
    {
        [SerializeField] private Light _sun;
        [SerializeField] private Vector3 _sunEuler = new(7f, 25f, 0f);
        [SerializeField] private Color _sunColor = new(1f, 0.6f, 0.35f);
        [SerializeField, Min(0f)] private float _sunIntensity = 2.4f;
        [SerializeField, Range(0f, 2f)] private float _ambientScale = 0.8f;
        [SerializeField] private Light[] _lightBars;
        [SerializeField, Min(0f)] private float _lightBarRange = 18f;
        [SerializeField] private Light _smokeLight;
        [SerializeField, Min(0f)] private float _smokeLightIntensity = 4f;
        [SerializeField, Min(0.01f)] private float _releaseTime = 3f;

        private Quaternion _sunRotation;
        private Color _sunBaseColor;
        private float _sunBaseIntensity;
        private Color _ambientSky;
        private Color _ambientEquator;
        private Color _ambientGround;
        private float[] _lightBarRanges;
        private float _release = -1f;
        private bool _applied;

        public bool IsReleasing => _release >= 0f;

        public void Begin()
        {
            _sunRotation = _sun.transform.rotation;
            _sunBaseColor = _sun.color;
            _sunBaseIntensity = _sun.intensity;
            _ambientSky = RenderSettings.ambientSkyColor;
            _ambientEquator = RenderSettings.ambientEquatorColor;
            _ambientGround = RenderSettings.ambientGroundColor;
            _lightBarRanges = new float[_lightBars.Length];
            for (int i = 0; i < _lightBars.Length; i++)
            {
                _lightBarRanges[i] = _lightBars[i].range;
            }

            _applied = true;
            _release = -1f;
            Apply(1f);
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

            Apply(1f - Easing.Smooth(_release / _releaseTime));
        }

        public void Stop()
        {
            if (!_applied)
            {
                return;
            }

            Apply(0f);
            _applied = false;
            _release = -1f;
        }

        private void Apply(float weight)
        {
            _sun.transform.rotation = Quaternion.Slerp(_sunRotation, Quaternion.Euler(_sunEuler), weight);
            _sun.color = Color.Lerp(_sunBaseColor, _sunColor, weight);
            _sun.intensity = Mathf.Lerp(_sunBaseIntensity, _sunIntensity, weight);
            float ambient = Mathf.Lerp(1f, _ambientScale, weight);
            RenderSettings.ambientSkyColor = _ambientSky * ambient;
            RenderSettings.ambientEquatorColor = _ambientEquator * ambient;
            RenderSettings.ambientGroundColor = _ambientGround * ambient;
            for (int i = 0; i < _lightBars.Length; i++)
            {
                _lightBars[i].range = Mathf.Lerp(_lightBarRanges[i], _lightBarRange, weight);
            }

            _smokeLight.intensity = _smokeLightIntensity * weight;
            _smokeLight.enabled = weight > 0f;
        }
    }
}
