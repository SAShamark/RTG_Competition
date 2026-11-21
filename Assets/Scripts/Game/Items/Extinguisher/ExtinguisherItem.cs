using UnityEngine;

namespace Game.Items.Extinguisher
{
    public class ExtinguisherItem : MonoBehaviour
    {
        [SerializeField] private Transform _nozzle;
        [SerializeField] private ParticleSystem _spray;
        [SerializeField] private AudioSource _sprayAudio;
        [SerializeField] private float _capacitySeconds = 12f;

        private float _charge = 1f;

        public Transform Nozzle => _nozzle;
        public float Charge => _charge;
        public bool IsEmpty => _charge <= 0f;

        public void Consume(float deltaTime)
        {
            _charge = Mathf.Max(0f, _charge - deltaTime / Mathf.Max(0.1f, _capacitySeconds));
        }

        public void Refill() => _charge = 1f;

        public void SetSpraying(bool isSpraying)
        {
            if (isSpraying && !_spray.isEmitting) _spray.Play(true);
            else if (!isSpraying && _spray.isEmitting) _spray.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            if (_sprayAudio != null)
            {
                if (isSpraying && !_sprayAudio.isPlaying && _sprayAudio.clip != null) _sprayAudio.Play();
                else if (!isSpraying && _sprayAudio.isPlaying) _sprayAudio.Stop();
            }
        }
    }
}
