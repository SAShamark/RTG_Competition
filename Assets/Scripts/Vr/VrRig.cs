using System.Collections.Generic;
using Gameplay.Entities.Characters.Player;
using Services;
using UI.Managers;
using UI.Popups;
using UI.Screens.Variants.Gameplay.Widgets;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;
using Vr.Hands;

namespace Vr
{
    [DefaultExecutionOrder(-1000)]
    public sealed class VrRig : MonoBehaviour
    {
        [Header("Tracking")]
        [SerializeField] private Transform _origin;
        [SerializeField] private Transform _cameraOffset;
        [SerializeField] private Camera _headCamera;
        [SerializeField] private Transform _rightGrip;
        [SerializeField] private Transform _leftGrip;
        [SerializeField] private Transform _rightAim;
        [SerializeField] private TrackedPoseDriver _rightAimDriver;
        [SerializeField, Min(0.5f)] private float _eyeHeight = 1.65f;
        [SerializeField, Min(0f)] private float _calibrationDelay = 0.5f;
        [SerializeField, Min(0f)] private float _followDeadzone = 0.01f;
        [SerializeField, Min(0f)] private float _groundStick = 0.02f;

        [Header("Player")]
        [SerializeField] private FirstPersonController _player;
        [SerializeField] private CharacterController _characterController;
        [SerializeField] private Transform _head;
        [SerializeField] private Transform _carrySocket;
        [SerializeField] private VrHandsLogic _hands = new();
        [SerializeField] private VrWristDisplay _wrist;
        [SerializeField] private VrRadio _radio;

        [Header("Turning")]
        [SerializeField, Range(0.1f, 1f)] private float _turnThreshold = 0.7f;
        [SerializeField, Min(10f)] private float _smoothTurnSpeed = 120f;
        [SerializeField, Range(0f, 1f)] private float _turnRearm = 0.3f;

        [Header("Cameras and UI")]
        [SerializeField] private Camera _brainCamera;
        [SerializeField] private CinemachineBrain _brain;
        [SerializeField] private CinemachineCamera _playerCamera;
        [SerializeField] private Camera _uiCamera;
        [SerializeField] private RenderTexture _cinemaTexture;
        [SerializeField] private Canvas _uiCanvas;
        [SerializeField] private Canvas[] _cinemaCanvases;
        [SerializeField, Min(0.01f)] private float _uiCanvasDistance = 1f;
        [SerializeField, Min(0.01f)] private float _cinemaCanvasDistance = 0.1f;
        [SerializeField] private LayerMask _screenLayer;
        [SerializeField] private Renderer _heatOverlay;
        [SerializeField, Range(0f, 0.1f)] private float _heatVisibleAlpha = 0.005f;
        [SerializeField, Range(0f, 1f)] private float _heatPulseAlpha = 0.25f;
        [SerializeField, Range(0f, 1f)] private float _heatPulseAmplitude = 0.35f;
        [SerializeField, Min(0.01f)] private float _heatPulseDuration = 0.08f;
        [SerializeField, Min(0.1f)] private float _heatPulseInterval = 0.9f;
        [SerializeField, Range(0.1f, 1f)] private float _heatPulseFastest = 0.5f;
        [SerializeField, Range(0f, 1f)] private float _heatPulseSoftest = 0.5f;
        [SerializeField] private VrScreenLogic _screen = new();
        [SerializeField] private VrPointerLogic _pointer = new();
        [SerializeField] private VrInputLogic _input = new();
        [SerializeField] private VrComfortVignette _vignette = new();

        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private readonly List<XRInputSubsystem> _inputSubsystems = new();
        private UniversalAdditionalCameraData _brainData;
        private UniversalAdditionalCameraData _headData;
        private MaterialPropertyBlock _heatBlock;
        private HeatVignetteWidget _heat;
        private IPopupsManager _popups;
        private int _worldMask;
        private int _brainMask;
        private bool _brainPostProcessing;
        private float _calibrationTimer;
        private bool _isCalibrated;
        private bool _isTurnArmed = true;
        private float _turnRate;
        private float _heatPulseTimer;

