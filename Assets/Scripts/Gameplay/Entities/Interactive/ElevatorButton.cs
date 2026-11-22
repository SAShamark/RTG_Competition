using Gameplay.Dialogue;
using Gameplay.Entities.Player;
using System;
using Services.Audio;
using Services.Motion;
using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    public sealed class ElevatorButton : Interactable
    {
        [SerializeField] private Renderer _lamp;
        [SerializeField] private Color _idleColor = new(0.35f, 0.08f, 0.05f);
        [SerializeField] private Color _pressedColor = new(1f, 0.25f, 0.1f);
        [SerializeField] private float _blinkDuration = 1.2f;
        [SerializeField] private float _blinkPeriod = 0.3f;
        [SerializeField] private float _blinkOnTime = 0.15f;

        private MaterialPropertyBlock _block;
        public override HandGesture Gesture => HandGesture.Push;
        public int Presses { get; private set; }

        public event Action<ElevatorButton> Pressed;

        public override string GetHint(Transform interactor) => _texts[TextId.HintCallElevator];

        protected override void OnInteract(Transform interactor)
        {
            Presses++;
            MotionService.Instance.Play(_blinkDuration, Blink, () => SetLamp(_idleColor));
            AudioService.Instance.PlayAt(SoundType.ElevatorClick, transform.position);

            Pressed?.Invoke(this);
        }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            SetLamp(_idleColor);
        }

        private void Blink(float progress)
        {
            bool isOn = Mathf.Repeat(progress * _blinkDuration, _blinkPeriod) < _blinkOnTime;
            SetLamp(isOn ? _pressedColor : _idleColor);
        }

        private void SetLamp(Color color)
        {
            _lamp.GetPropertyBlock(_block);
            _block.SetColor("_BaseColor", color);
            _block.SetColor("_EmissionColor", color * 2f);
            _lamp.SetPropertyBlock(_block);
        }
    }
}
