using Services.Audio;
using Services.Motion;
using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    public sealed class KeypadButton : Interactable, IPressable
    {
        [SerializeField] private SafeKeypad _keypad;
        [SerializeField] private string _key;
        [SerializeField] private Transform _cap;
        [SerializeField] private string _hint = "Натисни пальцем";
        [SerializeField] private float _pressDepth = 0.003f;
        [SerializeField] private float _pressTime = 0.15f;
        [SerializeField] private float _clickPitch = 1.4f;

        private Vector3 _capRest;

        public override string GetHint(Transform interactor) => _hint;

        protected override void OnInteract(Transform interactor)
        {
            _keypad.Press(_key);
            AudioService.Instance.PlayAt(SoundType.ElevatorClick, transform.position, _clickPitch);
            MotionService.Instance.Play(_pressTime, Sink, () => _cap.localPosition = _capRest);
        }

        private void Awake() => _capRest = _cap.localPosition;

        // Down and back up in one press: depth · sin(π·t).
        private void Sink(float progress) => _cap.localPosition = _capRest + Vector3.back * (_pressDepth * Mathf.Sin(progress * Mathf.PI));
    }
}
