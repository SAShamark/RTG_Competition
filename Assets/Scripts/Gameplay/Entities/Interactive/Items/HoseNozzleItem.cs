using Gameplay.Effects.WaterStream;
using Services;
using Services.Audio;
using Unity.Cinemachine;
using UnityEngine;

namespace Gameplay.Entities.Interactive.Items
{
    public sealed class HoseNozzleItem : MonoBehaviour
    {
        [SerializeField] private Transform _nozzle;
        [SerializeField] private Transform _hoseSocket;
        [SerializeField] private float _aimSharpness = 14f;
        [SerializeField] private WaterStreamVisual _stream;

        [Header("Dry sputter")]
        [SerializeField] private ParticleSystem _droplets;
        [SerializeField, Min(0)] private int _sputterDrops = 10;
        [SerializeField] private Vector2 _sputterSpeed = new(1f, 4f);
        [SerializeField, Range(0f, 1f)] private float _sputterSpread = 0.35f;
        [SerializeField] private SoundType _sputterSound = SoundType.HoseSputter;

        [Header("Hose run out")]
        [SerializeField] private SoundType _tugSound = SoundType.HoseTug;
        [SerializeField] private float _tugImpulse = 1.2f;

        [Header("Sound")]
        [SerializeField] private SoundType _loopSound = SoundType.HoseSprayLoop;
        [SerializeField] private SoundType _startSound = SoundType.HoseSprayStart;
        [SerializeField] private SoundType _stopSound = SoundType.HoseSprayStop;
        [SerializeField] private Vector2 _pressurePitch = new(0.8f, 1f);
        [SerializeField] private SoundType _impactSound = SoundType.WaterImpact;
        [SerializeField] private Transform _impactAnchor;

        [Header("Shake")]
        [SerializeField] private CinemachineImpulseSource _impulse;
        [SerializeField] private float _startImpulse = 0.9f;
        [SerializeField] private float _sustainImpulse = 0.16f;
        [SerializeField] private float _sustainInterval = 0.12f;

        [Header("Lever")]
        [SerializeField] private Transform _lever;
        [SerializeField] private float _leverOpenAngle = 40f;
        [SerializeField] private float _leverSharpness = 16f;

        private Quaternion _leverRest;
        private float _leverAngle;
        private ISoundLoop _loop = SilentLoop.Instance;
        private ISoundLoop _impactLoop = SilentLoop.Instance;
        private bool _isSpraying;
        private bool _isHitting;
        private float _sinceImpulse;

        private Quaternion _nozzleRest;

        public Transform Nozzle => _nozzle;
        public Quaternion RestRotation => _nozzle.parent.rotation * _nozzleRest;
        public Transform HoseSocket => _hoseSocket;
        public Vector3 TetherPull { get; private set; }
        public float TetherTension { get; private set; }
        public float Supply { get; private set; }

        public void SetSupply(float supply) => Supply = supply;

        public void SetTetherPull(Vector3 direction, float tension)
        {
            TetherPull = direction;
            TetherTension = tension;
        }

        private void Awake()
        {
            _droplets.Play(false);
            _leverRest = _lever.localRotation;
            _nozzleRest = _nozzle.localRotation;
        }

        private void Update()
        {
            float target = _isSpraying ? _leverOpenAngle : 0f;
            _leverAngle = Mathf.Lerp(_leverAngle, target, Easing.Damp(_leverSharpness, Time.deltaTime));
            _lever.localRotation = _leverRest * Quaternion.AngleAxis(_leverAngle, Vector3.forward);
        }

        public void AimAt(Vector3 point, float deltaTime)
        {
            Quaternion target = Quaternion.LookRotation(point - _nozzle.position, transform.up);
            _nozzle.rotation = Quaternion.Slerp(_nozzle.rotation, target, Easing.Damp(_aimSharpness, deltaTime));
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
                _loop = AudioService.Instance.PlayLoop(_loopSound, _nozzle);
                ApplyPressure();
                AudioService.Instance.PlayAt(_startSound, _nozzle.position);
                _impulse.GenerateImpulse(_startImpulse);
                _sinceImpulse = 0f;
                return;
            }

            _loop.Stop();
            _loop = SilentLoop.Instance;
            AudioService.Instance.PlayAt(_stopSound, _nozzle.position);
        }

        public void Hide()
        {
            _stream.Hide();
            SetHitting(false);
        }

        public void Tug()
        {
            AudioService.Instance.PlayAt(_tugSound, _nozzle.position);
            _impulse.GenerateImpulse(_tugImpulse);
        }

        public void Sputter()
        {
            AudioService.Instance.PlayAt(_sputterSound, _nozzle.position);
            var drop = new ParticleSystem.EmitParams { position = _nozzle.position, applyShapeToPosition = false };
            for (int i = 0; i < _sputterDrops; i++)
            {
                Vector3 direction = (_nozzle.forward + Random.insideUnitSphere * _sputterSpread).normalized;
                drop.velocity = direction * Random.Range(_sputterSpeed.x, _sputterSpeed.y);
                _droplets.Emit(drop, 1);
            }
        }

        public void Show(WaterJetPath path, float cut, float reach, bool isLanded, float deltaTime)
        {
            _stream.Show(path, cut, reach, isLanded, deltaTime);
            bool isHitting = isLanded && path.HasImpact;
            if (isHitting)
            {
                _impactAnchor.position = path.ImpactPoint;
            }

            SetHitting(isHitting);
            if (!_isSpraying)
            {
                return;
            }

            ApplyPressure();
            _sinceImpulse += deltaTime;
            if (_sinceImpulse >= _sustainInterval)
            {
                _sinceImpulse = 0f;
                _impulse.GenerateImpulse(_sustainImpulse);
            }
        }

        private void ApplyPressure()
        {
            _loop.SetVolume(Supply);
            _loop.SetPitch(Mathf.Lerp(_pressurePitch.x, _pressurePitch.y, Supply));
        }

        private void SetHitting(bool isHitting)
        {
            if (isHitting == _isHitting)
            {
                return;
            }

            _isHitting = isHitting;
            if (isHitting)
            {
                _impactLoop = AudioService.Instance.PlayLoop(_impactSound, _impactAnchor);
                return;
            }

            _impactLoop.Stop();
            _impactLoop = SilentLoop.Instance;
        }
    }
}
