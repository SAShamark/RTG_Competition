using System;
using UnityEngine;

namespace Gameplay.Cinematics.IntroDrift
{
    [Serializable]
    public sealed class DriftAudio
    {
        [SerializeField] private AudioSource _engine;
        [SerializeField] private AudioSource _siren;
        [SerializeField] private AudioSource _mech;
        [SerializeField] private AudioClip _engineClip;
        [SerializeField] private AudioClip _sirenClip;
        [SerializeField] private AudioClip _screechClip;
        [SerializeField] private AudioSource _stingers;
        [SerializeField] private AudioClip _slowMotionClip;
        [SerializeField] private AudioClip _panelHitClip;
        [SerializeField, Range(0.1f, 1f)] private float _slowPitch = 0.55f;
        [SerializeField, Range(0f, 1f)] private float _spatialBlend = 0.35f;

        [Header("Carry over into the intro timeline")]
        [SerializeField, Min(0f)] private float _sirenTail = 4.5f;
        [SerializeField, Min(0f)] private float _engineTail = 10.5f;
        [SerializeField, Min(0.01f)] private float _fadeOut = 0.4f;

        private float _tail = -1f;
        private float _sirenVolume;
        private float _engineVolume;
        private float _sirenSpatial;
        private float _engineSpatial;

        public bool IsTailing => _tail >= 0f;

        public void Begin()
        {
            _tail = -1f;
            _sirenVolume = _siren.volume;
            _engineVolume = _engine.volume;
            _sirenSpatial = _siren.spatialBlend;
            _engineSpatial = _engine.spatialBlend;
            _siren.spatialBlend = _spatialBlend;
            _engine.spatialBlend = _spatialBlend;
            Loop(_engine, _engineClip);
            Loop(_siren, _sirenClip);
        }

        public void Tick(float slowness)
        {
            float pitch = Mathf.Lerp(1f, _slowPitch, slowness);
            _engine.pitch = pitch;
            _siren.pitch = pitch;
            _mech.pitch = pitch;
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
            _siren.volume = _sirenVolume * Fade(_sirenTail);
            _engine.volume = _engineVolume * Fade(_engineTail);
            if (_tail >= Mathf.Max(_sirenTail, _engineTail))
            {
                Stop();
            }
        }

        public void Stop()
        {
            if (_tail < 0f && !_engine.isPlaying)
            {
                return;
            }

            _tail = -1f;
            Tick(0f);
            _engine.Stop();
            _siren.Stop();
            _engine.volume = _engineVolume;
            _siren.volume = _sirenVolume;
            _engine.spatialBlend = _engineSpatial;
            _siren.spatialBlend = _sirenSpatial;
        }

        private float Fade(float end) => Mathf.Clamp01((end - _tail) / _fadeOut);

        private static void Loop(AudioSource source, AudioClip clip)
        {
            source.clip = clip;
            source.loop = true;
            source.Play();
        }
    }
}
