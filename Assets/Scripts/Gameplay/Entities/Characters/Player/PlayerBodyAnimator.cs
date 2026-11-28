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
        private static readonly int SprayingId = Animator.StringToHash("Spraying");
        private static readonly int HoldingToolId = Animator.StringToHash("HoldingTool");
        private static readonly int JumpId = Animator.StringToHash("Jump");
        private static readonly int HardLandId = Animator.StringToHash("HardLand");
        private static readonly int PickUpId = Animator.StringToHash("PickUp");
        private static readonly int ChopId = Animator.StringToHash("Chop");
        private static readonly int AimingId = Animator.StringToHash("Aiming");
        private static readonly int HeldToolId = Animator.StringToHash("HeldTool");
        private static readonly int ChopAltId = Animator.StringToHash("ChopAlt");
        private static readonly int ChopBounceId = Animator.StringToHash("ChopBounce");
        private static readonly int ChopSpeedId = Animator.StringToHash("ChopSpeed");
        private static readonly int LocomotionStateId = Animator.StringToHash("Locomotion");
        private static readonly int CarryLocomotionStateId = Animator.StringToHash("CarryLocomotion");
        private const int NoToolIndex = 0;
        private const int ExtinguisherIndex = 1;
        private const int AxeIndex = 2;
        private const float FrozenSpeed = 0f;
        private const float NormalSpeed = 1f;

        private float _hitStop;

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

        public bool IsHitStopped => _hitStop > 0f;

        public void Tick(Vector3 localVelocity, float runSpeed, bool isGrounded, float verticalSpeed, bool allowWarp, float deltaTime)
        {
            _hitStop = Mathf.Max(0f, _hitStop - deltaTime);
            _animator.SetFloat(ChopSpeedId, IsHitStopped ? FrozenSpeed : NormalSpeed);
            float targetYaw = allowWarp ? WarpYaw(localVelocity) : 0f;
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
            _hitStop = 0f;
            _warpYaw = 0f;
            _animator.transform.localRotation = Quaternion.identity;
            _animator.SetFloat(ChopSpeedId, NormalSpeed);
            _animator.SetFloat(MoveXId, 0f);
            _animator.SetFloat(MoveZId, 0f);
            _animator.SetFloat(SpeedId, 0f);
            _animator.SetFloat(VerticalSpeedId, 0f);
            _animator.SetBool(GroundedId, true);
            _animator.SetBool(AimingId, false);
            _animator.SetBool(SprayingId, false);
            _animator.ResetTrigger(ChopId);
            _animator.ResetTrigger(ChopBounceId);
            _animator.ResetTrigger(PickUpId);
            _animator.ResetTrigger(JumpId);
            _animator.ResetTrigger(HardLandId);
        }

        public void SetAiming(bool isAiming) => _animator.SetBool(AimingId, isAiming);

        public void SetCarrying(bool isCarrying) => _animator.SetBool(CarryingId, isCarrying);

        public void SetSpraying(bool isSpraying) => _animator.SetBool(SprayingId, isSpraying);

        public void SetHoldingTool(bool isHolding) => _animator.SetBool(HoldingToolId, isHolding);

        public void SetHeldTool(ItemType type) => _animator.SetInteger(HeldToolId, type switch
        {
            ItemType.Axe => AxeIndex,
            ItemType.Extinguisher => ExtinguisherIndex,
            ItemType.Hose => ExtinguisherIndex,
            _ => NoToolIndex,
        });

        public void Bounce() => _animator.SetTrigger(ChopBounceId);

        public void HitStop(float duration) => _hitStop = duration;

        public void SetFrozen(bool isFrozen) => _animator.speed = isFrozen ? FrozenSpeed : NormalSpeed;

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

        public void PickUp() => _animator.SetTrigger(PickUpId);

        public void Chop(bool isAlternate)
        {
            _animator.ResetTrigger(ChopBounceId);
            _animator.SetBool(ChopAltId, isAlternate);
            _animator.SetTrigger(ChopId);
        }
    }
}
