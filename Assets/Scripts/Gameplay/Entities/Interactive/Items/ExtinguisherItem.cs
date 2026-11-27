using Services;
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
        [SerializeField] private ParticleSystem _puff;

        [Header("Aim")]
        [SerializeField] private float _aimSharpness = 14f;
        [SerializeField] private float _floorProbe = 3f;
        [SerializeField, Range(0f, 1f)] private float _minFloorNormal = 0.7f;
        [SerializeField] private float _cloudRadius = 0.25f;
        [SerializeField] private Vector2 _cloudSpeed = new(0.4f, 1.1f);
        [SerializeField, Range(0f, 1f)] private float _cloudLift = 0.35f;

        [Header("Nozzle puffs")]
        [SerializeField, Min(0)] private int _startBurst = 14;
        [SerializeField] private Vector2 _burstSpeed = new(2f, 6f);
        [SerializeField, Range(0f, 1f)] private float _burstSpread = 0.45f;
        [SerializeField, Min(0)] private int _stopWisps = 6;
        [SerializeField] private Vector2 _wispSpeed = new(0.2f, 0.9f);
        [SerializeField, Range(0f, 1f)] private float _wispSpread = 0.8f;

        [Header("Powder snow")]
        [SerializeField] private PowderSnowLayer _snow = new();
        [SerializeField, Range(0f, 1f)] private float _wallFallout = 0.35f;
        [SerializeField] private float _surfaceLift = 0.08f;
        [SerializeField, Min(0)] private int _scatterRays = 3;
        [SerializeField] private float _scatterAngle = 7f;
        [SerializeField, Range(0f, 1f)] private float _scatterShare = 0.5f;
        [SerializeField] private float _scatterOvershoot = 0.6f;

        [Header("Pressure")]
        [SerializeField, Range(0f, 1f)] private float _minFlow = 0.3f;
        [SerializeField, Range(0f, 1f)] private float _sputterThreshold = 0.2f;
        [SerializeField] private float _sputterSpeed = 7f;
        [SerializeField] private Vector2 _loopPitch = new(0.8f, 1.05f);
        [SerializeField, Range(0.01f, 1f)] private float _minJetLife = 0.12f;

        [Header("Shake")]
        [SerializeField] private CinemachineImpulseSource _impulse;
        [SerializeField] private float _startImpulse = 0.6f;
        [SerializeField] private float _sustainImpulse = 0.12f;
        [SerializeField] private float _sustainInterval = 0.15f;

        private ParticleSystem[] _flowSystems;
        private float _jetRate;
        private float _grainsRate;
        private float _jetDebt;
        private float _grainsDebt;
        private float _jetSpread;
        private float _grainsSpread;
        private float _flow;
        private float _cloudRate;
        private float _cloudDebt;
        private bool _hasFloor;
        private bool _hasWall;
        private Vector3 _impactNormal;
        private Vector3 _floorNormal;
        private Vector3 _impactPoint;
        private Vector3 _floorPoint;
        private float _baseJetSpeed;
        private float _baseJetLife;
        private Vector2 _jetLife;
        private Vector2 _grainsLife;
        private float _jetDrag;
        private float _jetReach;
        private ISoundLoop _sprayLoop = SilentLoop.Instance;
        private float _sinceImpulse;
        private bool _isSpraying;
        private bool _hasImpact;
        private LayerMask _impactMask;
        private Collider _impactSurface;
        private Collider _floorSurface;

        private Quaternion _nozzleRest;

        public Transform Nozzle => _nozzle;
        public Quaternion RestRotation => _nozzle.parent.rotation * _nozzleRest;

        private void Awake()
        {
            _nozzleRest = _nozzle.localRotation;
            _flowSystems = new[] { _jet, _grains };
            _jetRate = TakeRate(_jet);
            _grainsRate = TakeRate(_grains);
            _jetSpread = Mathf.Tan(_jet.shape.angle * Mathf.Deg2Rad);
            _grainsSpread = Mathf.Tan(_grains.shape.angle * Mathf.Deg2Rad);
            _cloudRate = TakeRate(_impactCloud);
            TakeRate(_puff);

            _baseJetSpeed = _jet.main.startSpeedMultiplier;
            _jetLife = LifeRange(_jet);
            _grainsLife = LifeRange(_grains);
            _baseJetLife = _jetLife.y;
            _jetDrag = _jet.limitVelocityOverLifetime.dragMultiplier;
            _jetReach = _baseJetSpeed / _jetDrag * (1f - Mathf.Exp(-_jetDrag * _baseJetLife));
        }

        public void AimAt(Vector3 point, float deltaTime)
        {
            Quaternion target = Quaternion.LookRotation(point - _nozzle.position, transform.up);
            _nozzle.rotation = Quaternion.Slerp(_nozzle.rotation, target, Easing.Damp(_aimSharpness, deltaTime));
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

        public void SetImpact(bool hasImpact, Vector3 point, Vector3 normal, Collider surface, LayerMask mask)
        {
            _hasImpact = hasImpact;
            _impactMask = mask;
            _impactSurface = surface;
            _impactPoint = point;
            _impactNormal = normal;
            _hasWall = hasImpact && _snow.IsWall(normal);
            _hasFloor = false;
            if (hasImpact && Physics.Raycast(point + normal * _surfaceLift + Vector3.up * _surfaceLift, Vector3.down, out RaycastHit floor, _floorProbe, mask, QueryTriggerInteraction.Ignore))
            {
                _hasFloor = floor.normal.y >= _minFloorNormal;
                _floorPoint = floor.point;
                _floorNormal = floor.normal;
                _floorSurface = floor.collider;
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
                EmitPuffs(_startBurst, _burstSpeed, _burstSpread);
                _sinceImpulse = 0f;
                return;
            }

            foreach (ParticleSystem system in _flowSystems) system.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            _sprayLoop.Stop();
            _sprayLoop = SilentLoop.Instance;
            AudioService.Instance.PlayAt(SoundType.ExtinguisherStop, _nozzle.position);
            EmitPuffs(_stopWisps, _wispSpeed, _wispSpread);
        }

        // Under linear drag a puff covers v₀/d · (1 − e^(−d·t)); solving for t gives the lifetime that ends it at the surface,
        // so the plume stops at whatever it hits instead of passing through.
        private float JetLifeFactor()
        {
            if (!_hasImpact)
            {
                return 1f;
            }

            float full = 1f - Mathf.Exp(-_jetDrag * _baseJetLife);
            float share = Mathf.Clamp01(Vector3.Distance(_nozzle.position, _impactPoint) / _jetReach);
            return Mathf.Max(_minJetLife, -Mathf.Log(1f - share * full) / (_jetDrag * _baseJetLife));
        }

        private static Vector2 LifeRange(ParticleSystem system)
        {
            ParticleSystem.MinMaxCurve life = system.main.startLifetime;
            return new Vector2(life.constantMin, life.constantMax);
        }

        private void EmitPuffs(int count, Vector2 speed, float spread)
        {
            var puff = new ParticleSystem.EmitParams { position = _nozzle.position, applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                Vector3 direction = (_nozzle.forward + Random.insideUnitSphere * spread).normalized;
                puff.velocity = direction * Random.Range(speed.x, speed.y);
                _puff.Emit(puff, 1);
            }
        }

        public void SetPressure(float pressure)
        {
            float flow = Mathf.Lerp(_minFlow, 1f, pressure);
            if (pressure < _sputterThreshold)
            {
                float sputter = Mathf.PerlinNoise(Time.time * _sputterSpeed, 0f);
                flow *= sputter * sputter * 2f;
            }

            _flow = flow;
            EmitImpact(flow);

            var main = _jet.main;
            main.startSpeedMultiplier = _baseJetSpeed * Mathf.Lerp(_minFlow, 1f, flow);
            float life = JetLifeFactor();
            main.startLifetime = new ParticleSystem.MinMaxCurve(_jetLife.x * life, _jetLife.y * life);
            var grainsMain = _grains.main;
            grainsMain.startLifetime = new ParticleSystem.MinMaxCurve(_grainsLife.x * life, _grainsLife.y * life);

            _sprayLoop.SetVolume(Mathf.Clamp01(flow));
            _sprayLoop.SetPitch(Mathf.Lerp(_loopPitch.x, _loopPitch.y, pressure));

            _sinceImpulse += Time.deltaTime;
            if (_sinceImpulse >= _sustainInterval)
            {
                _sinceImpulse = 0f;
                _impulse.GenerateImpulse(_sustainImpulse * flow);
            }
        }

        // Particle systems update before LateUpdate, when a tool carried by the chest still hangs where the animation left
        // the hand, so the plume is emitted by hand once the tool has been put in place.
        public void LateTick(float deltaTime)
        {
            if (!_isSpraying)
            {
                return;
            }

            EmitFlow(_jet, ref _jetDebt, _jetRate * _flow, _jetSpread, deltaTime);
            EmitFlow(_grains, ref _grainsDebt, _grainsRate * _flow, _grainsSpread, deltaTime);
        }

        private void EmitFlow(ParticleSystem system, ref float debt, float rate, float spread, float deltaTime)
        {
            debt += rate * deltaTime;
            ParticleSystem.MinMaxCurve speed = system.main.startSpeed;
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            while (debt >= 1f)
            {
                debt -= 1f;
                Vector3 direction = (_nozzle.forward + Random.insideUnitSphere * spread).normalized;
                emit.velocity = direction * speed.Evaluate(0f, Random.value);
                // Spread along the frame's travel, or a fast plume leaves the nozzle in clumps.
                emit.position = _nozzle.position + emit.velocity * (Random.value * deltaTime);
                system.Emit(emit, 1);
            }
        }

        // Stray rays inside the spray cone dust whatever sits around the aim point: neighbouring faces, edges, corners.
        private void ScatterDeposit(float amount)
        {
            Vector3 origin = _nozzle.position;
            Vector3 toImpact = _impactPoint - origin;
            float reach = toImpact.magnitude + _scatterOvershoot;
            Quaternion aim = Quaternion.LookRotation(toImpact);
            for (int i = 0; i < _scatterRays; i++)
            {
                Vector2 spread = Random.insideUnitCircle * _scatterAngle;
                Vector3 direction = aim * Quaternion.Euler(spread.y, spread.x, 0f) * Vector3.forward;
                if (Physics.Raycast(origin, direction, out RaycastHit hit, reach, _impactMask, QueryTriggerInteraction.Ignore))
                {
                    _snow.Deposit(hit.point, hit.normal, hit.collider, amount);
                }
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
                // Powder that hits a surface rolls out along it instead of bouncing straight back.
                Vector3 along = Vector3.ProjectOnPlane(Random.onUnitSphere, _impactNormal).normalized;
                Vector3 direction = (along + _impactNormal * _cloudLift).normalized;
                var cloud = new ParticleSystem.EmitParams
                {
                    position = _impactPoint + direction * Random.Range(0f, _cloudRadius),
                    velocity = direction * Random.Range(_cloudSpeed.x, _cloudSpeed.y),
                    applyShapeToPosition = false,
                };
                _impactCloud.Emit(cloud, 1);
            }

            float amount = flow * Time.deltaTime;
            if (_hasWall)
            {
                _snow.Deposit(_impactPoint, _impactNormal, _impactSurface, amount);
            }

            if (_hasFloor)
            {
                _snow.Deposit(_floorPoint, _floorNormal, _floorSurface, _hasWall ? amount * _wallFallout : amount);
            }

            ScatterDeposit(amount * _scatterShare);
        }
    }
}
