using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Entities.Interactive.Items
{
    [Serializable]
    public sealed class PowderSnowLayer
    {
        [Serializable]
        private struct SurfaceSettings
        {
            public float MaxDepth;
            public float DepositRate;
            public float BrushRadius;
            public float LevelStep;
        }

        private enum SurfaceKind
        {
            Scenery,
            Solid,
            Glass,
            Loose,
            Ignored,
        }

        private const int NormalBuckets = 8;

        [SerializeField] private PowderSnowChunk _chunkPrefab;
        [SerializeField] private LayerMask _surfaceMask = ~0;
        [SerializeField] private float _cellSize = 0.06f;
        [SerializeField, Min(1)] private int _chunkCells = 32;
        [SerializeField] private float _sink = 0.004f;
        [SerializeField, Range(0f, 1f)] private float _minFloorNormal = 0.7f;

        [Header("Growth")]
        [SerializeField] private SurfaceSettings _floor = new() { MaxDepth = 0.08f, DepositRate = 0.035f, BrushRadius = 0.55f, LevelStep = 0.5f };
        [SerializeField] private SurfaceSettings _wall = new() { MaxDepth = 0.018f, DepositRate = 0.012f, BrushRadius = 0.4f, LevelStep = 0.1f };
        [SerializeField, Range(0f, 1f)] private float _brushJitter = 0.35f;
        [SerializeField] private float _noiseScale = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _noiseAmount = 0.6f;

        [Header("Splats")]
        [SerializeField] private SurfaceSplat _splatPrefab;
        [SerializeField, Min(1)] private int _maxSplats = 320;
        [SerializeField] private float _splatMergeRadius = 0.14f;
        [SerializeField, Range(0f, 1f)] private float _splatMergeNormal = 0.8f;
        [SerializeField] private float _splatGrowth = 1.5f;
        [SerializeField] private float _splatJitter = 0.05f;

        private readonly Dictionary<(Vector3Int normal, int level, int u, int v), PowderSnowChunk> _chunks = new();
        private readonly Dictionary<Collider, SurfaceKind> _kinds = new();
        private SplatPool _splats;

        public bool IsFloor(Vector3 normal) => normal.y >= _minFloorNormal;

        public bool IsWall(Vector3 normal) => Mathf.Abs(normal.y) < _minFloorNormal;

        // Powder piles up as a mound on static scenery, level surfaces and glass; whatever can move gets a flat dusting that rides along.
        public void Deposit(Vector3 point, Vector3 normal, Collider surface, float amount)
        {
            SurfaceKind kind = KindOf(surface);
            if (kind == SurfaceKind.Ignored)
            {
                return;
            }

            if (kind is SurfaceKind.Glass or SurfaceKind.Scenery || (kind == SurfaceKind.Solid && IsFloor(normal)))
            {
                DepositMound(point, normal, amount);
                return;
            }

            DepositSplat(point, normal, surface, amount);
        }

        private SurfaceKind KindOf(Collider surface)
        {
            if (_kinds.TryGetValue(surface, out SurfaceKind kind))
            {
                return kind;
            }

            if (!SurfaceSplat.CanStickTo(surface))
            {
                kind = SurfaceKind.Ignored;
            }
            else if (surface.GetComponentInParent<BreakableGlass>() != null)
            {
                kind = SurfaceKind.Glass;
            }
            else if (surface.attachedRigidbody != null || surface.GetComponentInParent<Animator>() != null)
            {
                kind = SurfaceKind.Loose;
            }
            else
            {
                kind = surface.gameObject.isStatic ? SurfaceKind.Scenery : SurfaceKind.Solid;
            }

            _kinds.Add(surface, kind);
            return kind;
        }

        private void DepositSplat(Vector3 point, Vector3 normal, Collider surface, float amount)
        {
            _splats ??= new SplatPool(_splatPrefab, _maxSplats, _splatMergeRadius, _splatMergeNormal, _splatJitter);
            _splats.Deposit(surface, point, normal, amount * _splatGrowth);
        }

        private void DepositMound(Vector3 point, Vector3 normal, float amount)
        {
            SurfaceSettings settings = IsFloor(normal) ? _floor : _wall;
            var bucket = new Vector3Int(
                Mathf.RoundToInt(normal.x * NormalBuckets),
                Mathf.RoundToInt(normal.y * NormalBuckets),
                Mathf.RoundToInt(normal.z * NormalBuckets));
            Vector3 planeNormal = ((Vector3)bucket).normalized;
            Quaternion frame = Quaternion.FromToRotation(Vector3.up, planeNormal);
            Vector3 local = Quaternion.Inverse(frame) * point;
            int level = Mathf.RoundToInt(local.y / settings.LevelStep);

            Vector2 jitter = UnityEngine.Random.insideUnitCircle * (settings.BrushRadius * _brushJitter);
            float centerU = local.x + jitter.x;
            float centerV = local.z + jitter.y;
            float radius = settings.BrushRadius;
            int minU = Mathf.CeilToInt((centerU - radius) / _cellSize);
            int maxU = Mathf.FloorToInt((centerU + radius) / _cellSize);
            int minV = Mathf.CeilToInt((centerV - radius) / _cellSize);
            int maxV = Mathf.FloorToInt((centerV + radius) / _cellSize);
            float radiusSqr = radius * radius;

            for (int gv = minV; gv <= maxV; gv++)
            {
                for (int gu = minU; gu <= maxU; gu++)
                {
                    float du = gu * _cellSize - centerU;
                    float dv = gv * _cellSize - centerV;
                    float distanceSqr = du * du + dv * dv;
                    if (distanceSqr >= radiusSqr)
                    {
                        continue;
                    }

                    // Smooth (1 - r²)² falloff keeps the mound rounded; Perlin breaks the rim into drifts.
                    float falloff = 1f - distanceSqr / radiusSqr;
                    float noise = Mathf.PerlinNoise(gu * _noiseScale, gv * _noiseScale);
                    float weight = falloff * falloff * Mathf.Lerp(1f, noise * 2f, _noiseAmount);
                    AddToVertex(gu, gv, bucket, level, frame, local.y, settings.DepositRate * amount * weight, settings.MaxDepth);
                }
            }
        }

        private void AddToVertex(int gu, int gv, Vector3Int bucket, int level, Quaternion frame, float height, float depth, float maxDepth)
        {
            int chunkU = Mathf.FloorToInt((float)gu / _chunkCells);
            int chunkV = Mathf.FloorToInt((float)gv / _chunkCells);
            int localU = gu - chunkU * _chunkCells;
            int localV = gv - chunkV * _chunkCells;

            Apply(chunkU, chunkV, localU, localV);
            if (localU == 0)
            {
                Apply(chunkU - 1, chunkV, _chunkCells, localV);
            }

            if (localV == 0)
            {
                Apply(chunkU, chunkV - 1, localU, _chunkCells);
            }

            if (localU == 0 && localV == 0)
            {
                Apply(chunkU - 1, chunkV - 1, _chunkCells, _chunkCells);
            }

            void Apply(int cu, int cv, int lu, int lv)
            {
                var key = (bucket, level, cu, cv);
                if (!_chunks.TryGetValue(key, out PowderSnowChunk chunk))
                {
                    float chunkSize = _chunkCells * _cellSize;
                    Vector3 origin = frame * new Vector3(cu * chunkSize, height, cv * chunkSize);
                    chunk = UnityEngine.Object.Instantiate(_chunkPrefab, origin, frame);
                    chunk.Setup(_chunkCells, _cellSize, _sink, _surfaceMask, maxDepth);
                    _chunks.Add(key, chunk);
                }

                chunk.AddDepth(lu, lv, depth, maxDepth);
            }
        }
    }
}
