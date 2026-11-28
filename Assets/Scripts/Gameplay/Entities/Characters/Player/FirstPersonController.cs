using System;
using Gameplay.Entities.Characters.Residents;
using Gameplay.Entities.Interactive;
using Gameplay.Entities.Interactive.Items;
using Services;
using Services.Audio;
using Unity.Cinemachine;
using UnityEngine;

namespace Gameplay.Entities.Characters.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour, IItemHolder, IRescueCarrier, IPropHolder, IDialogueActor, IFreezable
    {
        private const float HalfTurn = 180f;
        private const float FullTurn = 360f;
        private const float MinFacingSqr = 0.001f;

        [SerializeField] private Transform _head;
        [SerializeField] private Transform _aim;
        [SerializeField] private float _mouseSensitivity = 0.12f;
        [SerializeField] private PlayerMotorLogic _motor = new();
        [SerializeField] private InteractionLogic _interactionLogic = new();
        [SerializeField] private HandsAnimationControl _hands = new();
        [SerializeField] private InventoryLogic _heldItem = new();
        [SerializeField] private ExtinguisherLogic _extinguisher = new();
        [SerializeField] private HoseLogic _hose = new();
        [SerializeField] private PlayerAudioLogic _audio = new();
        [SerializeField] private AxeLogic _axe = new();
        [SerializeField] private ShieldLogic _shield = new();
        [SerializeField] private FreezeLogic _freeze = new();
        [SerializeField] private ElementGunLogic _elementGun = new();
        [SerializeField] private FrostGrenadeLogic _frostGrenade = new();
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
        [SerializeField] private CinemachineImpulseSource _landImpulse;
        [SerializeField, Range(0f, 1f)] private float _firstPersonShake = 0.35f;

        [Header("Body & view")]
        [SerializeField] private PlayerViewLogic _view = new();
        [SerializeField] private PlayerBodyAnimator _body = new();
        [SerializeField] private PlayerBodyIK _bodyIK;
        [SerializeField] private ExtinguisherStanceLogic _stance = new();
        [SerializeField] private FootprintLogic _footprints = new();
        [SerializeField] private PlayerCameraFeel _cameraFeel = new();
        [SerializeField, Range(0f, 1f)] private float _idleTremor = 0.15f;

        [Header("Third person actions")]
        [SerializeField] private BodyActionLogic _bodyAction = new();
        [SerializeField] private float _bodyPickupContact = 0.37f;
        [SerializeField] private float _bodyPickupDuration = 0.95f;
        [SerializeField, Range(0f, 1f)] private float _lowPickupReach = 0.6f;
        [SerializeField] private float _lowPickupHeight = 0.5f;
        [SerializeField] private float _reachPickupContact = 0.28f;
        [SerializeField] private float _handPickupContact = 0.24f;
        [SerializeField] private float _handPickupDuration = 0.65f;
        [SerializeField] private float _handPickupDip = 0.07f;
        [SerializeField] private float _reachPickupDuration = 0.6f;
        [SerializeField] private AxeSwingProfile _swing = new();
        [SerializeField] private float _chopAimSharpness = 12f;
        [SerializeField] private float _recoilDuration = 0.45f;
        [SerializeField] private float _tossContact = 0.2f;
        [SerializeField] private float _tossDuration = 0.45f;
        [SerializeField] private float _tossReach = 0.6f;

        [Header("Conversation")]
        [SerializeField] private float _conversationTurnSharpness = 7f;
        [SerializeField] private JawControl _jaw;

        private CharacterController _characterController;
        private ClutterItem _prop;
        private float _pitch;
        private Transform _aimCamera;
        private bool _isChopping;
        private bool _chopAlternate;
        private float _swingSoundTimer = -1f;
        private float _chopAimWeight;
        private bool _isViewTogglePending;
        private bool _returnsToFirstPerson;

        public event Action<ClutterItem> PropChanged;

        public Transform Head => _head;
        public InteractionLogic Interaction => _interactionLogic;
        public InventoryLogic Items => _heldItem;
        public ExtinguisherLogic Extinguisher => _extinguisher;
        public HoseLogic Hose => _hose;
        public AxeLogic Axe => _axe;
        public ShieldLogic Shield => _shield;
        public FreezeLogic Freezing => _freeze;
        public CarryLogic Carry => _carry;
        public ItemType HeldItem => _heldItem.HeldItem;
        public PlayerViewLogic View => _view;
        public PlayerJumpLogic Jump => _jump;

        private bool IsHoldingProp => _prop != null;
        private bool HasFreeHands => !_carry.IsCarrying && !IsHoldingProp;
        private float ShakeFactor => _view.IsThirdPerson ? 1f : _firstPersonShake;
        private bool IsBodyBusy => _bodyAction.IsBusy;
        private bool CanAct => !_hands.IsBusy && !IsBodyBusy && !_freeze.IsFrozen;
        private bool IsSpraying => _extinguisher.IsSpraying || _hose.IsSpraying;
        private float SprayPressure => _hose.IsSpraying ? 1f : _extinguisher.Pressure;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _aimCamera = Camera.main.transform;
            _interactionLogic.Init(transform, _aim);
            _interactionLogic.SetFilter(target => !_carry.IsCarrying || target is IUsableWhileCarrying);
            _extinguisher.Init(_aim, _heldItem);
            _hose.Init(_aim, _heldItem);
            _audio.Init(transform, _head);
            _axe.Init(_aim, transform, _heldItem);
            _axe.Struck += HandleAxeStruck;
            _shield.Init(_heldItem, _characterController);
            _freeze.Init(transform);
            _freeze.Frozen += HandleFrozen;
            _freeze.Thawed += HandleThawed;
            _elementGun.Init(_aim, _heldItem, _characterController);
            _frostGrenade.Init(_aim, _heldItem, _characterController);
            _hands.SetChopTiming(_swing.Contact, _swing.Duration, _swing.Windup);
            _carry.Init(transform);
            _view.Init();
            _cameraFeel.Init(_view, _head);
            _heldItem.SetBodyHold(_view.IsThirdPerson);
            _view.ViewChanged += HandleViewChanged;
            _jump.Init(_characterController);
            _motor.Init(_characterController, transform, _jump);
            _bodyAction.Init(_bodyIK);
            _bodyAction.Finished += HandleBodyActionFinished;
            _footprints.Init(transform);
            _jump.Jumped += HandleJumped;
            _jump.Landed += HandleLanded;
            _heldItem.ItemChanged += HandleItemChanged;
            _carry.CarryChanged += HandleCarryChanged;
        }

        private void OnDestroy()
        {
            _extinguisher.Dispose();
            _hose.Dispose();
            _shield.Dispose();
            _freeze.Frozen -= HandleFrozen;
            _freeze.Thawed -= HandleThawed;
            _elementGun.Dispose();
            _frostGrenade.Dispose();
            _heldItem.ItemChanged -= HandleItemChanged;
            _carry.CarryChanged -= HandleCarryChanged;
            _view.ViewChanged -= HandleViewChanged;
            _jump.Jumped -= HandleJumped;
            _jump.Landed -= HandleLanded;
            _axe.Struck -= HandleAxeStruck;
            _bodyAction.Finished -= HandleBodyActionFinished;
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
            _hose.Tick(false, 0f);
            _hands.SetAiming(false);
            _hands.SetVisible(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;

            Vector2 look = _freeze.IsFrozen ? Vector2.zero : GameInput.Look * _mouseSensitivity + _hose.AimKick(deltaTime);
            HandleLook(look);
            UpdateAim();
            _bodyAction.Tick(deltaTime, _body.IsHitStopped);
            TickSwingSound(deltaTime);
            _view.Tick(deltaTime);
            _motor.Tick(deltaTime);
            _motor.Grip = FrostField.GripAt(transform.position);
            Vector3 move = HandleMovement(deltaTime);
            HandleViewToggle();

            _interactionLogic.Tick();
            HandleInteraction();
            HandlePrimaryAction();
            HandleElementSwitch();
            HandleDrop();
            HandleHotbar();

            bool wantsToSpray = GameInput.IsPrimaryHeld && CanAct && HasFreeHands;
            bool isStanceActive = IsStanceActive;
            _stance.Tick(isStanceActive, wantsToSpray, _heldItem.HeldTool, _view.IsThirdPerson, deltaTime);
            bool wasSpraying = IsSpraying;
            bool canSpray = wantsToSpray && (!isStanceActive || _stance.IsReadyToSpray);
            _extinguisher.Tick(canSpray, deltaTime);
            _hose.Tick(canSpray, deltaTime);
            _axe.Tick(deltaTime);
            _shield.Tick(deltaTime);
            _freeze.Tick(deltaTime);
            _frostGrenade.Tick();
            _elementGun.Tick(deltaTime);
            UpdateStance(wasSpraying);
            _hands.SetAiming(IsSpraying, SprayPressure);
            _hands.SetCarrying(_carry.IsCarrying);

            float planarSpeed = new Vector2(move.x, move.z).magnitude;
            _hands.Tick(deltaTime, Mathf.Clamp01(planarSpeed / _motor.RunSpeed), _audio.StepPhase, look);

            Vector3 localMove = transform.InverseTransformDirection(move);
            _body.Tick(localMove, _motor.RunSpeed, _jump.IsNearGround, _jump.VerticalSpeed, !_stance.IsAiming, deltaTime);
            bool isRunning = GameInput.IsRunHeld && !_carry.IsCarrying && !(_stance.IsAiming);
            _cameraFeel.Tick(localMove, _motor.RunSpeed, _audio.StepPhase, _jump.IsNearGround, isRunning, _carry.IsCarrying, _stance.Weight, deltaTime);
            _body.SetSpraying(IsSpraying);
            _body.SetCarrying(_carry.IsCarrying);
            UpdateBodyIK();
            _footprints.Tick(_jump.IsNearGround);
        }

        private void LateUpdate()
        {
            _stance.LateTick(_bodyIK);
            _extinguisher.LateTick(Time.deltaTime);
        }

        public void Discard(ToolItem tool)
        {
            if (_heldItem.HeldTool == tool)
            {
                _heldItem.Drop(Head);
            }
        }

        public void SetTalking(bool isTalking) => _jaw.SetTalking(isTalking);

        public void Freeze(Vector3 source)
        {
            if (!_shield.IsActive)
            {
                _freeze.Freeze(source);
            }
        }

        private void HandleFrozen()
        {
            _body.SetFrozen(true);
            _returnsToFirstPerson = !_view.IsThirdPerson;
            if (_returnsToFirstPerson)
            {
                _view.Toggle();
            }
        }

        private void HandleThawed()
        {
            _body.SetFrozen(false);
            if (_returnsToFirstPerson && _view.IsThirdPerson)
            {
                _view.Toggle();
            }
        }

        public void BeginConversation()
        {
            _motor.Stop();
            _stance.Reset();
            _chopAimWeight = 0f;
            _hands.SetStance(0f);
            _body.Rest();
            _bodyAction.ClearReach();
            _bodyIK.SetAim(0f, 0f);

            ToolItem tool = _heldItem.HeldTool;
            _heldItem.SetBodyHold(true);
            _heldItem.SetHidden(!HasFreeHands || (tool != null && !tool.HoldInBodyHand));
            if (!_carry.IsCarrying)
            {
                _bodyIK.SetTargets(false, null, null);
            }
        }

        public void FaceConversation(Vector3 point, float deltaTime)
        {
            Vector3 toPoint = point - _head.position;
            Vector3 planar = Vector3.ProjectOnPlane(toPoint, Vector3.up);
            if (planar.sqrMagnitude < MinFacingSqr)
            {
                return;
            }

            float blend = Easing.Damp(_conversationTurnSharpness, deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(planar), blend);
            float pitch = Mathf.Clamp(-Mathf.Atan2(toPoint.y, planar.magnitude) * Mathf.Rad2Deg, _pitchDown, _pitchUp);
            _pitch = Mathf.Lerp(_pitch, pitch, blend);
            _head.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        public void EndConversation()
        {
            _heldItem.SetBodyHold(_view.IsThirdPerson);
            _heldItem.SetHidden(!HasFreeHands);
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            _motor.Teleport(position, rotation);
            _head.localRotation = Quaternion.identity;
        }

        private void UpdateBodyIK()
        {
            ToolItem tool = IsHoldingProp ? null : _heldItem.HeldTool;
            bool isActive = _view.IsThirdPerson && (tool != null || _carry.IsCarrying);
            if (tool != null && tool.HoldInBodyHand)
            {
                _bodyIK.SetTargets(isActive, tool.RightGrip, tool.HasAimHold ? tool.LeftGrip : null, _stance.Weight, _stance.Weight);
                return;
            }

            Transform leftTarget = tool != null && tool.LeftGrip != null ? tool.LeftGrip : _hands.LeftHand;
            _bodyIK.SetTargets(isActive, _hands.RightHand, leftTarget);
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
                _bodyAction.Start(_bodyPickupContact, _bodyPickupDuration, true, grab, point, _lowPickupReach);
                return;
            }

            _bodyAction.Start(_reachPickupContact, _reachPickupDuration, true, grab, point, 1f);
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
                _bodyAction.Start(_swing.Contact, _swing.Duration, false, () => _axe.Strike(isAlternate));
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
                _bodyAction.Extend(_recoilDuration);
            }
        }

        private void HandleBodyActionFinished() => _isChopping = false;

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
            AudioService.Instance.Play(SoundType.ItemPickup);
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

        private Vector3 HandleMovement(float deltaTime)
        {
            Vector2 input = _bodyAction.LocksMovement || _freeze.IsFrozen ? Vector2.zero : GameInput.Move;
            float speed = GetMoveSpeed(input) * _motor.LandingSpeedFactor;
            Vector3 target = _motor.Steer(input, speed, deltaTime);
            _motor.PlanarVelocity = _hose.LimitMove(_motor.PlanarVelocity);

            _jump.HeightBoost = _hose.JumpBoost;
            _jump.Tick(GameInput.JumpPressed, GameInput.IsJumpHeld, !_carry.IsCarrying && !IsBodyBusy && !_freeze.IsFrozen, _motor.Gravity, deltaTime);
            Vector3 move = _motor.Move(_hose.RecoilPush(target), deltaTime);
            bool isFast = speed > _motor.WalkSpeed;
            bool isStriding = _body.TryGetStride(isFast, out float stride);
            _audio.Tick(_motor.IsGrounded, isFast, isStriding, stride, deltaTime);
            return move;
        }

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
                _motor.BeginLandingRecovery();
            }
        }

        private void HandleViewToggle()
        {
            _isViewTogglePending |= GameInput.ViewTogglePressed;
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
            bool isSpraying = IsSpraying;
            if (isSpraying && !wasSpraying)
            {
                _bodyIK.Kick();
            }

            float tremor = isSpraying ? Mathf.Lerp(_idleTremor, 1f, SprayPressure) : _idleTremor;
            _chopAimWeight = Mathf.Lerp(_chopAimWeight, _isChopping ? 1f : 0f, Easing.Damp(_chopAimSharpness, Time.deltaTime));
            _bodyIK.SetAim(Mathf.Max(_stance.Weight, _chopAimWeight), tremor);
            _hands.SetStance(_stance.Weight);
            _body.SetAiming(_stance.IsAiming);
        }

        private float GetMoveSpeed(Vector2 input)
        {
            if (IsStanceActive && _stance.IsAiming)
            {
                return _motor.WalkSpeed * _stance.MoveFactor;
            }

            if (_carry.IsCarrying)
            {
                return _motor.WalkSpeed * _carry.SpeedMultiplier;
            }

            float speed = GameInput.IsRunHeld ? _motor.RunSpeedFor(input) : _motor.WalkSpeed;
            return _isChopping ? speed * _swing.MoveFactor : speed;
        }

        private void HandleInteraction()
        {
            IInteractable target = _interactionLogic.Current;
            if (!CanAct || target == null || !target.CanInteract)
            {
                return;
            }

            if (GameInput.InteractPressed)
            {
                if (target.Gesture == HandGesture.Grab)
                {
                    StartPickup(target);
                    return;
                }

                _hands.Play(target.Gesture, () => _interactionLogic.Interact(target));
            }
            else if (GameInput.AltInteractPressed && target is IAltInteractable alt)
            {
                _hands.Play(target.Gesture, () => alt.AltInteract(transform));
            }
        }

        private void HandlePrimaryAction()
        {
            if (!CanAct || !GameInput.PrimaryPressed)
            {
                return;
            }

            if (IsHoldingProp)
            {
                ReleaseProp(true);
            }
            else if (HasFreeHands && _shield.CanToggle)
            {
                PressShieldButton();
            }
            else if (HasFreeHands && _axe.IsReady)
            {
                StartChop();
            }
            else if (HasFreeHands && _elementGun.IsReady)
            {
                _elementGun.Shoot();
            }
            else if (HasFreeHands && _frostGrenade.IsArmed)
            {
                StartToss(ThrowGrenade);
            }
            else if (HasFreeHands && _frostGrenade.IsHeld)
            {
                _frostGrenade.Arm();
            }
        }

        private void HandleElementSwitch()
        {
            if (CanAct && HasFreeHands && _elementGun.IsHeld && GameInput.ElementSwitchPressed)
            {
                _elementGun.SwitchElement();
            }
        }

        private void PressShieldButton()
        {
            if (_view.IsThirdPerson)
            {
                _shield.Toggle();
                return;
            }

            _hands.Play(HandGesture.ThumbPress, _shield.Toggle);
        }

        private void HandleDrop()
        {
            if (!CanAct || !GameInput.DropPressed)
            {
                return;
            }

            if (_carry.IsCarrying)
            {
                _carry.PutDown();
            }
            else if (!IsHoldingProp && _heldItem.HeldTool != null)
            {
                StartToss(DropHeldTool);
            }
        }

        private void DropHeldTool()
        {
            _heldItem.Drop(_head);
            AudioService.Instance.Play(SoundType.ItemThrow);
        }

        private void ThrowGrenade()
        {
            _frostGrenade.Throw();
            AudioService.Instance.Play(SoundType.ItemThrow);
        }

        private void StartToss(Action release)
        {
            if (!_view.IsThirdPerson)
            {
                _hands.Play(HandGesture.Toss, release);
                return;
            }

            _bodyAction.Start(_tossContact, _tossDuration, false, release, _head.position + _head.forward * _tossReach, 1f);
        }

        private void HandleHotbar()
        {
            int slot = GameInput.HotbarSlot;
            if (HasFreeHands && CanAct && slot != GameInput.NoSlot)
            {
                _heldItem.SelectSlot(slot);
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

            AudioService.Instance.Play(isThrown ? SoundType.ItemThrow : SoundType.ItemDrop);
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
                _hands.SetTool(null, null);
                _hands.SetHandPose(false, Vector3.zero, Vector3.zero);
                return;
            }

            _hands.SetTool(tool.transform, tool.LeftGrip);
            _hands.SetHandPose(tool.HasHandPose, tool.HandPosition, tool.HandEuler);
            if (tool.HasAimHold)
            {
                _hands.SetAimPose(tool.HandAimPosition, tool.HandAimEuler);
            }
        }

        private void HandleCarryChanged(Rescuable resident)
        {
            _heldItem.SetHidden(resident != null || IsHoldingProp);
            AudioService.Instance.Play(SoundType.CarryLift);
            if (resident == null)
            {
                _hands.SetCarryGrips(null, null);
                return;
            }

            Animator body = resident.Character.Animator;
            _hands.SetCarryGrips(body.GetBoneTransform(HumanBodyBones.LeftUpperArm), body.GetBoneTransform(HumanBodyBones.LeftLowerLeg));
        }

        private static float NormalizeAngle(float angle) => angle > HalfTurn ? angle - FullTurn : angle;
    }
}
