using System;
using UnityEngine;

namespace Gameplay.Entities.Environment
{
    [Serializable]
    public sealed class LightBarLogic
    {
        private const float FlashesPerSide = 4f;
        private const float FlashDuty = 0.55f;

        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private Light _left;
        [SerializeField] private Light _right;
        [SerializeField] private Renderer _leftLamp;
        [SerializeField] private Renderer _rightLamp;
        [SerializeField] private float _frequency = 3.5f;
        [SerializeField] private float _intensity = 6f;
        [SerializeField] private Color _redColor = new(1f, 0.1f, 0.08f);
        [SerializeField] private Color _blueColor = new(0.15f, 0.35f, 1f);
        [SerializeField] private float _onEmission = 6f;
        [SerializeField] private float _offEmission = 0.15f;

        private MaterialPropertyBlock _block;

        public void Init() => _block = new MaterialPropertyBlock();

        public void Tick(float time)
        {
            // double flash per side, like real light bars
            float phase = Mathf.Repeat(time * _frequency, 1f);
            bool flash = Mathf.Repeat(phase * FlashesPerSide, 1f) < FlashDuty;
            Set(_left, _leftLamp, phase < 0.5f && flash, _redColor);
            Set(_right, _rightLamp, phase >= 0.5f && flash, _blueColor);
        }

        private void Set(Light lamp, Renderer lampRenderer, bool isOn, Color color)
        {
            lamp.intensity = isOn ? _intensity : 0f;
            lampRenderer.GetPropertyBlock(_block);
            _block.SetColor(EmissionId, color * (isOn ? _onEmission : _offEmission));
            lampRenderer.SetPropertyBlock(_block);
        }
    }
}
