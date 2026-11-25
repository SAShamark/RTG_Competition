using System;
using UnityEngine;

namespace Gameplay.Entities.Interactive.Items
{
    [Serializable]
    public sealed class WaterJetView
    {
        private const float MinLength = 0.05f;
        private const int QuadIndices = 6;

        [SerializeField] private MeshFilter _filter;
        [SerializeField] private MeshRenderer _renderer;
        [SerializeField, Min(3)] private int _sides = 8;
        [SerializeField, Min(2)] private int _rings = 40;
        [SerializeField] private float _startRadius = 0.03f;
        [SerializeField] private float _endRadius = 0.16f;
        [SerializeField] private float _spreadDistance = 18f;
        [SerializeField] private float _uvLength = 1.5f;
        [SerializeField] private float _endFade = 1.2f;
        [SerializeField] private float _coreLength = 6f;
        [SerializeField] private float _breakupLength = 18f;

        public float BreakupLength => _breakupLength;

        private Mesh _mesh;
        private Vector3[] _vertices;
        private Vector3[] _normals;
        private Color32[] _colors;
        private Vector2[] _uvs;

        public void Init()
        {
            int ringSize = _sides + 1;
            int count = _rings * ringSize;
            _vertices = new Vector3[count];
            _normals = new Vector3[count];
            _colors = new Color32[count];
            _uvs = new Vector2[count];

            var triangles = new int[(_rings - 1) * _sides * QuadIndices];
            int index = 0;
            for (int ring = 0; ring < _rings - 1; ring++)
            {
                for (int side = 0; side < _sides; side++)
                {
                    int near = ring * ringSize + side;
                    int far = near + ringSize;
                    triangles[index++] = near;
                    triangles[index++] = far;
                    triangles[index++] = near + 1;
                    triangles[index++] = near + 1;
                    triangles[index++] = far;
                    triangles[index++] = far + 1;
                }
            }

            _mesh = new Mesh { name = "WaterJet" };
            _mesh.MarkDynamic();
            _mesh.SetVertices(_vertices);
            _mesh.SetTriangles(triangles, 0);
            _filter.sharedMesh = _mesh;
            _renderer.enabled = false;
        }

        public void Dispose() => UnityEngine.Object.Destroy(_mesh);

        public void Hide() => _renderer.enabled = false;

        public void Show(WaterJetPath path, float cut, float reach, bool isLanded)
        {
            isLanded &= reach <= _breakupLength;
            reach = Mathf.Min(reach, _breakupLength);
            if (reach - cut < MinLength)
            {
                Hide();
                return;
            }

            _renderer.enabled = true;
            Transform space = _filter.transform;
            int ringSize = _sides + 1;
            for (int ring = 0; ring < _rings; ring++)
            {
                float distance = Mathf.Lerp(cut, reach, ring / (float)(_rings - 1));
                path.Sample(distance, out Vector3 point, out Vector3 velocity);
                Vector3 side = path.Side;
                Vector3 up = Vector3.Cross(velocity.normalized, side);
                float radius = Mathf.Lerp(_startRadius, _endRadius, Mathf.Clamp01(distance / _spreadDistance));
                float tailFade = cut > 0f ? Mathf.Clamp01((distance - cut) / _endFade) : 1f;
                float headFade = isLanded ? 1f : Mathf.Clamp01((reach - distance) / _endFade);
                float solid = 1f - Mathf.InverseLerp(_coreLength, _breakupLength, distance);
                var color = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, (byte)(tailFade * headFade * solid * byte.MaxValue));

                for (int corner = 0; corner < ringSize; corner++)
                {
                    float turn = corner / (float)_sides;
                    float angle = turn * Mathf.PI * 2f;
                    Vector3 normal = side * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                    int vertex = ring * ringSize + corner;
                    _vertices[vertex] = space.InverseTransformPoint(point + normal * radius);
                    _normals[vertex] = space.InverseTransformDirection(normal);
                    _colors[vertex] = color;
                    _uvs[vertex] = new Vector2(distance / _uvLength, turn);
                }
            }

            _mesh.SetVertices(_vertices);
            _mesh.SetNormals(_normals);
            _mesh.SetColors(_colors);
            _mesh.SetUVs(0, _uvs);
            _mesh.RecalculateBounds();
        }
    }
}
