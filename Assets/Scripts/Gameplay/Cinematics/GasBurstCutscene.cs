using Gameplay.Entities.Interactive;
using Gameplay.Entities.Player;
using Services;
using Unity.Cinemachine;
using UnityEngine;

namespace Gameplay.Cinematics
{
    public sealed class GasBurstCutscene : CinematicBase
    {
        private const float ShakeVerticalRatio = 1.3f;

        [SerializeField] private CinemachineCamera _camera;
        [SerializeField] private Transform _shotStart;
        [SerializeField] private Transform _shotEnd;
        [SerializeField] private Transform _lookAt;
        [SerializeField] private GasValve _valve;
        [SerializeField] private ParticleSystem _pipeBreak;
        [SerializeField] private ParticleSystem _gasJet;
        [SerializeField] private ParticleSystem _spark;
        [SerializeField] private ParticleSystem _ignition;
        [SerializeField] private Light _flash;

        [SerializeField] private float _duration = 3.4f;
        [SerializeField] private float _burstTime = 0.6f;
        [SerializeField] private float _sparkTime = 1.7f;
        [SerializeField] private float _ignitionTime = 2.3f;
        [SerializeField] private float _flashIntensity = 8f;
        [SerializeField] private float _flashFade = 0.7f;
        [SerializeField] private float _shakeTime = 0.6f;
        [SerializeField] private float _shakeAmount = 0.05f;
        [SerializeField] private float _shakeFrequency = 35f;

        [SerializeField] private CinematicSettings _settings = new(0.35f, 0f);
        [SerializeField] private SubtitleCue[] _subtitles =
        {
            new(0.7f, 1.6f, "M07"),
        };

        private FirstPersonController _player;
        private bool _isBurst;
        private bool _isSparked;
        private bool _isIgnited;

        public override CinematicSettings Settings => _settings;
        public override SubtitleCue[] Subtitles => _subtitles;
        protected override float Duration => _duration;

        public void Play(FirstPersonController player)
        {
            if (!TryBegin())
            {
                return;
            }

            _player = player;
            _player.enabled = false;
            _isBurst = false;
            _isSparked = false;
            _isIgnited = false;
            _flash.enabled = false;

            _camera.transform.SetPositionAndRotation(_shotStart.position, Quaternion.LookRotation(_lookAt.position - _shotStart.position));
            _camera.gameObject.SetActive(true);
            PlayClock();
        }

        private void Update()
        {
            if (!IsPlaying)
            {
                return;
            }

            float t = ElapsedTime;
            TriggerBeats(t);
            MoveCamera(t);
            FadeFlash(t);
            StopAtEnd();
        }

        private void TriggerBeats(float t)
        {
            if (!_isBurst && t >= _burstTime)
            {
                _isBurst = true;
                _valve.Burst();
                EffectPlayback.Restart(_pipeBreak);
                _gasJet.Play(true);
            }

            if (!_isSparked && t >= _sparkTime)
            {
                _isSparked = true;
                EffectPlayback.Restart(_spark);
            }

            if (!_isIgnited && t >= _ignitionTime)
            {
                _isIgnited = true;
                _gasJet.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                EffectPlayback.Restart(_ignition);
                _flash.enabled = true;
            }
        }

        private void MoveCamera(float t)
        {
            float k = Easing.Smooth(Mathf.Clamp01(t / _duration));
            Vector3 position = Vector3.Lerp(_shotStart.position, _shotEnd.position, k);
            float sinceIgnition = t - _ignitionTime;
            if (sinceIgnition > 0f && sinceIgnition < _shakeTime)
            {
                float fade = 1f - sinceIgnition / _shakeTime;
                position += new Vector3(Mathf.Sin(t * _shakeFrequency), Mathf.Cos(t * _shakeFrequency * ShakeVerticalRatio), 0f) * (_shakeAmount * fade);
            }

            _camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(_lookAt.position - position));
        }

        private void FadeFlash(float t)
        {
            if (!_isIgnited)
            {
                return;
            }

            _flash.intensity = _flashIntensity * Mathf.Clamp01(1f - (t - _ignitionTime) / _flashFade);
        }

        protected override void OnStopped()
        {
            _camera.gameObject.SetActive(false);
            _flash.enabled = false;
            _player.enabled = true;
        }
    }
}
