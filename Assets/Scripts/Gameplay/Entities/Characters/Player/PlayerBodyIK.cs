using Services;
using UnityEngine;

namespace Gameplay.Entities.Characters.Player
{
    [RequireComponent(typeof(Animator))]
    public sealed class PlayerBodyIK : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private Transform _head;

        [Header("Hands")]
        [SerializeField] private float _blendSharpness = 10f;
        [SerializeField, Range(0f, 1f)] private float _rotationWeight = 0.6f;
        [SerializeField] private Vector3 _rightRotationOffset = new(0f, -90f, 90f);
        [SerializeField] private Vector3 _leftRotationOffset = new(0f, 90f, -90f);
        [SerializeField] private Vector3 _rightElbowHint = new(0.45f, -0.4f, -0.3f);
        [SerializeField] private Vector3 _leftElbowHint = new(-0.45f, -0.4f, -0.3f);

        [Header("Look")]
        [SerializeField] private float _lookDistance = 10f;
        [SerializeField, Range(0f, 1f)] private float _lookWeight = 0.8f;
        [SerializeField, Range(0f, 1f)] private float _lookBodyWeight = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _lookHeadWeight = 0.9f;

        [Header("Aim stance")]
        [SerializeField, Range(0f, 1f)] private float _aimBodyWeight = 0.9f;
        [SerializeField, Range(0f, 1f)] private float _lookClamp = 0.5f;
        [SerializeField, Range(0f, 1f)] private float _aimLookClamp = 0.25f;
        [SerializeField] private float _tremorOffset = 0.12f;
        [SerializeField] private float _tremorFrequency = 17f;
        [SerializeField] private float _kickOffset = 0.6f;
        [SerializeField] private float _kickDecay = 10f;

        [Header("Reach")]
        [SerializeField] private Vector3 _reachElbowHint = new(0.35f, -0.2f, -0.3f);
        [SerializeField, Range(0f, 1f)] private float _reachLookWeight = 0.8f;

        private Vector3 _reachPoint;
        private float _reachWeight;
        private float _aimWeight;
        private float _tremor;
        private float _kick;
        private float _leftWeight = 1f;
        private float _rightWeight = 1f;
        private Transform _rightTarget;
        private Transform _leftTarget;
        private bool _isActive;
        private float _weight;
        private Quaternion _rightHandRest;
        private Quaternion _leftHandRest;

        private void Awake()
        {
            Quaternion toBody = Quaternion.Inverse(transform.rotation);
            _rightHandRest = toBody * _animator.GetBoneTransform(HumanBodyBones.RightHand).rotation;
            _leftHandRest = toBody * _animator.GetBoneTransform(HumanBodyBones.LeftHand).rotation;
        }

        // Called after the animator: the look-at pass turns the chest once the arms are solved and carries the hands
        // off their goals, so a prop held in both hands gets its wrists put back exactly where its grips are.
        public void Carry(Transform rightGrip, Transform leftGrip, float weight)
        {
            // The prop may hang off one of the hands, so both grips are read before either arm moves.
            Vector3 leftPosition = leftGrip.position;
            Quaternion leftRotation = leftGrip.rotation;
            Grip(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, rightGrip.position, rightGrip.rotation * _rightHandRest, weight);
            Grip(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, leftPosition, leftRotation * _leftHandRest, weight);
        }

        private void Grip(HumanBodyBones upperBone, HumanBodyBones lowerBone, HumanBodyBones handBone, Vector3 grip, Quaternion rotation, float weight)
        {
            TwoBoneIK.Solve(_animator.GetBoneTransform(upperBone), _animator.GetBoneTransform(lowerBone), _animator.GetBoneTransform(handBone), grip, rotation, weight);
        }

        public void SetTargets(bool isActive, Transform right, Transform left, float leftWeight = 1f, float rightWeight = 1f)
        {
            _isActive = isActive;
            _rightTarget = right;
            _leftTarget = left;
            _leftWeight = leftWeight;
            _rightWeight = rightWeight;
        }

        public void SetAim(float weight, float tremor)
        {
            _aimWeight = weight;
            _tremor = tremor;
        }

        public void Kick() => _kick = 1f;

        public void SetReach(Vector3 point, float weight)
        {
            _reachPoint = point;
            _reachWeight = weight;
        }

        private void OnAnimatorIK(int layerIndex)
        {
            _weight = Mathf.Lerp(_weight, _isActive ? 1f : 0f, Easing.Damp(_blendSharpness, Time.deltaTime));

            float deltaTime = Time.deltaTime;
            _kick = Mathf.Lerp(_kick, 0f, Easing.Damp(_kickDecay, deltaTime));
            float bodyWeight = Mathf.Lerp(_lookBodyWeight, _aimBodyWeight, _aimWeight);
            float clamp = Mathf.Lerp(_lookClamp, _aimLookClamp, _aimWeight);
            _animator.SetLookAtWeight(_lookWeight, bodyWeight, _lookHeadWeight, 0f, clamp);
            _animator.SetLookAtPosition(LookTarget());

            ApplyHand(AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, _rightTarget, _rightRotationOffset, _rightElbowHint, _rightWeight);
            ApplyHand(AvatarIKGoal.LeftHand, AvatarIKHint.LeftElbow, _leftTarget, _leftRotationOffset, _leftElbowHint, _leftWeight);
            ApplyReach();
        }

        private void ApplyReach()
        {
            if (_reachWeight <= 0f)
            {
                return;
            }

            _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, _reachWeight);
            _animator.SetIKPosition(AvatarIKGoal.RightHand, _reachPoint);
            _animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, _reachWeight);
            _animator.SetIKHintPosition(AvatarIKHint.RightElbow, _reachPoint + transform.TransformVector(_reachElbowHint));
        }

        private Vector3 LookTarget()
        {
            float time = Time.time * _tremorFrequency;
            float shake = _tremorOffset * _tremor * _aimWeight;
            Vector3 offset = _head.right * ((Mathf.PerlinNoise(time, 0f) - 0.5f) * 2f * shake)
                + _head.up * ((Mathf.PerlinNoise(0f, time) - 0.5f) * 2f * shake + _kick * _kickOffset * _aimWeight);
            Vector3 aim = _head.position + _head.forward * _lookDistance + offset;
            return Vector3.Lerp(aim, _reachPoint, _reachWeight * _reachLookWeight);
        }

        private void ApplyHand(AvatarIKGoal goal, AvatarIKHint hint, Transform target, Vector3 rotationOffset, Vector3 elbowHint, float scale)
        {
            float weight = target != null ? _weight * scale : 0f;
            _animator.SetIKPositionWeight(goal, weight);
            _animator.SetIKRotationWeight(goal, weight * _rotationWeight);
            _animator.SetIKHintPositionWeight(hint, weight);
            if (target == null)
            {
                return;
            }

            _animator.SetIKPosition(goal, target.position);
            _animator.SetIKRotation(goal, target.rotation * Quaternion.Euler(rotationOffset));
            _animator.SetIKHintPosition(hint, target.position + transform.TransformVector(elbowHint));
        }
    }
}
