using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Entities.Interactive.Items
{
    public sealed class SplatPool
    {
        private readonly List<SurfaceSplat> _splats = new();
        private readonly SurfaceSplat _prefab;
        private readonly int _capacity;
        private readonly float _mergeRadius;
        private readonly float _mergeNormal;
        private readonly float _jitter;
        private int _nextRecycle;

        public SplatPool(SurfaceSplat prefab, int capacity, float mergeRadius, float mergeNormal, float jitter)
        {
            _prefab = prefab;
            _capacity = capacity;
            _mergeRadius = mergeRadius;
            _mergeNormal = mergeNormal;
            _jitter = jitter;
        }

        public void Deposit(Collider surface, Vector3 point, Vector3 normal, float amount)
        {
            SurfaceSplat nearest = null;
            float nearestSqr = _mergeRadius * _mergeRadius;
            foreach (SurfaceSplat splat in _splats)
            {
                if (splat.IsFree || splat.Surface != surface || Vector3.Dot(splat.Normal, normal) < _mergeNormal)
                {
                    continue;
                }

                float distanceSqr = (splat.Point - point).sqrMagnitude;
                if (distanceSqr < nearestSqr)
                {
                    nearestSqr = distanceSqr;
                    nearest = splat;
                }
            }

            if (nearest != null)
            {
                nearest.Add(amount);
                return;
            }

            Vector3 offset = Vector3.ProjectOnPlane(Random.insideUnitSphere, normal) * _jitter;
            Take().Place(surface, point + offset, normal);
        }

        private SurfaceSplat Take()
        {
            foreach (SurfaceSplat splat in _splats)
            {
                if (splat.IsFree)
                {
                    return splat;
                }
            }

            if (_splats.Count < _capacity)
            {
                SurfaceSplat created = Object.Instantiate(_prefab);
                _splats.Add(created);
                return created;
            }

            _nextRecycle = (_nextRecycle + 1) % _splats.Count;
            return _splats[_nextRecycle];
        }
    }
}
