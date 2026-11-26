using System;
using UnityEngine;

namespace Gameplay.Entities.Characters
{
    public enum CharacterPose
    {
        Animated = 0,
        Carried = 1,
        Flailing = 2,
        Victory = 3,
        Running = 4,
    }

    [Serializable]
    public sealed class CharacterPoseLogic
    {
        [Header("Carried (degrees, body frame)")]
        [SerializeField] private float _hipFlex = 85f;
        [SerializeField] private float _kneeBend = 85f;
        [SerializeField] private float _armDown = 75f;
        [SerializeField] private float _armForward = 25f;
        [SerializeField] private float _forearmIn = 110f;

        [Header("Flailing")]
        [SerializeField] private float _flailSpeed = 12f;
        [SerializeField] private float _flailLegRate = 0.8f;
        [SerializeField] private float _flailHip = 35f;
        [SerializeField] private float _flailKnee = 40f;
        [SerializeField] private float _flailLegSwing = 35f;
        [SerializeField] private float _flailArmDown = -60f;
        [SerializeField] private float _flailArmForward = 10f;
        [SerializeField] private float _flailArmSwing = 25f;
        [SerializeField] private float _flailForearmRate = 1.3f;
        [SerializeField] private float _flailForearmSwing = 30f;

        [Header("Running")]
        [SerializeField] private float _runSpeed = 9f;
        [SerializeField] private float _runHipSwing = 45f;
        [SerializeField] private float _runHipBack = 10f;
        [SerializeField] private float _runKneeBase = 25f;
        [SerializeField] private float _runKneeSwing = 50f;
        [SerializeField] private float _runArmDown = 72f;
        [SerializeField] private float _runArmSwing = 35f;
        [SerializeField] private float _runForearmIn = 15f;

        [Header("Victory")]
        [SerializeField] private float _victoryArmDown = -62f;
        [SerializeField] private float _victoryArmForward = 18f;
        [SerializeField] private float _victoryForearmIn = 25f;

        [Header("Body")]
        [SerializeField] private float _pelvisHeight = 0.9f;

        private const int LeftHip = 0;
        private const int LeftKnee = 1;
        private const int RightHip = 2;
        private const int RightKnee = 3;
        private const int LeftShoulder = 4;
        private const int LeftElbow = 5;
        private const int RightShoulder = 6;
        private const int RightElbow = 7;
        private const float KneePhase = 1f;
        private const float ArmPhase = 2f;

        private static readonly HumanBodyBones[] Bones =
        {
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm,
            HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm,
        };

        private Animator _animator;
        private Transform _root;
        private Transform[] _bones;
        private Quaternion[] _rest;
        private CharacterPose _mode;
        private float _time;

        public CharacterPose Mode => _mode;

        public Vector3 Pelvis => Bone(HumanBodyBones.Hips) != null ? Bone(HumanBodyBones.Hips).position
            : _root != null ? _root.position + Vector3.up * _pelvisHeight : Vector3.zero;

        public void Init(Animator animator)
        {
            _animator = animator;
            if (_animator == null || !_animator.isHuman)
            {
                return;
            }

            _root = _animator.transform;
            _bones = new Transform[Bones.Length];
            _rest = new Quaternion[Bones.Length];
            for (int i = 0; i < Bones.Length; i++)
            {
                _bones[i] = _animator.GetBoneTransform(Bones[i]);
                if (_bones[i] != null)
                {
                    _rest[i] = Quaternion.Inverse(_root.rotation) * _bones[i].rotation;
                }
            }
        }

        public void SetMode(CharacterPose mode)
        {
            _mode = mode;
            if (_animator != null)
            {
                _animator.enabled = mode == CharacterPose.Animated;
            }
        }

        public void Tick(float deltaTime)
        {
            if (_bones == null || _mode == CharacterPose.Animated)
            {
                return;
            }

            _time += deltaTime;
            if (_mode == CharacterPose.Carried)
            {
                Legs(_hipFlex, _kneeBend, _hipFlex, _kneeBend);
                Arms(_armDown, _armForward, _forearmIn, _armDown, _armForward, _forearmIn);
                return;
            }

            if (_mode == CharacterPose.Running)
            {
                float swing = Mathf.Sin(_time * _runSpeed);
                float forward = Mathf.Max(0f, swing);
                float back = Mathf.Max(0f, -swing);
                Legs(forward * _runHipSwing - _runHipBack, _runKneeBase + back * _runKneeSwing,
                    back * _runHipSwing - _runHipBack, _runKneeBase + forward * _runKneeSwing);
                Arms(_runArmDown, -swing * _runArmSwing, _runForearmIn, _runArmDown, swing * _runArmSwing, _runForearmIn);
                return;
            }

            if (_mode == CharacterPose.Victory)
            {
                Legs(0f, 0f, 0f, 0f);
                Arms(_victoryArmDown, _victoryArmForward, _victoryForearmIn, _victoryArmDown, _victoryArmForward, _victoryForearmIn);
                return;
            }

            float t = _time * _flailSpeed;
            float legs = t * _flailLegRate;
            Legs(_flailHip + Mathf.Sin(legs) * _flailLegSwing, _flailKnee + Mathf.Sin(legs + KneePhase) * _flailLegSwing,
                _flailHip + Mathf.Sin(legs + Mathf.PI) * _flailLegSwing, _flailKnee + Mathf.Sin(legs + KneePhase + Mathf.PI) * _flailLegSwing);
            Arms(_flailArmDown + Mathf.Sin(t) * _flailArmSwing, _flailArmForward, Mathf.Sin(t * _flailForearmRate) * _flailForearmSwing,
                _flailArmDown + Mathf.Sin(t + ArmPhase) * _flailArmSwing, _flailArmForward, Mathf.Sin(t * _flailForearmRate + KneePhase) * _flailForearmSwing);
        }

        private void Legs(float leftHip, float leftKnee, float rightHip, float rightKnee)
        {
            Set(LeftHip, Quaternion.Euler(-leftHip, 0f, 0f));
            Set(LeftKnee, Quaternion.Euler(-leftHip + leftKnee, 0f, 0f));
            Set(RightHip, Quaternion.Euler(-rightHip, 0f, 0f));
            Set(RightKnee, Quaternion.Euler(-rightHip + rightKnee, 0f, 0f));
        }

        private void Arms(float leftDown, float leftForward, float leftIn, float rightDown, float rightForward, float rightIn)
        {
            Quaternion leftUpper = Quaternion.Euler(0f, leftForward, 0f) * Quaternion.Euler(0f, 0f, leftDown);
            Quaternion rightUpper = Quaternion.Euler(0f, -rightForward, 0f) * Quaternion.Euler(0f, 0f, -rightDown);
            Set(LeftShoulder, leftUpper);
            Set(RightShoulder, rightUpper);
            if (_mode == CharacterPose.Carried)
            {
                Set(LeftElbow, Quaternion.Euler(0f, leftIn, 0f));
                Set(RightElbow, Quaternion.Euler(0f, -rightIn, 0f));
            }
            else
            {
                Set(LeftElbow, leftUpper * Quaternion.Euler(0f, 0f, -leftIn));
                Set(RightElbow, rightUpper * Quaternion.Euler(0f, 0f, rightIn));
            }
        }

        private void Set(int index, Quaternion bodyFrameTurn)
        {
            if (_bones[index] != null)
            {
                _bones[index].rotation = _root.rotation * bodyFrameTurn * _rest[index];
            }
        }

        private Transform Bone(HumanBodyBones bone) =>
            _animator != null && _animator.isHuman ? _animator.GetBoneTransform(bone) : null;
    }
}
