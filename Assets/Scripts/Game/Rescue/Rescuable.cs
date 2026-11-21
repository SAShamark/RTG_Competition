using System;
using Game.Interaction;
using Game.Player;
using UnityEngine;

namespace Game.Rescue
{
    public enum RescueState
    {
        Waiting = 0,
        Carried = 1,
        Flying = 2,
        Rescued = 3,
    }

    public class Rescuable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string _displayName = "мешканця";
        [SerializeField] private string _speakerName = "Мешканець";
        [SerializeField] private string _talkHint = "[E] Поговорити";

        [Header("Carry (relative to the player's carry socket)")]
        [SerializeField] private Vector3 _carryPelvis = new(0.22f, 0.86f, 0.62f);
        [SerializeField] private Vector3 _carryUp = new(-1f, 0.55f, 0.1f);
        [SerializeField] private Vector3 _carryForward = new(0.35f, 1f, -0.45f);
        [SerializeField] private float _liftTime = 0.45f;

        [Header("Flight")]
        [SerializeField] private string _flyLine = "А-А-А-А-А!";
        [SerializeField] private Vector3 _flightEuler = new(-70f, 0f, 0f);
        [SerializeField] private float _bodyCenterHeight = 0.8f;

        [SerializeField] private Collider[] _colliders;
        [SerializeField] private Animator _animator;
        [SerializeField] private ResidentPoseLogic _pose = new();

        private RescueState _state;
        private bool _canBeCarried = true;
        private Transform _socket;
        private float _lift = 1f;
        private Vector3 _liftFromPosition;
        private Quaternion _liftFromRotation;

        public RescueState State => _state;
        public string DisplayName => _displayName;
        public string SpeakerName => _speakerName;
        public string FlyLine => _flyLine;
        public Vector3 FlightEuler => _flightEuler;
        public float BodyCenterHeight => _bodyCenterHeight;
        public Animator Animator => _animator;
        public Transform Head => _pose.Head != null ? _pose.Head : transform;

        public bool CanInteract => _state == RescueState.Waiting;
        public HandGesture Gesture => _canBeCarried ? HandGesture.Grab : HandGesture.None;

        public event Action<Rescuable> OnRefused;
        public event Action<Rescuable> OnPickedUp;
        public event Action<Rescuable> OnRescued;

        private void Awake()
        {
            _pose.Init(_animator);
        }

        private void OnDestroy()
        {
            _pose.Dispose();
        }

        public void SetCanBeCarried(bool canBeCarried) => _canBeCarried = canBeCarried;

        public void SetPose(ResidentPose pose) => _pose.SetMode(pose);

        public string GetHint(Transform interactor) => _canBeCarried ? $"[E] Підхопити {_displayName}" : _talkHint;

        public void Interact(Transform interactor)
        {
            if (_state != RescueState.Waiting)
            {
                return;
            }

            if (!_canBeCarried)
            {
                OnRefused?.Invoke(this);
                return;
            }

            if (interactor.TryGetComponent(out IRescueCarrier carrier))
            {
                carrier.PickUp(this);
            }
        }

        public void SetCenterPose(Vector3 center, Quaternion rotation)
        {
            transform.SetPositionAndRotation(center - rotation * Vector3.up * _bodyCenterHeight, rotation);
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
            _pose.SetMode(ResidentPose.Carried);
            OnPickedUp?.Invoke(this);
        }

        public void PlaceAt(Vector3 position, Quaternion rotation)
        {
            _socket = null;
            transform.SetParent(null, true);
            transform.SetPositionAndRotation(position, rotation);
            SetCollidersEnabled(true);
            _pose.SetMode(ResidentPose.Animated);
            _state = RescueState.Waiting;
        }

        public void Launch()
        {
            _socket = null;
            transform.SetParent(null, true);
            SetCollidersEnabled(false);
            _pose.SetMode(ResidentPose.Flailing);
            _state = RescueState.Flying;
        }

        public void MarkRescued()
        {
            if (_state == RescueState.Rescued)
            {
                return;
            }

            _state = RescueState.Rescued;
            _pose.SetMode(ResidentPose.Animated);
            SetCollidersEnabled(true);
            OnRescued?.Invoke(this);
        }

        private void LateUpdate()
        {
            _pose.Tick(Time.deltaTime);
            if (_state == RescueState.Carried && _socket != null)
            {
                PlaceInArms();
            }
        }

        private void PlaceInArms()
        {
            Quaternion targetRotation = _socket.rotation * Quaternion.LookRotation(
                Vector3.ProjectOnPlane(_carryForward, _carryUp).normalized, _carryUp.normalized);
            transform.rotation = targetRotation;
            Vector3 pelvisOffset = _pose.Pelvis - transform.position;
            Vector3 targetPosition = _socket.TransformPoint(_carryPelvis) - pelvisOffset;

            if (_lift < 1f)
            {
                _lift = Mathf.Min(1f, _lift + Time.deltaTime / Mathf.Max(0.01f, _liftTime));
                float k = 1f - (1f - _lift) * (1f - _lift);
                // a small hop up in the middle of the lift
                Vector3 hop = Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.15f;
                transform.SetPositionAndRotation(Vector3.Lerp(_liftFromPosition, targetPosition, k) + hop,
                    Quaternion.Slerp(_liftFromRotation, targetRotation, k));
                return;
            }

            transform.position = targetPosition;
        }

        private void SetCollidersEnabled(bool isEnabled)
        {
            foreach (Collider residentCollider in _colliders)
            {
                if (residentCollider != null)
                {
                    residentCollider.enabled = isEnabled;
                }
            }
        }
    }
}
