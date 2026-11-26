using Gameplay.Effects.WaterStream;
using Gameplay.Entities.Environment;
using Gameplay.Entities.Interactive;
using Gameplay.Entities.Interactive.Items;
using Services;
using Services.Audio;
using UnityEngine;

namespace Gameplay.Entities.Characters
{
    [DefaultExecutionOrder(250)]
    public sealed class HoseCrewControl : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private Transform _nozzle;
        [SerializeField] private Transform _muzzle;
        [SerializeField] private Transform _rightGrip;
        [SerializeField] private Transform _leftGrip;
        [SerializeField] private WaterStreamVisual _stream;
        [SerializeField] private WaterJetPath _path = new();

        [Header("Aim")]
        [SerializeField] private float _turnSharpness = 4f;
        [SerializeField] private float _aimSharpness = 5f;
        [SerializeField] private float _liftGain = 2.5f;
        [SerializeField] private float _maxLift = 14f;
        [SerializeField] private float _fireCentreHeight = 0.6f;
        [SerializeField] private float _wander = 0.35f;
        [SerializeField] private float _wanderSpeed = 0.7f;

        [Header("Dousing")]
        [SerializeField] private float _power = 0.1f;
        [SerializeField] private float _hitRadius = 1.4f;
        [SerializeField] private float _giveUpTime = 5f;
        [SerializeField] private float _heightBias = 2f;

        [Header("Sound")]
        [SerializeField] private SoundType _loopSound = SoundType.HoseSprayLoop;
        [SerializeField, Range(0f, 1f)] private float _loopVolume = 0.45f;

        [Header("Stance")]
        [SerializeField] private float _crouch = 0.12f;
        [SerializeField] private float _lean = 10f;
        [SerializeField] private float _footSpread = 0.08f;
        [SerializeField] private float _footStagger = 0.24f;
        [SerializeField] private float _backFootTurn = 35f;
        [SerializeField] private float _kneeBias = 20f;

        [Header("Standing down")]
        [SerializeField] private float _standDownTime = 0.9f;
        [SerializeField] private Vector3 _droppedPosition = new(0.3f, 0.2f, 0.5f);

        [Header("Hose")]
        [SerializeField] private Transform _hoseSocket;
        [SerializeField] private Transform _hoseSource;
        [SerializeField, Range(0f, 1f)] private float _endBackShare = 0.35f;
        [SerializeField] private float _reelInSpeed = 1.5f;
        [SerializeField] private float _slack = 1.1f;
        [SerializeField] private float _settleTime = 2f;
        [SerializeField] private float _drainSpeed = 14f;
        [SerializeField] private HoseRope _rope = new();
        [SerializeField] private HoseRopeView _view = new();

        private FireSource[] _fires;
        private FireSource _target;
        private FireSource _skipped;
        private ISoundLoop _loop = SilentLoop.Instance;
        private Transform _rightUpper;
        private Transform _rightLower;
        private Transform _rightHand;
        private Transform _leftUpper;
        private Transform _leftLower;
        private Transform _leftHand;
        private Transform _hips;
        private Transform _spine;
        private Transform _rightThigh;
        private Transform _rightCalf;
        private Transform _rightFoot;
        private Transform _leftThigh;
        private Transform _leftCalf;
        private Transform _leftFoot;
        private Vector3 _nozzleHome;
        private float _engage;
        private float _chargedLength;
        private Quaternion _rightHandRest;
        private Quaternion _leftHandRest;
        private Quaternion _nozzleRest;
        private float _lift;
        private float _reach;
        private float _cut;
        private float _sinceHit;
        private float _seed;
        private bool _isWorking;
        private bool _isStowed;

        public void Begin(FireSource[] fires)
        {
            _fires = fires;
            _isWorking = true;
            _reach = 0f;
            _cut = 0f;
            _engage = 1f;
            _chargedLength = _rope.Length;
            _loop = AudioService.Instance.PlayLoop(_loopSound, _muzzle);
            _loop.SetVolume(_loopVolume);
        }

