using System;
using System.Collections.Generic;
using UnityEngine;

namespace Services.Audio
{
    [CreateAssetMenu(fileName = "AudioConfig", menuName = "Configs/Audio Config")]
    public sealed class AudioConfig : ScriptableObject
    {
        [Serializable]
        public sealed class SoundEntry
        {
            [SerializeField] private SoundType _type;
            [SerializeField] private AudioClip[] _clips = Array.Empty<AudioClip>();
            [SerializeField, Range(0f, 1f)] private float _volume = 1f;
            [SerializeField] private Vector2 _pitch = Vector2.one;
            [SerializeField, Range(0f, 1f)] private float _spatialBlend = 1f;
            [SerializeField, Min(1f)] private float _maxDistance = 25f;

            public SoundType Type => _type;
            public float Volume => _volume;
            public float SpatialBlend => _spatialBlend;
            public float MaxDistance => _maxDistance;
            public bool HasClips => _clips.Length > 0;

            public AudioClip PickClip() => _clips[UnityEngine.Random.Range(0, _clips.Length)];

            public float PickPitch() => UnityEngine.Random.Range(_pitch.x, _pitch.y);
        }

        [SerializeField] private SoundEntry[] _sounds = Array.Empty<SoundEntry>();

        private Dictionary<SoundType, SoundEntry> _byType;

        public bool TryGet(SoundType type, out SoundEntry entry)
        {
            if (_byType == null)
            {
                _byType = new Dictionary<SoundType, SoundEntry>();
                foreach (SoundEntry sound in _sounds)
                {
                    _byType[sound.Type] = sound;
                }
            }

            return _byType.TryGetValue(type, out entry) && entry.HasClips;
        }
    }
}
