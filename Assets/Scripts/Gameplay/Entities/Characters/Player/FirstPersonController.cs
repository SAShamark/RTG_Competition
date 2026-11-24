using System;
using Gameplay.Entities.Characters.Residents;
using Gameplay.Entities.Interactive;
using Gameplay.Entities.Interactive.Items;
using Services;
using Services.Audio;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gameplay.Entities.Characters.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour, IItemHolder, IRescueCarrier, IPropHolder
    {
        private const float HalfTurn = 180f;
        private const float FullTurn = 360f;

        [SerializeField] private Transform _head;
        [SerializeField] private Transform _aim;
        [SerializeField] private float _walkSpeed = 3.2f;
        [SerializeField] private float _runSpeed = 5.5f;
        [SerializeField, Range(0f, 1f)] private float _backwardRunFactor = 0.76f;
        [SerializeField, Range(0f, 1f)] private float _strafeRunFactor = 0.8f;
        [SerializeField] private float _mouseSensitivity = 0.12f;
        [SerializeField] private float _gravity = -18f;
        [SerializeField] private InteractionLogic _interactionLogic = new();
        [SerializeField] private HandsAnimationControl _hands = new();
        [SerializeField] private InventoryLogic _heldItem = new();
        [SerializeField] private ExtinguisherLogic _extinguisher = new();
        [SerializeField] private PlayerAudioLogic _audio = new();
        [SerializeField] private AxeLogic _axe = new();
        [SerializeField] private CarryLogic _carry = new();
        [SerializeField] private float _throwSpeed = 6.5f;
        [SerializeField] private float _throwLift = 1.5f;
        [SerializeField] private float _dropReach = 1.4f;
        [SerializeField] private float _dropSurfaceOffset = 0.15f;
        [SerializeField] private float _limitedPitchDown = -15f;
        [SerializeField] private float _pitchDown = -80f;
        [SerializeField] private float _limitedPitchUp = 28f;
        [SerializeField] private float _pitchUp = 80f;

        [Header("Jump")]
        [SerializeField] private PlayerJumpLogic _jump = new();
        [SerializeField] private float _hardLandingRecovery = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _hardLandingSpeedFactor = 0.35f;
        [SerializeField] private CinemachineImpulseSource _landImpulse;
        [SerializeField, Range(0f, 1f)] private float _firstPersonShake = 0.35f;

        [Header("Acceleration")]
        [SerializeField] private float _groundAcceleration = 35f;
        [SerializeField] private float _groundDeceleration = 45f;
        [SerializeField] private float _airAcceleration = 7f;

        [Header("Body & view")]
        [SerializeField] private PlayerViewLogic _view = new();
        [SerializeField] private PlayerBodyAnimator _body = new();
        [SerializeField] private PlayerBodyIK _bodyIK;
        [SerializeField] private ExtinguisherStanceLogic _stance = new();
        [SerializeField] private FootprintLogic _footprints = new();
        [SerializeField] private PlayerCameraFeel _cameraFeel = new();
        [SerializeField, Range(0f, 1f)] private float _idleTremor = 0.15f;

        [Header("Third person actions")]
        [SerializeField] private float _bodyPickupContact = 0.37f;
        [SerializeField] private float _bodyPickupDuration = 0.95f;
        [SerializeField, Range(0f, 1f)] private float _lowPickupReach = 0.6f;
        [SerializeField] private float _lowPickupHeight = 0.5f;
        [SerializeField] private float _reachPickupContact = 0.28f;
        [SerializeField] private float _handPickupContact = 0.24f;
        [SerializeField] private float _handPickupDuration = 0.65f;
        [SerializeField] private float _handPickupDip = 0.07f;
        [SerializeField] private float _reachPickupDuration = 0.6f;
        [SerializeField, Range(0f, 1f)] private float _reachReleaseShare = 0.6f;
        [SerializeField] private AxeSwingProfile _swing = new();
        [SerializeField] private float _chopAimSharpness = 12f;
        [SerializeField] private float _recoilDuration = 0.45f;
        [SerializeField] private float _tossContact = 0.2f;
        [SerializeField] private float _tossDuration = 0.45f;
        [SerializeField] private float _tossReach = 0.6f;

        private CharacterController _characterController;
        private ClutterItem _prop;
        private float _pitch;
        private Vector3 _planarVelocity;
        private Transform _aimCamera;
        private float _landingRecovery;
        private Action _bodyContact;
        private float _bodyActionTime;
        private float _bodyContactTime;
        private float _bodyActionDuration;
        private bool _bodyLocksMovement;
        private bool _isChopping;
        private bool _chopAlternate;
        private float _swingSoundTimer = -1f;
        private float _chopAimWeight;
        private Vector3 _reachPoint;
        private float _reachStrength;
        private bool _isViewTogglePending;

        public event Action<ClutterItem> PropChanged;

        public Transform Head => _head;
        public float WalkSpeed => _walkSpeed;
        public float RunSpeed => _runSpeed;
        public float MouseSensitivity => _mouseSensitivity;
        public float Gravity => _gravity;
        public InteractionLogic Interaction => _interactionLogic;
        public InventoryLogic Items => _heldItem;
        public ExtinguisherLogic Extinguisher => _extinguisher;
        public AxeLogic Axe => _axe;
        public CarryLogic Carry => _carry;
        public ItemType HeldItem => _heldItem.HeldItem;
        public PlayerViewLogic View => _view;
        public PlayerJumpLogic Jump => _jump;

        private bool IsHoldingProp => _prop != null;
        private bool HasFreeHands => !_carry.IsCarrying && !IsHoldingProp;
        private float ShakeFactor => _view.IsThirdPerson ? 1f : _firstPersonShake;
        private bool IsBodyBusy => _bodyActionDuration > 0f;
        private bool CanAct => !_hands.IsBusy && !IsBodyBusy;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _aimCamera = Camera.main.transform;
            _interactionLogic.Init(transform, _aim);
            _interactionLogic.SetFilter(target => !_carry.IsCarrying || target is IUsableWhileCarrying);
            _extinguisher.Init(_aim, _heldItem);
            _audio.Init(transform, _head);
            _axe.Init(_aim, transform, _heldItem);
            _axe.Struck += HandleAxeStruck;
            _hands.SetChopTiming(_swing.Contact, _swing.Duration, _swing.Windup);
            _carry.Init(transform);
            _view.Init();
            _cameraFeel.Init(_view, _head);
            _heldItem.SetBodyHold(_view.IsThirdPerson);
            _view.ViewChanged += HandleViewChanged;
            _jump.Init(_characterController);
            _footprints.Init(transform);
            _jump.Jumped += HandleJumped;
            _jump.Landed += HandleLanded;
            _heldItem.ItemChanged += HandleItemChanged;
            _carry.CarryChanged += HandleCarryChanged;
        }

        private void OnDestroy()
        {
            _extinguisher.Dispose();
            _heldItem.ItemChanged -= HandleItemChanged;
            _carry.CarryChanged -= HandleCarryChanged;
            _view.ViewChanged -= HandleViewChanged;
            _jump.Jumped -= HandleJumped;
            _jump.Landed -= HandleLanded;
            _axe.Struck -= HandleAxeStruck;
        }

        private void OnEnable()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            _pitch = NormalizeAngle(_head.localEulerAngles.x);
            _hands.SetVisible(true);
        }

        private void OnDisable()
        {
            _interactionLogic.Clear();
            _extinguisher.Tick(false, 0f);
            _hands.SetAiming(false);
            _hands.SetVisible(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            float deltaTime = Time.deltaTime;

            Vector2 look = mouse.delta.ReadValue() * _mouseSensitivity;
            HandleLook(look);
            UpdateAim();
            TickBodyAction(deltaTime);
            TickSwingSound(deltaTime);
            _view.Tick(deltaTime);
            _landingRecovery = Mathf.Max(0f, _landingRecovery - deltaTime);
            Vector3 move = HandleMovement(keyboard, deltaTime);
            HandleViewToggle(keyboard);

            _interactionLogic.Tick();
            HandleInteraction(keyboard);
            HandlePrimaryAction(mouse);
            HandleDrop(keyboard);
            HandleHotbar(keyboard);

            bool wantsToSpray = mouse.leftButton.isPressed && CanAct && HasFreeHands;
            bool isStanceActive = IsStanceActive;
            _stance.Tick(isStanceActive, wantsToSpray, _heldItem.HeldTool, _view.IsThirdPerson, deltaTime);
            bool wasSpraying = _extinguisher.IsSpraying;
            _extinguisher.Tick(wantsToSpray && (!isStanceActive || _stance.IsReadyToSpray), deltaTime);
            _axe.Tick(deltaTime);
            UpdateStance(wasSpraying);
            _hands.SetAiming(_extinguisher.IsSpraying, _extinguisher.Pressure);
            _hands.SetCarrying(_carry.IsCarrying);

            float planarSpeed = new Vector2(move.x, move.z).magnitude;
            _hands.Tick(deltaTime, Mathf.Clamp01(planarSpeed / _runSpeed), _audio.StepPhase, look);

            Vector3 localMove = transform.InverseTransformDirection(move);
            _body.Tick(localMove, _runSpeed, _jump.IsNearGround, _jump.VerticalSpeed, !_stance.IsAiming, deltaTime);
            bool isRunning = keyboard.leftShiftKey.isPressed && !_carry.IsCarrying && !(_stance.IsAiming);
            _cameraFeel.Tick(localMove, _runSpeed, _audio.StepPhase, _jump.IsNearGround, isRunning, _carry.IsCarrying, _stance.Weight, deltaTime);
            _body.SetSpraying(_extinguisher.IsSpraying);
            _body.SetCarrying(_carry.IsCarrying);
            UpdateBodyIK();
            _footprints.Tick(_jump.IsNearGround);
        }

        private void UpdateBodyIK()
        {
            ToolItem tool = IsHoldingProp ? null : _heldItem.HeldTool;
            bool isActive = _view.IsThirdPerson && (tool != null || _carry.IsCarrying);
            if (tool != null && tool.HoldInBodyHand)
            {
                _bodyIK.SetTargets(isActive, null, tool.HasAimHold ? tool.LeftGrip : null, _stance.Weight);
                return;
            }

            Transform leftTarget = tool != null && tool.LeftGrip != null ? tool.LeftGrip : _hands.LeftHand;
            _bodyIK.SetTargets(isActive, _hands.RightHand, leftTarget);
        }

        private void StartBodyAction(float contactTime, float duration, bool locksMovement, Action onContact, Vector3 reachPoint = default, float reachStrength = 0f)
        {
            _reachPoint = reachPoint;
            _reachStrength = reachStrength;
            _bodyActionTime = 0f;
            _bodyContactTime = contactTime;
            _bodyActionDuration = duration;
            _bodyLocksMovement = locksMovement;
            _bodyContact = onContact;
        }

        private void TickBodyAction(float deltaTime)
        {
            if (!IsBodyBusy || _body.IsHitStopped)
            {
                return;
            }

            _bodyActionTime += deltaTime;
            _bodyIK.SetReach(_reachPoint, ReachWeight() * _reachStrength);
            if (_bodyContact != null && _bodyActionTime >= _bodyContactTime)
            {
                Action contact = _bodyContact;
                _bodyContact = null;
                contact();
            }

            if (_bodyActionTime >= _bodyActionDuration)
            {
                _bodyActionDuration = 0f;
                _isChopping = false;
                _bodyLocksMovement = false;
                _bodyIK.SetReach(_reachPoint, 0f);
            }
        }

        private float ReachWeight()
        {
            if (_bodyActionTime <= _bodyContactTime)
            {
                return Mathf.SmoothStep(0f, 1f, _bodyActionTime / _bodyContactTime);
            }

            float release = (_bodyActionDuration - _bodyContactTime) * _reachReleaseShare;
            return Mathf.SmoothStep(1f, 0f, (_bodyActionTime - _bodyContactTime) / release);
        }

        private void StartPickup(IInteractable target)
        {
            var component = (Component)target;
            Collider bounds = component.GetComponentInChildren<Collider>();
            Vector3 point = bounds != null ? bounds.bounds.center : component.transform.position;
            Action grab = () => _interactionLogic.Interact(target);
            bool isLow = point.y - transform.position.y < _lowPickupHeight;
            if (!_view.IsThirdPerson)
            {
                _hands.PlayReach(point, _handPickupContact, _handPickupDuration, grab);
                if (isLow)
                {
                    _cameraFeel.Dip(_handPickupDip);
                }

                return;
            }

            if (isLow)
            {
                _body.PickUp();
                StartBodyAction(_bodyPickupContact, _bodyPickupDuration, true, grab, point, _lowPickupReach);
                return;
            }

            StartBodyAction(_reachPickupContact, _reachPickupDuration, true, grab, point, 1f);
        }

        private void StartChop()
        {
            if (!_jump.IsNearGround)
            {
                return;
            }

            _axe.SpendSwing();
            _chopAlternate = !_chopAlternate;
            bool isAlternate = _chopAlternate;
            _swingSoundTimer = _swing.SwingSoundTime;
            if (_view.IsThirdPerson)
            {
                _body.Chop(isAlternate);
                StartBodyAction(_swing.Contact, _swing.Duration, false, () => _axe.Strike(isAlternate));
                _isChopping = true;
                return;
            }

            _hands.SetChopVariant(isAlternate);
            _hands.Play(HandGesture.Chop, () => _axe.Strike(isAlternate));
        }

        private void TickSwingSound(float deltaTime)
        {
            if (_swingSoundTimer < 0f)
            {
                return;
            }

            _swingSoundTimer -= deltaTime;
            if (_swingSoundTimer < 0f)
            {
                AudioService.Instance.Play(SoundType.AxeSwing);
            }
        }

        private void HandleAxeStruck(RaycastHit hit, AxeImpactLibrary.Impact impact)
        {
            _landImpulse.GenerateImpulseWithVelocity(_axe.SwingDirection * (impact.CameraImpulse * ShakeFactor));
            if (!_view.IsThirdPerson)
            {
                _hands.Freeze(impact.HitStop);
                if (impact.Bounces)
                {
                    _hands.Bounce();
                }

                return;
            }

            _body.HitStop(impact.HitStop);
            if (impact.Bounces)
            {
                _body.Bounce();
                _bodyActionDuration = _bodyActionTime + _recoilDuration;
            }
        }

        private void HandleViewChanged(PlayerView view)
        {
            _heldItem.SetBodyHold(_view.IsThirdPerson);
        }

        public bool TakeTool(ToolItem tool) => HasFreeHands && _heldItem.Take(tool);

        public bool StoreStack(ItemDefinition definition) => _heldItem.AddStack(definition);

        public bool GrabProp(ClutterItem item)
        {
            if (!HasFreeHands || !_heldItem.StowHand())
            {
                return false;
            }

            _prop = item;
            PropChanged?.Invoke(item);
            _heldItem.SetHidden(true);
            item.SetHeld(_head);
            _interactionLogic.SetOverride(item);
            return true;
        }

        public void PutDownProp() => ReleaseProp(false);

        public bool PickUp(Rescuable resident)
        {
            _heldItem.StowHand();
            return _carry.PickUp(resident);
        }

        private void HandleLook(Vector2 look)
        {
            transform.Rotate(0f, look.x, 0f);
            bool isLimited = _carry.IsCarrying;
            float min = isLimited ? _limitedPitchDown : _pitchDown;
            float max = isLimited ? _limitedPitchUp : _pitchUp;
            _pitch = Mathf.Clamp(_pitch - look.y, min, max);
            _head.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void UpdateAim()
        {
            if (!_view.IsThirdPerson)
            {
                _aim.SetPositionAndRotation(_head.position, _head.rotation);
                return;
            }

            // Project the head onto the camera ray so third-person casts start beside the body yet still pass through the crosshair.
            Vector3 forward = _aimCamera.forward;
            Vector3 origin = _aimCamera.position + forward * Vector3.Dot(_head.position - _aimCamera.position, forward);
            _aim.SetPositionAndRotation(origin, _aimCamera.rotation);
        }

        private Vector3 HandleMovement(Keyboard keyboard, float deltaTime)
        {
            Vector2 input = _bodyLocksMovement ? Vector2.zero : ReadMoveInput(keyboard);
            float speed = GetMoveSpeed(keyboard, input) * LandingSpeedFactor();
            Vector3 target = (transform.forward * input.y + transform.right * input.x).normalized * speed;
            _planarVelocity = Vector3.MoveTowards(_planarVelocity, target, GetAcceleration(target) * deltaTime);

            _jump.Tick(keyboard.spaceKey.wasPressedThisFrame, keyboard.spaceKey.isPressed, !_carry.IsCarrying && !IsBodyBusy, _gravity, deltaTime);
            Vector3 move = _planarVelocity;
            move.y = _jump.VerticalSpeed;
            _jump.HandleCollision(_characterController.Move(move * deltaTime));
            _audio.Tick(_characterController.isGrounded, speed > _walkSpeed, deltaTime);
            return move;
        }

        private float GetAcceleration(Vector3 target)
        {
            if (!_jump.IsNearGround)
            {
                return _airAcceleration;
            }

            return target.sqrMagnitude >= _planarVelocity.sqrMagnitude ? _groundAcceleration : _groundDeceleration;
        }

        private float LandingSpeedFactor() => _landingRecovery > 0f ? _hardLandingSpeedFactor : 1f;

        private void HandleJumped()
        {
            _body.Jump();
            _audio.PlayJump();
        }

        private void HandleLanded(float impact)
        {
            bool isHard = impact >= _jump.HardLandingSpeed;
            _body.Land(isHard);
            _audio.PlayLanding(isHard);
            _footprints.Land();
            _cameraFeel.Land(impact * ShakeFactor);
            if (isHard)
            {
                _landingRecovery = _hardLandingRecovery;
            }
        }

        private void HandleViewToggle(Keyboard keyboard)
        {
            _isViewTogglePending |= keyboard.vKey.wasPressedThisFrame;
            if (_isViewTogglePending && CanAct)
            {
                _isViewTogglePending = false;
                _view.Toggle();
            }
        }

        private bool IsStanceActive
        {
            get
            {
                ToolItem tool = _heldItem.HeldTool;
                return tool != null && tool.HasAimHold && HasFreeHands;
            }
        }

        private void UpdateStance(bool wasSpraying)
        {
            bool isSpraying = _extinguisher.IsSpraying;
            if (isSpraying && !wasSpraying)
            {
                _bodyIK.Kick();
            }

            float tremor = isSpraying ? Mathf.Lerp(_idleTremor, 1f, _extinguisher.Pressure) : _idleTremor;
            _chopAimWeight = Mathf.Lerp(_chopAimWeight, _isChopping ? 1f : 0f, Easing.Damp(_chopAimSharpness, Time.deltaTime));
            _bodyIK.SetAim(Mathf.Max(_stance.Weight, _chopAimWeight), tremor);
            _hands.SetStance(_stance.Weight);
            _body.SetAiming(_stance.IsAiming);
        }

        private float RunDirectionFactor(Vector2 input)
        {
            if (input.y > 0f)
            {
                return 1f;
            }

            return input.y < 0f ? _backwardRunFactor : _strafeRunFactor;
        }

        private float GetMoveSpeed(Keyboard keyboard, Vector2 input)
        {
            if (IsStanceActive && _stance.IsAiming)
            {
                return _walkSpeed * _stance.MoveFactor;
            }

            if (_carry.IsCarrying)
            {
                return _walkSpeed * _carry.SpeedMultiplier;
            }

            float speed = keyboard.leftShiftKey.isPressed ? _runSpeed * RunDirectionFactor(input) : _walkSpeed;
            return _isChopping ? speed * _swing.MoveFactor : speed;
        }

        private void HandleInteraction(Keyboard keyboard)
        {
            IInteractable target = _interactionLogic.Current;
            if (!CanAct || target == null || !target.CanInteract)
            {
                return;
            }

            if (keyboard.eKey.wasPressedThisFrame)
            {
                if (target.Gesture == HandGesture.Grab)
                {
                    StartPickup(target);
                    return;
                }

                _hands.Play(target.Gesture, () => _interactionLogic.Interact(target));
            }
            else if (keyboard.qKey.wasPressedThisFrame && target is IAltInteractable alt)
            {
                _hands.Play(target.Gesture, () => alt.AltInteract(transform));
            }
        }

        private void HandlePrimaryAction(Mouse mouse)
        {
            if (!CanAct || !mouse.leftButton.wasPressedThisFrame)
            {
                return;
            }

            if (IsHoldingProp)
            {
                ReleaseProp(true);
            }
            else if (HasFreeHands && _axe.IsReady && !_axe.IsAimingAtProtected)
            {
                StartChop();
            }
        }

        private void HandleDrop(Keyboard keyboard)
        {
            if (!CanAct || !keyboard.gKey.wasPressedThisFrame)
            {
                return;
            }

            if (_carry.IsCarrying)
            {
                _carry.PutDown();
            }
            else if (!IsHoldingProp && _heldItem.HeldTool != null)
            {
                StartToss();
            }
        }

        private void StartToss()
        {
            Action release = () => _heldItem.Drop(_head);
            if (!_view.IsThirdPerson)
            {
                _hands.Play(HandGesture.Toss, release);
                return;
            }

            StartBodyAction(_tossContact, _tossDuration, false, release, _head.position + _head.forward * _tossReach, 1f);
        }

        private void HandleHotbar(Keyboard keyboard)
        {
            if (!HasFreeHands || !CanAct)
            {
                return;
            }

            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                _heldItem.SelectSlot(0);
            }
            else if (keyboard.digit2Key.wasPressedThisFrame)
            {
                _heldItem.SelectSlot(1);
            }
            else if (keyboard.digit3Key.wasPressedThisFrame)
            {
                _heldItem.SelectSlot(2);
            }
        }

        private void ReleaseProp(bool isThrown)
        {
            if (_prop == null)
            {
                return;
            }

            ClutterItem item = _prop;
            _prop = null;
            PropChanged?.Invoke(null);
            _interactionLogic.SetOverride(null);
            _heldItem.SetHidden(false);

            if (isThrown)
            {
                item.Release(item.transform.position, _head.forward * _throwSpeed + Vector3.up * _throwLift);
                return;
            }

            Vector3 dropPoint = Physics.Raycast(_head.position, _head.forward, out RaycastHit hit, _dropReach, ~0, QueryTriggerInteraction.Ignore)
                ? hit.point + hit.normal * _dropSurfaceOffset
                : _head.position + _head.forward * _dropReach;
            item.Release(dropPoint, Vector3.zero);
        }

        private void HandleItemChanged(ItemType type)
        {
            AudioService.Instance.Play(SoundType.ItemSwitch);
            ToolItem tool = _heldItem.HeldTool;
            _body.SetHoldingTool(tool != null);
            _body.SetHeldTool(type);
            if (tool == null)
            {
                _hands.SetLeftGrip(null);
                _hands.SetHandPose(false, Vector3.zero, Vector3.zero);
                return;
            }

            _hands.SetLeftGrip(tool.LeftGrip);
            _hands.SetHandPose(tool.HasHandPose, tool.HandPosition, tool.HandEuler);
            if (tool.HasAimHold)
            {
                _hands.SetAimPose(tool.HandAimPosition, tool.HandAimEuler);
            }
        }

        private void HandleCarryChanged(Rescuable resident)
        {
            _heldItem.SetHidden(resident != null || IsHoldingProp);
            if (resident == null)
            {
                _hands.SetCarryGrips(null, null);
                return;
            }

            Animator body = resident.Animator;
            _hands.SetCarryGrips(body.GetBoneTransform(HumanBodyBones.LeftUpperArm), body.GetBoneTransform(HumanBodyBones.LeftLowerLeg));
        }

        private static Vector2 ReadMoveInput(Keyboard keyboard)
        {
            Vector2 input = Vector2.zero;
            input.y += keyboard.wKey.isPressed ? 1f : 0f;
            input.y -= keyboard.sKey.isPressed ? 1f : 0f;
            input.x += keyboard.dKey.isPressed ? 1f : 0f;
            input.x -= keyboard.aKey.isPressed ? 1f : 0f;
            return input;
        }

        private static float NormalizeAngle(float angle) => angle > HalfTurn ? angle - FullTurn : angle;
    }
}