        public void Stop()
        {
            _isWorking = false;
            _loop.Stop();
            _loop = SilentLoop.Instance;
        }

        private void Awake()
        {
            _path.Init();
            _seed = Random.value * 100f;
            _nozzleRest = _nozzle.localRotation;
            _nozzleHome = _nozzle.localPosition;
            _hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
            _spine = _animator.GetBoneTransform(HumanBodyBones.Spine);
            _rightThigh = _animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            _rightCalf = _animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            _rightFoot = _animator.GetBoneTransform(HumanBodyBones.RightFoot);
            _leftThigh = _animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            _leftCalf = _animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            _leftFoot = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            _rightUpper = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _rightLower = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            _rightHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            _leftUpper = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            _leftLower = _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            _leftHand = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Quaternion toBody = Quaternion.Inverse(transform.rotation);
            _rightHandRest = toBody * _rightHand.rotation;
            _leftHandRest = toBody * _leftHand.rotation;
            _rope.Init(_hoseSource.position, _hoseSocket.position, transform);
            _rope.Settle(_hoseSource.position, _hoseSocket.position, _slack, _settleTime);
            _view.Init(_rope);
        }

        private void OnDestroy() => _view.Dispose();

        private void Update()
        {
            if (_isStowed)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            float travel = _path.Speed * deltaTime;
            TickHose(deltaTime);
            if (_engage <= 0f)
            {
                Stow();
                return;
            }

            if (!_isWorking)
            {
                _nozzle.localRotation = Quaternion.Slerp(_nozzle.localRotation, _nozzleRest, Easing.Damp(_aimSharpness, deltaTime));
                _cut = Mathf.Min(_cut + travel, _reach);
                _stream.Show(_path, _cut, _reach, false, deltaTime);
                return;
            }

            if (_target == null || _target.IsOut)
            {
                Retarget(null);
            }

            if (_target == null)
            {
                Stop();
                return;
            }

            Vector3 centre = _target.transform.position + Vector3.up * _fireCentreHeight;
            Aim(centre, deltaTime);
            _path.Trace(_muzzle.position, _muzzle.forward, 1f, transform);
            // The water sags on its way, so the aim is walked up by however far below the fire the jet comes down.
            Vector3 landing = _path.PointAtRange(Vector3.ProjectOnPlane(centre - _muzzle.position, Vector3.up).magnitude);
            _lift = Mathf.Clamp(_lift + (centre.y - landing.y) * _liftGain * deltaTime, 0f, _maxLift);
            _reach = Mathf.Min(_reach + travel, _path.Length);
            bool isLanded = _reach >= _path.Length;
            _stream.Show(_path, 0f, _reach, isLanded, deltaTime);
            Douse(centre, isLanded, deltaTime);
        }

        private void LateUpdate()
        {
            if (_isStowed)
            {
                return;
            }

            float weight = Mathf.SmoothStep(0f, 1f, _engage);
            if (weight > 0f)
            {
                Brace(weight);
                TwoBoneIK.Solve(_rightUpper, _rightLower, _rightHand, _rightGrip.position, _rightGrip.rotation * _rightHandRest, weight);
                TwoBoneIK.Solve(_leftUpper, _leftLower, _leftHand, _leftGrip.position, _leftGrip.rotation * _leftHandRest, weight);
            }

            _view.Show(_rope, _hoseSocket.position, HoseDirection, _hoseSource.forward, _chargedLength, Time.deltaTime);
        }

        private void Stow()
        {
            _isStowed = true;
            _stream.Hide();
            _view.SetVisible(false);
            _nozzle.gameObject.SetActive(false);
        }

        private Vector3 HoseDirection => (Vector3.down - _nozzle.forward * _endBackShare).normalized;

