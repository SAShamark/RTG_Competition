using System;
using Services.Audio;
using UnityEngine;

namespace Gameplay.Entities.Interactive.Items
{
    public sealed class FrostGrenadeItem : MonoBehaviour
    {
        [Serializable]
        private struct GlowSlot
        {
            [SerializeField] private Renderer _renderer;
            [SerializeField, Min(0)] private int _materialIndex;

            public Renderer Renderer => _renderer;
            public int MaterialIndex => _materialIndex;
        }

        private const float FullCycle = Mathf.PI * 2f;

        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private Rigidbody _body;

        [Header("Fuse")]
        [SerializeField, Min(0.1f)] private float _fuseDuration = 5f;
        [SerializeField] private FrostBlast _blast;
        [SerializeField] private SoundType _pinSound = SoundType.GrenadePin;

        [Header("Pin & lever")]
        [SerializeField] private Transform _pin;
        [SerializeField] private Vector3 _pinPull = new(0.08f, 0f, 0f);
        [SerializeField, Min(0.01f)] private float _pinDuration = 0.25f;
        [SerializeField] private Transform _lever;
        [SerializeField] private Vector3 _leverOpenEuler = new(0f, 0f, -75f);
        [SerializeField, Min(0.01f)] private float _leverDuration = 0.2f;

        [Header("Glow")]
        [SerializeField] private GlowSlot[] _glow;
        [SerializeField, ColorUsage(false, true)] private Color _glowColor = new(0.15f, 0.85f, 1f);
        [SerializeField, Min(0f)] private float _armedGlow = 3f;
        [SerializeField, Min(0f)] private float _peakGlow = 16f;
        [SerializeField, Min(0f)] private float _slowPulse = 1.5f;
        [SerializeField, Min(0f)] private float _fastPulse = 9f;
        [SerializeField, Range(0f, 1f)] private float _pulseDepth = 0.45f;
        [SerializeField] private Light _light;
        [SerializeField, Min(0f)] private float _armedLight = 0.4f;
        [SerializeField, Min(0f)] private float _peakLight = 5f;

        [Header("Flight")]
        [SerializeField, Min(0f)] private float _spin = 9f;
        [SerializeField, Min(0f)] private float _glassBreakSpeed = 4f;
        [SerializeField, Range(0f, 1f)] private float _glassSpeedKeep = 0.8f;

        private MaterialPropertyBlock _block;
        private Vector3 _pinRest;
        private Quaternion _leverRest;
        private Vector3 _velocity;
        private float _explodeTime;
        private float _pinTime;
        private float _leverTime;
        private float _phase;

        public bool IsArmed { get; private set; }

        public event Action<FrostGrenadeItem> Exploded;

        public void Arm()
        {
            IsArmed = true;
            _explodeTime = Time.time + _fuseDuration;
            _pinTime = 0f;
            _light.enabled = true;
            AudioService.Instance.Play(_pinSound);
        }

        public void Launch(Collider owner)
        {
            foreach (Collider itemCollider in GetComponentsInChildren<Collider>())
            {
                Physics.IgnoreCollision(itemCollider, owner, true);
            }

            _body.angularVelocity = UnityEngine.Random.insideUnitSphere * _spin;
            if (IsArmed)
            {
                _leverTime = 0f;
            }
        }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _pinRest = _pin.localPosition;
            _leverRest = _lever.localRotation;
            _pinTime = _pinDuration;
            _leverTime = _leverDuration;
            _light.enabled = false;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            TickPin(deltaTime);
            TickLever(deltaTime);
            if (!IsArmed)
            {
                return;
            }

            ApplyGlow(1f - Mathf.Clamp01((_explodeTime - Time.time) / _fuseDuration), deltaTime);
            CheckFuse();
        }

        // The holder calls this too: a hidden grenade does not get its own Update, yet its fuse keeps burning.
        public void CheckFuse()
        {
            if (IsArmed && Time.time >= _explodeTime)
            {
                Explode();
            }
        }

        private void FixedUpdate()
        {
            if (!_body.isKinematic)
            {
                _velocity = _body.linearVelocity;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_velocity.magnitude < _glassBreakSpeed)
            {
                return;
            }

            BreakableGlass glass = collision.collider.GetComponentInParent<BreakableGlass>();
            if (glass == null)
            {
                return;
            }

            glass.Smash(collision.GetContact(0).point, _velocity.normalized, false);
            _body.linearVelocity = _velocity * _glassSpeedKeep;
        }

        private void TickPin(float deltaTime)
        {
            if (_pinTime >= _pinDuration)
            {
                return;
            }

            _pinTime = Mathf.Min(_pinTime + deltaTime, _pinDuration);
            float progress = _pinTime / _pinDuration;
            _pin.localPosition = _pinRest + _pinPull * progress;
            _pin.localScale = Vector3.one * (1f - progress * progress);
        }

        private void TickLever(float deltaTime)
        {
            if (_leverTime >= _leverDuration)
            {
                return;
            }

            _leverTime = Mathf.Min(_leverTime + deltaTime, _leverDuration);
            _lever.localRotation = _leverRest * Quaternion.Euler(_leverOpenEuler * (_leverTime / _leverDuration));
        }

        private void ApplyGlow(float progress, float deltaTime)
        {
            _phase += deltaTime * Mathf.Lerp(_slowPulse, _fastPulse, progress) * FullCycle;
            float pulse = 1f - _pulseDepth * (0.5f + 0.5f * Mathf.Sin(_phase));
            // Squared so the glow stays calm for most of the fuse and flares up at the end.
            float heat = progress * progress;
            _block.SetColor(EmissionId, _glowColor * (Mathf.Lerp(_armedGlow, _peakGlow, heat) * pulse));
            foreach (GlowSlot slot in _glow)
            {
                slot.Renderer.SetPropertyBlock(_block, slot.MaterialIndex);
            }

            _light.intensity = Mathf.Lerp(_armedLight, _peakLight, heat) * pulse;
        }

        private void Explode()
        {
            IsArmed = false;
            Instantiate(_blast, transform.position, Quaternion.identity);
            Exploded?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
