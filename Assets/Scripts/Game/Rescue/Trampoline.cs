using UnityEngine;

namespace Game.Rescue
{
    public class Trampoline : MonoBehaviour
    {
        [SerializeField] private Transform _bed;
        [SerializeField] private Transform _landingPoint;
        [SerializeField] private Transform _safePoint;
        [SerializeField] private AudioSource _boingAudio;

        [Header("Spring")]
        [SerializeField] private float _impactSpeed = 4.5f;
        [SerializeField] private float _stiffness = 160f;
        [SerializeField] private float _damping = 7f;
        [SerializeField] private float _squash = 0.6f;

        private Vector3 _bedRestPosition;
        private Vector3 _bedRestScale;
        private float _offset;
        private float _velocity;

        public Vector3 LandingPoint => _landingPoint.position;
        public Vector3 SafePoint => _safePoint.position;
        public Quaternion SafeRotation => _safePoint.rotation;

        private void Awake()
        {
            _bedRestPosition = _bed.localPosition;
            _bedRestScale = _bed.localScale;
        }

        public void Bounce(float strength = 1f)
        {
            _velocity = -_impactSpeed * strength;
            _boingAudio.pitch = Random.Range(0.92f, 1.08f);
            _boingAudio.Play();
        }

        private void Update()
        {
            if ((Mathf.Abs(_offset) < 0.0005f && Mathf.Abs(_velocity) < 0.0005f))
            {
                return;
            }

            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            _velocity += (-_stiffness * _offset - _damping * _velocity) * dt;
            _offset += _velocity * dt;

            _bed.localPosition = _bedRestPosition + Vector3.up * _offset;
            float spread = 1f + Mathf.Max(0f, -_offset) * _squash * 0.3f;
            _bed.localScale = new Vector3(_bedRestScale.x * spread, _bedRestScale.y, _bedRestScale.z * spread);
        }
    }
}
