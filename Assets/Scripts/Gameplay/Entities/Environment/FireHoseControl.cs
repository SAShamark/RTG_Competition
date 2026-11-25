using Gameplay.Entities.Interactive;
using Gameplay.Entities.Interactive.Items;
using Services;
using UnityEngine;

namespace Gameplay.Entities.Environment
{
    [DefaultExecutionOrder(200)]
    public sealed class FireHoseControl : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private Transform _outlet;
        [SerializeField] private HoseOutlet _port;
        [SerializeField] private PipeValve _valve;
        [SerializeField] private HoseNozzleItem _nozzle;
        [SerializeField] private GameObject _storedRoll;
        [SerializeField] private Hatch _lockerDoor;
        [SerializeField] private Collider _nozzleGrip;
        [SerializeField] private Collider _lockerDoorGrip;

        [Header("Step lamps")]
        [SerializeField] private Renderer _lockerLamp;
        [SerializeField] private Renderer _portLamp;
        [SerializeField] private Renderer _valveLamp;
        [SerializeField, ColorUsage(false, true)] private Color _pendingColor = new(2.5f, 0.25f, 0.1f);
        [SerializeField, ColorUsage(false, true)] private Color _doneColor = new(0.2f, 2.2f, 0.4f);
        [SerializeField] private float _blinkFrequency = 2.5f;
        [SerializeField, Range(0f, 1f)] private float _idleGlow = 0.25f;
        [SerializeField] private float _endSmoothing = 12f;
        [SerializeField] private float _reelInSpeed = 1.5f;
        [SerializeField] private float _heldSlack = 1.12f;
        [SerializeField] private float _droppedSlack = 1.02f;

        [Header("Pressure")]
        [SerializeField] private float _chargeSpeed = 9f;
        [SerializeField] private float _drainSpeed = 14f;
        [SerializeField] private float _pressureRate = 2.5f;
        [SerializeField] private float _frontSlack = 0.3f;

        [Header("Pulling a dropped hose back")]
        [SerializeField] private float _retractDelay = 1f;
        [SerializeField] private float _retractSpeed = 5f;
        [SerializeField] private float _parkLength = 3f;
        [SerializeField] private float _dragTurnSharpness = 8f;
        [SerializeField] private Vector3 _dragEuler = new(0f, -90f, 0f);

        [Header("Hose")]
        [SerializeField, Range(0f, 1f)] private float _endBackShare = 0.35f;
        [SerializeField] private HoseRope _rope = new();
        [SerializeField] private HoseRopeView _view = new();

        private ToolItem _tool;
        private Rigidbody _nozzleBody;
        private float _sinceDropped;
        private bool _isRetracting;
        private Vector3 _end;
        private float _chargedLength;
        private float _supply;
        private bool _isHoseOut;
        private MaterialPropertyBlock _lampBlock;

        public bool IsConnected => _port.IsConnected;
        public float Supply => _supply;

        // The hose drops away below the nozzle rather than straight back, so it never loops up into a first-person view.
        private Vector3 EndDirection => (Vector3.down - _nozzle.transform.right * _endBackShare).normalized;

        private void Awake()
        {
            _lampBlock = new MaterialPropertyBlock();
            _tool = _nozzle.GetComponent<ToolItem>();
            _end = _nozzle.HoseSocket.position;
            _nozzleBody = _nozzle.GetComponent<Rigidbody>();
            _rope.Init(_outlet.position, _end, transform);
            _view.Init(_rope);
            _view.SetVisible(false);
            _port.Setup(() => _tool.IsTaken);
            _port.Connected += HandleConnected;
            _tool.Taken += HandleNozzleTaken;
            _nozzleGrip.enabled = false;
            _lockerDoor.Opened += HandleLockerOpened;
        }

        private void OnDestroy()
        {
            _port.Connected -= HandleConnected;
            _tool.Taken -= HandleNozzleTaken;
            _lockerDoor.Opened -= HandleLockerOpened;
            _view.Dispose();
        }

