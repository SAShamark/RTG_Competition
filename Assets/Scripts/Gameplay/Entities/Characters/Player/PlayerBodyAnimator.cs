using System;
using Services;
using Gameplay.Entities.Interactive.Items;
using UnityEngine;

namespace Gameplay.Entities.Characters.Player
{
    [Serializable]
    public sealed class PlayerBodyAnimator
    {
        private const float HalfTurn = 180f;

        private static readonly int MoveXId = Animator.StringToHash("MoveX");
        private static readonly int MoveZId = Animator.StringToHash("MoveZ");
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int GroundedId = Animator.StringToHash("Grounded");
        private static readonly int VerticalSpeedId = Animator.StringToHash("VerticalSpeed");
        private static readonly int CarryingId = Animator.StringToHash("Carrying");
        private static readonly int HoldingToolId = Animator.StringToHash("HoldingTool");
        private static readonly int JumpId = Animator.StringToHash("Jump");
        private static readonly int HardLandId = Animator.StringToHash("HardLand");
        private static readonly int HeldToolId = Animator.StringToHash("HeldTool");
        private static readonly int LocomotionStateId = Animator.StringToHash("Locomotion");
        private static readonly int CarryLocomotionStateId = Animator.StringToHash("CarryLocomotion");
        private const int NoToolIndex = 0;
        private const int ExtinguisherIndex = 1;
        private const int AxeIndex = 2;

        [SerializeField] private Animator _animator;
        [SerializeField] private float _moveDamp = 0.12f;

        [Header("Orientation warping")]
        [SerializeField] private float _warpLimit = 67.5f;
        [SerializeField] private float _warpSharpness = 10f;
        [SerializeField] private float _warpMinSpeed = 0.3f;

        [Header("Footsteps")]
        [SerializeField, Min(1)] private int _stepsPerCycle = 2;
        [SerializeField, Min(1)] private int _carryStepsPerCycle = 4;
        [SerializeField, Range(0f, 1f)] private float _walkContactPhase = 0.02f;
        [SerializeField, Range(0f, 1f)] private float _runContactPhase = 0.33f;
        [SerializeField, Range(0f, 1f)] private float _carryContactPhase = 0.06f;

        private float _warpYaw;
        private bool _wasGrounded;

        public void Tick(Vector3 localVelocity, float runSpeed, bool isGrounded, float verticalSpeed, float deltaTime)
        {
            float targetYaw = WarpYaw(localVelocity);
            _warpYaw = Mathf.LerpAngle(_warpYaw, targetYaw, Easing.Damp(_warpSharpness, deltaTime));
            _animator.transform.localRotation = Quaternion.Euler(0f, _warpYaw, 0f);
            localVelocity = Quaternion.Euler(0f, -_warpYaw, 0f) * localVelocity;

            _animator.SetFloat(MoveXId, localVelocity.x / runSpeed, _moveDamp, deltaTime);
            _animator.SetFloat(MoveZId, localVelocity.z / runSpeed, _moveDamp, deltaTime);
            _animator.SetFloat(SpeedId, new Vector2(localVelocity.x, localVelocity.z).magnitude / runSpeed, _moveDamp, deltaTime);
            if (_wasGrounded && !isGrounded)
            {
                _animator.ResetTrigger(HardLandId);
            }

            _wasGrounded = isGrounded;
            _animator.SetBool(GroundedId, isGrounded);
            _animator.SetFloat(VerticalSpeedId, verticalSpeed);
        }

        // Counts foot contacts of the walk cycle: the whole part changes on the frame a foot lands.
        public bool TryGetStride(bool isRunning, out float stride)
        {
            AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
            bool isCarrying = state.shortNameHash == CarryLocomotionStateId;
            float contactPhase = isCarrying ? _carryContactPhase : isRunning ? _runContactPhase : _walkContactPhase;
            stride = (state.normalizedTime - contactPhase) * (isCarrying ? _carryStepsPerCycle : _stepsPerCycle);
            return isCarrying || state.shortNameHash == LocomotionStateId;
        }

        private float WarpYaw(Vector3 localVelocity)
        {
            var planar = new Vector2(localVelocity.x, localVelocity.z);
            if (planar.magnitude < _warpMinSpeed)
            {
                return 0f;
            }

            float angle = Mathf.Atan2(planar.x, planar.y) * Mathf.Rad2Deg;
            if (Mathf.Abs(angle) <= _warpLimit)
            {
                return angle;
            }

            float backAngle = Mathf.DeltaAngle(HalfTurn, angle);
            return Mathf.Abs(backAngle) <= _warpLimit ? backAngle : 0f;
        }

        public void Rest()
        {
            _warpYaw = 0f;
            _animator.transform.localRotation = Quaternion.identity;
            _animator.SetFloat(MoveXId, 0f);
            _animator.SetFloat(MoveZId, 0f);
            _animator.SetFloat(SpeedId, 0f);
            _animator.SetFloat(VerticalSpeedId, 0f);
            _animator.SetBool(GroundedId, true);
            _animator.ResetTrigger(JumpId);
            _animator.ResetTrigger(HardLandId);
        }

        public void SetCarrying(bool isCarrying) => _animator.SetBool(CarryingId, isCarrying);

        public void SetHoldingTool(bool isHolding) => _animator.SetBool(HoldingToolId, isHolding);

        public void SetHeldTool(ItemType type) => _animator.SetInteger(HeldToolId, type switch
        {
            ItemType.Axe => AxeIndex,
            ItemType.Extinguisher => ExtinguisherIndex,
            ItemType.Hose => ExtinguisherIndex,
            _ => NoToolIndex,
        });

        public void Jump()
        {
            _animator.ResetTrigger(HardLandId);
            _animator.SetTrigger(JumpId);
        }

        public void Land(bool isHard)
        {
            _animator.ResetTrigger(JumpId);
            if (isHard)
            {
                _animator.SetTrigger(HardLandId);
            }
        }
    }
}
