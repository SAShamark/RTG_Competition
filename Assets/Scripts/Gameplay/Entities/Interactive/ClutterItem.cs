using Gameplay.Dialogue;
using System;
using Gameplay.Entities.Characters.Player;
using Services.Audio;
using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ClutterItem : Interactable, IToolReactive
    {
        [SerializeField] private ToolResponse _response = new();
        [SerializeField] private string _displayName = "річ";
        [SerializeField] private Vector3 _holdPosition = new(0.05f, -0.25f, 0.65f);
        [SerializeField] private Vector3 _holdEuler = new(0f, 20f, 0f);
        [SerializeField] private float _lostDepth = 3f;
        [SerializeField] private Sprite _icon;
        [SerializeField] private float _throwSpin = 4f;
        [SerializeField] private ImpactSound _impact = new(SoundType.PropImpact);

        private Rigidbody _body;
        private Collider[] _colliders;
        private Vector3 _homePosition;
        private Quaternion _homeRotation;
        private Transform _homeParent;
        private bool _moved;

        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public Vector3 HoldPosition => _holdPosition;
        public Vector3 HoldEuler => _holdEuler;
        public bool IsHeld { get; private set; }
        public override HandGesture Gesture => HandGesture.Grab;

        public event Action<ClutterItem> Moved;

        public override string GetHint(Transform interactor) => IsHeld
            ? _texts[TextId.HintPutDownOrThrow]
            : _texts.Format(TextId.HintTake, _displayName);

        protected override void OnInteract(Transform interactor)
        {
            if (!interactor.TryGetComponent(out IPropHolder holder))
            {
                return;
            }

            if (IsHeld)
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
            IsHeld = true;
            _body.isKinematic = true;
            _body.interpolation = RigidbodyInterpolation.None;
            SetCollidersEnabled(false);
            transform.SetParent(socket, false);
            transform.localPosition = _holdPosition;
            transform.localRotation = Quaternion.Euler(_holdEuler);
            if (first)
            {
                Moved?.Invoke(this);
            }
        }

        public void Release(Vector3 position, Vector3 velocity)
        {
            IsHeld = false;
            transform.SetParent(_homeParent, true);
            transform.position = position;
            SetCollidersEnabled(true);
            _body.isKinematic = false;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _body.linearVelocity = velocity;
            _body.angularVelocity = velocity.magnitude > 1f ? UnityEngine.Random.insideUnitSphere * _throwSpin : Vector3.zero;
        }

        public void React(in ToolHit hit)
        {
            if (!IsHeld)
            {
                _response.Apply(_body, hit);
            }
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
            if (!IsHeld && !_body.isKinematic && transform.position.y < _homePosition.y - _lostDepth)
            {
                _body.isKinematic = true;
                transform.SetPositionAndRotation(_homePosition, _homeRotation);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            _impact.Handle(collision, transform.position);
            ToolImpact.Collide(collision);
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
