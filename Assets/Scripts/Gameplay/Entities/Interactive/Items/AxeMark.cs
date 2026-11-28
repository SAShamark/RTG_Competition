using UnityEngine;

namespace Gameplay.Entities.Interactive.Items
{
    public sealed class AxeMark : MonoBehaviour
    {
        [SerializeField] private float _checkInterval = 0.2f;

        private Collider _surface;
        private Transform _anchor;
        private Vector3 _localPoint;
        private Quaternion _localRotation;
        private float _sinceCheck;

        public void Bind(Collider surface)
        {
            _surface = surface;
            _anchor = surface.transform;
            _localPoint = _anchor.InverseTransformPoint(transform.position);
            _localRotation = Quaternion.Inverse(_anchor.rotation) * transform.rotation;
        }

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

        // The mark rides whatever it was cut into, so a shoved crate or a swinging door keeps its scars.
        private void LateUpdate()
        {
            if (_anchor != null)
            {
                transform.SetPositionAndRotation(_anchor.TransformPoint(_localPoint), _anchor.rotation * _localRotation);
            }
        }
    }
}
