using UnityEngine;
using UnityEngine.UI;

namespace UI.Widgets.Hud
{
    public class HeatVignetteWidget : MonoBehaviour
    {
        [SerializeField] private Image _image;
        [SerializeField] private float _smoothing = 3f;

        private float _target;
        private float _shown;

        public void SetIntensity(float value) => _target = Mathf.Clamp01(value);

        private void Update()
        {
            _shown = Mathf.Lerp(_shown, _target, 1f - Mathf.Exp(-_smoothing * Time.deltaTime));
            float flicker = _shown > 0.01f ? Mathf.Sin(Time.time * 7f) * 0.05f * _shown : 0f;
            Color c = _image.color;
            c.a = Mathf.Clamp01(_shown * 0.85f + flicker);
            _image.color = c;
            _image.enabled = c.a > 0.005f;
        }
    }
}
