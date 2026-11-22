using UnityEngine;
using UnityEngine.Rendering;

namespace Gameplay.Entities.Interactive.Items
{
    public sealed class ToolItem : InventoryItem
    {
        [SerializeField] private Vector3 _holdPosition;
        [SerializeField] private Vector3 _holdEuler;
        [SerializeField] private float _lostDepth = 4f;
        [SerializeField] private Transform _leftGrip;
        [SerializeField] private bool _hasHandPose;
        [SerializeField] private Vector3 _handPosition;
        [SerializeField] private Vector3 _handEuler;

        private Transform _worldParent;
        [SerializeField] private Rigidbody _body;
        private Vector3 _dropPosition;
        private bool _dropped;

        public Vector3 HoldPosition => _holdPosition;
        public Vector3 HoldEuler => _holdEuler;
        public Transform LeftGrip => _leftGrip;
        public bool HasHandPose => _hasHandPose;
        public Vector3 HandPosition => _handPosition;
        public Vector3 HandEuler => _handEuler;

        protected override bool Store(IItemHolder holder) => holder.TakeTool(this);

        protected override void OnCollected()
        {
            SetPhysics(false);
            SetCollidersEnabled(false);
        }

        public void Release(Vector3 position, Vector3 velocity)
        {
            ReturnToWorld();
            _dropped = true;
            _dropPosition = position;
            transform.SetParent(_worldParent, true);
            transform.position = position;
            SetCollidersEnabled(true);
            foreach (Renderer itemRenderer in GetComponentsInChildren<Renderer>())
            {
                itemRenderer.shadowCastingMode = ShadowCastingMode.On;
            }

            SetPhysics(true);
            _body.linearVelocity = velocity;
        }

        private void Awake()
        {
            _worldParent = transform.parent;
            _body.interpolation = RigidbodyInterpolation.None;
        }

        private void SetPhysics(bool isSimulated)
        {
            _body.interpolation = isSimulated ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
            _body.isKinematic = !isSimulated;
            _body.detectCollisions = isSimulated;
        }

        private void Update()
        {
            if (_dropped && !IsTaken && transform.position.y < _dropPosition.y - _lostDepth)
            {
                _body.linearVelocity = Vector3.zero;
                transform.position = _dropPosition;
            }
        }

        private void SetCollidersEnabled(bool isEnabled)
        {
            foreach (Collider itemCollider in GetComponentsInChildren<Collider>())
            {
                itemCollider.enabled = isEnabled;
            }
        }
    }
}
