using System;
using Services;
using UnityEngine;

namespace Gameplay.Cinematics.IntroDrift
{
    [Serializable]
    public sealed class DriftAudio
    {
        [SerializeField] private AudioSource _siren;
        [SerializeField] private AudioSource _mech;
        [SerializeField] private AudioClip _sirenClip;
        [SerializeField] private AudioClip _screechClip;
        [SerializeField] private AudioSource _stingers;
        [SerializeField] private AudioClip _slowMotionClip;
        [SerializeField] private AudioClip _panelHitClip;
        [SerializeField, Range(0.1f, 1f)] private float _slowPitch = 0.55f;
        [SerializeField, Range(0f, 1f)] private float _spatialBlend = 0.35f;

        [Header("Music")]
        [SerializeField] private AudioSource _music;
        [SerializeField] private AudioClip _musicClip;
        [SerializeField, Min(0.01f)] private float _musicFadeOut = 2f;
        [SerializeField, Min(0.01f)] private float _musicEndFade = 1.5f;
        [SerializeField, Min(0.01f)] private float _soundsFadeIn = 1.5f;

        [Header("Carry over into the intro timeline")]
        [SerializeField, Min(0f)] private float _sirenTail = 4.5f;
        [SerializeField, Min(0.01f)] private float _fadeOut = 0.4f;

        private float _tail = -1f;
        private float _sirenVolume;
        private float _sirenSpatial;
        private float _musicVolume;

        public bool IsTailing => _tail >= 0f;
        public AudioSource Music => _music;

        public void Begin()
        {
            _tail = -1f;
            _sirenVolume = _siren.volume;
            _sirenSpatial = _siren.spatialBlend;
            _musicVolume = _music.volume;
            _siren.spatialBlend = _spatialBlend;
            _siren.clip = _sirenClip;
            _siren.loop = true;
            _siren.volume = 0f;
            _siren.Play();
            _music.clip = _musicClip;
            _music.Play();
        }

        public void Tick(float slowness)
        {
            float pitch = Mathf.Lerp(1f, _slowPitch, slowness);
            _siren.pitch = pitch;
            _mech.pitch = pitch;
            _music.volume = _musicVolume * MusicEndFade();
        }

        public void Screech()
        {
            _mech.PlayOneShot(_screechClip);
            _stingers.PlayOneShot(_slowMotionClip);
        }

        public void PanelHit() => _stingers.PlayOneShot(_panelHitClip);

        public void BeginTail()
        {
            Tick(0f);
            _tail = 0f;
        }

        public void TickTail(float deltaTime)
        {
            _tail += deltaTime;
            float fadeIn = Easing.Smooth(Mathf.Clamp01(_tail / _soundsFadeIn));
            _music.volume = _musicVolume * MusicEndFade() * (1f - Easing.Smooth(Mathf.Clamp01(_tail / _musicFadeOut)));
            _siren.volume = _sirenVolume * fadeIn * Mathf.Clamp01((_sirenTail - _tail) / _fadeOut);
            if (_tail >= Mathf.Max(_sirenTail, _musicFadeOut))
            {
                Stop();
            }
        }

        public void Stop()
        {
            if (_tail < 0f && !_siren.isPlaying && !_music.isPlaying)
            {
                return;
            }

            _tail = -1f;
            Tick(0f);
            _siren.Stop();
            _music.Stop();
            _siren.volume = _sirenVolume;
            _siren.spatialBlend = _sirenSpatial;
            _music.volume = _musicVolume;
        }

        private float MusicEndFade() => Easing.Smooth(Mathf.Clamp01((_musicClip.length - _music.time) / _musicEndFade));
    }
}
