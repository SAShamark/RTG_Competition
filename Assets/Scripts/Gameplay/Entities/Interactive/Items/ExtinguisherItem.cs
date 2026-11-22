using Services.Audio;
using Unity.Cinemachine;
using UnityEngine;

namespace Gameplay.Entities.Interactive.Items
{
    public sealed class ExtinguisherItem : MonoBehaviour
    {
        [SerializeField] private Transform _nozzle;
        [SerializeField] private ParticleSystem _jet;
        [SerializeField] private ParticleSystem _grains;
        [SerializeField] private ParticleSystem _impactCloud;
        [SerializeField] private ParticleSystem _floorPowder;

        [Header("Aim")]
        [SerializeField] private float _aimSharpness = 14f;
        [SerializeField] private float _floorProbe = 3f;
        [SerializeField] private float _floorOffset = 0.02f;
        [SerializeField, Range(0f, 1f)] private float _minFloorNormal = 0.7f;
        [SerializeField] private float _cloudRadius = 0.25f;
        [SerializeField] private Vector2 _cloudSpeed = new(0.4f, 1.1f);
        [SerializeField] private float _floorRadius = 0.5f;

        [Header("Pressure")]
        [SerializeField, Range(0f, 1f)] private float _minFlow = 0.3f;
        [SerializeField, Range(0f, 1f)] private float _sputterThreshold = 0.2f;
        [SerializeField] private float _sputterSpeed = 7f;
        [SerializeField] private Vector2 _loopPitch = new(0.8f, 1.05f);

        [Header("Shake")]
        [SerializeField] private CinemachineImpulseSource _impulse;
        [SerializeField] private float _startImpulse = 0.6f;
        [SerializeField] private float _sustainImpulse = 0.12f;
        [SerializeField] private float _sustainInterval = 0.15f;

        private ParticleSystem[] _flowSystems;
        private float _jetRate;
        private float _grainsRate;
        private float _cloudRate;
        private float _floorRate;
        private float _cloudDebt;
        private float _floorDebt;
        private bool _hasFloor;
        private Vector3 _impactPoint;
        private Vector3 _floorPoint;
        private float _baseJetSpeed;
        private ISoundLoop _sprayLoop = SilentLoop.Instance;
        private float _sinceImpulse;
        private bool _isSpraying;
        private bool _hasImpact;

        public Transform Nozzle => _nozzle;

        private void Awake()
        {
            _flowSystems = new[] { _jet, _grains };
            _jetRate = _jet.emission.rateOverTimeMultiplier;
            _grainsRate = _grains.emission.rateOverTimeMultiplier;
            _cloudRate = TakeRate(_impactCloud);
            _floorRate = TakeRate(_floorPowder);

            _baseJetSpeed = _jet.main.startSpeedMultiplier;
        }

        public void AimAt(Vector3 point, float deltaTime)
        {
            Quaternion target = Quaternion.LookRotation(point - _nozzle.position, transform.up);
            _nozzle.rotation = Quaternion.Slerp(_nozzle.rotation, target, 1f - Mathf.Exp(-_aimSharpness * deltaTime));
        }

        // World-space systems emitted by hand, so the emitters riding on the held item never leak particles at hand height.
        private static float TakeRate(ParticleSystem system)
        {
            var emission = system.emission;
            float rate = emission.rateOverTimeMultiplier;
            emission.rateOverTimeMultiplier = 0f;
            system.Play(false);
            return rate;
        }

        public void SetImpact(bool hasImpact, Vector3 point, LayerMask mask)
        {
            _hasImpact = hasImpact;
            _impactPoint = point;
            _hasFloor = false;
            if (hasImpact && Physics.Raycast(point + Vector3.up * 0.1f, Vector3.down, out RaycastHit floor, _floorProbe, mask, QueryTriggerInteraction.Ignore))
            {
                _hasFloor = floor.normal.y >= _minFloorNormal;
                _floorPoint = floor.point + Vector3.up * _floorOffset;
            }
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
                foreach (ParticleSystem system in _flowSystems) system.Play(false);
                _sprayLoop = AudioService.Instance.PlayLoop(SoundType.ExtinguisherSpray, _nozzle);
                AudioService.Instance.PlayAt(SoundType.ExtinguisherStart, _nozzle.position);
                _impulse.GenerateImpulse(_startImpulse);
                _sinceImpulse = 0f;
                return;
            }

            foreach (ParticleSystem system in _flowSystems) system.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            _sprayLoop.Stop();
            _sprayLoop = SilentLoop.Instance;
            AudioService.Instance.PlayAt(SoundType.ExtinguisherStop, _nozzle.position);
        }

        public void SetPressure(float pressure)
        {
            float flow = Mathf.Lerp(_minFlow, 1f, pressure);
            if (pressure < _sputterThreshold)
            {
                float sputter = Mathf.PerlinNoise(Time.time * _sputterSpeed, 0f);
                flow *= sputter * sputter * 2f;
            }

            var jetEmission = _jet.emission;
            jetEmission.rateOverTimeMultiplier = _jetRate * flow;
            var grainsEmission = _grains.emission;
            grainsEmission.rateOverTimeMultiplier = _grainsRate * flow;
            EmitImpact(flow);

            var main = _jet.main;
            main.startSpeedMultiplier = _baseJetSpeed * Mathf.Lerp(_minFlow, 1f, flow);

            _sprayLoop.SetVolume(Mathf.Clamp01(flow));
            _sprayLoop.SetPitch(Mathf.Lerp(_loopPitch.x, _loopPitch.y, pressure));

            _sinceImpulse += Time.deltaTime;
            if (_sinceImpulse >= _sustainInterval)
            {
                _sinceImpulse = 0f;
                _impulse.GenerateImpulse(_sustainImpulse * flow);
            }
        }

        private void EmitImpact(float flow)
        {
            if (!_hasImpact)
            {
                return;
            }

            _cloudDebt += _cloudRate * flow * Time.deltaTime;
            while (_cloudDebt >= 1f)
            {
                _cloudDebt -= 1f;
                Vector3 direction = Random.onUnitSphere;
                direction.y = Mathf.Abs(direction.y);
                var cloud = new ParticleSystem.EmitParams
                {
                    position = _impactPoint + direction * Random.Range(0f, _cloudRadius),
                    velocity = direction * Random.Range(_cloudSpeed.x, _cloudSpeed.y),
                    applyShapeToPosition = false,
                };
                _impactCloud.Emit(cloud, 1);
            }

            if (!_hasFloor)
            {
                return;
            }

            _floorDebt += _floorRate * flow * Time.deltaTime;
            while (_floorDebt >= 1f)
            {
                _floorDebt -= 1f;
                Vector2 spread = Random.insideUnitCircle * _floorRadius;
                var spot = new ParticleSystem.EmitParams
                {
                    position = _floorPoint + new Vector3(spread.x, 0f, spread.y),
                    velocity = Vector3.zero,
                    applyShapeToPosition = false,
                };
                _floorPowder.Emit(spot, 1);
            }
        }
    }
}
