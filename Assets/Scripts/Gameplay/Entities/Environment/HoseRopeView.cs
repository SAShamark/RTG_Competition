using System;
using Services;
using UnityEngine;

namespace Gameplay.Entities.Environment
{
    [Serializable]
    public sealed class HoseRopeView
    {
        private const int QuadIndices = 6;
        private const float MinSideSqr = 0.0001f;
        private const int ExtraPoints = 2;

        [SerializeField] private MeshFilter _filter;
        [SerializeField, Min(3)] private int _sides = 6;
        [SerializeField, Min(1)] private int _subdivisions = 2;
        [SerializeField] private float _uvLength = 0.5f;

        [Header("Empty and charged")]
        [SerializeField] private float _flatWidth = 1.35f;
        [SerializeField] private float _flatHeight = 0.3f;
        [SerializeField] private float _chargeBlend = 0.6f;
        [SerializeField] private float _frontBulge = 0.25f;
        [SerializeField] private Vector2 _steepTangent = new(0.5f, 0.9f);

        [SerializeField] private float _glideSharpness = 12f;

        [Header("Ends")]
        [SerializeField] private float _endStub = 0.12f;
        [SerializeField] private float _endRound = 0.3f;

        private MeshRenderer _renderer;
        private Vector3[] _shown;
        private int _shownCount;
        private float[] _along;

        private Mesh _mesh;
        private Vector3[] _path;
        private Vector3[] _centres;
        private Vector3[] _vertices;
        private Vector3[] _normals;
        private Vector2[] _uvs;
        private int _rings;

        public void Init(HoseRope rope)
        {
            _path = new Vector3[rope.Capacity + ExtraPoints + 1];
            _shown = new Vector3[rope.Capacity];
            _rings = (rope.Capacity + ExtraPoints) * _subdivisions + 1;
            int ringSize = _sides + 1;
            _centres = new Vector3[_rings];
            _along = new float[_rings];
            _renderer = _filter.GetComponent<MeshRenderer>();
            _vertices = new Vector3[_rings * ringSize];
            _normals = new Vector3[_vertices.Length];
            _uvs = new Vector2[_vertices.Length];

            var triangles = new int[(_rings - 1) * _sides * QuadIndices];
            int index = 0;
            for (int ring = 0; ring < _rings - 1; ring++)
            {
                for (int side = 0; side < _sides; side++)
                {
                    int near = ring * ringSize + side;
                    int far = near + ringSize;
                    triangles[index++] = near;
                    triangles[index++] = near + 1;
                    triangles[index++] = far + 1;
                    triangles[index++] = near;
                    triangles[index++] = far + 1;
                    triangles[index++] = far;
                }
            }

            _mesh = new Mesh { name = "HoseRope" };
            _mesh.MarkDynamic();
            _mesh.SetVertices(_vertices);
            _mesh.SetTriangles(triangles, 0);
            _filter.sharedMesh = _mesh;
        }

        public void Dispose() => UnityEngine.Object.Destroy(_mesh);

        public void SetVisible(bool isVisible) => _renderer.enabled = isVisible;

        public void Snap() => _shownCount = 0;

