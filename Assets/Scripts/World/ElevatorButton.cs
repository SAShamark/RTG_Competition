using System;
using Game.Interaction;
using Game.Player;
using UnityEngine;

namespace Game.World
{
    public class ElevatorButton : MonoBehaviour, IInteractable
    {
        [SerializeField] private Renderer _lamp;
        [SerializeField] private Color _idleColor = new(0.35f, 0.08f, 0.05f);
        [SerializeField] private Color _pressedColor = new(1f, 0.25f, 0.1f);
        [SerializeField] private float _blinkDuration = 1.2f;
        [SerializeField] private AudioSource _clickAudio;

        private MaterialPropertyBlock _block;
        private float _blinkTime = -1f;
        private int _presses;

        public bool CanInteract => true;
        public HandGesture Gesture => HandGesture.Push;
        public int Presses => _presses;

        public event Action<ElevatorButton> OnPressed;

        public string GetHint(Transform interactor) => "[E] Викликати ліфт";

        public void Interact(Transform interactor)
        {
            _presses++;
            _blinkTime = 0f;
            _clickAudio.Play();

            OnPressed?.Invoke(this);
        }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            SetLamp(_idleColor);
        }

        private void Update()
        {
            if (_blinkTime < 0f)
            {
                return;
            }

            _blinkTime += Time.deltaTime;
            bool isOn = Mathf.Repeat(_blinkTime, 0.3f) < 0.15f;
            SetLamp(isOn ? _pressedColor : _idleColor);
            if (_blinkTime >= _blinkDuration)
            {
                _blinkTime = -1f;
                SetLamp(_idleColor);
            }
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
