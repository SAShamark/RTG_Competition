using System;
using Game.Interaction;
using Game.Player;
using UnityEngine;

namespace Game.World
{
    [RequireComponent(typeof(Rigidbody))]
    public class ClutterItem : MonoBehaviour, IInteractable
    {
        [SerializeField] private string _displayName = "річ";
        [SerializeField] private Vector3 _holdPosition = new(0.05f, -0.25f, 0.65f);
        [SerializeField] private Vector3 _holdEuler = new(0f, 20f, 0f);
        [SerializeField] private float _lostDepth = 3f;

        private Rigidbody _body;
        private Collider[] _colliders;
        private Vector3 _homePosition;
        private Quaternion _homeRotation;
        private Transform _homeParent;
        private bool _isHeld;
        private bool _moved;

        public string DisplayName => _displayName;
        public Vector3 HoldPosition => _holdPosition;
        public Vector3 HoldEuler => _holdEuler;
        public bool IsHeld => _isHeld;
        public bool CanInteract => true;
        public HandGesture Gesture => HandGesture.Grab;

        public event Action<ClutterItem> OnMoved;

        public string GetHint(Transform interactor) => _isHeld
            ? "[E] Покласти   [ЛКМ] Кинути"
            : $"[E] Взяти {_displayName}";

        public void Interact(Transform interactor)
        {
            if (!interactor.TryGetComponent(out IPropHolder holder))
            {
                return;
            }

            if (_isHeld)
            {
                holder.PutDownProp();
            }
            else
            {
                holder.GrabProp(this);
            }
        }

        public void SetHeld(Transform socket)
        {
            bool first = !_moved;
            _moved = true;
            _isHeld = true;
            _body.isKinematic = true;
            _body.interpolation = RigidbodyInterpolation.None;
            SetCollidersEnabled(false);
            transform.SetParent(socket, false);
            transform.localPosition = _holdPosition;
            transform.localRotation = Quaternion.Euler(_holdEuler);
            if (first)
            {
                OnMoved?.Invoke(this);
            }
        }

        public void Release(Vector3 position, Vector3 velocity)
        {
            _isHeld = false;
            transform.SetParent(_homeParent, true);
            transform.position = position;
            SetCollidersEnabled(true);
            _body.isKinematic = false;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _body.linearVelocity = velocity;
            _body.angularVelocity = velocity.magnitude > 1f ? UnityEngine.Random.insideUnitSphere * 4f : Vector3.zero;
        }

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _body.isKinematic = true;
            _colliders = GetComponentsInChildren<Collider>();
            _homeParent = transform.parent;
            _homePosition = transform.position;
            _homeRotation = transform.rotation;
        }

        private void Update()
        {
            if (!_isHeld && !_body.isKinematic && transform.position.y < _homePosition.y - _lostDepth)
            {
                _body.isKinematic = true;
                transform.SetPositionAndRotation(_homePosition, _homeRotation);
            }
        }

        private void SetCollidersEnabled(bool isEnabled)
        {
            foreach (Collider itemCollider in _colliders)
            {
                itemCollider.enabled = isEnabled;
            }
        }
    }
}
