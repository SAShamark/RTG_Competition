using Services.Audio;
using UnityEngine;

namespace Gameplay.Entities.Environment
{
    public sealed class Trampoline : MonoBehaviour
    {
        private const float RestThreshold = 0.0005f;
        private const float MinFrameRate = 30f;

        [SerializeField] private Transform _bed;
        [SerializeField] private Transform _landingPoint;
        [SerializeField] private Transform _safePoint;

        [Header("Spring")]
        [SerializeField] private float _impactSpeed = 4.5f;
        [SerializeField] private float _stiffness = 160f;
        [SerializeField] private float _damping = 7f;
        [SerializeField] private float _squash = 0.6f;
        [SerializeField] private float _spreadFactor = 0.3f;

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
            AudioService.Instance.PlayAt(SoundType.TrampolineBoing, transform.position);
        }

        private void Update()
        {
            if ((Mathf.Abs(_offset) < RestThreshold && Mathf.Abs(_velocity) < RestThreshold))
            {
                return;
            }

            float dt = Mathf.Min(Time.deltaTime, 1f / MinFrameRate);
            _velocity += (-_stiffness * _offset - _damping * _velocity) * dt;
            _offset += _velocity * dt;

            _bed.localPosition = _bedRestPosition + Vector3.up * _offset;
            float spread = 1f + Mathf.Max(0f, -_offset) * _squash * _spreadFactor;
            _bed.localScale = new Vector3(_bedRestScale.x * spread, _bedRestScale.y, _bedRestScale.z * spread);
        }
    }
}
