using Gameplay.Dialogue;
using Gameplay.Entities.Player;
using System;
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

        private Func<bool> _takeWheel;
        private MotionHandle _turn = MotionHandle.Idle;
        private int _direction;
        private Quaternion _spinRest;

        public PipeKind Kind => _kind;
        public int TargetTurns => _targetTurns;
        public int Turns { get; private set; }
        public bool HasWheel => _hasWheel;
        public bool IsTurning => _turn.IsPlaying;
        public override bool CanInteract => !IsTurning;
        public override HandGesture Gesture => _hasWheel ? HandGesture.Grab : HandGesture.Rattle;

        public event Action<PipeValve> Turned;
        public event Action<PipeValve> NoWheel;

        public void Setup(Func<bool> takeWheel) => _takeWheel = takeWheel;

        public void ResetTurns() => Turns = 0;

        public override string GetHint(Transform interactor) => _hasWheel
            ? _texts[TextId.HintTurnValve]
            : _texts[TextId.HintPlaceWheel];

        protected override void OnInteract(Transform interactor)
        {
            if (IsTurning)
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
            if (!IsTurning && _hasWheel)
            {
                Turn(-1);
            }
        }

        private void Turn(int direction)
        {
            _direction = direction;
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
            // the wheel faces the room: clockwise for the player is a negative turn around its axis
            _spinner.localRotation = _spinRest * Quaternion.AngleAxis(-_direction * 360f * Easing.Smooth(progress), _spinAxis);
        }

        private void CompleteTurn()
        {
            _spinner.localRotation = _spinRest;
            Turns += _direction;
            Turned?.Invoke(this);
        }
    }
}
