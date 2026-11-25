using UnityEngine;

namespace Gameplay.Effects.WaterStream
{
    public sealed class RibbonStreamVisual : WaterStreamVisual
    {
        private const float MinSideSqr = 0.0001f;
        private const float SideSeed = 3.1f;
        private const float LiftSeed = 47.7f;

        private static readonly int StreamRangeId = Shader.PropertyToID("_StreamRange");

        [SerializeField] private LineRenderer[] _lines;
        [SerializeField, Min(2)] private int _points = 48;

        [Header("Wobble")]
        [SerializeField] private float _wobbleAmplitude = 0.16f;
        [SerializeField] private float _wobbleFrequency = 0.45f;
        [SerializeField, Range(0f, 1f)] private float _wobbleDetail = 0.35f;
        [SerializeField] private float _wobbleDetailFrequency = 1.7f;
        [SerializeField] private float _wobbleSpeed = 12f;
        [SerializeField] private float _wobbleRamp = 7f;

        [Header("Droplets")]
        [SerializeField] private StreamDroplets _droplets = new();

        private Vector3[] _positions;
        private MaterialPropertyBlock _block;
        private Vector3 _side = Vector3.right;
        private float _travel;

        protected override void OnInit()
        {
            _positions = new Vector3[_points];
            _block = new MaterialPropertyBlock();
            foreach (LineRenderer line in _lines)
            {
                line.positionCount = _points;
            }

            _droplets.Init();
            OnHide();
        }

        protected override void OnShow(IWaterStreamCurve curve, float cut, float reach, bool isLanded, float deltaTime)
        {
            _travel += _wobbleSpeed * deltaTime;
            for (int i = 0; i < _points; i++)
            {
                float distance = Mathf.Lerp(cut, reach, i / (float)(_points - 1));
                curve.Sample(distance, out Vector3 point, out Vector3 velocity);
                _positions[i] = point + Wobble(distance, velocity.normalized);
            }

            _block.SetVector(StreamRangeId, new Vector4(cut, reach, isLanded ? 1f : 0f, 0f));
            foreach (LineRenderer line in _lines)
            {
                line.enabled = true;
                line.SetPositions(_positions);
                line.SetPropertyBlock(_block);
            }

            _droplets.Emit(curve, cut, reach, deltaTime);
        }

        protected override void OnHide()
        {
            foreach (LineRenderer line in _lines)
            {
                line.enabled = false;
            }
        }

        // Kinks are born at the nozzle and ride downstream with the water, growing as the jet loosens.
        private Vector3 Wobble(float distance, Vector3 forward)
        {
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            if (side.sqrMagnitude > MinSideSqr)
            {
                _side = side.normalized;
            }

            Vector3 lift = Vector3.Cross(forward, _side);
            float phase = distance - _travel;
            float strength = _wobbleAmplitude * Mathf.Clamp01(distance / _wobbleRamp);
            return (_side * Noise(phase, SideSeed) + lift * Noise(phase, LiftSeed)) * strength;
        }

        private float Noise(float phase, float seed)
        {
            float slow = Mathf.PerlinNoise(phase * _wobbleFrequency, seed) - 0.5f;
            float fast = Mathf.PerlinNoise(phase * _wobbleDetailFrequency, seed + seed) - 0.5f;
            return (slow + fast * _wobbleDetail) * 2f;
        }
    }
}
