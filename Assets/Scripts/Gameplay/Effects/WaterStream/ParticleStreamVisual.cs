using UnityEngine;

namespace Gameplay.Effects.WaterStream
{
    public sealed class ParticleStreamVisual : WaterStreamVisual
    {
        private const int HistorySize = 64;
        private const float HistoryInterval = 0.033f;
        private const int FlightSamples = 12;
        private const float MinSpeed = 0.5f;

        [Header("Flight (same numbers as the gameplay trajectory)")]
        [SerializeField] private float _gravity = 9.81f;
        [SerializeField] private float _airDrag = 0.6f;

        [SerializeField] private StreamParticleLayer _streaks = new();
        [SerializeField] private StreamParticleLayer _beads = new();
        [SerializeField] private StreamParticleLayer _mist = new();

        private readonly float[] _times = new float[HistorySize];
        private readonly Vector3[] _points = new Vector3[HistorySize];
        private readonly Vector3[] _normals = new Vector3[HistorySize];
        private readonly bool[] _hits = new bool[HistorySize];
        private int _head;
        private int _stored;
        private bool _isFlowing;

        protected override void OnInit()
        {
            _streaks.Init();
            _beads.Init();
            _mist.Init();
        }

        protected override void OnDispose()
        {
            _streaks.Dispose();
            _beads.Dispose();
            _mist.Dispose();
        }

        protected override void OnShow(IWaterStreamCurve curve, float cut, float reach, bool isLanded, float deltaTime)
        {
            if (cut > 0f)
            {
                _isFlowing = false;
                return;
            }

            if (!_isFlowing)
            {
                _isFlowing = true;
                _stored = 0;
            }

            Remember(curve);
            curve.Sample(0f, out Vector3 origin, out Vector3 velocity);
            _streaks.Emit(origin, velocity, deltaTime);
            _beads.Emit(origin, velocity, deltaTime);
            _mist.Emit(origin, velocity, deltaTime);
        }

        protected override void OnHide() => _isFlowing = false;

        // The water landing now left the nozzle a flight ago, so it splashes where the jet was aimed back then.
        protected override bool ResolveImpact(IWaterStreamCurve curve, bool isLanded, out Vector3 point, out Vector3 normal)
        {
            float launched = Time.time - FlightTime(curve);
            for (int back = 0; back < _stored; back++)
            {
                int index = (_head - 1 - back + HistorySize) % HistorySize;
                if (_times[index] <= launched || back == _stored - 1)
                {
                    point = _points[index];
                    normal = _normals[index];
                    return isLanded && _hits[index];
                }
            }

            return base.ResolveImpact(curve, isLanded, out point, out normal);
        }

        private void LateUpdate()
        {
            float deltaTime = Time.deltaTime;
            _streaks.Integrate(_gravity, _airDrag, deltaTime);
            _beads.Integrate(_gravity, _airDrag, deltaTime);
            _mist.Integrate(_gravity, _airDrag, deltaTime);
        }

        private void Remember(IWaterStreamCurve curve)
        {
            float now = Time.time;
            if (_stored > 0 && now - _times[(_head - 1 + HistorySize) % HistorySize] < HistoryInterval)
            {
                return;
            }

            _times[_head] = now;
            _points[_head] = curve.ImpactPoint;
            _normals[_head] = curve.ImpactNormal;
            _hits[_head] = curve.HasImpact;
            _head = (_head + 1) % HistorySize;
            _stored = Mathf.Min(_stored + 1, HistorySize);
        }

        private static float FlightTime(IWaterStreamCurve curve)
        {
            float step = curve.Length / FlightSamples;
            float time = 0f;
            for (int i = 0; i < FlightSamples; i++)
            {
                curve.Sample((i + 0.5f) * step, out _, out Vector3 velocity);
                time += step / Mathf.Max(velocity.magnitude, MinSpeed);
            }

            return time;
        }
    }
}
