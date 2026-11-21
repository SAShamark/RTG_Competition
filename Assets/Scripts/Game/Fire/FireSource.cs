using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Fire
{
    public class FireSource : MonoBehaviour
    {
        private static readonly List<FireSource> ActiveList = new();

        [SerializeField, Range(0f, 1f)] private float _intensity = 1f;
        [SerializeField] private float _toughness = 1f;
        [SerializeField] private float _heatRadius = 2.5f;

        [Header("Reignite")]
        [SerializeField] private bool _reignite;
        [SerializeField] private float _reigniteDelay = 1.2f;
        [SerializeField] private float _reigniteSpeed = 0.35f;
        [SerializeField] private float _reigniteFloor = 0.25f;

        [Header("Visuals")]
        [SerializeField] private ParticleSystem[] _particles;
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private float _minVisualScale = 0.3f;
        [SerializeField] private Light _light;
        [SerializeField] private float _lightIntensity = 3f;
        [SerializeField] private float _flickerSpeed = 9f;
        [SerializeField] private float _flickerAmount = 0.25f;
        [SerializeField] private ParticleSystem _extinguishPuff;

        [Header("Gameplay")]
        [SerializeField] private Collider _blocker;
        [SerializeField] private AudioSource _loopAudio;

        private float[] _baseRates;
        private Vector3 _baseScale;
        private float _baseVolume;
        private float _sinceSprayed = 999f;
        private float _noiseSeed;
        private bool _isOut;

        public static IReadOnlyList<FireSource> Active => ActiveList;

        public float Intensity => _intensity;
        public bool IsOut => _isOut;
        public float HeatRadius => _heatRadius;

        public event Action<FireSource> OnExtinguished;
        public event Action<FireSource> OnReignited;
        public event Action<FireSource, float> OnIntensityChanged;

        private void Awake()
        {
            _noiseSeed = UnityEngine.Random.value * 100f;
            _baseRates = new float[_particles.Length];
            for (int i = 0; i < _baseRates.Length; i++)
            {
                _baseRates[i] = _particles[i].emission.rateOverTimeMultiplier;
            }

            _baseScale = _visualRoot.localScale;
            _baseVolume = _loopAudio != null ? _loopAudio.volume : 0f;
            _isOut = _intensity <= 0f;
            ApplyVisuals();
        }

        private void OnEnable() => ActiveList.Add(this);

        private void OnDisable() => ActiveList.Remove(this);

        public void SetReignite(bool reignite) => _reignite = reignite;

        public void ForceOut()
        {
            if (!_isOut && !_reignite)
            {
                SetIntensity(0f);
            }
        }

        public void Extinguish(float power, float deltaTime)
        {
            if (_isOut || power <= 0f)
            {
                return;
            }

            _sinceSprayed = 0f;
            float next = _intensity - power * deltaTime / Mathf.Max(0.05f, _toughness);
            SetIntensity(_reignite ? Mathf.Max(next, _reigniteFloor) : next);
        }

        public void Ignite(float intensity = 1f)
        {
            _isOut = false;
            foreach (ParticleSystem particles in _particles)
            {
                particles.Play(false);
            }

            SetIntensity(intensity);
            OnReignited?.Invoke(this);
        }

        public float HeatAt(Vector3 point)
        {
            if (_isOut)
            {
                return 0f;
            }

            float distance = Vector3.Distance(point, transform.position);
            return _intensity * Mathf.Clamp01(1f - distance / _heatRadius);
        }

        private void Update()
        {
            _sinceSprayed += Time.deltaTime;
            if (_reignite && _intensity > 0f && _intensity < 1f && _sinceSprayed > _reigniteDelay)
            {
                SetIntensity(_intensity + _reigniteSpeed * Time.deltaTime);
            }

            if (!_isOut)
            {
                float noise = Mathf.PerlinNoise(_noiseSeed, Time.time * _flickerSpeed) - 0.5f;
                _light.intensity = _lightIntensity * _intensity * (1f + noise * 2f * _flickerAmount);
            }
        }

        private void SetIntensity(float value)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(value, _intensity))
            {
                return;
            }

            _intensity = value;
            ApplyVisuals();
            OnIntensityChanged?.Invoke(this, _intensity);

            if (_intensity <= 0f && !_isOut)
            {
                _isOut = true;
                _extinguishPuff.Play(true);

                OnExtinguished?.Invoke(this);
            }
        }

        private void ApplyVisuals()
        {
            float visual = _intensity <= 0f ? 0f : Mathf.Lerp(0.15f, 1f, _intensity);
            for (int i = 0; i < _baseRates.Length; i++)
            {
                var emission = _particles[i].emission;
                emission.rateOverTimeMultiplier = _baseRates[i] * visual;
            }

            _visualRoot.localScale = _baseScale * Mathf.Lerp(_minVisualScale, 1f, _intensity);

            if (_intensity <= 0f)
            {
                foreach (ParticleSystem particles in _particles)
                {
                    particles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                }
            }

            _light.enabled = _intensity > 0f;

            _blocker.enabled = _intensity > 0f;

            if (_loopAudio != null)
            {
                _loopAudio.volume = _baseVolume * visual;
                if (_intensity <= 0f)
                {
                    _loopAudio.Stop();
                }
                else if (!_loopAudio.isPlaying && _loopAudio.clip != null)
                {
                    _loopAudio.Play();
                }
            }
        }
    }
}
