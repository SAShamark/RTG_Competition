using System;
using Services.Audio;
using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    [Serializable]
    public sealed class ImpactSound
    {
        [SerializeField] private SoundType _sound;
        [SerializeField] private float _minSpeed = 1.2f;
        [SerializeField] private float _interval = 0.12f;

        private float _lastTime = float.MinValue;

        public ImpactSound(SoundType sound)
        {
            _sound = sound;
        }

        public void Handle(Collision collision, Vector3 position)
        {
            if (collision.relativeVelocity.magnitude < _minSpeed || Time.time - _lastTime < _interval)
            {
                return;
            }

            _lastTime = Time.time;
            AudioService.Instance.PlayAt(_sound, position);
        }
    }
}
