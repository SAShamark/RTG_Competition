using System;
using UnityEngine;

namespace Game.Rescue
{
    public enum ResidentPose
    {
        Animated = 0,
        Carried = 1,
        Flailing = 2,
        Victory = 3,
        Running = 4,
    }

    [Serializable]
    public class ResidentPoseLogic
    {
        [Header("Carried (degrees, body frame)")]
        [SerializeField] private float _hipFlex = 85f;
        [SerializeField] private float _kneeBend = 85f;
        [SerializeField] private float _armDown = 75f;
        [SerializeField] private float _armForward = 25f;
        [SerializeField] private float _forearmIn = 110f;

        [Header("Flailing")]
        [SerializeField] private float _flailSpeed = 12f;

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
        private ResidentPose _mode;
        private float _time;

        public ResidentPose Mode => _mode;

        public Vector3 Pelvis => Bone(HumanBodyBones.Hips) != null ? Bone(HumanBodyBones.Hips).position
            : _root != null ? _root.position + Vector3.up * 0.9f : Vector3.zero;

        public Transform Head => Bone(HumanBodyBones.Head);

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

        public void Dispose()
        {
        }

        public void SetMode(ResidentPose mode)
        {
            _mode = mode;
            if (_animator != null)
            {
                _animator.enabled = mode == ResidentPose.Animated;
            }
        }

        public void Tick(float deltaTime)
        {
            if (_bones == null || _mode == ResidentPose.Animated)
            {
                return;
            }

            _time += deltaTime;
            if (_mode == ResidentPose.Carried)
            {
                Legs(_hipFlex, _kneeBend, _hipFlex, _kneeBend);
                Arms(_armDown, _armForward, _forearmIn, _armDown, _armForward, _forearmIn);
                return;
            }

            if (_mode == ResidentPose.Running)
            {
                float r = _time * 9f;
                Legs(Mathf.Max(0f, Mathf.Sin(r)) * 45f - 10f, 25f + Mathf.Max(0f, -Mathf.Sin(r)) * 50f,
                    Mathf.Max(0f, -Mathf.Sin(r)) * 45f - 10f, 25f + Mathf.Max(0f, Mathf.Sin(r)) * 50f);
                Arms(72f, -Mathf.Sin(r) * 35f, 15f, 72f, Mathf.Sin(r) * 35f, 15f);
                return;
            }

            if (_mode == ResidentPose.Victory)
            {
                Legs(0f, 0f, 0f, 0f);
                Arms(-62f, 18f, 25f, -62f, 18f, 25f);
                return;
            }

            float t = _time * _flailSpeed;
            Legs(35f + Mathf.Sin(t * 0.8f) * 35f, 40f + Mathf.Sin(t * 0.8f + 1f) * 35f,
                35f + Mathf.Sin(t * 0.8f + Mathf.PI) * 35f, 40f + Mathf.Sin(t * 0.8f + 1f + Mathf.PI) * 35f);
            // arms up over the head, waving
            Arms(-60f + Mathf.Sin(t) * 25f, 10f, Mathf.Sin(t * 1.3f) * 30f,
                -60f + Mathf.Sin(t + 2f) * 25f, 10f, Mathf.Sin(t * 1.3f + 1f) * 30f);
        }

        private void Legs(float leftHip, float leftKnee, float rightHip, float rightKnee)
        {
            Set(0, Quaternion.Euler(-leftHip, 0f, 0f));
            Set(1, Quaternion.Euler(-leftHip + leftKnee, 0f, 0f));
            Set(2, Quaternion.Euler(-rightHip, 0f, 0f));
            Set(3, Quaternion.Euler(-rightHip + rightKnee, 0f, 0f));
        }

        private void Arms(float leftDown, float leftForward, float leftIn, float rightDown, float rightForward, float rightIn)
        {
            Quaternion leftUpper = Quaternion.Euler(0f, leftForward, 0f) * Quaternion.Euler(0f, 0f, leftDown);
            Quaternion rightUpper = Quaternion.Euler(0f, -rightForward, 0f) * Quaternion.Euler(0f, 0f, -rightDown);
            Set(4, leftUpper);
            Set(6, rightUpper);
            if (_mode == ResidentPose.Carried)
            {
                Set(5, Quaternion.Euler(0f, leftIn, 0f));
                Set(7, Quaternion.Euler(0f, -rightIn, 0f));
            }
            else
            {
                Set(5, leftUpper * Quaternion.Euler(0f, 0f, -leftIn));
                Set(7, rightUpper * Quaternion.Euler(0f, 0f, rightIn));
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
