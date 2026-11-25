using UnityEngine;

namespace Gameplay.Effects.WaterStream
{
    public sealed class TubeStreamVisual : WaterStreamVisual
    {
        private const int QuadIndices = 6;
        private const float MinSideSqr = 0.0001f;

        [Header("Tube")]
        [SerializeField] private MeshFilter _filter;
        [SerializeField] private MeshRenderer _renderer;
        [SerializeField, Min(3)] private int _sides = 10;
        [SerializeField, Min(2)] private int _rings = 48;
        [SerializeField] private float _startRadius = 0.028f;
        [SerializeField] private float _endRadius = 0.13f;
        [SerializeField] private float _spreadDistance = 18f;
        [SerializeField] private float _endFade = 1.2f;
        [SerializeField] private float _coreLength = 7f;
        [SerializeField] private float _breakupLength = 20f;

        [Header("Spray past the breakup")]
        [SerializeField] private ParticleSystem _droplets;
        [SerializeField] private float _dropletRate = 260f;
        [SerializeField] private float _dropletSpread = 1.1f;
        [SerializeField] private Vector2 _dropletSize = new(0.04f, 0.2f);
        [SerializeField] private Vector2 _dropletSpeedFactor = new(0.75f, 1f);

        private Mesh _mesh;
        private Vector3[] _vertices;
        private Vector3[] _normals;
        private Color32[] _colors;
        private Vector2[] _uvs;
        private Vector3 _side = Vector3.right;
        private float _dropletDebt;

        protected override void OnInit()
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

            _mesh = new Mesh { name = "WaterStreamTube" };
            _mesh.MarkDynamic();
            _mesh.SetVertices(_vertices);
            _mesh.SetTriangles(triangles, 0);
            _filter.sharedMesh = _mesh;
            _renderer.enabled = false;
            _droplets.Play(false);
        }

        protected override void OnDispose() => Destroy(_mesh);

        protected override void OnShow(IWaterStreamCurve curve, float cut, float reach, bool isLanded, float deltaTime)
        {
            EmitDroplets(curve, cut, reach, deltaTime);
            isLanded &= reach <= _breakupLength;
            reach = Mathf.Min(reach, _breakupLength);
            if (reach <= cut)
            {
                _renderer.enabled = false;
                return;
            }

            _renderer.enabled = true;
            Transform space = _filter.transform;
            int ringSize = _sides + 1;
            for (int ring = 0; ring < _rings; ring++)
            {
                float distance = Mathf.Lerp(cut, reach, ring / (float)(_rings - 1));
                curve.Sample(distance, out Vector3 point, out Vector3 velocity);
                Vector3 forward = velocity.normalized;
                Vector3 side = Vector3.Cross(Vector3.up, forward);
                if (side.sqrMagnitude > MinSideSqr)
                {
                    _side = side.normalized;
                }

                Vector3 up = Vector3.Cross(forward, _side);
                float radius = Mathf.Lerp(_startRadius, _endRadius, Mathf.Clamp01(distance / _spreadDistance));
                float tailFade = cut > 0f ? Mathf.Clamp01((distance - cut) / _endFade) : 1f;
                float headFade = isLanded ? 1f : Mathf.Clamp01((reach - distance) / _endFade);
                float solid = 1f - Mathf.InverseLerp(_coreLength, _breakupLength, distance);
                var color = new Color32((byte)(solid * byte.MaxValue), byte.MaxValue, byte.MaxValue, (byte)(tailFade * headFade * byte.MaxValue));

                for (int corner = 0; corner < ringSize; corner++)
                {
                    float turn = corner / (float)_sides;
                    float angle = turn * Mathf.PI * 2f;
                    Vector3 normal = _side * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                    int vertex = ring * ringSize + corner;
                    _vertices[vertex] = space.InverseTransformPoint(point + normal * radius);
                    _normals[vertex] = space.InverseTransformDirection(normal);
                    _colors[vertex] = color;
                    _uvs[vertex] = new Vector2(distance, turn);
                }
            }

            _mesh.SetVertices(_vertices);
            _mesh.SetNormals(_normals);
            _mesh.SetColors(_colors);
            _mesh.SetUVs(0, _uvs);
            _mesh.RecalculateBounds();
        }

        protected override void OnHide() => _renderer.enabled = false;

        private void EmitDroplets(IWaterStreamCurve curve, float cut, float reach, float deltaTime)
        {
            var droplet = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            _dropletDebt += _dropletRate * deltaTime;
            while (_dropletDebt >= 1f)
            {
                _dropletDebt -= 1f;
                // Square root pushes the samples towards the far end, where the jet has broken up into rain.
                float distance = Mathf.Lerp(cut, reach, Mathf.Sqrt(Random.value));
                float breakup = Mathf.Clamp01(distance / _breakupLength);
                curve.Sample(distance, out Vector3 point, out Vector3 velocity);
                droplet.position = point;
                droplet.startSize = Mathf.Lerp(_dropletSize.x, _dropletSize.y, breakup * Random.value);
                velocity += Random.insideUnitSphere * (_dropletSpread * breakup);
                droplet.velocity = velocity * Random.Range(_dropletSpeedFactor.x, _dropletSpeedFactor.y);
                _droplets.Emit(droplet, 1);
            }
        }
    }
}
