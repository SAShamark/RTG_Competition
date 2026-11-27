using Services.Audio;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gameplay.Entities.Interactive.Items
{
    public sealed class ToolItem : InventoryItem
    {
        [SerializeField] private Vector3 _holdPosition;
        [SerializeField] private Vector3 _holdEuler;
        [SerializeField] private float _lostDepth = 4f;
        [SerializeField] private bool _isHandOnly;
        [SerializeField] private Transform _leftGrip;
        [SerializeField] private Transform _rightGrip;
        [SerializeField, Range(0.005f, 0.05f)] private float _gripRadius = 0.017f;
        [SerializeField] private bool _hasTrigger;

        [Header("Sound")]
        [SerializeField] private ImpactSound _impact = new(SoundType.ToolImpact);

        private Transform _worldParent;
        [SerializeField] private Rigidbody _body;
        private Vector3 _dropPosition;
        private bool _dropped;

        public Vector3 HoldPosition => _holdPosition;
        public Vector3 HoldEuler => _holdEuler;
        public bool IsHandOnly => _isHandOnly;
        public Transform LeftGrip => _leftGrip;
        public Transform RightGrip => _rightGrip;
        public float GripRadius => _gripRadius;
        public bool HasTrigger => _hasTrigger;
        public override Vector3 PullPoint => _body.worldCenterOfMass;

        public override void BeginPull()
        {
            Release(transform.position, Vector3.zero);
            SetPhysics(false);
            SetCollidersEnabled(false);
        }

        public override void EndPull(Vector3 velocity) => Release(transform.position, velocity);

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

        private void OnCollisionEnter(Collision collision)
        {
            _impact.Handle(collision, transform.position);
            ToolImpact.Collide(collision);
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
