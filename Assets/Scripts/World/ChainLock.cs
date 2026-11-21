using UnityEngine;

namespace Game.World
{
    public class ChainLock : MonoBehaviour
    {
        [SerializeField] private float _kick = 1.6f;
        [SerializeField] private float _despawnDelay = 8f;

        private bool _isBroken;

        public void Break(Vector3 pushDirection)
        {
            if (_isBroken)
            {
                return;
            }

            _isBroken = true;
            foreach (Transform piece in GetComponentsInChildren<Transform>())
            {
                if (piece == transform || piece.GetComponent<Renderer>() == null)
                {
                    continue;
                }

                if (piece.GetComponent<Collider>() == null)
                {
                    piece.gameObject.AddComponent<BoxCollider>();
                }

                Rigidbody body = piece.gameObject.AddComponent<Rigidbody>();
                body.mass = 0.3f;
                body.AddForce((pushDirection + Random.insideUnitSphere * 0.5f) * _kick, ForceMode.Impulse);
                body.AddTorque(Random.insideUnitSphere * 0.4f, ForceMode.Impulse);
            }

            Destroy(gameObject, _despawnDelay);
        }
    }
}
