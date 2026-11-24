using Services;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Screens.Variants.Gameplay.Widgets
{
    public sealed class HeatVignetteWidget : MonoBehaviour
    {
        private const float VisibleThreshold = 0.01f;
        private const float HiddenThreshold = 0.005f;

        [SerializeField] private Image _image;
        [SerializeField] private float _smoothing = 3f;
        [SerializeField] private float _flickerFrequency = 7f;
        [SerializeField] private float _flickerAmount = 0.05f;
        [SerializeField] private float _maxAlpha = 0.85f;

        private float _target;
        private float _shown;

        public void SetIntensity(float value) => _target = Mathf.Clamp01(value);

        private void Update()
        {
            _shown = Mathf.Lerp(_shown, _target, Easing.Damp(_smoothing, Time.deltaTime));
            float flicker = _shown > VisibleThreshold ? Mathf.Sin(Time.time * _flickerFrequency) * _flickerAmount * _shown : 0f;
            Color c = _image.color;
            c.a = Mathf.Clamp01(_shown * _maxAlpha + flicker);
            _image.color = c;
            _image.enabled = c.a > HiddenThreshold;
        }
    }
}