        // Both ends get a short straight stub along their fitting, so the hose leaves the nozzle and the truck squarely.
        public void Show(HoseRope rope, Vector3 end, Vector3 endDirection, Vector3 sourceDirection, float chargedLength, float deltaTime)
        {
            // The drawn hose glides after the simulated one, so a point hopping round a corner reads as a quick slide, not a pop.
            float blend = Easing.Damp(_glideSharpness, deltaTime);
            for (int i = 1; i < rope.Count; i++)
            {
                _shown[i] = i < _shownCount ? Vector3.Lerp(_shown[i], rope[i], blend) : rope[i];
            }

            _shownCount = rope.Count;
            int last = rope.Count + ExtraPoints;
            _path[0] = end;
            _path[1] = end + endDirection * _endStub;
            for (int i = 1; i < rope.Count; i++)
            {
                _path[i + 1] = _shown[i];
            }

            _path[last - 1] = rope.Source + sourceDirection * _endStub;
            _path[last] = rope.Source;

            // Rings past the reel collapse onto it, so one fixed-size mesh serves any paid-out length.
            int usedRings = last * _subdivisions + 1;
            for (int ring = 0; ring < _rings; ring++)
            {
                if (ring >= usedRings)
                {
                    _centres[ring] = _path[last];
                    continue;
                }

                int segment = Mathf.Min(ring / _subdivisions, last - 1);
                float share = (ring - segment * _subdivisions) / (float)_subdivisions;
                _centres[ring] = Spline(
                    _path[Mathf.Max(segment - 1, 0)],
                    _path[segment],
                    _path[segment + 1],
                    _path[Mathf.Min(segment + 2, last)],
                    share);
            }

            _along[0] = 0f;
            for (int ring = 1; ring < _rings; ring++)
            {
                _along[ring] = _along[ring - 1] + Vector3.Distance(_centres[ring], _centres[ring - 1]);
            }

            Transform space = _filter.transform;
            int ringSize = _sides + 1;
            float radius = rope.Radius;
            float total = _along[_rings - 1];
            Vector3 side = space.right;
            Vector3 tangent = space.forward;
            for (int ring = 0; ring < _rings; ring++)
            {
                Vector3 centre = _centres[ring];
                Vector3 along = _centres[Mathf.Min(ring + 1, _rings - 1)] - _centres[Mathf.Max(ring - 1, 0)];
                if (along.sqrMagnitude > MinSideSqr)
                {
                    tangent = along.normalized;
                }

                // An empty hose lies flat, so its side leans towards level; the steeper the run, the more the previous side is
                // carried over instead (parallel transport), which keeps the tube from twisting where world up is no use.
                Vector3 carried = Vector3.ProjectOnPlane(side, tangent);
                Vector3 level = Vector3.Cross(Vector3.up, tangent);
                level *= Mathf.Sign(Vector3.Dot(level, side));
                float steepness = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_steepTangent.x, _steepTangent.y, Mathf.Abs(tangent.y)));
                Vector3 across = Vector3.Lerp(level.normalized, carried.normalized, steepness);
                if (across.sqrMagnitude > MinSideSqr)
                {
                    side = across.normalized;
                }

                Vector3 up = Vector3.Cross(tangent, side);
                float fromEnd = Mathf.Min(_along[ring], total - _along[ring]);
                float fitting = 1f - Mathf.SmoothStep(0f, 1f, fromEnd / _endRound);
                float charge = Mathf.Clamp01((chargedLength - (total - _along[ring])) / _chargeBlend);
                float bulge = Mathf.Sin(charge * Mathf.PI) * _frontBulge * (1f - fitting);
                float round = Mathf.Max(charge, fitting);
                float width = radius * (Mathf.Lerp(_flatWidth, 1f, round) + bulge);
                float height = radius * (Mathf.Lerp(_flatHeight, 1f, round) + bulge);
                centre += Vector3.down * ((radius - Mathf.Min(radius, height)) * (1f - fitting));

                for (int corner = 0; corner < ringSize; corner++)
                {
                    float turn = corner / (float)_sides;
                    float angle = turn * Mathf.PI * 2f;
                    Vector3 offset = side * (Mathf.Cos(angle) * width) + up * (Mathf.Sin(angle) * height);
                    Vector3 normal = (side * (Mathf.Cos(angle) * height) + up * (Mathf.Sin(angle) * width)).normalized;
                    int vertex = ring * ringSize + corner;
                    _vertices[vertex] = space.InverseTransformPoint(centre + offset);
                    _normals[vertex] = space.InverseTransformDirection(normal);
                    _uvs[vertex] = new Vector2(_along[ring] / _uvLength, turn);
                }
            }

            _mesh.SetVertices(_vertices);
            _mesh.SetNormals(_normals);
            _mesh.SetUVs(0, _uvs);
            _mesh.RecalculateBounds();
        }

        // Catmull-Rom through p1..p2: ½·(2p₁ + (p₂−p₀)t + (2p₀−5p₁+4p₂−p₃)t² + (−p₀+3p₁−3p₂+p₃)t³)
        private static Vector3 Spline(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
        }
    }
}
