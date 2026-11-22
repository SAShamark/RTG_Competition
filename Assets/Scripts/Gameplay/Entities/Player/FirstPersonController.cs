using Gameplay.Entities.Interactive;
using Gameplay.Entities.Interactive.Items;
using Gameplay.Entities.Residents;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Gameplay.Entities.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour, IItemHolder, IRescueCarrier, IPropHolder
    {
        [SerializeField, FormerlySerializedAs("Head")] private Transform _head;
        [SerializeField, FormerlySerializedAs("WalkSpeed")] private float _walkSpeed = 3.2f;
        [SerializeField, FormerlySerializedAs("RunSpeed")] private float _runSpeed = 5.5f;
        [SerializeField, FormerlySerializedAs("MouseSensitivity")] private float _mouseSensitivity = 0.12f;
        [SerializeField, FormerlySerializedAs("Gravity")] private float _gravity = -18f;

        public Transform Head => _head;
        public float WalkSpeed => _walkSpeed;
        public float RunSpeed => _runSpeed;
        public float MouseSensitivity => _mouseSensitivity;
        public float Gravity => _gravity;
        [SerializeField] private InteractionLogic _interactionLogic = new();
        [SerializeField] private HandsAnimationControl _hands = new();
        [SerializeField] private InventoryLogic _heldItem = new();
        [SerializeField] private ExtinguisherLogic _extinguisher = new();
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

        ClutterItem _prop;

        public event System.Action<ClutterItem> PropChanged;

        public InteractionLogic Interaction => _interactionLogic;
        public InventoryLogic Items => _heldItem;
        public ExtinguisherLogic Extinguisher => _extinguisher;
        public AxeLogic Axe => _axe;
        public CarryLogic Carry => _carry;
        public ItemType HeldItem => _heldItem.HeldItem;

        public bool TakeTool(ToolItem tool) => !_carry.IsCarrying && _prop == null && _heldItem.Take(tool);

        public bool StoreStack(ItemDefinition definition) => _heldItem.AddStack(definition);

        public bool GrabProp(ClutterItem item)
        {
            if (_prop != null || _carry.IsCarrying || !_heldItem.StowHand()) return false;
            _prop = item;
            PropChanged?.Invoke(item);
            _heldItem.SetHidden(true);
            item.SetHeld(Head);
            _interactionLogic.SetOverride(item);
            return true;
        }

        public void PutDownProp() => ReleaseProp(false);

        void ReleaseProp(bool thrown)
        {
            if (_prop == null) return;
            ClutterItem item = _prop;
            _prop = null;
            PropChanged?.Invoke(null);
            _interactionLogic.SetOverride(null);
            _heldItem.SetHidden(false);
            Vector3 start = item.transform.position;
            Vector3 velocity = Vector3.zero;
            if (thrown)
            {
                velocity = Head.forward * _throwSpeed + Vector3.up * _throwLift;
            }
            else
            {
                float reach = _dropReach;
                start = Physics.Raycast(Head.position, Head.forward, out RaycastHit hit, reach, ~0, QueryTriggerInteraction.Ignore)
                    ? hit.point + hit.normal * _dropSurfaceOffset
                    : Head.position + Head.forward * reach;
            }

            item.Release(start, velocity);
        }

        public bool PickUp(Rescuable resident)
        {
            _heldItem.StowHand();
            return _carry.PickUp(resident);
        }

        CharacterController _cc;
        float _pitch, _vy;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _interactionLogic.Init(transform, Head);
            _interactionLogic.SetFilter(target => !_carry.IsCarrying || target is IUsableWhileCarrying);
            _extinguisher.Init(Head, _heldItem);
            _axe.Init(Head, _heldItem);
            _heldItem.ItemChanged += HandleItemChanged;
            _carry.Init(transform);
            _carry.CarryChanged += HandleCarryChanged;
        }

        void OnDestroy()
        {
            _extinguisher.Dispose();
            _heldItem.ItemChanged -= HandleItemChanged;
            _carry.CarryChanged -= HandleCarryChanged;
        }

        void HandleItemChanged(ItemType type) => _hands.SetLeftGrip(_heldItem.HeldTool != null ? _heldItem.HeldTool.LeftGrip : null);

        void HandleCarryChanged(Rescuable resident)
        {
            _heldItem.SetHidden(resident != null || _prop != null);
            _hands.SetVisible(resident == null);
        }

        void OnEnable()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            _pitch = NormalizeAngle(Head.localEulerAngles.x);
            _hands.SetVisible(!_carry.IsCarrying);
        }

        void OnDisable()
        {
            _interactionLogic.Clear();
            _extinguisher.Tick(false, 0f);
            _hands.SetAiming(false);
            _hands.SetVisible(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Update()
        {
            var kb = Keyboard.current; var mouse = Mouse.current;

            Vector2 d = mouse.delta.ReadValue() * MouseSensitivity;
            transform.Rotate(0f, d.x, 0f);
            bool limitPitch = _carry.IsCarrying;
            _pitch = Mathf.Clamp(_pitch - d.y, limitPitch ? _limitedPitchDown : _pitchDown, limitPitch ? _limitedPitchUp : _pitchUp);
            Head.localRotation = Quaternion.Euler(_pitch, 0f, 0f);

            Vector2 input = Vector2.zero;
            if (kb.wKey.isPressed) input.y += 1; if (kb.sKey.isPressed) input.y -= 1;
            if (kb.dKey.isPressed) input.x += 1; if (kb.aKey.isPressed) input.x -= 1;
            bool carrying = _carry.IsCarrying;
            bool holdingProp = _prop != null;
            float speed = carrying ? WalkSpeed * _carry.SpeedMultiplier : kb.leftShiftKey.isPressed ? RunSpeed : WalkSpeed;
            Vector3 move = (transform.forward * input.y + transform.right * input.x).normalized * speed;
            _vy = _cc.isGrounded ? -2f : _vy + Gravity * Time.deltaTime;
            move.y = _vy;
            _cc.Move(move * Time.deltaTime);

            _interactionLogic.Tick();
            IInteractable target = _interactionLogic.Current;
            if (kb.eKey.wasPressedThisFrame && !_hands.IsBusy && target != null && target.CanInteract)
            {
                _hands.Play(target.Gesture, () => _interactionLogic.Interact(target));
            }

            if (kb.qKey.wasPressedThisFrame && !_hands.IsBusy && target is IAltInteractable alt && target.CanInteract)
            {
                _hands.Play(target.Gesture, () => alt.AltInteract(transform));
            }

            if (holdingProp && mouse.leftButton.wasPressedThisFrame && !_hands.IsBusy) ReleaseProp(true);
            else if (!carrying && !holdingProp && _axe.IsReady && mouse.leftButton.wasPressedThisFrame && !_hands.IsBusy) _hands.Play(HandGesture.Chop, _axe.Strike);

            if (carrying && kb.gKey.wasPressedThisFrame && !_hands.IsBusy) _carry.PutDown();
            else if (!holdingProp && kb.gKey.wasPressedThisFrame && !_hands.IsBusy && _heldItem.HeldTool != null) _heldItem.Drop(Head);

            if (!carrying && !holdingProp && kb.digit1Key.wasPressedThisFrame) _heldItem.SelectSlot(0);
            if (!carrying && !holdingProp && kb.digit2Key.wasPressedThisFrame) _heldItem.SelectSlot(1);
            if (!carrying && !holdingProp && kb.digit3Key.wasPressedThisFrame) _heldItem.SelectSlot(2);
            _extinguisher.Tick(mouse.leftButton.isPressed && !_hands.IsBusy && !carrying && !holdingProp, Time.deltaTime);
            _hands.SetAiming(_extinguisher.IsSpraying);
            _hands.SetCarrying(_carry.IsCarrying);

            float planarSpeed = new Vector2(move.x, move.z).magnitude;
            _hands.Tick(Time.deltaTime, Mathf.Clamp01(planarSpeed / RunSpeed), d);
        }

        static float NormalizeAngle(float a) => a > 180f ? a - 360f : a;
    }
}
