using Services.Audio;
using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PhysicsProp : MonoBehaviour, IToolReactive
    {
        [SerializeField] private ToolResponse _response = new();
        [SerializeField] private ImpactSound _impact = new(SoundType.PropImpact);
        [SerializeField] private float _lostDepth = 3f;

        private Rigidbody _body;
        private Vector3 _homePosition;
        private Quaternion _homeRotation;

        public void React(in ToolHit hit) => _response.Apply(_body, hit);

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _body.isKinematic = true;
            _homePosition = transform.position;
            _homeRotation = transform.rotation;
        }

        private void Update()
        {
            if (!_body.isKinematic && transform.position.y < _homePosition.y - _lostDepth)
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
    }
}
