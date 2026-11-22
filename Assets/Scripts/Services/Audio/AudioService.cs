using Services.Audio;
using UnityEngine;

namespace Services.Audio
{
    [DefaultExecutionOrder(-1000)]
    public sealed class AudioService : MonoSingleton<AudioService>
    {
        [SerializeField] private AudioConfig _config;
        [SerializeField] private AudioSource _voiceSource;
        [SerializeField] private AudioSource[] _oneShotSources;
        [SerializeField] private AudioSource[] _loopSources;

        private int _nextOneShot;

        private void Awake()
        {
            InitializeSingleton(false);
        }

        public void Play(SoundType type, float pitch = 1f) => PlayAt(type, transform.position, pitch, true);

        public void PlayAt(SoundType type, Vector3 position, float pitch = 1f) => PlayAt(type, position, pitch, false);

        public ISoundLoop PlayLoop(SoundType type, Transform anchor)
        {
            if (!_config.TryGet(type, out AudioConfig.SoundEntry entry))
            {
                return SilentLoop.Instance;
            }

            foreach (AudioSource source in _loopSources)
            {
                if (source.isPlaying)
                {
                    continue;
                }

                source.transform.SetParent(anchor, false);
                Setup(source, entry, false);
                source.Play();
                return new SoundLoop(source, entry.Volume, transform);
            }

            return SilentLoop.Instance;
        }

        public void PlayVoice(AudioClip clip)
        {
            _voiceSource.Stop();
            _voiceSource.clip = clip;
            _voiceSource.Play();
        }

        private void PlayAt(SoundType type, Vector3 position, float pitch, bool isFlat)
        {
            if (!_config.TryGet(type, out AudioConfig.SoundEntry entry))
            {
                return;
            }

            AudioSource source = _oneShotSources[_nextOneShot];
            _nextOneShot = (_nextOneShot + 1) % _oneShotSources.Length;
            source.transform.position = position;
            Setup(source, entry, isFlat);
            source.pitch *= pitch;
            source.Play();
        }

        private static void Setup(AudioSource source, AudioConfig.SoundEntry entry, bool isFlat)
        {
            source.clip = entry.PickClip();
            source.volume = entry.Volume;
            source.pitch = entry.PickPitch();
            source.spatialBlend = isFlat ? 0f : entry.SpatialBlend;
            source.maxDistance = entry.MaxDistance;
        }
    }
}
