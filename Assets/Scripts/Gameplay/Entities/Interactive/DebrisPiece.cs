using Services.Audio;
using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class DebrisPiece : MonoBehaviour
    {
        private static float _lastImpactTime = float.MinValue;

        [SerializeField] private Rigidbody _body;
        [SerializeField] private BoxCollider _collider;
        [SerializeField] private MeshRenderer _renderer;

        [Header("Settle")]
        [SerializeField] private SoundType _impactSound = SoundType.FootstepWood;
        [SerializeField] private Vector2 _impactPitch = new(0.9f, 1.2f);
        [SerializeField] private float _impactSpeed = 1.5f;
        [SerializeField] private float _impactInterval = 0.08f;
        [SerializeField] private float _collisionDelay = 0.08f;
        [SerializeField] private float _shrinkDuration = 0.6f;

        private float _lifetime;
        private float _age;
        private bool _isFree;
        private bool _hasLanded;
        private Vector3 _baseScale;

        public Rigidbody Body => _body;
        public BoxCollider Collider => _collider;
        public MeshRenderer Renderer => _renderer;

        public void Place(Vector3 localPosition, Quaternion localRotation)
        {
            _body.interpolation = RigidbodyInterpolation.None;
            transform.SetLocalPositionAndRotation(localPosition, localRotation);
            _body.position = transform.position;
            _body.rotation = transform.rotation;
        }

        public void Free(Vector3 force, Vector3 torque, float lifetime)
        {
            _isFree = true;
            _lifetime = lifetime;
            _baseScale = transform.localScale;
            transform.SetParent(null, true);
            _collider.enabled = false;
            _body.isKinematic = false;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _body.AddForce(force, ForceMode.Impulse);
            _body.AddTorque(torque, ForceMode.Impulse);
        }

        private void Update()
        {
            if (!_isFree)
            {
                return;
            }

            _age += Time.deltaTime;
            _collider.enabled = _age >= _collisionDelay;
            float shrinkStart = _lifetime - _shrinkDuration;
            if (_age < shrinkStart)
            {
                return;
            }

            float k = 1f - Mathf.Clamp01((_age - shrinkStart) / _shrinkDuration);
            transform.localScale = _baseScale * k;
            if (k <= 0f)
            {
                Destroy(gameObject);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!_isFree || _hasLanded || collision.relativeVelocity.magnitude < _impactSpeed || Time.time - _lastImpactTime < _impactInterval)
            {
                return;
            }

            _hasLanded = true;
            _lastImpactTime = Time.time;
            AudioService.Instance.PlayAt(_impactSound, transform.position, Random.Range(_impactPitch.x, _impactPitch.y));
        }
    }
}
