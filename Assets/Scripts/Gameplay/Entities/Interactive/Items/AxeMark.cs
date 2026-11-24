using UnityEngine;

namespace Gameplay.Entities.Interactive.Items
{
    public sealed class AxeMark : MonoBehaviour
    {
        [SerializeField] private float _checkInterval = 0.2f;

        private Collider _surface;
        private float _sinceCheck;

        public void Bind(Collider surface) => _surface = surface;

        private void Update()
        {
            _sinceCheck += Time.deltaTime;
            if (_sinceCheck < _checkInterval)
            {
                return;
            }

            _sinceCheck = 0f;
            if (_surface == null || !_surface.enabled || !_surface.gameObject.activeInHierarchy)
            {
                Destroy(gameObject);
            }
        }
    }
}
