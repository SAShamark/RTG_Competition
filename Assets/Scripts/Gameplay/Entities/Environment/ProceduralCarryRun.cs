using Services;
using Services.Audio;
using UnityEngine;

namespace Gameplay.Entities.Environment
{
    public sealed class ProceduralCarryRun : MonoBehaviour
    {
        private const float MinDeltaTime = 0.0001f;

        [SerializeField] private Transform _model;
        [SerializeField] private float _strideLength = 1.1f;
        [SerializeField] private float _legSwing = 38f;
        [SerializeField] private float _kneeBend = 65f;
        [SerializeField] private float _bobHeight = 0.07f;
        [SerializeField] private float _turnSpeed = 10f;
        [SerializeField] private Vector3 _upperArmDir = new(0.32f, -0.6f, 0.75f);
        [SerializeField] private Vector3 _lowerArmDir = new(-0.85f, 0.15f, 0.45f);
        [SerializeField] private float _speedSharpness = 10f;
        [SerializeField] private float _minMoveSqr = 0.05f;
        [SerializeField] private float _chestLean = 0.22f;
        [SerializeField] private float _bounceHeight = 0.06f;
        [SerializeField] private float _kneePhase = 1.2f;
        [SerializeField] private float _kneeRestBend = 5f;
        [SerializeField] private float _fullSpeed = 2f;
        [SerializeField] private float _swayAngle = 5f;

        [Header("Head look")]
        [SerializeField] private float _headLookMaxAngle = 75f;
        [SerializeField, Range(0f, 1f)] private float _headLookWeight = 0.8f;
        [SerializeField] private float _headLookSharpness = 8f;

        [Header("Footsteps")]
        [SerializeField] private SoundType _footstep = SoundType.FootstepConcrete;
        [SerializeField, Range(0f, 1f)] private float _footstepMinMoving = 0.5f;

        private Transform _thighL, _thighR, _calfL, _calfR, _footL, _footR;
        private Transform _upperArmL, _upperArmR, _lowerArmL, _lowerArmR, _handL, _handR, _spine, _neck, _head;
        private Quaternion[] _bind;
        private Transform[] _bones;
        private Vector3 _lastPosition;
        private float _phase;
        private float _speed;
        private Vector3 _modelRest;
        private Transform _lookAt;
        private float _headLook;
        private int _stepIndex;

        public void LookAt(Transform target) => _lookAt = target;

        private void Awake()
        {
            _thighL = Find("thigh_l"); _thighR = Find("thigh_r");
            _calfL = Find("calf_l"); _calfR = Find("calf_r");
            _footL = Find("foot_l"); _footR = Find("foot_r");
            _upperArmL = Find("upperarm_l"); _upperArmR = Find("upperarm_r");
            _lowerArmL = Find("lowerarm_l"); _lowerArmR = Find("lowerarm_r");
            _handL = Find("hand_l"); _handR = Find("hand_r");
            _spine = Find("spine_03");
            _neck = Find("neck_01");
            _head = Find("head");
            _bones = new[] { _thighL, _thighR, _calfL, _calfR, _upperArmL, _upperArmR, _lowerArmL, _lowerArmR, _spine, _head };
            _bind = new Quaternion[_bones.Length];
            for (int i = 0; i < _bones.Length; i++)
            {
                if (_bones[i] != null) _bind[i] = _bones[i].localRotation;
            }

            Animator animator = GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.enabled = false;
            }

            _modelRest = _model.localPosition;
            _lastPosition = transform.position;
        }

        private void OnEnable()
        {
            _lastPosition = transform.position;
        }