        private void Update()
        {
            TickLamps();
            if (!_port.IsConnected)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            bool isHeld = _tool.IsTaken;
            TickRetract(isHeld, deltaTime);
            // The hand shakes while spraying; the rope follows a smoothed end so the tremor never whips down the hose.
            _end = _isRetracting
                ? _nozzle.HoseSocket.position
                : Vector3.Lerp(_end, _nozzle.HoseSocket.position, Easing.Damp(_endSmoothing, deltaTime));
            _rope.Tick(_outlet.position, _end, isHeld ? EndDirection : Vector3.zero, _isRetracting ? _retractSpeed : _reelInSpeed, isHeld ? _heldSlack : _droppedSlack, deltaTime);
            _nozzle.SetTetherPull(_rope.PullDirection, isHeld ? _rope.Tension : 0f);
            TickPressure(deltaTime);
        }

        private void LateUpdate()
        {
            if (_port.IsConnected)
            {
                _view.Show(_rope, _nozzle.HoseSocket.position, _tool.IsTaken ? EndDirection : -_nozzle.transform.right, _outlet.right, _chargedLength, Time.deltaTime);
            }
        }

        // A hose left lying is hauled back towards the truck: the nozzle is dragged along the line of the hose itself,
        // tail first, until only a short length is left out.
        private void TickRetract(bool isHeld, float deltaTime)
        {
            _sinceDropped = isHeld ? 0f : _sinceDropped + deltaTime;
            bool isRetracting = !isHeld && _sinceDropped >= _retractDelay && _rope.Length > _parkLength;
            if (isRetracting != _isRetracting)
            {
                _isRetracting = isRetracting;
                if (!isHeld)
                {
                    _nozzleBody.isKinematic = isRetracting;
                }
            }

            if (!isRetracting)
            {
                return;
            }

            Transform nozzle = _nozzle.transform;
            Vector3 socket = _nozzle.HoseSocket.position;
            Vector3 target = _rope.PointAlong(_retractSpeed * deltaTime);
            Vector3 heading = Vector3.ProjectOnPlane(socket - target, Vector3.up);
            if (heading.sqrMagnitude > 0f)
            {
                Quaternion facing = Quaternion.LookRotation(heading, Vector3.up) * Quaternion.Euler(_dragEuler);
                nozzle.rotation = Quaternion.Slerp(nozzle.rotation, facing, Easing.Damp(_dragTurnSharpness, deltaTime));
            }

            nozzle.position += target - _nozzle.HoseSocket.position;
        }

        // Water takes time to run down the hose: the charged front travels from the truck, and only then the nozzle gets pressure.
        private void TickPressure(float deltaTime)
        {
            float opening = Mathf.Clamp01(_valve.Turns / (float)_valve.TargetTurns);
            float length = _rope.Length;
            _chargedLength = opening > 0f
                ? Mathf.Min(length, _chargedLength + _chargeSpeed * deltaTime)
                : Mathf.Max(0f, _chargedLength - _drainSpeed * deltaTime);

            bool hasArrived = _chargedLength >= length - _frontSlack;
            _supply = Mathf.MoveTowards(_supply, hasArrived ? opening : 0f, _pressureRate * deltaTime);
            _nozzle.SetSupply(_supply);
        }

        private void HandleConnected()
        {
            _end = _nozzle.HoseSocket.position;
            _rope.Lay(_outlet.position, _end);
            _view.Snap();
            _view.SetVisible(true);
        }

        // Each lamp is green once its step is done; of the rest, the next one to do blinks and the later ones only glow.
        private void TickLamps()
        {
            bool isValveOpen = _valve.Turns > 0;
            float blink = 0.5f + 0.5f * Mathf.Sin(Time.time * _blinkFrequency * Mathf.PI * 2f);
            SetLamp(_lockerLamp, _isHoseOut, true, blink);
            SetLamp(_portLamp, _port.IsConnected, _isHoseOut, blink);
            SetLamp(_valveLamp, isValveOpen, _port.IsConnected, blink);
        }

        private void SetLamp(Renderer lamp, bool isDone, bool isNext, float blink)
        {
            Color color = isDone ? _doneColor : _pendingColor * (isNext ? blink : _idleGlow);
            _lampBlock.SetColor(BaseColorId, color);
            _lampBlock.SetColor(EmissionColorId, color);
            lamp.SetPropertyBlock(_lampBlock);
        }

        private void HandleNozzleTaken(ItemPickup item)
        {
            _isHoseOut = true;
            _storedRoll.SetActive(false);
        }

        private void HandleLockerOpened(Hatch door)
        {
            _lockerDoor.Opened -= HandleLockerOpened;
            _nozzleGrip.enabled = true;
            _lockerDoorGrip.enabled = false;
        }
    }
}
