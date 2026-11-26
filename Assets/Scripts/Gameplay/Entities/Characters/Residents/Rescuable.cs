using System;
using Gameplay.Dialogue;
using Gameplay.Entities.Characters.Player;
using Gameplay.Entities.Interactive;
using UnityEngine;

namespace Gameplay.Entities.Characters.Residents
{
    public enum RescueState
    {
        Waiting = 0,
        Carried = 1,
        Flying = 2,
        Rescued = 3,
    }

    [DefaultExecutionOrder(Order)]
    public sealed class Rescuable : Interactable
    {
        private const float MinLiftTime = 0.01f;
        private const int Order = 100;

        [SerializeField] private string _displayName;

        [Header("Carry (relative to the player's carry socket)")]
        [SerializeField] private Vector3 _carryPelvis = new(0.22f, 0.86f, 0.62f);
        [SerializeField] private Vector3 _carryUp = new(-1f, 0.55f, 0.1f);
        [SerializeField] private Vector3 _carryForward = new(0.35f, 1f, -0.45f);
        [SerializeField] private float _liftTime = 0.45f;
        [SerializeField] private HeadBumpLogic _headBump = new();
        [SerializeField] private string _bumpLineId;

        [Header("Flight")]
        [SerializeField] private string _flyLineId;
        [SerializeField] private Sprite _icon;
        [SerializeField] private Vector3 _flightEuler = new(-70f, 0f, 0f);

        [SerializeField] private Collider[] _colliders;
        [SerializeField] private CharacterControl _character;
        [SerializeField] private float _liftHop = 0.15f;

        private RescueState _state;
        private bool _canBeCarried = true;
        private Transform _socket;
        private float _lift = 1f;
        private Vector3 _liftFromPosition;
        private Quaternion _liftFromRotation;

        public RescueState State => _state;
        public string DisplayName => _displayName;
        public string FlyLineId => _flyLineId;
        public string BumpLineId => _bumpLineId;
        public Vector3 HeadCenter => _headBump.HeadCenter;
        public Sprite Icon => _icon;
        public Vector3 FlightEuler => _flightEuler;
        public CharacterControl Character => _character;
        public Transform Head => _character.Head;

        public override bool CanInteract => _state == RescueState.Waiting;
        public override HandGesture Gesture => _canBeCarried ? HandGesture.Grab : HandGesture.None;

        public event Action<Rescuable> Refused;
        public event Action<Rescuable> PickedUp;
        public event Action<Rescuable> Rescued;
        public event Action<Rescuable> HeadBumped;

        private void Awake()
        {
            _headBump.Init(_character.Animator);
        }

        public void SetCanBeCarried(bool canBeCarried) => _canBeCarried = canBeCarried;

        public override string GetHint(Transform interactor) => _canBeCarried ? _texts.Format(TextId.HintPickUpResident, _displayName) : _texts[TextId.HintTalk];

        protected override void OnInteract(Transform interactor)
        {
            if (_state != RescueState.Waiting)
            {
                return;
            }

            if (!_canBeCarried)
            {
                Refused?.Invoke(this);
                return;
            }

            if (interactor.TryGetComponent(out IRescueCarrier carrier))
            {
                carrier.PickUp(this);
            }
        }

        public void AttachTo(Transform socket)
        {
            _state = RescueState.Carried;
            SetCollidersEnabled(false);
            _socket = socket;
            _liftFromPosition = transform.position;
            _liftFromRotation = transform.rotation;
            _lift = 0f;
            transform.SetParent(socket, true);
            _character.SetPose(CharacterPose.Carried);
            _headBump.Begin(((Component)socket.GetComponentInParent<IRescueCarrier>()).transform);
            PickedUp?.Invoke(this);
        }

        public void PlaceAt(Vector3 position, Quaternion rotation)
        {
            _socket = null;
            _headBump.End();
            transform.SetParent(null, true);
            transform.SetPositionAndRotation(position, rotation);
            SetCollidersEnabled(true);
            _character.SetPose(CharacterPose.Animated);
            _state = RescueState.Waiting;
        }

        public void Launch()
        {
            _socket = null;
            _headBump.End();
            transform.SetParent(null, true);
            SetCollidersEnabled(false);
            _character.SetPose(CharacterPose.Flailing);
            _state = RescueState.Flying;
        }

        public void MarkRescued()
        {
            if (_state == RescueState.Rescued)
            {
                return;
            }

            _state = RescueState.Rescued;
            _character.SetPose(CharacterPose.Animated);
            SetCollidersEnabled(true);
            Rescued?.Invoke(this);
        }

        private void LateUpdate()
        {
            if (_state == RescueState.Carried)
            {
                PlaceInArms();
            }
        }

        private void PlaceInArms()
        {
            Quaternion targetRotation = _socket.rotation * Quaternion.LookRotation(
                Vector3.ProjectOnPlane(_carryForward, _carryUp).normalized, _carryUp.normalized);
            transform.rotation = targetRotation;
            Vector3 pelvisOffset = _character.Pelvis - transform.position;
            Vector3 targetPosition = _socket.TransformPoint(_carryPelvis) - pelvisOffset;

            if (_lift < 1f)
            {
                _lift = Mathf.Min(1f, _lift + Time.deltaTime / Mathf.Max(MinLiftTime, _liftTime));
                float k = 1f - (1f - _lift) * (1f - _lift);
                // a small hop up in the middle of the lift
                Vector3 hop = Vector3.up * Mathf.Sin(k * Mathf.PI) * _liftHop;
                transform.SetPositionAndRotation(Vector3.Lerp(_liftFromPosition, targetPosition, k) + hop,
                    Quaternion.Slerp(_liftFromRotation, targetRotation, k));
                return;
            }

            transform.position = targetPosition;
            if (_headBump.Tick(Time.deltaTime))
            {
                HeadBumped?.Invoke(this);
            }
        }

        private void SetCollidersEnabled(bool isEnabled)
        {
            foreach (Collider residentCollider in _colliders)
            {
                residentCollider.enabled = isEnabled;
            }
        }
    }
}