        public VrHandsLogic Hands => _hands;
        public VrInputLogic Input => _input;
        public VrWristDisplay Wrist => _wrist;
        public VrRadio Radio => _radio;
        public Transform TrackingSpace => _cameraOffset;
        public Transform Head => _headCamera.transform;
        public Transform RightAim => AimSource;

        public Vector3 MoveForward
        {
            get
            {
                Transform source = VrComfort.MoveDirection == VrMoveDirection.Hand ? _leftGrip : _headCamera.transform;
                Vector3 forward = Vector3.ProjectOnPlane(source.forward, Vector3.up);
                return forward.sqrMagnitude > Mathf.Epsilon ? forward.normalized : _player.transform.forward;
            }
        }

        private Transform AimSource => _rightAimDriver.positionInput.action is { controls: { Count: > 0 } } ? _rightAim : _rightGrip;

        private bool IsCinema => _brain.IsBlending || _brain.ActiveVirtualCamera != (ICinemachineCamera)_playerCamera;

        private void Awake()
        {
            if (!VrBootstrap.IsXrLoaded)
            {
                Debug.LogWarning("[VR] XR is not running: no headset found. The rig works only with simulated devices.");
            }

            GameInput.Vr = _input;
            GameInput.Pointer = _pointer;
            _input.Enable();
            _pointer.Initialize();
            SetupCameras();
            SetupCanvas(_uiCanvas, _uiCamera, _uiCanvasDistance);
            foreach (Canvas canvas in _cinemaCanvases)
            {
                SetupCanvas(canvas, _brainCamera, _cinemaCanvasDistance);
            }

            _screen.Initialize(_headCamera.transform);
            ApplyCameras(false);
            _player.Teleported += HandleTeleported;
            RequestFloorTracking();
            SetupHands();
            _vignette.Initialize();
            _wrist.Initialize(_headCamera.transform, _input);
        }

        private void Start()
        {
            _popups = UIManager.Instance.PopupsManager;
        }

        private void OnDestroy()
        {
            _input.Disable();
            _player.Teleported -= HandleTeleported;
            Application.onBeforeRender -= _hands.ApplyHolds;
            foreach (XRInputSubsystem subsystem in _inputSubsystems)
            {
                subsystem.trackingOriginUpdated -= HandleTrackingOriginUpdated;
            }
        }

        private void Update()
        {
            TickCalibration(Time.unscaledDeltaTime);
            bool isPlayerActive = _player.isActiveAndEnabled;
            bool isPlaying = isPlayerActive && _screen.Mode == VrScreenMode.Visor;
            _turnRate = 0f;
            if (isPlaying)
            {
                TickTurn(Time.deltaTime);
            }

            if (isPlayerActive)
            {
                SyncBody();
            }

            UpdateScreen();
            _hands.Tick(Time.deltaTime, _input);
            _vignette.Tick(_headCamera.transform, isPlaying ? _player.MoveSpeed : 0f, _turnRate, Time.unscaledDeltaTime);
        }

        private void FixedUpdate()
        {
            _hands.FixedTick(Time.fixedDeltaTime);
        }

        private void LateUpdate()
        {
            _hands.ApplyHolds();
            UpdateHeatOverlay();
        }

        public void SetCurtain(float amount) => _vignette.SetCurtain(amount);

        public void RenderWorld(RenderTexture target)
        {
            RenderTexture previousTarget = _brainCamera.targetTexture;
            int previousMask = _brainCamera.cullingMask;
            _brainCamera.targetTexture = target;
            _brainCamera.cullingMask = _brainMask;
            _brainCamera.Render();
            _brainCamera.targetTexture = previousTarget;
            _brainCamera.cullingMask = previousMask;
        }

