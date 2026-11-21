using Game.Interaction;
using Game.Items;
using Game.Items.Extinguisher;
using Game.Player;
using Game.Rescue;
using Game.World;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour, IItemHolder, IRescueCarrier, IPropHolder
{
    public Transform Head;
    public float WalkSpeed = 3.2f;
    public float RunSpeed = 5.5f;
    public float MouseSensitivity = 0.12f;
    public float Gravity = -18f;
    [SerializeField] private InteractionLogic _interactionLogic = new();
    [SerializeField] private HandsAnimationControl _hands = new();
    [SerializeField] private HeldItemLogic _heldItem = new();
    [SerializeField] private ExtinguisherLogic _extinguisher = new();
    [SerializeField] private CarryLogic _carry = new();
    [SerializeField] private float _throwSpeed = 6.5f;

    ClutterItem _prop;

    public InteractionLogic Interaction => _interactionLogic;
    public HeldItemLogic Items => _heldItem;
    public ExtinguisherLogic Extinguisher => _extinguisher;
    public CarryLogic Carry => _carry;
    public ItemType HeldItem => _heldItem.HeldItem;

    public bool TakeItem(Pickable pickable) => !_carry.IsCarrying && _prop == null && _heldItem.Take(pickable);

    public bool GrabProp(ClutterItem item)
    {
        if (_prop != null || _carry.IsCarrying) return false;
        _prop = item;
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
        _interactionLogic.SetOverride(null);
        _heldItem.SetHidden(false);
        Vector3 start = item.transform.position;
        Vector3 velocity = Vector3.zero;
        if (thrown)
        {
            velocity = Head.forward * _throwSpeed + Vector3.up * 1.5f;
        }
        else
        {
            const float reach = 1.4f;
            start = Physics.Raycast(Head.position, Head.forward, out RaycastHit hit, reach, ~0, QueryTriggerInteraction.Ignore)
                ? hit.point + hit.normal * 0.15f
                : Head.position + Head.forward * reach;
        }

        item.Release(start, velocity);
    }

    public bool PickUp(Rescuable resident) => _carry.PickUp(resident);

    CharacterController _cc;
    float _pitch, _vy;

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _interactionLogic.Init(transform, Head);
        _interactionLogic.SetFilter(target => !_carry.IsCarrying || target is IUsableWhileCarrying);
        _extinguisher.Init(Head, _heldItem);
        _carry.Init(transform);
        _carry.OnCarryChanged += HandleCarryChanged;
    }

    void OnDestroy()
    {
        _extinguisher.Dispose();
        _carry.OnCarryChanged -= HandleCarryChanged;
    }

    void HandleCarryChanged(Rescuable resident) => _heldItem.SetHidden(resident != null || _prop != null);

    void OnEnable()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        _pitch = NormalizeAngle(Head.localEulerAngles.x);
        _hands.SetVisible(true);
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
        _pitch = Mathf.Clamp(_pitch - d.y, limitPitch ? -15f : -80f, limitPitch ? 28f : 80f);
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

        if (carrying && kb.gKey.wasPressedThisFrame && !_hands.IsBusy) _carry.PutDown();

        if (!carrying && !holdingProp && kb.digit1Key.wasPressedThisFrame) _heldItem.Select(ItemType.Crowbar);
        if (!carrying && !holdingProp && kb.digit3Key.wasPressedThisFrame) _heldItem.Select(ItemType.Extinguisher);
        _extinguisher.Tick(mouse.leftButton.isPressed && !_hands.IsBusy && !carrying && !holdingProp, Time.deltaTime);
        _hands.SetAiming(_extinguisher.IsSpraying);
        _hands.SetCarrying(_carry.IsCarrying);

        float planarSpeed = new Vector2(move.x, move.z).magnitude;
        _hands.Tick(Time.deltaTime, Mathf.Clamp01(planarSpeed / RunSpeed), d);
    }

    static float NormalizeAngle(float a) => a > 180f ? a - 360f : a;
}
