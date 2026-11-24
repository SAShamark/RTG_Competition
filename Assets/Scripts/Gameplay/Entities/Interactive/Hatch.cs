using Gameplay.Dialogue;
using System;
using Gameplay.Entities.Characters.Player;
using Services;
using Services.Audio;
using Services.Motion;
using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    public sealed class Hatch : Interactable
    {
        [SerializeField] private string _displayName = "відсік";
        [SerializeField] private Vector3 _axis = Vector3.right;
        [SerializeField] private float _openAngle = -100f;
        [SerializeField] private float _duration = 0.5f;

        private Quaternion _closedRotation;
        private float _angle;
        private MotionHandle _motion = MotionHandle.Idle;

        public bool IsOpen { get; private set; }
        public override bool CanInteract => !_motion.IsPlaying;
        public override HandGesture Gesture => HandGesture.Push;

        public event Action<Hatch> Opened;

        public override string GetHint(Transform interactor) => IsOpen ? _texts.Format(TextId.HintCloseNamed, _displayName) : _texts.Format(TextId.HintOpenNamed, _displayName);

        protected override void OnInteract(Transform interactor)
        {
            if (!CanInteract)
            {
                return;
            }

            IsOpen = !IsOpen;
            float from = _angle;
            float to = IsOpen ? _openAngle : 0f;
            _motion = MotionService.Instance.Play(_duration, k =>
            {
                _angle = Mathf.LerpUnclamped(from, to, Easing.OutCubic(k));
                transform.localRotation = _closedRotation * Quaternion.AngleAxis(_angle, _axis);
            });
            AudioService.Instance.PlayAt(IsOpen ? SoundType.HatchOpen : SoundType.HatchClose, transform.position);

            if (IsOpen)
            {
                Opened?.Invoke(this);
            }
        }

        private void Awake()
        {
            _closedRotation = transform.localRotation;
        }

    }
}
