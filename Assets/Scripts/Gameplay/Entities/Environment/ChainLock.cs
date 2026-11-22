using Services;
using Services.Audio;
using Unity.Cinemachine;
using UnityEngine;

namespace Gameplay.Entities.Environment
{
    public sealed class ChainLock : MonoBehaviour
    {
        [SerializeField] private float _kick = 1.2f;
        [SerializeField] private float _spread = 0.5f;
        [SerializeField] private Rigidbody[] _pieces;
        [SerializeField] private float _spin = 8f;
        [SerializeField] private Rigidbody _shackle;
        [SerializeField] private Vector3 _shackleKick = new(0.5f, 3f, -0.8f);
        [SerializeField] private float _shackleSpin = 12f;
        [SerializeField] private ParticleSystem _sparks;
        [SerializeField] private CinemachineImpulseSource _impulse;
        [SerializeField] private string _debrisLayer = "Debris";

        private bool _isBroken;

        public void Break(Vector3 pushDirection)
        {
            if (_isBroken)
            {
                return;
            }

            _isBroken = true;
            EffectPlayback.Restart(_sparks);
            AudioService.Instance.PlayAt(SoundType.LockBreak, _sparks.transform.position);
            _impulse.GenerateImpulse();

            int layer = LayerMask.NameToLayer(_debrisLayer);
            foreach (Rigidbody body in _pieces)
            {
                Release(body, layer);
                body.AddForce((pushDirection + Random.insideUnitSphere * _spread) * _kick, ForceMode.VelocityChange);
                body.AddTorque(Random.insideUnitSphere * _spin, ForceMode.VelocityChange);
            }

            Release(_shackle, layer);
            _shackle.AddForce(transform.TransformDirection(_shackleKick), ForceMode.VelocityChange);
            _shackle.AddTorque(Random.onUnitSphere * _shackleSpin, ForceMode.VelocityChange);
        }

        private static void Release(Rigidbody body, int layer)
        {
            body.gameObject.layer = layer;
            body.GetComponent<Collider>().enabled = true;
            body.isKinematic = false;
        }
    }
}
