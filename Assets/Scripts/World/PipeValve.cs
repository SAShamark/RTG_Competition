using System;
using Game.Interaction;
using Game.Player;
using UnityEngine;

namespace Game.World
{
    public enum PipeKind { Water, Vent, Gas }

    public class PipeValve : MonoBehaviour, IInteractable, IAltInteractable
    {
        [SerializeField] private PipeKind _kind;
        [SerializeField] private int _targetTurns;
        [SerializeField] private bool _hasWheel = true;
        [SerializeField] private GameObject _wheelVisual;
        [SerializeField] private Transform _spinner;
        [SerializeField] private Vector3 _spinAxis = Vector3.right;
        [SerializeField] private float _turnTime = 0.7f;
        [SerializeField] private AudioSource _audio;

        private Func<bool> _takeWheel;
        private float _turn = -1f;
        private int _direction;
        private int _turns;
        private Quaternion _spinRest;

        public PipeKind Kind => _kind;
        public int TargetTurns => _targetTurns;
        public int Turns => _turns;
        public bool HasWheel => _hasWheel;
        public bool IsTurning => _turn >= 0f;
        public bool CanInteract => !IsTurning;
        public HandGesture Gesture => _hasWheel ? HandGesture.Grab : HandGesture.Rattle;

        public event Action<PipeValve> OnTurned;
        public event Action<PipeValve> OnNoWheel;

        public void Setup(Func<bool> takeWheel) => _takeWheel = takeWheel;

        public void ResetTurns() => _turns = 0;

        public string GetHint(Transform interactor) => _hasWheel
            ? "[E] Повернути ↻   [Q] Повернути ↺"
            : "[E] Поставити маховик";

        public void Interact(Transform interactor)
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
                    OnNoWheel?.Invoke(this);
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
            _turn = 0f;
            _audio.Play();
        }

        private void Awake()
        {
            _spinRest = _spinner.localRotation;

            _wheelVisual.SetActive(_hasWheel);
        }

        private void Update()
        {
            if (_turn < 0f)
            {
                return;
            }

            _turn = Mathf.Min(1f, _turn + Time.deltaTime / _turnTime);
            float k = _turn * _turn * (3f - 2f * _turn);
            // the wheel faces the room: clockwise for the player is a negative turn around its axis
            _spinner.localRotation = _spinRest * Quaternion.AngleAxis(-_direction * 360f * k, _spinAxis);

            if (_turn >= 1f)
            {
                _turn = -1f;
                _spinner.localRotation = _spinRest;

                _turns += _direction;
                OnTurned?.Invoke(this);
            }
        }
    }
}
