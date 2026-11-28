using Services;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Screens.Variants.Gameplay.Widgets
{
    public sealed class BurnFrameWidget : MonoBehaviour
    {
        private const float HiddenThreshold = 0.003f;

        private static readonly int Fill = Shader.PropertyToID("_Fill");
        private static readonly int FrameTime = Shader.PropertyToID("_FrameTime");

        [SerializeField] private RawImage _image;
        [SerializeField, Min(0f)] private float _smoothing = 5f;

        private Material _material;
        private float _target;
        private float _shown;

        public void SetFill(float value) => _target = Mathf.Clamp01(value);

        private void Awake()
        {
            _material = Instantiate(_image.material);
            _image.material = _material;
            _image.enabled = false;
        }

        private void OnDestroy() => Destroy(_material);

        private void Update()
        {
            _shown = Mathf.Lerp(_shown, _target, Easing.Damp(_smoothing, Time.unscaledDeltaTime));
            _image.enabled = _shown > HiddenThreshold;
            _material.SetFloat(Fill, _shown);
            _material.SetFloat(FrameTime, Time.unscaledTime);
        }
    }
}
