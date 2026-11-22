using UnityEngine;

namespace Gameplay.Entities.Environment
{
    public sealed class ChainLock : MonoBehaviour
    {
        [SerializeField] private float _kick = 1.6f;
        [SerializeField] private float _despawnDelay = 8f;
        [SerializeField] private Rigidbody[] _pieces;
        [SerializeField] private float _spin = 0.4f;
        [SerializeField] private ParticleSystem[] _sparks;

        private bool _isBroken;

        public void Break(Vector3 pushDirection)
        {
            if (_isBroken)
            {
                return;
            }

            _isBroken = true;
            foreach (ParticleSystem sparks in _sparks)
            {
                sparks.Play(true);
            }

            foreach (Rigidbody body in _pieces)
            {
                body.GetComponent<Collider>().enabled = true;
                body.isKinematic = false;
                body.AddForce((pushDirection + Random.insideUnitSphere * 0.5f) * _kick, ForceMode.Impulse);
                body.AddTorque(Random.insideUnitSphere * _spin, ForceMode.Impulse);
            }

            Destroy(gameObject, _despawnDelay);
        }
    }
}