        public void Recenter()
        {
            _isCalibrated = false;
            _calibrationTimer = _calibrationDelay;
            AlignOriginToBody();
        }

        private void SetupCameras()
        {
            _brainData = _brainCamera.GetUniversalAdditionalCameraData();
            _headData = _headCamera.GetUniversalAdditionalCameraData();
            _brainPostProcessing = _brainData.renderPostProcessing;
            _brainMask = _brainCamera.cullingMask & ~_screenLayer.value;
            _uiCamera.cullingMask = ~_screenLayer.value;
            _worldMask = _brainMask | _screenLayer.value;
            _brainCamera.targetTexture = _cinemaTexture;
            _headCamera.farClipPlane = _brainCamera.farClipPlane;
            _headCamera.allowHDR = _brainCamera.allowHDR;
            LensSettings lens = _playerCamera.Lens;
            lens.FieldOfView = _screen.VisorFieldOfView;
            _playerCamera.Lens = lens;
        }

        private void ApplyCameras(bool isCinema)
        {
            _headCamera.cullingMask = isCinema ? _screenLayer.value : _worldMask;
            _headCamera.clearFlags = isCinema ? CameraClearFlags.SolidColor : CameraClearFlags.Skybox;
            _headCamera.backgroundColor = Color.black;
            _brainCamera.cullingMask = isCinema ? _brainMask : 0;
            _brainData.renderPostProcessing = isCinema && _brainPostProcessing;
            _headData.renderPostProcessing = !isCinema;
        }

        private void UpdateScreen()
        {
            bool isPopupOpen = _popups.OpenCount > 0;
            VrScreenMode mode = IsCinema ? VrScreenMode.Cinema : isPopupOpen ? VrScreenMode.Panel : VrScreenMode.Visor;
            if (mode != _screen.Mode)
            {
                _screen.SetMode(mode, _headCamera.transform);
                ApplyCameras(mode == VrScreenMode.Cinema);
            }

            _screen.Tick(_headCamera.transform);

            bool isPointing = mode == VrScreenMode.Panel || (mode == VrScreenMode.Cinema && isPopupOpen);
            bool isTouchPressed = _input.IsPrimaryHeld || _input.IsGripHeld(false);
            _pointer.Tick(isPointing, AimSource, _screen.UiQuad, _screen.Resolution, _input.IsPrimaryHeld, _hands.Right.Grip, isTouchPressed);
        }

        private void TickTurn(float deltaTime)
        {
            float turn = _input.Turn;
            if (VrComfort.TurnMode == VrTurnMode.Smooth)
            {
                TickSmoothTurn(turn, deltaTime);
                return;
            }

            if (!_isTurnArmed)
            {
                _isTurnArmed = Mathf.Abs(turn) < _turnRearm;
                return;
            }

            if (Mathf.Abs(turn) < _turnThreshold)
            {
                return;
            }

            _isTurnArmed = false;
            _origin.RotateAround(_headCamera.transform.position, Vector3.up, Mathf.Sign(turn) * VrComfort.SnapAngle);
        }

        private void TickSmoothTurn(float turn, float deltaTime)
        {
            if (Mathf.Abs(turn) < _turnRearm)
            {
                return;
            }

            _turnRate = turn * _smoothTurnSpeed;
            _origin.RotateAround(_headCamera.transform.position, Vector3.up, _turnRate * deltaTime);
        }

        private void SyncBody()
        {
            Transform camera = _headCamera.transform;
            Vector3 forward = Vector3.ProjectOnPlane(camera.forward, Vector3.up);
            Vector3 originPosition = _origin.position;
            Quaternion originRotation = _origin.rotation;
            if (forward.sqrMagnitude > Mathf.Epsilon)
            {
                _player.transform.rotation = Quaternion.LookRotation(forward);
            }

            Vector3 offset = camera.position - _player.transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude > _followDeadzone * _followDeadzone)
            {
                _characterController.Move(offset + Vector3.down * _groundStick);
            }

