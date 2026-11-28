using System;
using System.Collections.Generic;
using Gameplay.Effects.Puddle;
using UnityEngine;

namespace Gameplay.Entities.Interactive.Items
{
    [Serializable]
    public sealed class WaterTraceLayer
    {
        private const float Half = 0.5f;
        private const float RunoffLift = 0.05f;

        [Header("Puddles")]
        [SerializeField] private PuddleControl _puddlePrefab;
        [SerializeField, Min(1f)] private float _tileSize = 12f;
        [SerializeField, Min(1)] private int _maxTiles = 10;
        [SerializeField] private float _edgeMargin = 0.5f;
        [SerializeField, Range(0f, 1f)] private float _minFloorNormal = 0.95f;
        [SerializeField] private LayerMask _floorMask = ~0;
        [SerializeField, Min(0f)] private float _runoffHeight = 3f;
        [SerializeField, Range(0f, 1f)] private float _runoffShare = 0.5f;

        [Header("Stains")]
        [SerializeField] private SurfaceSplat _stainPrefab;
        [SerializeField, Min(1)] private int _maxStains = 48;
        [SerializeField] private float _mergeRadius = 0.3f;
        [SerializeField, Range(0f, 1f)] private float _mergeNormal = 0.8f;
        [SerializeField] private float _jitter = 0.08f;
        [SerializeField, Min(0f)] private float _soakPerLitre = 0.25f;

        private readonly Dictionary<Collider, bool> _holds = new();
        private readonly List<PuddleControl> _tiles = new();
        private SplatPool _pool;

        // Level ground gets a real puddle, laid out in tiles wherever water lands. Anything else gets a wet stain, and what runs down it
        // feeds the puddle on the ground below.
        public void Wet(Collider surface, Vector3 point, Vector3 normal, float litres)
        {
            bool isFloor = normal.y >= _minFloorNormal;
            if (isFloor && IsGround(surface) && Flood(point, litres))
            {
                return;
            }

            if (!isFloor && Physics.Raycast(point + normal * RunoffLift, Vector3.down, out RaycastHit below, _runoffHeight, _floorMask, QueryTriggerInteraction.Ignore)
                && below.normal.y >= _minFloorNormal && IsGround(below.collider))
            {
                Flood(below.point, litres * _runoffShare);
            }

            if (Holds(surface))
            {
                _pool ??= new SplatPool(_stainPrefab, _maxStains, _mergeRadius, _mergeNormal, _jitter);
                _pool.Deposit(surface, point, normal, litres * _soakPerLitre);
            }
        }

        private bool IsGround(Collider surface) => surface.attachedRigidbody == null && Holds(surface);

        private bool Holds(Collider surface)
        {
            if (!_holds.TryGetValue(surface, out bool holds))
            {
                holds = SurfaceSplat.CanStickTo(surface);
                _holds.Add(surface, holds);
            }

            return holds;
        }

        // The tiles next to a splash near an edge are laid too, so the mound is never cut off at a tile border.
        private bool Flood(Vector3 point, float litres)
        {
            LayTile(point);
            LayTile(point + Vector3.right * _edgeMargin);
            LayTile(point + Vector3.left * _edgeMargin);
            LayTile(point + Vector3.forward * _edgeMargin);
            LayTile(point + Vector3.back * _edgeMargin);
            return PuddleControl.PourAll(point, litres);
        }

        private void LayTile(Vector3 point)
        {
            if (PuddleControl.IsCovered(point) || !TryTakeTile(out PuddleControl tile))
            {
                return;
            }

            var centre = new Vector3(
                (Mathf.Floor(point.x / _tileSize) + Half) * _tileSize,
                point.y,
                (Mathf.Floor(point.z / _tileSize) + Half) * _tileSize);
            tile.Place(centre, _tileSize);
        }

        private bool TryTakeTile(out PuddleControl tile)
        {
            if (_tiles.Count < _maxTiles)
            {
                tile = UnityEngine.Object.Instantiate(_puddlePrefab);
                _tiles.Add(tile);
                return true;
            }

            foreach (PuddleControl used in _tiles)
            {
                if (used.IsDry)
                {
                    tile = used;
                    return true;
                }
            }

            tile = null;
            return false;
        }
    }
}
