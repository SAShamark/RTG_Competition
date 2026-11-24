using Services;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Gameplay.Entities.Interactive.Items
{
    public sealed class PowderSplat : MonoBehaviour
    {
        private const int AtlasCells = 2;
        private const float FullTurn = 360f;

        [SerializeField] private DecalProjector _projector;
        [SerializeField] private Vector2 _sizeRange = new(0.28f, 0.42f);
        [SerializeField] private float _depth = 0.04f;
        [SerializeField] private float _fullGrowth = 1.25f;
        [SerializeField, Range(0f, 1f)] private float _startDensity = 0.25f;
        [SerializeField, Range(0f, 1f)] private float _minFade = 0.35f;
        [SerializeField] private float _fadeOutDuration = 0.7f;

        private Collider _surface;
        private Transform _anchor;
        private Vector3 _localPoint;
        private Quaternion _localRotation;
        private float _baseSize;
        private float _density;
        private float _presence;

        public bool IsFree { get; private set; } = true;
        public Collider Surface => _surface;
        public Vector3 Point => transform.position;
        public Vector3 Normal => -transform.forward;

        public void Place(Collider surface, Vector3 point, Vector3 normal)
        {
            _surface = surface;
            _anchor = surface.transform;
            Quaternion rotation = Quaternion.LookRotation(-normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, FullTurn));
            _localPoint = _anchor.InverseTransformPoint(point);
            _localRotation = Quaternion.Inverse(_anchor.rotation) * rotation;
            _baseSize = Random.Range(_sizeRange.x, _sizeRange.y);
            _density = _startDensity;
            _presence = 1f;

            float cell = 1f / AtlasCells;
            _projector.uvScale = new Vector2(cell, cell);
            _projector.uvBias = new Vector2(Random.Range(0, AtlasCells) * cell, Random.Range(0, AtlasCells) * cell);
            _projector.pivot = Vector3.zero;

            IsFree = false;
            gameObject.SetActive(true);
            Follow();
            Apply();
        }

        public void Add(float amount)
        {
            _density = Mathf.Min(1f, _density + amount);
            Apply();
        }

        private void Apply()
        {
            float size = _baseSize * Mathf.Lerp(1f, _fullGrowth, _density);
            _projector.size = new Vector3(size, size, _depth);
            _projector.fadeFactor = Mathf.Lerp(_minFade, 1f, Easing.Smooth(_density)) * _presence;
        }

        private void Follow()
        {
            transform.SetPositionAndRotation(_anchor.TransformPoint(_localPoint), _anchor.rotation * _localRotation);
        }

        // The splat rides on its surface; once the surface is gone it thins out and returns to the pool.
        private void LateUpdate()
        {
            if (_surface != null && _surface.enabled && _surface.gameObject.activeInHierarchy)
            {
                Follow();
                return;
            }

            _presence -= Time.deltaTime / _fadeOutDuration;
            if (_presence > 0f)
            {
                Apply();
                return;
            }

            IsFree = true;
            gameObject.SetActive(false);
        }
    }
}
