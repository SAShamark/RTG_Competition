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

        [Header("Background")]
        [SerializeField] private AudioSource _music;
        [SerializeField] private AudioSource _streetAmbience;
        [SerializeField] private AudioSource _buildingAmbience;
        [SerializeField, Range(0f, 1f)] private float _musicVolume = 0.22f;
        [SerializeField, Range(0f, 1f)] private float _ambienceVolume = 0.5f;
        [SerializeField, Range(0f, 1f)] private float _duckedMusic = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _voiceDuck = 0.6f;
        [SerializeField] private float _fadeSpeed = 1.5f;

        private int _nextOneShot;
        private bool _isDucked;
        private bool _isIndoors;
        private float _voiceUntil;
        private Transform _listener;

        public Vector3 ListenerPosition => _listener != null ? _listener.position : transform.position;

        private void Awake()
        {
            InitializeSingleton(false);
            AudioListener listener = FindAnyObjectByType<AudioListener>();
            _listener = listener != null ? listener.transform : null;
            _music.volume = 0f;
            _streetAmbience.volume = 0f;
            _buildingAmbience.volume = 0f;
            _music.Play();
            _streetAmbience.Play();
            _buildingAmbience.Play();
        }

        private void Update()
        {
            float step = Time.unscaledDeltaTime * _fadeSpeed;
            float duck = _isDucked ? _duckedMusic : 1f;
            if (Time.unscaledTime < _voiceUntil)
            {
                duck = Mathf.Min(duck, _voiceDuck);
            }

            _music.volume = Mathf.MoveTowards(_music.volume, _musicVolume * duck, step * _musicVolume);
            _streetAmbience.volume = Mathf.MoveTowards(_streetAmbience.volume, _isIndoors ? 0f : _ambienceVolume, step * _ambienceVolume);
            _buildingAmbience.volume = Mathf.MoveTowards(_buildingAmbience.volume, _isIndoors ? _ambienceVolume : 0f, step * _ambienceVolume);
        }

        public void SetDucked(bool isDucked) => _isDucked = isDucked;

        public void SetIndoors(bool isIndoors) => _isIndoors = isIndoors;

        public bool IsAudible(SoundType type, Vector3 position) =>
            _config.TryGet(type, out AudioConfig.SoundEntry entry) && (position - ListenerPosition).sqrMagnitude <= entry.MaxDistance * entry.MaxDistance;

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

        public bool IsVoicePlaying => Time.unscaledTime < _voiceUntil;

        public void PlayVoice(AudioClip clip)
        {
            _voiceSource.Stop();
            _voiceSource.clip = clip;
            _voiceSource.Play();
            _voiceUntil = Time.unscaledTime + clip.length;
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
