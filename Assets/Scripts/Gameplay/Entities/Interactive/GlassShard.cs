using Services.Audio;
using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class GlassShard : MonoBehaviour
    {
        private static float _lastTinkleTime = float.MinValue;

        [SerializeField] private Rigidbody _body;
        [SerializeField] private MeshFilter _filter;
        [SerializeField] private MeshRenderer _renderer;
        [SerializeField] private BoxCollider _collider;

        [Header("Settle")]
        [SerializeField] private float _tinkleSpeed = 1.2f;
        [SerializeField] private float _tinkleInterval = 0.06f;
        [SerializeField] private SoundType _tinkleSound = SoundType.GlassTinkle;
        [SerializeField] private float _shrinkDuration = 0.5f;
        [SerializeField] private float _collisionDelay = 0.12f;

        private float _lifetime;
        private float _age;
        private bool _isFree;
        private bool _hasTinkled;
        private Vector3 _baseScale;

        public Rigidbody Body => _body;
        public MeshFilter Filter => _filter;
        public MeshRenderer Renderer => _renderer;
        public BoxCollider Collider => _collider;
        public bool IsIntact { get; set; }
        public int Ring { get; set; }
        public Vector3[] Contacts { get; set; } = System.Array.Empty<Vector3>();

        public void Place(Vector3 localPosition, Quaternion localRotation)
        {
            _body.interpolation = RigidbodyInterpolation.None;
            transform.SetLocalPositionAndRotation(localPosition, localRotation);
            _body.position = transform.position;
            _body.rotation = transform.rotation;
        }

        public void Free(Vector3 velocity, Vector3 angularVelocity, float lifetime)
        {
            _isFree = true;
            _lifetime = lifetime;
            _baseScale = transform.localScale;
            transform.SetParent(null, true);
            _collider.enabled = false;
            _body.isKinematic = false;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _body.linearVelocity = velocity;
            _body.angularVelocity = angularVelocity;
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
            if (!_isFree || _hasTinkled || collision.relativeVelocity.magnitude < _tinkleSpeed || Time.time - _lastTinkleTime < _tinkleInterval)
            {
                return;
            }

            _hasTinkled = true;
            _lastTinkleTime = Time.time;
            AudioService.Instance.PlayAt(_tinkleSound, transform.position);
        }
    }
}
