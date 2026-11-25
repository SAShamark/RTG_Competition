using Services.Audio;
using UnityEngine;

namespace Gameplay.Entities.Environment
{
    public sealed class Trampoline : MonoBehaviour
    {
        private const float RestThreshold = 0.0005f;
        private const float MinFrameRate = 30f;

        [SerializeField] private Transform _frame;
        [SerializeField] private Transform _landingPoint;
        [SerializeField] private Transform _safePoint;
        [SerializeField] private TrampolineMembrane _membrane = new();
        [SerializeField] private TrampolineRiderLogic _rider = new();

        [Header("Spring")]
        [SerializeField, Min(0f)] private float _impactSpeed = 4.5f;
        [SerializeField, Min(0f)] private float _stiffness = 160f;
        [SerializeField, Min(0f)] private float _damping = 7f;
        [SerializeField, Min(0f)] private float _maxDent = 0.38f;

        [Header("Frame")]
        [SerializeField, Min(0f)] private float _frameTilt = 2.5f;
        [SerializeField, Range(0f, 1f)] private float _frameSquash = 0.05f;

        private Vector2 _contact;
        private float _offset;
        private float _velocity;

        public Vector3 LandingPoint => _landingPoint.position;
        public Vector3 SafePoint => _safePoint.position;
        public Quaternion SafeRotation => _safePoint.rotation;

        // Half period of the damped spring: the time the cloth needs to dip and come back to rest height.
        public float ContactDuration => Mathf.PI / Mathf.Sqrt(_stiffness - _damping * _damping * 0.25f);

        private void Awake()
        {
            _membrane.Init();
            _rider.Init(_membrane.Surface, _membrane.Radius);
            _rider.Impacted += HandleImpacted;
        }

        private void OnDisable()
        {
            _rider.Release();
        }

        private void OnDestroy()
        {
            _rider.Impacted -= HandleImpacted;
            _membrane.Dispose();
        }

        private void OnTriggerEnter(Collider other)
        {
            _rider.Enter(other);
        }

        private void OnTriggerExit(Collider other)
        {
            _rider.Exit(other);
        }

        public void Bounce(float strength = 1f)
        {
            Vector3 local = _membrane.Surface.InverseTransformPoint(_landingPoint.position);
            Hit(new Vector2(local.x, local.z), _impactSpeed * strength);
        }

        public float SampleDent(Vector3 worldPoint)
        {
            Vector3 local = _membrane.Surface.InverseTransformPoint(worldPoint);
            Vector2 point = new(local.x, local.z);
            return point.sqrMagnitude <= _membrane.Radius * _membrane.Radius ? _membrane.Sample(point) : 0f;
        }

        private void Update()
        {
            _rider.Tick(_offset, _velocity);
            if (_rider.IsRiding || _rider.IsStanding)
            {
                _contact = _rider.Point;
            }

            float sag = _rider.Sag;
            bool isResting = Mathf.Abs(_offset + sag) < RestThreshold && Mathf.Abs(_velocity) < RestThreshold;
            if (isResting && !_rider.IsStanding)
            {
                return;
            }

            float dt = Mathf.Min(Time.deltaTime, 1f / MinFrameRate);
            _velocity += (-_stiffness * (_offset + sag) - _damping * _velocity) * dt;
            _offset += _velocity * dt;
            _rider.Follow(_velocity);

            _membrane.Apply(_contact, _offset);
            PoseFrame();
        }

        private void HandleImpacted(float speed, float startOffset)
        {
            _offset = Mathf.Min(_offset, startOffset);
            Hit(_rider.Point, speed);
        }

        private void Hit(Vector2 point, float speed)
        {
            // An undamped spring hit at speed v dips by v / sqrt(k), so v is capped to keep the dent above the ground.
            _contact = point;
            _velocity = -Mathf.Min(speed, _maxDent * Mathf.Sqrt(_stiffness));
            AudioService.Instance.PlayAt(SoundType.TrampolineBoing, transform.position);
        }

        private void PoseFrame()
        {
            float dent = Mathf.Clamp01(-_offset / _maxDent);
            Vector2 lean = _contact / _membrane.Radius * (_frameTilt * dent);
            _frame.localRotation = Quaternion.Euler(lean.y, 0f, -lean.x);
            _frame.localScale = new Vector3(1f, 1f - _frameSquash * dent, 1f);
        }
    }
}
