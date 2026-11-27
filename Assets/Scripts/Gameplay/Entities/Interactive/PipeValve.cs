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

    public sealed class PipeValve : Interactable, IAltInteractable, IHandDraggable
    {
        [SerializeField] private PipeKind _kind;
        [SerializeField] private int _targetTurns;
        [SerializeField] private bool _hasWheel = true;
        [SerializeField] private GameObject _wheelVisual;
        [SerializeField] private Transform _spinner;
        [SerializeField] private Vector3 _spinAxis = Vector3.right;
        [SerializeField] private float _turnTime = 0.7f;
        [SerializeField] private float _rewindTime = 1.5f;

        [Header("Hand twist")]
        [SerializeField] private float _degreesPerTurn = 180f;
        [SerializeField] private float _twistMoveAngle = 10f;

        private Func<bool> _takeWheel;
        private MotionHandle _turn = MotionHandle.Idle;
        private int _spinTurns;
        private Quaternion _spinRest;
        private Vector3 _twistAxis;
        private Vector3 _lastTwist;
        private bool _hasTwist;
        private float _twistAngle;
        private float _twistTravel;

        public PipeKind Kind => _kind;
        public int TargetTurns => _targetTurns;
        public int Turns { get; private set; }
        public bool HasWheel => _hasWheel;
        public bool IsTurning => _turn.IsPlaying;
        public bool IsRewinding { get; private set; }
        public bool IsLocked { get; private set; }
        public override bool CanInteract => !IsTurning && !IsLocked;
        public bool CanDrag => CanInteract && _hasWheel;
        public event Action<PipeValve> Turned;
        public event Action<PipeValve> NoWheel;
        public event Action<PipeValve> RewindStarted;
        public event Action<PipeValve> Rewound;

        public void Setup(Func<bool> takeWheel) => _takeWheel = takeWheel;

        public void Lock() => IsLocked = true;

        public void Rewind()
        {
            _turn.Stop();
            _twistAngle = 0f;
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

        public void BeginDrag(Transform interactor, Pose hand, bool isLeft)
        {
            _twistAxis = HandTwist.WorldAxis(_spinner, _spinRest, _spinAxis);
            _lastTwist = HandTwist.Reference(hand.rotation, _twistAxis);
            _twistTravel = 0f;
            _hasTwist = false;
        }

        public void Drag(Pose hand)
        {
            if (IsLocked || IsTurning)
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
            _twistAngle += delta;
            _twistTravel += Mathf.Abs(delta);
            if (Mathf.Abs(_twistAngle) >= _degreesPerTurn)
            {
                int direction = (int)Mathf.Sign(_twistAngle);
                _twistAngle -= direction * _degreesPerTurn;
                Turns += direction;
                AudioService.Instance.PlayAt(SoundType.PipeValveTurn, transform.position);
                Turned?.Invoke(this);
            }

            _spinner.localRotation = _spinRest * Quaternion.AngleAxis(_twistAngle, _spinAxis);
        }

        public bool EndDrag() => _twistTravel > _twistMoveAngle;

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
            _twistAngle = 0f;
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
