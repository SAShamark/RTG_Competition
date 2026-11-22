using UnityEngine;

namespace Services.Audio
{
    public interface ISoundLoop
    {
        bool IsPlaying { get; }
        void SetVolume(float volume);
        void SetPitch(float pitch);
        void Stop();
    }

    public sealed class SilentLoop : ISoundLoop
    {
        public static SilentLoop Instance { get; } = new();

        public bool IsPlaying => false;
        public void SetVolume(float volume) { }
        public void SetPitch(float pitch) { }
        public void Stop() { }
    }

    public sealed class SoundLoop : ISoundLoop
    {
        private readonly AudioSource _source;
        private readonly float _baseVolume;
        private readonly Transform _home;

        public SoundLoop(AudioSource source, float baseVolume, Transform home)
        {
            _source = source;
            _baseVolume = baseVolume;
            _home = home;
        }

        public bool IsPlaying => _source.isPlaying;

        public void SetVolume(float volume) => _source.volume = _baseVolume * volume;

        public void SetPitch(float pitch) => _source.pitch = pitch;

        public void Stop()
        {
            _source.Stop();
            _source.transform.SetParent(_home, false);
        }
    }
}
