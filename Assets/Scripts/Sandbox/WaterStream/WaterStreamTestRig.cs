using Gameplay.Effects.WaterStream;
using UnityEngine;

namespace Sandbox.WaterStream
{
    public sealed class WaterStreamTestRig : MonoBehaviour
    {
        [SerializeField] private Transform _nozzle;
        [SerializeField] private WaterStreamVisual _visual;
        [SerializeField] private BallisticStreamCurve _curve = new();
        [SerializeField, Range(0f, 1f)] private float _pressure = 1f;

        [Header("Auto cycle")]
        [SerializeField] private bool _isAuto = true;
        [SerializeField] private float _onTime = 6f;
        [SerializeField] private float _offTime = 2f;
        [SerializeField] private float _sweepAngle = 10f;
        [SerializeField] private float _sweepPeriod = 5f;

        [Header("Steam")]
        [SerializeField] private Transform[] _hotSpots;
        [SerializeField] private float _hotRadius = 1.2f;

        private Quaternion _restRotation;
        private bool _isSpraying;
        private float _reach;
        private float _cut;
        private float _time;

        public void SetAuto(bool isAuto)
        {
            _isAuto = isAuto;
            _nozzle.localRotation = _restRotation;
            _time = 0f;
            SetSpraying(false);
        }

        public void SetSpraying(bool isSpraying)
        {
            if (isSpraying == _isSpraying)
            {
                return;
            }

            _isSpraying = isSpraying;
            if (isSpraying)
            {
                _reach = 0f;
                _cut = 0f;
            }
        }

        private void Awake()
        {
            _curve.Init();
            _restRotation = _nozzle.localRotation;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (_isAuto)
            {
                _time += deltaTime;
                SetSpraying(Mathf.Repeat(_time, _onTime + _offTime) < _onTime);
                float yaw = Mathf.Sin(_time * Mathf.PI * 2f / _sweepPeriod) * _sweepAngle;
                _nozzle.localRotation = _restRotation * Quaternion.Euler(0f, yaw, 0f);
            }

            float travel = _curve.Speed * deltaTime;
            if (_isSpraying)
            {
                _curve.Trace(_nozzle.position, _nozzle.forward, _pressure);
                _reach = Mathf.Min(_reach + travel, _curve.Length);
            }
            else
            {
                _cut = Mathf.Min(_cut + travel, _reach);
            }

            bool isLanded = _reach > _cut && _reach >= _curve.Length;
            _visual.Show(_curve, _cut, _reach, isLanded, deltaTime);
            if (isLanded && _isSpraying && _curve.HasImpact)
            {
                SteamHotSpots(deltaTime);
            }
        }

        private void SteamHotSpots(float deltaTime)
        {
            foreach (Transform spot in _hotSpots)
            {
                if (Vector3.Distance(spot.position, _curve.ImpactPoint) <= _hotRadius)
                {
                    _visual.Steam(_curve.ImpactPoint, deltaTime);
                    return;
                }
            }
        }
    }
}
