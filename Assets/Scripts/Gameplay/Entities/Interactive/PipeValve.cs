using Gameplay.Dialogue;
using System;
using Gameplay.Entities.Characters.Player;
using Services;
using Services.Audio;
using Services.Motion;
using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    public enum PipeKind { Water, Vent, Gas }

    public sealed class PipeValve : Interactable, IAltInteractable
    {
        [SerializeField] private PipeKind _kind;
        [SerializeField] private int _targetTurns;
        [SerializeField] private bool _hasWheel = true;
        [SerializeField] private GameObject _wheelVisual;
        [SerializeField] private Transform _spinner;
        [SerializeField] private Vector3 _spinAxis = Vector3.right;
        [SerializeField] private float _turnTime = 0.7f;
        [SerializeField] private float _rewindTime = 1.5f;

        private Func<bool> _takeWheel;
        private MotionHandle _turn = MotionHandle.Idle;
        private int _spinTurns;
        private Quaternion _spinRest;

        public PipeKind Kind => _kind;
        public int TargetTurns => _targetTurns;
        public int Turns { get; private set; }
        public bool HasWheel => _hasWheel;
        public bool IsTurning => _turn.IsPlaying;
        public bool IsRewinding { get; private set; }
        public bool IsLocked { get; private set; }
        public override bool CanInteract => !IsTurning && !IsLocked;
        public override HandGesture Gesture => _hasWheel ? HandGesture.Grab : HandGesture.Rattle;

        public event Action<PipeValve> Turned;
        public event Action<PipeValve> NoWheel;
        public event Action<PipeValve> RewindStarted;
        public event Action<PipeValve> Rewound;

        public void Setup(Func<bool> takeWheel) => _takeWheel = takeWheel;

        public void Lock() => IsLocked = true;

        public void Rewind()
        {
            _turn.Stop();
            _spinTurns = -Turns;
            Turns = 0;
            IsRewinding = true;
            _turn = MotionService.Instance.Play(_rewindTime, Spin, CompleteRewind);
            if (_spinTurns != 0)
            {
                AudioService.Instance.PlayAt(SoundType.PipeValveTurn, transform.position);
            }

            RewindStarted?.Invoke(this);
        }

        public override string GetHint(Transform interactor) => _hasWheel
            ? _texts[TextId.HintTurnValve]
            : _texts[TextId.HintPlaceWheel];

        protected override void OnInteract(Transform interactor)
        {
            if (!CanInteract)
            {
                return;
            }

            if (!_hasWheel)
            {
                if (_takeWheel())
                {
                    _hasWheel = true;
                    _wheelVisual.SetActive(true);
                }
                else
                {
                    NoWheel?.Invoke(this);
                }

                return;
            }

            Turn(1);
        }

        public void AltInteract(Transform interactor)
        {
            if (CanInteract && _hasWheel)
            {
                Turn(-1);
            }
        }

        private void Turn(int direction)
        {
            _spinTurns = direction;
            _turn = MotionService.Instance.Play(_turnTime, Spin, CompleteTurn);
            AudioService.Instance.PlayAt(SoundType.PipeValveTurn, transform.position);
        }

        private void Awake()
        {
            _spinRest = _spinner.localRotation;

            _wheelVisual.SetActive(_hasWheel);
        }

        private void Spin(float progress)
        {
            // the spin axis points at the player, so a positive turn around it looks clockwise
            _spinner.localRotation = _spinRest * Quaternion.AngleAxis(_spinTurns * 360f * Easing.Smooth(progress), _spinAxis);
        }

        private void CompleteTurn()
        {
            _spinner.localRotation = _spinRest;
            Turns += _spinTurns;
            Turned?.Invoke(this);
        }

        private void CompleteRewind()
        {
            _spinner.localRotation = _spinRest;
            IsRewinding = false;
            Rewound?.Invoke(this);
        }
    }
}
