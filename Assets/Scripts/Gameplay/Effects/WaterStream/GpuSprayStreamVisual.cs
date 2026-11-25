using UnityEngine;

namespace Gameplay.Effects.WaterStream
{
    public sealed class GpuSprayStreamVisual : WaterStreamVisual
    {
        // Must match CURVE_POINTS in WaterGpuSpray.shader.
        private const int CurvePoints = 24;

        private static readonly int CurveId = Shader.PropertyToID("_Curve");
        private static readonly int CurveLengthId = Shader.PropertyToID("_CurveLength");
        private static readonly int StreamRangeId = Shader.PropertyToID("_StreamRange");
        private static readonly int PhaseId = Shader.PropertyToID("_Phase");
        private static readonly int SpanId = Shader.PropertyToID("_Span");

        [SerializeField] private MeshRenderer _renderer;
        [SerializeField] private float _span = 32f;
        [SerializeField] private float _boundsPadding = 1.5f;

        private readonly Vector4[] _curve = new Vector4[CurvePoints];
        private MaterialPropertyBlock _block;
        private float _phase;

        protected override void OnInit()
        {
            _block = new MaterialPropertyBlock();
            _renderer.enabled = false;
        }

        protected override void OnShow(IWaterStreamCurve curve, float cut, float reach, bool isLanded, float deltaTime)
        {
            float length = Mathf.Min(curve.Length, _span);
            curve.Sample(0f, out Vector3 origin, out Vector3 launch);
            var bounds = new Bounds(origin, Vector3.zero);
            for (int i = 0; i < CurvePoints; i++)
            {
                curve.Sample(length * i / (CurvePoints - 1), out Vector3 point, out _);
                _curve[i] = point;
                bounds.Encapsulate(point);
            }

            bounds.Expand(_boundsPadding);
            _phase = Mathf.Repeat(_phase + launch.magnitude * deltaTime / _span, 1f);
            _block.SetVectorArray(CurveId, _curve);
            _block.SetFloat(CurveLengthId, length);
            _block.SetFloat(PhaseId, _phase);
            _block.SetFloat(SpanId, _span);
            _block.SetVector(StreamRangeId, new Vector4(cut, Mathf.Min(reach, length), isLanded ? 1f : 0f, 0f));
            _renderer.SetPropertyBlock(_block);
            _renderer.bounds = bounds;
            _renderer.enabled = true;
        }

        protected override void OnHide() => _renderer.enabled = false;
    }
}