        private void LateUpdate()
        {
            float dt = Mathf.Max(Time.deltaTime, MinDeltaTime);
            Vector3 velocity = (transform.position - _lastPosition) / dt;
            velocity.y = 0f;
            _lastPosition = transform.position;
            _speed = Mathf.Lerp(_speed, velocity.magnitude, Easing.Damp(_speedSharpness, dt));

            if (velocity.sqrMagnitude > _minMoveSqr)
            {
                Quaternion look = Quaternion.LookRotation(velocity.normalized, Vector3.up);
                _model.rotation = Quaternion.Slerp(_model.rotation, look, Easing.Damp(_turnSpeed, dt));
            }

            float moving = Mathf.Clamp01(_speed / _fullSpeed);
            _phase += _speed / _strideLength * Mathf.PI * dt;

            for (int i = 0; i < _bones.Length; i++)
            {
                if (_bones[i] != null) _bones[i].localRotation = _bind[i];
            }

            Aim(_spine, _neck, new Vector3(0f, 1f, _chestLean * moving));
            _spine.rotation = Quaternion.AngleAxis(Mathf.Sin(_phase) * _swayAngle * moving, _model.forward) * _spine.rotation;

            PoseLeg(_thighL, _calfL, _footL, _phase, moving);
            PoseLeg(_thighR, _calfR, _footR, _phase + Mathf.PI, moving);

            float bounce = Mathf.Sin(_phase * 2f) * _bounceHeight * moving;
            Aim(_upperArmL, _lowerArmL, Mirror(_upperArmDir) + Vector3.up * bounce);
            Aim(_upperArmR, _lowerArmR, _upperArmDir + Vector3.up * bounce);
            Aim(_lowerArmL, _handL, Mirror(_lowerArmDir));
            Aim(_lowerArmR, _handR, _lowerArmDir);

            _model.localPosition = _modelRest + Vector3.up * (Mathf.Abs(Mathf.Sin(_phase)) * _bobHeight * moving);

            if (_lookAt != null)
            {
                TurnHead(dt);
            }

            PlayFootstep(moving);
        }

        private void TurnHead(float dt)
        {
            Vector3 toTarget = _lookAt.position - _head.position;
            bool isInView = Vector3.Angle(_model.forward, toTarget) < _headLookMaxAngle;
            _headLook = Mathf.Lerp(_headLook, isInView ? _headLookWeight : 0f, Easing.Damp(_headLookSharpness, dt));
            Quaternion turn = Quaternion.FromToRotation(_model.forward, toTarget.normalized);
            turn = Quaternion.RotateTowards(Quaternion.identity, turn, _headLookMaxAngle);
            _head.rotation = Quaternion.Slerp(Quaternion.identity, turn, _headLook) * _head.rotation;
        }

        // one footfall per half stride: the phase passes a multiple of π
        private void PlayFootstep(float moving)
        {
            int stepIndex = Mathf.FloorToInt(_phase / Mathf.PI);
            if (stepIndex != _stepIndex && moving > _footstepMinMoving)
            {
                AudioService.Instance.PlayAt(_footstep, transform.position);
            }

            _stepIndex = stepIndex;
        }

        private void PoseLeg(Transform thigh, Transform calf, Transform foot, float phase, float moving)
        {
            float swing = Mathf.Sin(phase) * _legSwing * moving;
            float bend = Mathf.Max(0f, Mathf.Sin(phase - _kneePhase)) * _kneeBend * moving + _kneeRestBend;
            Aim(thigh, calf, Angled(swing));
            Aim(calf, foot, Angled(swing - bend));
        }

        private static Vector3 Angled(float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            return new Vector3(0f, -Mathf.Cos(r), Mathf.Sin(r));
        }

        private static Vector3 Mirror(Vector3 v) => new(-v.x, v.y, v.z);

        private void Aim(Transform bone, Transform child, Vector3 localDir)
        {
            if (bone == null || child == null)
            {
                return;
            }

            Vector3 current = child.position - bone.position;
            Vector3 desired = _model.TransformDirection(localDir.normalized);
            bone.rotation = Quaternion.FromToRotation(current, desired) * bone.rotation;
        }

        private Transform Find(string boneName)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == boneName) return t;
            }

            return null;
        }
    }
}
