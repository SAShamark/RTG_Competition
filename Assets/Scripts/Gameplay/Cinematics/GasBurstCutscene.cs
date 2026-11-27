using Gameplay.Dialogue;
using Gameplay.Entities.Characters.Player;
using Gameplay.Entities.Interactive;
using Services;
using UnityEngine;

namespace Gameplay.Cinematics
{
    public sealed class GasBurstCutscene : CinematicBase
    {
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
        [SerializeField, Range(0f, 1f)] private float _ignitionPulse = 0.9f;
        [SerializeField, Min(0.01f)] private float _ignitionPulseDuration = 0.5f;

        [SerializeField] private CinematicSettings _settings = new(0.35f, 0f);
        [SerializeField] private SubtitleCue[] _subtitles =
        {
            new(0.7f, 1.6f, DialogIds.PlayerJumpWindow),
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
                _player.Rig.Input.Pulse(true, _ignitionPulse, _ignitionPulseDuration);
                _player.Rig.Input.Pulse(false, _ignitionPulse, _ignitionPulseDuration);
            }
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
            _flash.enabled = false;
            _player.enabled = true;
        }
    }
}