            _origin.SetPositionAndRotation(originPosition, originRotation);

            _head.SetPositionAndRotation(camera.position, camera.rotation);
            _carrySocket.SetPositionAndRotation(_player.transform.position, _player.transform.rotation);
        }

        private void SetupHands()
        {
            _hands.Initialize(_leftGrip, _rightGrip, _player.GetComponentsInChildren<Collider>(true));
            Application.onBeforeRender += _hands.ApplyHolds;
        }

        private void UpdateHeatOverlay()
        {
            if (_heat == null)
            {
                _heat = _uiCanvas.GetComponentInChildren<HeatVignetteWidget>(true);
                if (_heat == null)
                {
                    return;
                }

                _heatBlock = new MaterialPropertyBlock();
                _heatBlock.SetTexture(MainTexId, _heat.Texture);
            }

            Color color = _heat.Color;
            bool isVisible = _heat.isActiveAndEnabled && color.a > _heatVisibleAlpha && _screen.Mode == VrScreenMode.Visor;
            _heatOverlay.enabled = isVisible;
            TickHeatPulse(isVisible ? color.a : 0f);
            if (isVisible)
            {
                _heatBlock.SetColor(ColorId, color);
                _heatOverlay.SetPropertyBlock(_heatBlock);
            }
        }

        private void TickHeatPulse(float heat)
        {
            _heatPulseTimer -= Time.deltaTime;
            if (heat < _heatPulseAlpha || _heatPulseTimer > 0f)
            {
                return;
            }

            // Pulses come faster and stronger as the heat rises past the threshold.
            float strength = Mathf.InverseLerp(_heatPulseAlpha, 1f, heat);
            _heatPulseTimer = _heatPulseInterval * Mathf.Lerp(1f, _heatPulseFastest, strength);
            float amplitude = _heatPulseAmplitude * Mathf.Lerp(_heatPulseSoftest, 1f, strength);
            _input.Pulse(true, amplitude, _heatPulseDuration);
            _input.Pulse(false, amplitude, _heatPulseDuration);
        }

        private void AlignOriginToBody()
        {
            Transform camera = _headCamera.transform;
            Vector3 forward = Vector3.ProjectOnPlane(camera.forward, Vector3.up);
            if (forward.sqrMagnitude > Mathf.Epsilon)
            {
                float turn = Vector3.SignedAngle(forward, _player.transform.forward, Vector3.up);
                _origin.RotateAround(camera.position, Vector3.up, turn);
            }

            Vector3 offset = _player.transform.position - camera.position;
            offset.y = 0f;
            _origin.position += offset;
            _head.SetPositionAndRotation(camera.position, camera.rotation);
        }

        private void TickCalibration(float deltaTime)
        {
            if (_isCalibrated)
            {
                return;
            }

            _calibrationTimer -= deltaTime;
            if (_calibrationTimer > 0f)
            {
                return;
            }

            Vector3 tracked = _headCamera.transform.localPosition;
            if (tracked.sqrMagnitude < Mathf.Epsilon)
            {
                return;
            }

            _isCalibrated = true;
            float eye = tracked.y;
            _cameraOffset.localPosition = Vector3.up * (_eyeHeight - eye);
        }

        private void RequestFloorTracking()
        {
            SubsystemManager.GetSubsystems(_inputSubsystems);
            foreach (XRInputSubsystem subsystem in _inputSubsystems)
            {
                subsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
                subsystem.trackingOriginUpdated += HandleTrackingOriginUpdated;
            }

            _calibrationTimer = _calibrationDelay;
        }

        private void HandleTrackingOriginUpdated(XRInputSubsystem _) => Recenter();

        private void HandleTeleported()
        {
            AlignOriginToBody();
            _vignette.Blink();
        }

        private static void SetupCanvas(Canvas canvas, Camera camera, float distance)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = distance;
        }

    }
}
