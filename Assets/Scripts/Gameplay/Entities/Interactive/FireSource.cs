using Services.Audio;
using System;
using System.Collections.Generic;
using Gameplay.Entities.Interactive.Items;
using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    public sealed class FireSource : MonoBehaviour
    {
        private const float NeverSprayed = 999f;
        private const float MinToughness = 0.05f;

        private static readonly List<FireSource> ActiveList = new();
        private static readonly int PowderId = Shader.PropertyToID("_Powder");

        [SerializeField, Range(0f, 1f)] private float _intensity = 1f;
        [SerializeField] private float _toughness = 1f;
        [SerializeField] private float _heatRadius = 2.5f;
        [SerializeField] private float _heatHeight = 2.5f;

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

        [Header("Under spray")]
        [SerializeField] private ParticleSystem _steam;
        [SerializeField] private ParticleSystem _smolder;
        [SerializeField] private GameObject _scorch;
        [SerializeField] private ParticleSystem _embers;
        [SerializeField] private ParticleSystem _splash;
        [SerializeField] private int _outSteamBurst = 30;
        [SerializeField] private int _outSplashBurst = 40;
        [SerializeField] private float _sprayedWindow = 0.15f;
        [SerializeField] private float _squash = 0.35f;
        [SerializeField] private float _wobbleSpeed = 14f;
        [SerializeField] private float _wobbleAngle = 8f;
        [SerializeField] private float _sprayedFlicker = 0.6f;
        [SerializeField] private float _sprayBlendSpeed = 6f;

        [Header("Powder")]
        [SerializeField] private float _powderProbeRadius = 0.35f;
        [SerializeField, Min(1)] private int _powderProbeCount = 8;
        [SerializeField] private float _powderCoverDepth = 0.015f;
        [SerializeField, Range(0f, 1f)] private float _powderCoverFraction = 0.6f;
        [SerializeField] private float _powderCheckInterval = 0.25f;
        [SerializeField] private float _meltRadius = 0.9f;
        [SerializeField] private float _meltRate = 0.012f;

        [Header("Debris")]
        [SerializeField] private Renderer[] _dustedDebris;

        [Header("Gameplay")]
        [SerializeField] private Collider _blocker;
        [SerializeField] private float _minVisual = 0.15f;

        private float[] _baseRates;
        private Vector3 _baseScale;
        private ISoundLoop _loop = SilentLoop.Instance;
        private float _loopVolume;
        private float _sinceSprayed = NeverSprayed;
        private float _noiseSeed;
        private float _steamRate;
        private float _embersRate;
        private float _splashRate;
        private float _sprayBlend;
        private ISoundLoop _sizzle = SilentLoop.Instance;
        private float _sincePowderCheck;
        private MaterialPropertyBlock _dustBlock;
        private float _dust;

        public static IReadOnlyList<FireSource> Active => ActiveList;

        public float Intensity => _intensity;
        public bool IsOut { get; private set; }
        public float HeatRadius => _heatRadius;
        public bool IsSuppressedByPowder { get; private set; }

        public event Action<FireSource> Extinguished;
        public event Action<FireSource> Reignited;
        public event Action<FireSource, float> IntensityChanged;

        private void Awake()
        {
            _noiseSeed = UnityEngine.Random.value * 100f;
            _dustBlock = new MaterialPropertyBlock();
            _baseRates = new float[_particles.Length];
            for (int i = 0; i < _baseRates.Length; i++)
            {
                _baseRates[i] = _particles[i].emission.rateOverTimeMultiplier;
            }

            _baseScale = _visualRoot.localScale;
            _steamRate = _steam.emission.rateOverTimeMultiplier;
            _embersRate = _embers.emission.rateOverTimeMultiplier;
            _splashRate = _splash.emission.rateOverTimeMultiplier;
            _scorch.SetActive(false);
            IsOut = _intensity <= 0f;
            ApplyVisuals();
        }

        private void OnEnable() => ActiveList.Add(this);

        private void OnDisable() => ActiveList.Remove(this);

        public void SetReignite(bool reignite) => _reignite = reignite;

        public void ForceOut()
        {
            // An inactive fire has not run Awake yet: it just wakes up already out.
            if (_baseRates == null)
            {
                _intensity = 0f;
                return;
            }

            if (!IsOut && !_reignite)
            {
                SetIntensity(0f);
            }
        }

        public void BurnDown(float intensity)
        {
            if (!IsOut && intensity < _intensity)
            {
                SetIntensity(intensity);
            }
        }

        public void Grow(float amount)
        {
            if (!IsOut && !IsSuppressedByPowder)
            {
                SetIntensity(Mathf.Min(1f, _intensity + amount));
            }
        }

        public void Extinguish(float power, float deltaTime) => Suppress(power, deltaTime, true);

        public void Douse(float power, float deltaTime) => Suppress(power, deltaTime, false);

        private void Suppress(float power, float deltaTime, bool leavesPowder)
        {
            if (IsOut || power <= 0f)
            {
                return;
            }

            _sinceSprayed = 0f;
            float next = _intensity - power * deltaTime / Mathf.Max(MinToughness, _toughness);
            if (leavesPowder)
            {
                DustDebris(1f - Mathf.Clamp01(next));
            }

            SetIntensity(_reignite ? Mathf.Max(next, _reigniteFloor) : next);
        }

        // The powder that knocks the flames down stays on whatever was burning.
        private void DustDebris(float amount)
        {
            if (amount <= _dust)
            {
                return;
            }

            _dust = amount;
            _dustBlock.SetFloat(PowderId, _dust);
            foreach (Renderer debris in _dustedDebris)
            {
                debris.SetPropertyBlock(_dustBlock);
            }
        }

        public void Ignite(float intensity = 1f)
        {
            IsOut = false;
            _smolder.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            foreach (ParticleSystem particles in _particles)
            {
                particles.Play(false);
            }

            SetIntensity(intensity);
            Reignited?.Invoke(this);
        }

        public float HeatAt(Vector3 point)
        {
            if (IsOut)
            {
                return 0f;
            }

            Vector3 offset = point - transform.position;
            if (offset.y < 0f || offset.y > _heatHeight)
            {
                return 0f;
            }

            // Horizontal distance (the head is well above the fire base); square root makes heat build up early.
            offset.y = 0f;
            float closeness = Mathf.Clamp01(1f - offset.magnitude / _heatRadius);
            return _intensity * Mathf.Sqrt(closeness);
        }

        private void Update()
        {
            _sinceSprayed += Time.deltaTime;
            UpdatePowder(Time.deltaTime);
            if (_reignite && !IsSuppressedByPowder && _intensity > 0f && _intensity < 1f && _sinceSprayed > _reigniteDelay)
            {
                SetIntensity(_intensity + _reigniteSpeed * Time.deltaTime);
            }

            UpdateLoop();
            bool sprayed = !IsOut && _sinceSprayed < _sprayedWindow;
            _sprayBlend = Mathf.MoveTowards(_sprayBlend, sprayed ? 1f : 0f, Time.deltaTime * _sprayBlendSpeed);
            UpdateSprayReaction(sprayed);

            if (!IsOut)
            {
                float noise = Mathf.PerlinNoise(_noiseSeed, Time.time * _flickerSpeed) - 0.5f;
                float flicker = Mathf.Lerp(_flickerAmount, _sprayedFlicker, _sprayBlend);
                _light.intensity = _lightIntensity * _intensity * (1f + noise * 2f * flicker);
            }
        }

        private void UpdatePowder(float deltaTime)
        {
            Vector3 center = transform.position;
            if (!IsOut)
            {
                PowderSnowChunk.MeltAll(center, _meltRadius, _meltRate * _intensity * deltaTime);
            }

            _sincePowderCheck += deltaTime;
            if (_sincePowderCheck < _powderCheckInterval)
            {
                return;
            }

            _sincePowderCheck = 0f;
            int covered = 0;
            for (int i = 0; i < _powderProbeCount; i++)
            {
                float angle = i * Mathf.PI * 2f / _powderProbeCount;
                Vector3 probe = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _powderProbeRadius;
                if (PowderSnowChunk.SampleDepth(probe) >= _powderCoverDepth)
                {
                    covered++;
                }
            }

            IsSuppressedByPowder = (float)covered / _powderProbeCount >= _powderCoverFraction;
        }

        private void UpdateSprayReaction(bool sprayed)
        {
            var steamEmission = _steam.emission;
            steamEmission.rateOverTimeMultiplier = _steamRate * _sprayBlend * Mathf.Max(_intensity, _minVisual);
            var embersEmission = _embers.emission;
            embersEmission.rateOverTimeMultiplier = _embersRate * _sprayBlend * _intensity;
            var splashEmission = _splash.emission;
            splashEmission.rateOverTimeMultiplier = _splashRate * _sprayBlend;
            if (sprayed && !_steam.isEmitting)
            {
                _steam.Play(false);
                _embers.Play(false);
                _splash.Play(false);
            }

            if (sprayed && !_sizzle.IsPlaying)
            {
                _sizzle = AudioService.Instance.PlayLoop(SoundType.FireSizzle, transform);
            }

            _sizzle.SetVolume(_sprayBlend);
            if (_sprayBlend <= 0f && _sizzle.IsPlaying)
            {
                _sizzle.Stop();
                _sizzle = SilentLoop.Instance;
                _steam.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                _embers.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                _splash.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }

            if (IsOut)
            {
                return;
            }

            float wobble = (Mathf.PerlinNoise(Time.time * _wobbleSpeed, _noiseSeed) - 0.5f) * 2f * _wobbleAngle * _sprayBlend;
            Vector3 scale = _baseScale * Mathf.Lerp(_minVisualScale, 1f, _intensity);
            scale.y *= 1f - _squash * _sprayBlend;
            _visualRoot.localScale = scale;
            _visualRoot.localRotation = Quaternion.Euler(wobble, 0f, wobble * 0.5f);
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
            IntensityChanged?.Invoke(this, _intensity);

            if (_intensity <= 0f && !IsOut)
            {
                IsOut = true;
                _extinguishPuff.Play(true);
                _smolder.Play(true);
                _steam.Emit(_outSteamBurst);
                _splash.Emit(_outSplashBurst);
                _scorch.transform.localRotation = Quaternion.Euler(90f, UnityEngine.Random.Range(0f, 360f), 0f);
                _scorch.SetActive(true);
                AudioService.Instance.PlayAt(SoundType.FireOut, transform.position);

                Extinguished?.Invoke(this);
            }
        }

        private void ApplyVisuals()
        {
            float visual = _intensity <= 0f ? 0f : Mathf.Lerp(_minVisual, 1f, _intensity);
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

            _loopVolume = visual;
            UpdateLoop();
        }

        // Only nearby fires hold one of the shared loop sources, so the spray and gas loops never run out.
        private void UpdateLoop()
        {
            bool audible = _intensity > 0f && AudioService.Instance.IsAudible(SoundType.FireLoop, transform.position);
            if (!audible)
            {
                _loop.Stop();
                _loop = SilentLoop.Instance;
                return;
            }

            if (!_loop.IsPlaying)
            {
                _loop = AudioService.Instance.PlayLoop(SoundType.FireLoop, transform);
            }

            _loop.SetVolume(_loopVolume);
        }
    }
}
