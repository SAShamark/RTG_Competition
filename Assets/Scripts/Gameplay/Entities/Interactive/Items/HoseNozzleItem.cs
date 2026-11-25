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
        [SerializeField] private float _sputterPitch = 1.5f;

        [Header("Hose run out")]
        [SerializeField] private SoundType _tugSound = SoundType.DoorLocked;
        [SerializeField] private float _tugImpulse = 1.2f;

        [Header("Sound")]
        [SerializeField] private SoundType _loopSound = SoundType.ExtinguisherSpray;
        [SerializeField] private SoundType _startSound = SoundType.ExtinguisherStart;
        [SerializeField] private SoundType _stopSound = SoundType.ExtinguisherStop;
        [SerializeField] private float _loopPitch = 0.7f;

        [Header("Shake")]
        [SerializeField] private CinemachineImpulseSource _impulse;
        [SerializeField] private float _startImpulse = 0.9f;
        [SerializeField] private float _sustainImpulse = 0.16f;
        [SerializeField] private float _sustainInterval = 0.12f;

        private ISoundLoop _loop = SilentLoop.Instance;
        private bool _isSpraying;
        private float _sinceImpulse;

        public Transform Nozzle => _nozzle;
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

        private void Awake() => _droplets.Play(false);

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
                _loop.SetPitch(_loopPitch);
                AudioService.Instance.PlayAt(_startSound, _nozzle.position, _loopPitch);
                _impulse.GenerateImpulse(_startImpulse);
                _sinceImpulse = 0f;
                return;
            }

            _loop.Stop();
            _loop = SilentLoop.Instance;
            AudioService.Instance.PlayAt(_stopSound, _nozzle.position, _loopPitch);
        }

        public void Hide() => _stream.Hide();

        public void Tug()
        {
            AudioService.Instance.PlayAt(_tugSound, _nozzle.position);
            _impulse.GenerateImpulse(_tugImpulse);
        }

        public void Sputter()
        {
            AudioService.Instance.PlayAt(_stopSound, _nozzle.position, _sputterPitch);
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
            if (!_isSpraying)
            {
                return;
            }

            _sinceImpulse += deltaTime;
            if (_sinceImpulse >= _sustainInterval)
            {
                _sinceImpulse = 0f;
                _impulse.GenerateImpulse(_sustainImpulse);
            }
        }
    }
}
