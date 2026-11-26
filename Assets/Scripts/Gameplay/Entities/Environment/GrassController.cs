using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gameplay.Entities.Environment
{
    [Serializable]
    public sealed class GrassController
    {
        private const int MaxInstancesPerDraw = 1023;
        private const float FullTurn = 360f;

        private static readonly int FadeDistanceId = Shader.PropertyToID("_FadeDistance");
        private static readonly int PusherId = Shader.PropertyToID("_GrassPusher");

        [Header("Look")]
        [SerializeField] private Renderer[] _lawns = Array.Empty<Renderer>();
        [SerializeField] private Mesh _tuftMesh;
        [SerializeField] private Material _material;
        [SerializeField, Min(0f)] private float _tuftsPerSquareMetre = 22f;
        [SerializeField] private Vector2 _tuftScale = new(0.8f, 1.25f);
        [SerializeField, Min(0f)] private float _tuftHeight = 0.3f;
        [SerializeField, Min(0f)] private float _edgeInset = 0.12f;
        [SerializeField] private int _seed = 1907;

        [Header("Obstacles")]
        [SerializeField] private LayerMask _obstacleMask = ~0;
        [SerializeField, Min(0f)] private float _obstacleProbeHeight = 0.2f;
        [SerializeField, Min(0f)] private float _obstacleProbeRadius = 0.12f;

        [Header("Performance")]
        [SerializeField, Min(1f)] private float _chunkSize = 6f;
        [SerializeField, Min(1f)] private float _drawDistance = 38f;

        [Header("Player")]
        [SerializeField, Min(0f)] private float _pushRadius = 0.75f;

        private readonly List<Chunk> _chunks = new();
        private float _sqrDrawDistance;

        public void Init()
        {
            _sqrDrawDistance = _drawDistance * _drawDistance;
            var properties = new MaterialPropertyBlock();
            properties.SetFloat(FadeDistanceId, _drawDistance);
            var random = new System.Random(_seed);
            var matrices = new List<Matrix4x4>(MaxInstancesPerDraw);

            foreach (Renderer lawn in _lawns)
            {
                Plant(lawn, random, matrices, properties);
            }
        }

        public void Tick(Vector3 viewer, Vector3 pusher)
        {
            Shader.SetGlobalVector(PusherId, new Vector4(pusher.x, pusher.y, pusher.z, _pushRadius));
            foreach (Chunk chunk in _chunks)
            {
                if (chunk.Params.worldBounds.SqrDistance(viewer) < _sqrDrawDistance)
                {
                    Graphics.RenderMeshInstanced(chunk.Params, _tuftMesh, 0, chunk.Matrices);
                }
            }
        }

        private void Plant(Renderer lawn, System.Random random, List<Matrix4x4> matrices, MaterialPropertyBlock properties)
        {
            Transform root = lawn.transform;
            Bounds local = lawn.localBounds;
            Vector3 scale = root.lossyScale;
            float width = local.size.x * Mathf.Abs(scale.x) - _edgeInset * 2f;
            float depth = local.size.z * Mathf.Abs(scale.z) - _edgeInset * 2f;
            if (width <= 0f || depth <= 0f)
            {
                return;
            }

            int columns = Mathf.CeilToInt(width / _chunkSize);
            int rows = Mathf.CeilToInt(depth / _chunkSize);
            float cellWidth = width / columns;
            float cellDepth = depth / rows;
            int perCell = Mathf.RoundToInt(cellWidth * cellDepth * _tuftsPerSquareMetre);
            Vector3 corner = root.TransformPoint(new Vector3(local.min.x, local.max.y, local.min.z))
                + (root.right * Mathf.Sign(scale.x) + root.forward * Mathf.Sign(scale.z)) * _edgeInset;
            Vector3 right = root.right * Mathf.Sign(scale.x);
            Vector3 forward = root.forward * Mathf.Sign(scale.z);
            Vector3 up = root.up;
            Collider ground = lawn.GetComponent<Collider>();

            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    Vector3 cellCorner = corner + right * (column * cellWidth) + forward * (row * cellDepth);
                    for (int i = 0; i < perCell; i++)
                    {
                        Vector3 position = cellCorner
                            + right * ((float)random.NextDouble() * cellWidth)
                            + forward * ((float)random.NextDouble() * cellDepth);
                        float size = Mathf.Lerp(_tuftScale.x, _tuftScale.y, (float)random.NextDouble());
                        Quaternion rotation = Quaternion.AngleAxis((float)random.NextDouble() * FullTurn, up) * root.rotation;
                        if (IsCovered(position, up, ground))
                        {
                            continue;
                        }

                        matrices.Add(Matrix4x4.TRS(position, rotation, Vector3.one * size));
                        if (matrices.Count == MaxInstancesPerDraw)
                        {
                            Flush(matrices, properties, lawn.gameObject.layer);
                        }
                    }

                    Flush(matrices, properties, lawn.gameObject.layer);
                }
            }
        }

        private bool IsCovered(Vector3 position, Vector3 up, Collider ground)
        {
            Vector3 probe = position + up * _obstacleProbeHeight;
            if (Physics.CheckSphere(probe, _obstacleProbeRadius, _obstacleMask, QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            return Physics.Raycast(probe, -up, out RaycastHit hit, _obstacleProbeHeight * 2f, _obstacleMask, QueryTriggerInteraction.Ignore)
                && hit.collider != ground;
        }

        private void Flush(List<Matrix4x4> matrices, MaterialPropertyBlock properties, int layer)
        {
            if (matrices.Count == 0)
            {
                return;
            }

            var bounds = new Bounds(matrices[0].GetPosition(), Vector3.zero);
            foreach (Matrix4x4 matrix in matrices)
            {
                bounds.Encapsulate(matrix.GetPosition());
            }

            bounds.Expand(_tuftHeight * _tuftScale.y * 2f);
            _chunks.Add(new Chunk
            {
                Matrices = matrices.ToArray(),
                Params = new RenderParams(_material)
                {
                    worldBounds = bounds,
                    layer = layer,
                    matProps = properties,
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = true,
                },
            });
            matrices.Clear();
        }

        private struct Chunk
        {
            public Matrix4x4[] Matrices;
            public RenderParams Params;
        }
    }
}