        // While there is water in the jet the nozzle is held; once it has run dry the hands let go, it drops and is put away.
        private void TickHose(float deltaTime)
        {
            bool isHolding = _isWorking || _cut < _reach;
            _engage = Mathf.MoveTowards(_engage, isHolding ? 1f : 0f, deltaTime / _standDownTime);
            float fallen = 1f - _engage;
            // Free fall: the distance covered grows with the square of the time.
            _nozzle.localPosition = Vector3.Lerp(_nozzleHome, _droppedPosition, fallen * fallen);
            _chargedLength = _isWorking ? _rope.Length : Mathf.Max(0f, _chargedLength - _drainSpeed * deltaTime);
            _rope.Tick(_hoseSource.position, _hoseSocket.position, isHolding ? HoseDirection : Vector3.zero, _reelInSpeed, _slack, false, deltaTime);
        }

        // The jet pushes back, so the body sinks, leans into it and sets the left foot ahead of the right.
        private void Brace(float weight)
        {
            Vector3 leftPosition = _leftFoot.position + transform.TransformVector(-_footSpread, 0f, _footStagger) * weight;
            Vector3 rightPosition = _rightFoot.position + transform.TransformVector(_footSpread, 0f, -_footStagger) * weight;
            Quaternion leftRotation = _leftFoot.rotation;
            Quaternion rightRotation = Quaternion.AngleAxis(_backFootTurn * weight, Vector3.up) * _rightFoot.rotation;
            Vector3 side = transform.right;
            _hips.position += Vector3.down * (_crouch * weight);
            _spine.rotation = Quaternion.AngleAxis(_lean * weight, side) * _spine.rotation;
            Quaternion kneeBias = Quaternion.AngleAxis(_kneeBias * weight, side);
            _leftCalf.rotation = kneeBias * _leftCalf.rotation;
            _rightCalf.rotation = kneeBias * _rightCalf.rotation;
            TwoBoneIK.Solve(_leftThigh, _leftCalf, _leftFoot, leftPosition, leftRotation, 1f);
            TwoBoneIK.Solve(_rightThigh, _rightCalf, _rightFoot, rightPosition, rightRotation, 1f);
        }

        private void Aim(Vector3 centre, float deltaTime)
        {
            Vector3 planar = Vector3.ProjectOnPlane(centre - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(planar), Easing.Damp(_turnSharpness, deltaTime));
            float time = Time.time * _wanderSpeed + _seed;
            Vector3 wander = new Vector3(Mathf.PerlinNoise(time, 0f) - 0.5f, Mathf.PerlinNoise(0f, time) - 0.5f, 0f) * (2f * _wander);
            Vector3 aimPoint = centre + Vector3.up * _lift + transform.TransformVector(wander);
            Quaternion aim = Quaternion.LookRotation(aimPoint - _nozzle.position);
            _nozzle.rotation = Quaternion.Slerp(_nozzle.rotation, aim, Easing.Damp(_aimSharpness, deltaTime));
        }

        private void Douse(Vector3 centre, bool isLanded, float deltaTime)
        {
            if (isLanded && _path.DistanceTo(centre, _reach) <= _hitRadius)
            {
                _sinceHit = 0f;
                _target.Douse(_power, deltaTime);
                return;
            }

            _sinceHit += deltaTime;
            if (_sinceHit >= _giveUpTime)
            {
                Retarget(_target);
            }
        }

        // The crew works from the top down, where a hose from the street cannot reach: each takes the highest fire
        // on its own side, and leaves alone the one it has just failed to hit.
        private void Retarget(FireSource skipped)
        {
            _skipped = skipped;
            _target = null;
            _lift = 0f;
            _sinceHit = 0f;
            float best = float.MinValue;
            foreach (FireSource fire in _fires)
            {
                if (fire.IsOut || fire == _skipped || !fire.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector3 toFire = fire.transform.position - transform.position;
                float score = toFire.y * _heightBias - Vector3.ProjectOnPlane(toFire, Vector3.up).magnitude;
                if (score > best)
                {
                    best = score;
                    _target = fire;
                }
            }
        }
    }
}
