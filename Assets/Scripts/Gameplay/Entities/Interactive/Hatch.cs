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
        [SerializeField] private string _lockedAction = "Відчинити відсік";
        [SerializeField] private string _lockedHint = "зачинено";

        private Quaternion _closedRotation;
        private float _openness;
        private MotionHandle _motion = MotionHandle.Idle;
        private bool _isLocked;

        public bool IsOpen { get; private set; }
        public override bool CanInteract => !_motion.IsPlaying;
        public event Action<Hatch> Opened;
        public event Action<float> Moved;

        public void SetLocked(bool isLocked) => _isLocked = isLocked;

        public void Open()
        {
            if (IsOpen)
            {
                return;
            }

            _isLocked = false;
            _motion.Stop();
            Toggle();
        }

        public override string GetHint(Transform interactor)
        {
            if (_isLocked)
            {
                return _texts.Format(TextId.HintActionBlocked, _lockedAction, _lockedHint);
            }

            return IsOpen ? _texts.Format(TextId.HintCloseNamed, _displayName) : _texts.Format(TextId.HintOpenNamed, _displayName);
        }

        protected override void OnInteract(Transform interactor)
        {
            if (!CanInteract)
            {
                return;
            }

            if (_isLocked)
            {
                AudioService.Instance.PlayAt(SoundType.DoorLocked, transform.position);
                return;
            }

            Toggle();
        }

        private void Toggle()
        {
            IsOpen = !IsOpen;
            float from = _openness;
            float to = IsOpen ? 1f : 0f;
            _motion = MotionService.Instance.Play(_duration, k =>
            {
                _openness = Mathf.LerpUnclamped(from, to, Easing.OutCubic(k));
                transform.localRotation = _closedRotation * Quaternion.AngleAxis(_openAngle * _openness, _axis);
                Moved?.Invoke(_openness);
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
