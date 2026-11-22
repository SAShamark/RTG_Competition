using Services.Audio;
using UnityEngine;

namespace Gameplay.Entities.Interactive.Items
{
    public sealed class ExtinguisherItem : MonoBehaviour
    {
        [SerializeField] private Transform _nozzle;
        [SerializeField] private ParticleSystem _spray;

        private ISoundLoop _sprayLoop = SilentLoop.Instance;

        public Transform Nozzle => _nozzle;

        public void SetSpraying(bool isSpraying)
        {
            if (isSpraying && !_spray.isEmitting) _spray.Play(true);
            else if (!isSpraying && _spray.isEmitting) _spray.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            if (isSpraying && !_sprayLoop.IsPlaying)
            {
                _sprayLoop = AudioService.Instance.PlayLoop(SoundType.ExtinguisherSpray, _nozzle);
            }
            else if (!isSpraying)
            {
                _sprayLoop.Stop();
                _sprayLoop = SilentLoop.Instance;
            }
        }
    }
}
