using Services;
using Services.Audio;
using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    public sealed class SafeHandle : Interactable, IHandDraggable
    {
        [SerializeField] private WallSafe _safe;
        [SerializeField] private Vector3 _axis = Vector3.right;
        [SerializeField] private string _hint = "[E] Поверни ручку";
        [SerializeField, Min(1f)] private float _openAngle = 70f;
        [SerializeField, Min(0f)] private float _lockedPlay = 8f;
        [SerializeField, Min(0.1f)] private float _dragMoveAngle = 6f;
        [SerializeField, Range(0f, 1f)] private float _lockedPulse = 0.5f;
        [SerializeField, Min(0.01f)] private float _lockedPulseDuration = 0.06f;

        private Quaternion _rest;
        private Vector3 _twistAxis;
        private Vector3 _lastTwist;
        private bool _hasTwist;
        private bool _isLeft;
        private bool _isRattled;
        private float _angle;
        private float _dragTravel;

        public override bool CanInteract => !_safe.IsOpen;
        public bool CanDrag => CanInteract;

        public override string GetHint(Transform interactor) => _hint;

        public void BeginDrag(Transform interactor, Pose hand, bool isLeft)
        {
            _isLeft = isLeft;
            _twistAxis = HandTwist.WorldAxis(transform, _rest, _axis);
            _hasTwist = false;
            _isRattled = false;
            _dragTravel = 0f;
        }

        public void Drag(Pose hand)
        {
            if (_safe.IsOpen)
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
            float limit = _safe.IsUnlocked ? _openAngle : _lockedPlay;
            _angle = Mathf.Clamp(_angle + delta, -limit, limit);
            transform.localRotation = _rest * Quaternion.AngleAxis(_angle, _axis);
            if (_safe.IsUnlocked && Mathf.Abs(_angle) >= _openAngle)
            {
                _safe.Open();
                return;
            }

            if (!_safe.IsUnlocked && Mathf.Abs(_angle) >= _lockedPlay && !_isRattled)
            {
                _isRattled = true;
                AudioService.Instance.PlayAt(SoundType.DoorLocked, transform.position);
                GameInput.Vr.Pulse(_isLeft, _lockedPulse, _lockedPulseDuration);
            }
        }

        public bool EndDrag()
        {
            if (!_safe.IsOpen)
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
