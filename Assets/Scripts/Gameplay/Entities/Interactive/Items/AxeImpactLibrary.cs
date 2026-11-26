using System;
using System.Collections.Generic;
using Gameplay.Entities.Characters;
using Services.Audio;
using UnityEngine;

namespace Gameplay.Entities.Interactive.Items
{
    [CreateAssetMenu(menuName = "Flame & Blame/Axe Impact Library")]
    public sealed class AxeImpactLibrary : ScriptableObject
    {
        [Serializable]
        public sealed class Impact
        {
            [SerializeField] private SurfaceType _surface;
            [SerializeField] private SoundType _sound = SoundType.AxeHitHard;
            [SerializeField] private float _pitch = 1f;
            [SerializeField] private GameObject _effect;
            [SerializeField] private float _effectLifetime = 2f;
            [SerializeField] private GameObject _mark;
            [SerializeField] private GameObject _edgeMark;
            [SerializeField] private float _cameraImpulse = 0.4f;
            [SerializeField] private float _hitStop = 0.08f;
            [SerializeField] private bool _bounces = true;
            [SerializeField] private bool _blocksDamage;
            [SerializeField] private float _pushForce = 4f;
            [SerializeField, Min(0)] private int _debrisCount = 12;
            [SerializeField] private Vector2 _debrisSize = new(0.012f, 0.03f);

            public SurfaceType Surface => _surface;
            public SoundType Sound => _sound;
            public float Pitch => _pitch;
            public GameObject Effect => _effect;
            public float EffectLifetime => _effectLifetime;
            public GameObject Mark => _mark;
            public GameObject EdgeMark => _edgeMark;
            public float CameraImpulse => _cameraImpulse;
            public float HitStop => _hitStop;
            public bool Bounces => _bounces;
            public bool BlocksDamage => _blocksDamage;
            public float PushForce => _pushForce;
            public int DebrisCount => _debrisCount;
            public Vector2 DebrisSize => _debrisSize;
        }

        [Serializable]
        private sealed class Keywords
        {
            [SerializeField] private SurfaceType _surface;
            [SerializeField] private string[] _words = Array.Empty<string>();

            public SurfaceType Surface => _surface;
            public string[] Words => _words;
        }

        [SerializeField] private SurfaceType _fallback = SurfaceType.Concrete;
        [SerializeField] private Impact[] _impacts = Array.Empty<Impact>();
        [SerializeField] private Keywords[] _keywords = Array.Empty<Keywords>();

        private readonly Dictionary<Collider, SurfaceType> _cache = new();
        private Dictionary<SurfaceType, Impact> _bySurface;

        public Impact Get(SurfaceType surface)
        {
            _bySurface ??= BuildLookup();
            return _bySurface.TryGetValue(surface, out Impact impact) ? impact : _bySurface[_fallback];
        }

        public SurfaceType Resolve(Collider collider)
        {
            if (!_cache.TryGetValue(collider, out SurfaceType surface))
            {
                surface = Detect(collider);
                _cache[collider] = surface;
            }

            return surface;
        }

        private SurfaceType Detect(Collider collider)
        {
            SurfaceMaterial marked = collider.GetComponentInParent<SurfaceMaterial>();
            if (marked != null)
            {
                return marked.Type;
            }

            if (collider is CharacterController || collider.GetComponentInParent<CharacterControl>() != null)
            {
                return SurfaceType.Flesh;
            }

            if (collider.GetComponentInParent<IChoppable>() != null || collider.GetComponentInChildren<IChoppable>() != null)
            {
                return SurfaceType.Wood;
            }

            Renderer renderer = collider.GetComponent<Renderer>();
            string material = renderer != null && renderer.sharedMaterial != null ? renderer.sharedMaterial.name : string.Empty;
            foreach (Keywords group in _keywords)
            {
                foreach (string word in group.Words)
                {
                    if (material.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0
                        || collider.name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return group.Surface;
                    }
                }
            }

            return _fallback;
        }

        private Dictionary<SurfaceType, Impact> BuildLookup()
        {
            var lookup = new Dictionary<SurfaceType, Impact>();
            foreach (Impact impact in _impacts)
            {
                lookup[impact.Surface] = impact;
            }

            return lookup;
        }
    }
}
