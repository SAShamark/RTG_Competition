using System;
using Services;
using Services.Audio;
using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    public sealed class DoorKey : Interactable, IHandDraggable
    {
        [SerializeField] private Vector3 _axis = Vector3.forward;
        [SerializeField] private string _hint = "[E] Поверни ключ";
        [SerializeField, Min(1f)] private float _unlockAngle = 90f;
        [SerializeField, Min(0f)] private float _unlockTolerance = 4f;
        [SerializeField, Min(0.1f)] private float _dragMoveAngle = 6f;
        [SerializeField, Range(0f, 1f)] private float _unlockPulse = 0.6f;
        [SerializeField, Min(0.01f)] private float _unlockPulseDuration = 0.08f;

        private Quaternion _rest;
        private Vector3 _twistAxis;
        private Vector3 _lastTwist;
        private bool _hasTwist;
        private bool _isLeft;
        private float _angle;
        private float _dragTravel;

        public bool IsTurned { get; private set; }
        public override bool CanInteract => !IsTurned;
        public bool CanDrag => CanInteract;

        public event Action<DoorKey> Turned;

        public override string GetHint(Transform interactor) => _hint;

        public void SetTurned() => IsTurned = true;

        public void BeginDrag(Transform interactor, Pose hand, bool isLeft)
        {
            _isLeft = isLeft;
            _twistAxis = HandTwist.WorldAxis(transform, _rest, _axis);
            _hasTwist = false;
            _dragTravel = 0f;
        }

        public void Drag(Pose hand)
        {
            if (IsTurned)
            {
                return;
            }

            Vector3 reference = HandTwist.Reference(hand.rotation, _twistAxis);
            if (!_hasTwist)
            {
                _hasTwist = true;
                _lastTwist = reference;
                return;
            }

            float delta = Vector3.SignedAngle(_lastTwist, reference, _twistAxis);
            _lastTwist = reference;
            _dragTravel += Mathf.Abs(delta);
            _angle = Mathf.Clamp(_angle + delta, -_unlockAngle, _unlockAngle);
            transform.localRotation = _rest * Quaternion.AngleAxis(_angle, _axis);
            if (Mathf.Abs(_angle) < _unlockAngle - _unlockTolerance)
            {
                return;
            }

            IsTurned = true;
            AudioService.Instance.PlayAt(SoundType.LockpickOpen, transform.position);
            GameInput.Vr.Pulse(_isLeft, _unlockPulse, _unlockPulseDuration);
            Turned?.Invoke(this);
        }

        public bool EndDrag()
        {
            if (!IsTurned)
            {
                _angle = 0f;
                transform.localRotation = _rest;
            }

            return _dragTravel > _dragMoveAngle;
        }

        protected override void OnInteract(Transform interactor)
        {
        }

        private void Awake() => _rest = transform.localRotation;
    }
}
