using System;
using UnityEngine;

namespace Vr
{
    [Serializable]
    public sealed class VrComfortVignette
    {
        [SerializeField] private Renderer _renderer;
        [SerializeField, Range(10f, 120f)] private float _openAperture = 80f;
        [SerializeField, Range(10f, 90f)] private float _lowAperture = 52f;
        [SerializeField, Range(10f, 90f)] private float _highAperture = 36f;
        [SerializeField, Range(1f, 45f)] private float _feather = 14f;
        [SerializeField, Min(0.1f)] private float _fullSpeed = 3f;
        [SerializeField, Min(1f)] private float _fullTurnRate = 90f;
        [SerializeField, Min(0.1f)] private float _riseSpeed = 5f;
        [SerializeField, Min(0.1f)] private float _fallSpeed = 2.5f;
        [SerializeField, Min(0.05f)] private float _blinkTime = 0.35f;

        private static readonly int ApertureId = Shader.PropertyToID("_Aperture");
        private static readonly int FeatherId = Shader.PropertyToID("_Feather");
        private static readonly int HeadForwardId = Shader.PropertyToID("_HeadForward");

        private MaterialPropertyBlock _block;
        private float _intensity;
        private float _blink;
        private float _curtain;

        public void Initialize()
        {
            _block = new MaterialPropertyBlock();
            _renderer.enabled = false;
        }

        public void Blink() => _blink = 1f;

        public void SetCurtain(float amount) => _curtain = Mathf.Clamp01(amount);

        public void Tick(Transform head, float speed, float turnRate, float deltaTime)
        {
            VrVignetteLevel level = VrComfort.Vignette;
            float target = level == VrVignetteLevel.Off ? 0f : Mathf.Clamp01(Mathf.Max(speed / _fullSpeed, Mathf.Abs(turnRate) / _fullTurnRate));
            _intensity = Mathf.MoveTowards(_intensity, target, (target > _intensity ? _riseSpeed : _fallSpeed) * deltaTime);
            _blink = Mathf.MoveTowards(_blink, 0f, deltaTime / _blinkTime);

            float dark = Mathf.Max(_blink, _curtain);
            bool isVisible = _intensity > Mathf.Epsilon || dark > Mathf.Epsilon;
            _renderer.enabled = isVisible;
            if (!isVisible)
            {
                return;
            }

            float aperture = level == VrVignetteLevel.High ? _highAperture : _lowAperture;
            float comfort = Mathf.Lerp(_openAperture, aperture, _intensity);
            _block.SetFloat(ApertureId, Mathf.Lerp(comfort, -_feather, dark));
            _block.SetFloat(FeatherId, _feather);
            _block.SetVector(HeadForwardId, head.forward);
            _renderer.SetPropertyBlock(_block);
        }
    }
}
