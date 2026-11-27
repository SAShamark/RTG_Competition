using System;
using Gameplay.Entities.Characters.Residents;
using Gameplay.Entities.Interactive;
using Gameplay.Entities.Interactive.Items;
using Services;
using Services.Audio;
using UnityEngine;
using Vr;
using Vr.Hands;

namespace Gameplay.Entities.Characters.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour, IItemHolder, IRescueCarrier, IPropHolder, IDialogueActor
    {
        private const int NoSlot = -1;

        [SerializeField] private Transform _head;
        [SerializeField] private Transform _aim;
        [SerializeField] private VrRig _rig;
        [SerializeField] private PlayerMotorLogic _motor = new();
        [SerializeField] private InteractionLogic _interactionLogic = new();
        [SerializeField] private TargetHighlightLogic _highlight = new();
        [SerializeField] private InventoryLogic _heldItem = new();
        [SerializeField] private BeltLogic _belt = new();
        [SerializeField] private ExtinguisherLogic _extinguisher = new();
        [SerializeField] private HoseLogic _hose = new();
        [SerializeField] private PlayerAudioLogic _audio = new();
        [SerializeField] private AxeLogic _axe = new();
        [SerializeField] private CarryLogic _carry = new();
        [SerializeField] private PlayerJumpLogic _jump = new();
        [SerializeField] private PlayerBodyAnimator _body = new();
        [SerializeField] private FootprintLogic _footprints = new();
        [SerializeField] private JawControl _jaw;
        [SerializeField] private ForcePullLogic _pull = new();

        [Header("Hands")]
        [SerializeField] private float _throwFactor = 1.3f;
        [SerializeField] private float _throwSoundSpeed = 1.5f;
        [SerializeField] private float _nozzleGrabRadius = 0.18f;
        [SerializeField] private float _axeGrabRadius = 0.12f;
        [SerializeField] private float _transferRadius = 0.1f;
        [SerializeField, Min(0f)] private float _tapTime = 0.35f;

        [Header("Haptics")]
        [SerializeField, Range(0f, 1f)] private float _grabPulse = 0.2f;
        [SerializeField] private float _grabPulseDuration = 0.05f;
        [SerializeField, Range(0f, 1f)] private float _weakStrikePulse = 0.35f;
        [SerializeField] private float _strikePulseDuration = 0.12f;
        [SerializeField] private float _bounceStrikePulseDuration = 0.25f;
        [SerializeField, Range(0f, 1f)] private float _sprayPulse = 0.12f;
        [SerializeField] private float _sprayPulseDuration = 0.05f;

        private CharacterController _characterController;
        private ClutterItem _prop;
        private bool _isPropLeft;
        private bool _isGrabbingLeft;
        private bool _isToolLeft;
        private bool _isOtherAiming;
        private bool _isOtherOnAxe;
        private readonly IHandDraggable[] _dragged = new IHandDraggable[2];
        private readonly float[] _dragStartTime = new float[2];
        private IInteractable _rightTarget;
        private IInteractable _leftTarget;
        private IInteractable _rightPressed;
        private IInteractable _leftPressed;

        public event Action<ClutterItem> PropChanged;
        public event Action Teleported;

        public Transform Head => _head;
        public InteractionLogic Interaction => _interactionLogic;
        public InventoryLogic Items => _heldItem;
        public ExtinguisherLogic Extinguisher => _extinguisher;
        public HoseLogic Hose => _hose;
        public AxeLogic Axe => _axe;
        public CarryLogic Carry => _carry;
        public ItemType HeldItem => _heldItem.HeldItem;
        public PlayerJumpLogic Jump => _jump;
        public float MoveSpeed => _motor.PlanarVelocity.magnitude;
        public VrWristDisplay Wrist => _rig.Wrist;
        public VrRig Rig => _rig;

        private VrInputLogic Input => _rig.Input;
        private bool IsSpraying => _extinguisher.IsSpraying || _hose.IsSpraying;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _interactionLogic.Init(transform, target => !_carry.IsCarrying || target is IUsableWhileCarrying);
            _interactionLogic.TargetChanged += _highlight.Set;
            _extinguisher.Init(_aim, _heldItem);
            _hose.Init(_aim, _heldItem);
            _audio.Init(transform, _head);
            _axe.Init(transform, _heldItem, _rig.TrackingSpace);
            _axe.Struck += HandleAxeStruck;
            _carry.Init(transform);
            _belt.Init(_heldItem);
            _pull.Init(_head, Input);
            _jump.Init(_characterController);
            _motor.Init(_characterController, transform, _jump);
            _footprints.Init(transform);
            _jump.Jumped += HandleJumped;
            _jump.Landed += HandleLanded;
            _heldItem.ItemChanged += HandleItemChanged;
            _heldItem.InventoryChanged += HandleInventoryChanged;
            _carry.CarryChanged += HandleCarryChanged;
        }

        private void OnDestroy()
        {
            _extinguisher.Dispose();
            _hose.Dispose();
            _heldItem.ItemChanged -= HandleItemChanged;
            _heldItem.InventoryChanged -= HandleInventoryChanged;
            _carry.CarryChanged -= HandleCarryChanged;
            _jump.Jumped -= HandleJumped;
            _jump.Landed -= HandleLanded;
            _axe.Struck -= HandleAxeStruck;
            _interactionLogic.TargetChanged -= _highlight.Set;
        }

        private void OnDisable()
        {
            _interactionLogic.Clear();
            _pull.Clear();
            EndDrag(false);
            EndDrag(true);
            _extinguisher.Tick(false, 0f);
            _hose.Tick(false, 0f);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            _motor.Tick(deltaTime);
            Vector3 move = HandleMovement(deltaTime);

            _rightTarget = TickHand(false);
            _leftTarget = TickHand(true);
            _interactionLogic.Tick(_rightTarget, _leftTarget);
            HandleAltAction();

            UpdateSprayAim();
            bool wantsToSpray = Input.IsTriggerHeld(_isToolLeft) && !_carry.IsCarrying;
            _extinguisher.Tick(wantsToSpray, deltaTime);
            _hose.Tick(wantsToSpray, deltaTime);
            if (IsSpraying)
            {
                Input.Pulse(_isToolLeft, _sprayPulse, _sprayPulseDuration);
            }

            _axe.Tick(deltaTime);
            _belt.Tick(_head, deltaTime, HoveredSlot());

            Vector3 localMove = transform.InverseTransformDirection(move);
            _body.Tick(localMove, _motor.RunSpeed, _jump.IsNearGround, _jump.VerticalSpeed, deltaTime);
            _body.SetCarrying(_carry.IsCarrying);
            _footprints.Tick(_jump.IsNearGround);
        }

        private void LateUpdate()
        {
            _extinguisher.LateTick(Time.deltaTime);
        }

        public void SetTalking(bool isTalking) => _jaw.SetTalking(isTalking);

        public void BeginConversation()
        {
            _motor.Stop();
            _body.Rest();
        }

        public void EndConversation() => _heldItem.SetHidden(_carry.IsCarrying);

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            _motor.Teleport(position, rotation);
            _head.localRotation = Quaternion.identity;
            Teleported?.Invoke();
            _belt.SnapTo(_head);
        }

        public bool TakeTool(ToolItem tool) => !_carry.IsCarrying && _heldItem.Take(tool);

        public bool StoreStack(ItemDefinition definition) => _heldItem.AddStack(definition);

        public bool GrabProp(ClutterItem item)
        {
            if (_prop != null || _carry.IsCarrying)
            {
                return false;
            }

            Transform hand = _rig.Hands.Get(_isGrabbingLeft).Body;
            Vector3 position = item.transform.position;
            Quaternion rotation = item.transform.rotation;
            item.SetHeld(hand);
            item.transform.SetPositionAndRotation(position, rotation);
            _prop = item;
            _isPropLeft = _isGrabbingLeft;
            AudioService.Instance.Play(SoundType.ItemPickup);
            PropChanged?.Invoke(item);
            return true;
        }

        public void PutDownProp() => ReleaseProp(Vector3.zero);

        public bool PickUp(Rescuable resident)
        {
            _heldItem.StowHand();
            ReleaseProp(Vector3.zero);
            return _carry.PickUp(resident);
        }

        private IInteractable TickHand(bool isLeft)
        {
            PhysicalHand hand = _rig.Hands.Get(isLeft);
            if (!hand.IsVisible)
            {
                return null;
            }

            TickPress(isLeft, hand);
            bool isGripPressed = Input.GripPressed(isLeft);
            bool isGripHeld = Input.IsGripHeld(isLeft);
            if (IsHandBusy(isLeft))
            {
                if (!isGripHeld)
                {
                    Release(isLeft, hand);
                }
                else if (_dragged[HandIndex(isLeft)] != null)
                {
                    _dragged[HandIndex(isLeft)].Drag(HandPose(hand));
                }
            }
            else
            {
                IInteractable target = _interactionLogic.Find(hand.Grip.position);
                IPullable caught = _pull.Tick(isLeft, hand, target == null, isGripPressed, isGripHeld, Time.deltaTime);
                if (caught != null)
                {
                    Grab(isLeft, hand, caught);
                }
                else if (isGripPressed)
                {
                    Grab(isLeft, hand, target);
                }
                else
                {
                    hand.SetHolding(false);
                    return target;
                }
            }

            hand.SetHolding(IsHandBusy(isLeft));
            return null;
        }

        private bool IsHandBusy(bool isLeft)
        {
            if ((_prop != null && _isPropLeft == isLeft) || _dragged[HandIndex(isLeft)] != null)
            {
                return true;
            }

            if (_heldItem.HeldTool == null)
            {
                return false;
            }

            return isLeft == _isToolLeft || _isOtherAiming || _isOtherOnAxe;
        }

        private void Grab(bool isLeft, PhysicalHand hand, IInteractable target)
        {
            Vector3 palm = hand.Grip.position;
            if (TryTakeFromOtherHand(isLeft, hand, palm))
            {
                return;
            }

            if (_heldItem.HeldTool == null)
            {
                int slot = _belt.FindSlot(palm);
                if (slot != NoSlot && _heldItem.Slot(slot).Tool != null)
                {
                    SetToolHand(isLeft);
                    _heldItem.SelectSlot(slot);
                    Input.Pulse(isLeft, _grabPulse, _grabPulseDuration);
                    return;
                }
            }

            if (target == null)
            {
                return;
            }

            if (target is IHandDraggable { CanDrag: true } draggable)
            {
                if (ReferenceEquals(draggable, _dragged[HandIndex(!isLeft)]))
                {
                    return;
                }

                _dragged[HandIndex(isLeft)] = draggable;
                _dragStartTime[HandIndex(isLeft)] = Time.time;
                draggable.BeginDrag(transform, HandPose(hand), isLeft);
                Input.Pulse(isLeft, _grabPulse, _grabPulseDuration);
                return;
            }

            if (target is ToolItem && _heldItem.HeldTool == null)
            {
                SetToolHand(isLeft);
            }

            _isGrabbingLeft = isLeft;
            _interactionLogic.Interact(target);
            Input.Pulse(isLeft, _grabPulse, _grabPulseDuration);
        }

        private bool TryTakeFromOtherHand(bool isLeft, PhysicalHand hand, Vector3 palm)
        {
            ToolItem tool = _heldItem.HeldTool;
            if (tool != null && isLeft != _isToolLeft)
            {
                if (TryGetNozzle(out Transform nozzle, out _) && (nozzle.position - palm).sqrMagnitude <= _nozzleGrabRadius * _nozzleGrabRadius)
                {
                    _isOtherAiming = true;
                    hand.SetGrip(tool.GripRadius, false);
                }
                else if (IsNearAxeHandle(palm))
                {
                    _isOtherOnAxe = true;
                    hand.SetGrip(tool.GripRadius, false);
                    _axe.SetSupport(hand.Grip);
                }
                else if (IsNear(tool, palm))
                {
                    SetToolHand(isLeft);
                }
                else
                {
                    return false;
                }

                Input.Pulse(isLeft, _grabPulse, _grabPulseDuration);
                return true;
            }

            if (_prop == null || isLeft == _isPropLeft || !IsNear(_prop, palm))
            {
                return false;
            }

            Transform item = _prop.transform;
            Vector3 position = item.position;
            Quaternion rotation = item.rotation;
            _prop.SetHeld(hand.Body);
            item.SetPositionAndRotation(position, rotation);
            _isPropLeft = isLeft;
            Input.Pulse(isLeft, _grabPulse, _grabPulseDuration);
            return true;
        }

        private void Release(bool isLeft, PhysicalHand hand)
        {
            if (_prop != null && _isPropLeft == isLeft)
            {
                ReleaseProp(hand.Velocity * _throwFactor);
                return;
            }

            IHandDraggable dragged = _dragged[HandIndex(isLeft)];
            if (dragged != null)
            {
                bool isTap = !EndDrag(isLeft) && Time.time - _dragStartTime[HandIndex(isLeft)] <= _tapTime;
                if (isTap)
                {
                    _interactionLogic.Interact(dragged);
                }

                return;
            }

            if (isLeft != _isToolLeft)
            {
                ReleaseSupport();
                return;
            }

            if (_isOtherAiming || _isOtherOnAxe)
            {
                SetToolHand(!isLeft);
                Input.Pulse(!isLeft, _grabPulse, _grabPulseDuration);
                return;
            }

            int slot = _heldItem.CanStow ? _belt.FindSlot(hand.Grip.position) : NoSlot;
            if (slot != NoSlot && _heldItem.Slot(slot).IsEmpty)
            {
                _heldItem.SelectSlot(slot);
                return;
            }

            if (_heldItem.CanStow && _heldItem.StowHand())
            {
                return;
            }

            Vector3 velocity = hand.Velocity * _throwFactor;
            _heldItem.Throw(velocity);
            AudioService.Instance.Play(velocity.magnitude >= _throwSoundSpeed ? SoundType.ItemThrow : SoundType.ItemDrop);
        }

        private void SetToolHand(bool isLeft)
        {
            ReleaseSupport();
            _isToolLeft = isLeft;
            _heldItem.SetHandSocket(_rig.Hands.Get(isLeft).ToolSocket);
            FitToolGrip();
        }

        private int HoveredSlot()
        {
            if (_heldItem.HeldTool != null)
            {
                return _heldItem.CanStow ? _belt.FindSlot(_rig.Hands.Get(_isToolLeft).Grip.position) : NoSlot;
            }

            int slot = _belt.FindSlot(_rig.Hands.Right.Grip.position);
            return slot != NoSlot ? slot : _belt.FindSlot(_rig.Hands.Left.Grip.position);
        }

        private bool IsNear(Component item, Vector3 palm)
        {
            foreach (Renderer itemRenderer in item.GetComponentsInChildren<Renderer>())
            {
                if (itemRenderer is not ParticleSystemRenderer && itemRenderer.bounds.SqrDistance(palm) <= _transferRadius * _transferRadius)
                {
                    return true;
                }
            }

            return false;
        }

        private bool EndDrag(bool isLeft)
        {
            IHandDraggable dragged = _dragged[HandIndex(isLeft)];
            if (dragged == null)
            {
                return false;
            }

            _dragged[HandIndex(isLeft)] = null;
            return dragged.EndDrag();
        }

        private static int HandIndex(bool isLeft) => isLeft ? 1 : 0;

        private bool IsNearAxeHandle(Vector3 palm)
        {
            ToolItem axe = _heldItem.HeldTool;
            if (_heldItem.HeldItem != ItemType.Axe || axe == null)
            {
                return false;
            }

            Vector3 start = axe.transform.position;
            Vector3 handle = axe.transform.TransformDirection(_axe.HandleAxis) * _axe.HandleLength;
            float along = Mathf.Clamp01(Vector3.Dot(palm - start, handle) / handle.sqrMagnitude);
            return (start + handle * along - palm).sqrMagnitude <= _axeGrabRadius * _axeGrabRadius;
        }

        private void ReleaseSupport()
        {
            _isOtherAiming = false;
            if (_isOtherOnAxe)
            {
                _isOtherOnAxe = false;
                _axe.SetSupport(null);
                _heldItem.ResetHold();
            }

            _rig.Hands.Get(!_isToolLeft).ResetGrip();
        }

        private void FitToolGrip()
        {
            PhysicalHand toolHand = _rig.Hands.Get(_isToolLeft);
            ToolItem tool = _heldItem.HeldTool;
            if (tool == null)
            {
                toolHand.ResetGrip();
                return;
            }

            toolHand.SetGrip(tool.GripRadius, tool.HasTrigger);
        }

        private static Pose HandPose(PhysicalHand hand) => new(hand.Grip.position, hand.Grip.rotation);

        private void TickPress(bool isLeft, PhysicalHand hand)
        {
            IInteractable pressed = _interactionLogic.Find(hand.FingerTip.position, _interactionLogic.PressRadius);
            pressed = pressed is IPressable ? pressed : null;
            IInteractable previous = isLeft ? _leftPressed : _rightPressed;
            if (pressed != null && !ReferenceEquals(pressed, previous))
            {
                _interactionLogic.Interact(pressed);
                Input.Pulse(isLeft, _grabPulse, _grabPulseDuration);
            }

            if (isLeft)
            {
                _leftPressed = pressed;
            }
            else
            {
                _rightPressed = pressed;
            }
        }

        private void HandleAltAction()
        {
            if (!GameInput.AltInteractPressed)
            {
                return;
            }

            if (_rightTarget is IAltInteractable rightAlt)
            {
                rightAlt.AltInteract(transform);
            }
            else if (_leftTarget is IAltInteractable leftAlt)
            {
                leftAlt.AltInteract(transform);
            }
            else if (_carry.IsCarrying)
            {
                _carry.PutDown();
            }
        }

        private void UpdateSprayAim()
        {
            if (!TryGetNozzle(out Transform nozzle, out Quaternion rest))
            {
                _isOtherAiming = false;
                return;
            }

            Quaternion rotation = _isOtherAiming ? _rig.Hands.Get(!_isToolLeft).Body.rotation : rest;
            _aim.SetPositionAndRotation(nozzle.position, rotation);
        }

        private bool TryGetNozzle(out Transform nozzle, out Quaternion rest)
        {
            if (_extinguisher.Current != null)
            {
                nozzle = _extinguisher.Current.Nozzle;
                rest = _extinguisher.Current.RestRotation;
                return true;
            }

            if (_hose.Current != null)
            {
                nozzle = _hose.Current.Nozzle;
                rest = _hose.Current.RestRotation;
                return true;
            }

            nozzle = null;
            rest = Quaternion.identity;
            return false;
        }

        private Vector3 HandleMovement(float deltaTime)
        {
            Vector2 input = GameInput.Move;
            float speed = GetMoveSpeed(input) * _motor.LandingSpeedFactor;
            Vector3 target = _motor.Steer(input, _rig.MoveForward, speed, deltaTime);
            _motor.PlanarVelocity = _hose.LimitMove(_motor.PlanarVelocity);

            _jump.HeightBoost = _hose.JumpBoost;
            _jump.Tick(GameInput.JumpPressed, GameInput.IsJumpHeld, !_carry.IsCarrying, _motor.Gravity, deltaTime);
            Vector3 move = _motor.Move(_hose.RecoilPush(target), deltaTime);
            bool isFast = speed > _motor.WalkSpeed;
            bool isStriding = _body.TryGetStride(isFast, out float stride);
            _audio.Tick(_motor.IsGrounded, isFast, isStriding, stride, deltaTime);
            return move;
        }

        private float GetMoveSpeed(Vector2 input)
        {
            if (_carry.IsCarrying)
            {
                return _motor.WalkSpeed * _carry.SpeedMultiplier;
            }

            return GameInput.IsRunHeld ? _motor.RunSpeedFor(input) : _motor.WalkSpeed;
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
            if (isHard)
            {
                _motor.BeginLandingRecovery();
            }
        }

        private void HandleAxeStruck(RaycastHit hit, AxeImpactLibrary.Impact impact, float power)
        {
            float duration = impact.Bounces ? _bounceStrikePulseDuration : _strikePulseDuration;
            float amplitude = Mathf.Lerp(_weakStrikePulse, 1f, power);
            Input.Pulse(_isToolLeft, amplitude, duration);
            if (_isOtherOnAxe)
            {
                Input.Pulse(!_isToolLeft, amplitude, duration);
            }
        }

        private void ReleaseProp(Vector3 velocity)
        {
            if (_prop == null)
            {
                return;
            }

            ClutterItem item = _prop;
            _prop = null;
            PropChanged?.Invoke(null);
            bool isThrown = velocity.magnitude >= _throwSoundSpeed;
            AudioService.Instance.Play(isThrown ? SoundType.ItemThrow : SoundType.ItemDrop);
            item.Release(item.transform.position, velocity);
        }

        private void HandleItemChanged(ItemType type)
        {
            AudioService.Instance.Play(SoundType.ItemSwitch);
            ReleaseSupport();
            FitToolGrip();
            _body.SetHoldingTool(_heldItem.HeldTool != null);
            _body.SetHeldTool(type);
        }

        private void HandleInventoryChanged() => _belt.Refresh(_heldItem);

        private void HandleCarryChanged(Rescuable resident)
        {
            _heldItem.SetHidden(resident != null);
            AudioService.Instance.Play(SoundType.CarryLift);
        }
    }
}
