using Gameplay.Entities.Characters;
using Services;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Gameplay.Entities.Interactive.Items
{
    public sealed class SurfaceSplat : MonoBehaviour
    {
        private const int AtlasCells = 2;
        private const float FullTurn = 360f;
        private const float Half = 0.5f;

        [SerializeField] private DecalProjector _projector;
        [SerializeField] private Vector2 _sizeRange = new(0.28f, 0.42f);
        [SerializeField] private float _depth = 0.04f;
        [SerializeField] private float _proxyInset = 0.12f;
        [SerializeField] private float _fullGrowth = 1.25f;
        [SerializeField, Range(0f, 1f)] private float _startDensity = 0.25f;
        [SerializeField, Range(0f, 1f)] private float _minFade = 0.35f;
        [SerializeField] private float _fadeOutDuration = 0.7f;
        [SerializeField, Min(0f)] private float _dryDuration;

        private Collider _surface;
        private Transform _anchor;
        private Vector3 _localPoint;
        private Quaternion _localRotation;
        private float _baseSize;
        private float _density;
        private float _presence;
        private float _boxDepth;

        public bool IsFree { get; private set; } = true;
        public Collider Surface => _surface;
        public Vector3 Point => transform.position;
        public Vector3 Normal => -transform.forward;

        // A flat splat cannot ride a body that bends under it, and a fire is not a surface at all.
        public static bool CanStickTo(Collider surface)
        {
            if (surface is CharacterController || surface.GetComponentInParent<CharacterControl>() != null || surface.GetComponentInParent<FireSource>() != null)
            {
                return false;
            }

            Animator animator = surface.GetComponentInParent<Animator>();
            return animator == null || animator.GetComponentInChildren<SkinnedMeshRenderer>() == null;
        }

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
            // A collider with no visible mesh of its own is a stand-in (a door blocker, a hidden original door): what the player sees can sit
            // several centimetres behind it, so the projection reaches further in to land on it.
            float outside = _depth * Half;
            bool hasVisibleMesh = surface.TryGetComponent(out Renderer own) && own.enabled;
            float inside = hasVisibleMesh ? outside : _proxyInset;
            _boxDepth = outside + inside;
            _projector.pivot = new Vector3(0f, 0f, (inside - outside) * Half);

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
            _projector.size = new Vector3(size, size, _boxDepth);
            _projector.fadeFactor = Mathf.Lerp(_minFade, 1f, Easing.Smooth(_density)) * _presence;
        }

        private void Follow()
        {
            transform.SetPositionAndRotation(_anchor.TransformPoint(_localPoint), _anchor.rotation * _localRotation);
        }

        // The splat rides on its surface; once the surface is gone, or the splat has dried out, it thins away and returns to the pool.
        private void LateUpdate()
        {
            bool hasSurface = _surface != null && _surface.enabled && _surface.gameObject.activeInHierarchy;
            if (hasSurface)
            {
                Follow();
            }
            else
            {
                _presence -= Time.deltaTime / _fadeOutDuration;
            }

            if (_dryDuration > 0f)
            {
                _density -= Time.deltaTime / _dryDuration;
            }
            else if (hasSurface)
            {
                return;
            }

            if (_presence > 0f && _density > 0f)
            {
                Apply();
                return;
            }

            IsFree = true;
            gameObject.SetActive(false);
        }
    }
}
