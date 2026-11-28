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
        [SerializeField, Min(0f)] private float _stiffness = 160f;
        [SerializeField, Min(0f)] private float _damping = 7f;
        [SerializeField, Min(0f)] private float _maxDent = 0.38f;

        [Header("Wobble")]
        [SerializeField, Min(0f)] private float _wobbleStiffness = 320f;
        [SerializeField, Min(0f)] private float _wobbleDamping = 3.2f;
        [SerializeField, Min(0f)] private float _wobbleKick = 0.55f;
        [SerializeField, Min(0f)] private float _wobbleFade = 5f;

        [Header("Frame")]
        [SerializeField, Min(0f)] private float _frameTilt = 2.5f;
        [SerializeField, Range(0f, 1f)] private float _frameSquash = 0.09f;
        [SerializeField, Range(0f, 1f)] private float _frameBulge = 0.05f;
        [SerializeField, Min(0f)] private float _frameDip = 0.03f;

        private Vector2 _contact;
        private float _offset;
        private float _velocity;
        private float _wobble;
        private float _wobbleVelocity;
        private float _wobbleWeight;
        private Vector3 _frameRest;
        private bool _isPressed;

        public Vector3 LandingPoint => _landingPoint.position;
        public Vector3 SafePoint => _safePoint.position;
        public Quaternion SafeRotation => _safePoint.rotation;
        public float MaxDent => _maxDent;

        private void Awake()
        {
            _membrane.Init();
            _frameRest = _frame.localPosition;
            _rider.Init(_membrane.Surface, _membrane.Radius);
            _rider.Impacted += HandleImpacted;
        }

        private void OnDisable()
        {
            _isPressed = false;
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

        public void Press(Vector3 worldPoint, float offset)
        {
            if (!_isPressed)
            {
                _isPressed = true;
                AudioService.Instance.PlayAt(SoundType.TrampolineBoing, transform.position);
            }

            Vector3 local = _membrane.Surface.InverseTransformPoint(worldPoint);
            _contact = new Vector2(local.x, local.z);
            _offset = Mathf.Max(offset, -_maxDent);
            _velocity = 0f;
            _wobble = 0f;
            _wobbleVelocity = 0f;
            _membrane.Apply(_contact, _offset);
            PoseFrame(_offset);
        }

        public void Release(float velocity)
        {
            _isPressed = false;
            _velocity = velocity;
            _wobbleVelocity = velocity * _wobbleKick;
        }

        private void Update()
        {
            if (_isPressed)
            {
                return;
            }

            _rider.Tick(_offset, _velocity);
            if (_rider.IsRiding || _rider.IsStanding)
            {
                _contact = _rider.Point;
            }

            float sag = _rider.Sag;
            bool isResting = Mathf.Abs(_offset + sag) < RestThreshold && Mathf.Abs(_velocity) < RestThreshold
                && Mathf.Abs(_wobble) < RestThreshold && Mathf.Abs(_wobbleVelocity) < RestThreshold;
            if (isResting && !_rider.IsStanding)
            {
                return;
            }

            float dt = Mathf.Min(Time.deltaTime, 1f / MinFrameRate);
            _velocity += (-_stiffness * (_offset + sag) - _damping * _velocity) * dt;
            _offset += _velocity * dt;
            _rider.Follow(_velocity);

            // A second, lightly damped spring rings on after the hit. It is only drawn, and only while nobody is on the
            // cloth, so the feet of a rider always match the surface the gameplay spring gives them.
            _wobbleVelocity += (-_wobbleStiffness * _wobble - _wobbleDamping * _wobbleVelocity) * dt;
            _wobble += _wobbleVelocity * dt;
            bool isLoaded = _rider.IsRiding || _rider.IsStanding;
            _wobbleWeight = Mathf.MoveTowards(_wobbleWeight, isLoaded ? 0f : 1f, _wobbleFade * dt);
            float shown = Mathf.Clamp(_offset + _wobble * _wobbleWeight, -_maxDent, _maxDent);

            _membrane.Apply(_contact, shown);
            PoseFrame(shown);
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
            _wobbleVelocity = _velocity * _wobbleKick;
            AudioService.Instance.PlayAt(SoundType.TrampolineBoing, transform.position);
        }

        // The frame squashes and bulges as the cloth dips, and stretches the other way as it springs back above rest.
        private void PoseFrame(float offset)
        {
            float dent = Mathf.Clamp(-offset / _maxDent, -1f, 1f);
            Vector2 lean = _contact / _membrane.Radius * (_frameTilt * Mathf.Max(0f, dent));
            float bulge = 1f + _frameBulge * dent;
            _frame.localRotation = Quaternion.Euler(lean.y, 0f, -lean.x);
            _frame.localScale = new Vector3(bulge, 1f - _frameSquash * dent, bulge);
            _frame.localPosition = _frameRest + Vector3.down * (_frameDip * dent);
        }
    }
}
