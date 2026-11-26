using Gameplay.Dialogue;
using Gameplay.Cinematics.Finale;
using Gameplay.Entities.Characters.Player;
using Gameplay.Entities.Characters.Residents;
using Gameplay.Entities.Interactive;
using Services.Motion;
using Services;
using Services.Audio;
using Unity.Cinemachine;
using UnityEngine;

namespace Gameplay.Cinematics
{
    public sealed class FinaleCutscene : CinematicBase, IFireProgressSource
    {
        private const float AlmostOut = 0.99f;
        private const float FallLead = 0.05f;
        private const int DepthBits = 24;

        [SerializeField] private CinemachineCamera _camera;
        [SerializeField] private PosedActor _stunt;
        [SerializeField] private EvacuationWindow _window;
        [SerializeField] private GameObject _firefighters;
        [SerializeField] private GameObject[] _firefighterGear;
        [SerializeField] private FireSource[] _houseFires;
        [SerializeField] private CinematicSettings _jumpSettings = new(0.4f, 0.4f);
        [SerializeField] private CinematicSettings _settings = new(0.4f, 0f, titleLine: "Пожежу загашено", titleSubLine: "вул. Липова, 12 · усі на вулиці", titleTime: 0.3f, titleDuration: 3.8f);

        [Header("Shots")]
        [SerializeField] private FinaleRunShot _runShot = new();
        [SerializeField] private FinaleUncleScene _uncleScene = new();

        [Header("The explosion behind him")]
        [SerializeField] private GameObject _explosion;
        [SerializeField] private FireSource _blastFire;
        [SerializeField] private float _shakeTime = 0.8f;
        [SerializeField] private float _shakeAmount = 0.25f;
        [SerializeField] private Light _blastLight;
        [SerializeField] private float _flashDuration = 1.2f;
        [SerializeField] private float _flashFlickerFrequency = 40f;
        [SerializeField] private float _flashFlickerAmount = 0.15f;

        [Header("Timing")]
        [SerializeField] private float _run = 1.1f;
        [SerializeField] private float _hang = 0.35f;
        [SerializeField] private float _fall = 1.35f;
        [SerializeField] private float _bounce = 1.0f;
        [SerializeField] private float _landHold = 1.0f;
        [SerializeField] private float _poseHold = 0.9f;

        [Header("Stunt")]
        [SerializeField] private float _fallApex = 1.9f;
        [SerializeField] private float _bounceApex = 2.0f;
        [SerializeField] private float _landingLift = 0.25f;
        [SerializeField] private float _safeOffset = 1.1f;
        [SerializeField] private float _sillLift = 0.1f;
        [SerializeField] private float _sillTilt = -10f;
        [SerializeField] private float _tumbleStart = 0.3f;
        [SerializeField] private float _bounceStrength = 1.3f;
        [SerializeField] private float _settleSpeed = 1.4f;
        [SerializeField] private float _stuntAimHeight = 0.9f;
        [SerializeField] private float _aimSharpness = 7f;
        [SerializeField] private float _landingAimBlend = 0.4f;

        [Header("Subtitles and fire bar")]
        [SerializeField] private float _lastFlamePeeShare = 0.6f;
        [SerializeField] private float _lastFlameFade = 0.5f;
        [SerializeField] private float _safeLineLead = 0.1f;
        [SerializeField] private float _safeLineExtra = 0.6f;
        [SerializeField] private float _uncleLineDelay = 0.3f;
        [SerializeField] private float _uncleLineDuration = 2.6f;
        [SerializeField] private float _orbitLineDelay = 1.2f;
        [SerializeField] private float _orbitLineDuration = 2.2f;

        private SubtitleCue[] _subtitles;
        private Shot _shot;
        private bool _hasBounced;
        private bool _exploded;
        private bool _isEnding;
        private FirstPersonController _player;
        private Vector3 _launch, _landing, _safe, _outward, _shotPosition;
        private Quaternion _safeRotation;
        private RenderTexture _frame;
        private Camera _mainCamera;
        private float _flashPeak;

        private enum Shot
        {
            None,
            Window,
            Uncle,
        }

        public override CinematicSettings Settings => _isEnding ? _settings : _jumpSettings;
        public override SubtitleCue[] Subtitles => _subtitles;
        public Texture CapturedFrame => _frame;

        private float FallTime => _run + _hang;
        private float LandTime => FallTime + _fall;
        private float SafeTime => LandTime + _bounce;
        private float JumpDuration => SafeTime + _landHold;
        private float OrbitTime => _uncleScene.RunIn + _uncleScene.Pee;
        protected override float Duration => _isEnding ? _uncleScene.Duration + _poseHold : JumpDuration;

        public bool IsEnding => _isEnding;
        public FireSource[] HouseFires => _houseFires;

        public float FireProgress
        {
            get
            {
                if (!IsPlaying || !_isEnding)
                {
                    return -1f;
                }

                float lastFlame = _uncleScene.RunIn + _uncleScene.Pee * _lastFlamePeeShare;
                return Mathf.Lerp(AlmostOut, 1f, (ElapsedTime - lastFlame) / _lastFlameFade);
            }
        }

        private void Awake()
        {
            _camera.gameObject.SetActive(false);
            _stunt.gameObject.SetActive(false);
            _firefighters.SetActive(false);
            _uncleScene.Init();
            _flashPeak = _blastLight.intensity;
        }

        public void Init(Camera camera) => _mainCamera = camera;

        public void Play(FirstPersonController player)
        {
            if (!TryBegin())
            {
                return;
            }

            _isEnding = false;
            _player = player;
            _hasBounced = false;
            _exploded = false;
            _shot = Shot.None;
            _launch = _window.LaunchPoint.position;
            _landing = _window.Trampoline.LandingPoint + Vector3.up * _landingLift;
            _safeRotation = _window.Trampoline.SafeRotation;
            _safe = _window.Trampoline.SafePoint + _safeRotation * Vector3.right * _safeOffset;
            _outward = Vector3.ProjectOnPlane(_landing - _launch, Vector3.up).normalized;
            _window.Break(_outward);
            _uncleScene.Prepare();

            Transform head = player.Head;
            _runShot.Begin(head, _launch, _outward);
            player.enabled = false;

            _subtitles = new[]
            {
                new SubtitleCue(SafeTime - _safeLineLead, _landHold + _safeLineExtra, DialogIds.PartnerSuperhero),
            };

            _camera.transform.SetPositionAndRotation(head.position, head.rotation);
            _camera.gameObject.SetActive(true);
            PlayClock();
        }

        public void PlayEnding(FirstPersonController player)
        {
            if (!TryBegin())
            {
                return;
            }

            _isEnding = true;
            _player = player;
            _shot = Shot.Uncle;
            player.enabled = false;
            _firefighters.SetActive(true);
            _stunt.gameObject.SetActive(true);
            _stunt.SetPose(ResidentPose.Animated);
            _stunt.transform.SetPositionAndRotation(_safe, _safeRotation);

            _subtitles = new[]
            {
                new SubtitleCue(_uncleScene.RunIn + _uncleLineDelay, _uncleLineDuration, DialogIds.UncleProfessional),
                new SubtitleCue(OrbitTime + _orbitLineDelay, _orbitLineDuration, DialogIds.UncleSavedEveryone),
            };

            _camera.gameObject.SetActive(true);
            PlayClock();
        }

        public void Release(FirstPersonController player)
        {
            _camera.gameObject.SetActive(false);
            _stunt.gameObject.SetActive(false);
            player.Teleport(_safe, _safeRotation);
        }

        private void Update()
        {
            if (!IsPlaying)
            {
                return;
            }

            float t = ElapsedTime;
            Transform cam = _camera.transform;
            if (_isEnding)
            {
                _uncleScene.Tick(cam, t);
            }
            else if (t < _run)
            {
                _runShot.Tick(cam, t, _run);
            }
            else
            {
                TickStunt(cam, t);
                SetShot(Shot.Window);
                TickWindowShot(cam, t);
            }

            StopAtEnd();
        }

        private void TickStunt(Transform cam, float t)
        {
            _stunt.gameObject.SetActive(true);
            if (t >= FallTime - FallLead)
            {
                Explode();
            }

            Quaternion facing = Quaternion.LookRotation(_outward);
            Quaternion lying = Quaternion.LookRotation(Vector3.up, _outward);
            if (t < FallTime)
            {
                _stunt.SetPose(ResidentPose.Carried);
                _stunt.SetCenterPose(_launch + Vector3.up * _sillLift, facing * Quaternion.Euler(_sillTilt, 0f, 0f));
            }
            else if (t < LandTime)
            {
                float u = (t - FallTime) / _fall;
                _stunt.SetPose(ResidentPose.Flailing);
                _stunt.SetCenterPose(Arc(_launch, _landing, _fallApex, u), Quaternion.Slerp(facing, lying, Easing.Smooth(Mathf.InverseLerp(_tumbleStart, 1f, u))));
            }
            else if (t < SafeTime)
            {
                if (!_hasBounced)
                {
                    _hasBounced = true;
                    _window.Trampoline.Bounce(_bounceStrength);
                }

                float contact = _window.Trampoline.ContactDuration;
                float since = t - LandTime;
                if (since < contact)
                {
                    _stunt.SetCenterPose(_landing + Vector3.up * _window.Trampoline.SampleDent(_landing), lying);
                }
                else
                {
                    float u = (since - contact) / (_bounce - contact);
                    _stunt.SetCenterPose(Arc(_landing, _safe + Vector3.up * _stunt.BodyCenterHeight, _bounceApex, u),
                        Quaternion.Slerp(lying, _safeRotation, Easing.Smooth(u * _settleSpeed)));
                }
            }
            else
            {
                _stunt.SetPose(ResidentPose.Animated);
                _stunt.transform.SetPositionAndRotation(_safe, _safeRotation);
            }
        }

        private void TickWindowShot(Transform cam, float t)
        {
            if (_exploded)
            {
                float since = t - FallTime;
                float shake = since >= 0f && since < _shakeTime ? (1f - since / _shakeTime) * _shakeAmount : 0f;
                cam.position = _shotPosition + Random.insideUnitSphere * shake;
            }

            Vector3 target = _stunt.transform.position + Vector3.up * _stuntAimHeight;
            cam.rotation = Quaternion.Slerp(cam.rotation, Quaternion.LookRotation(target - cam.position, Vector3.up), Easing.Damp(_aimSharpness, Time.deltaTime));
        }

        private void SetShot(Shot shot)
        {
            if (_shot == shot)
            {
                return;
            }

            _shot = shot;
            Transform cam = _camera.transform;
            if (shot == Shot.Window)
            {
                cam.position = _window.CameraAnchor.position;
                _shotPosition = cam.position;
                cam.rotation = Quaternion.LookRotation(Vector3.Lerp(_launch, _landing, _landingAimBlend) - cam.position, Vector3.up);
            }
        }

        private void Explode()
        {
            if (_exploded)
            {
                return;
            }

            _exploded = true;
            _explosion.SetActive(false);
            _explosion.SetActive(true);
            AudioService.Instance.Play(SoundType.FinaleBoom);
            _blastFire.gameObject.SetActive(true);
            MotionService.Instance.Play(_flashDuration, Flash);
        }

        private void Flash(float progress)
        {
            float fade = (1f - progress) * (1f - progress);
            float flicker = 1f + Mathf.Sin(progress * _flashDuration * _flashFlickerFrequency) * _flashFlickerAmount * (1f - progress);
            _blastLight.intensity = _flashPeak * fade * flicker;
        }

        private void CaptureFrame()
        {
            if (_frame == null)
            {
                _frame = new RenderTexture(Screen.width, Screen.height, DepthBits);
            }

            RenderTexture previous = _mainCamera.targetTexture;
            _mainCamera.targetTexture = _frame;
            _mainCamera.Render();
            _mainCamera.targetTexture = previous;
        }

        protected override void OnStopped()
        {
            if (!_isEnding)
            {
                Release(_player);
                _player.enabled = true;
                return;
            }

            _uncleScene.Finish();
            CaptureFrame();
            _uncleScene.StandIdle();
            foreach (GameObject gear in _firefighterGear)
            {
                gear.SetActive(false);
            }

            foreach (FireSource fire in _houseFires)
            {
                fire.SetReignite(false);
                fire.ForceOut();
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_frame != null)
            {
                _frame.Release();
            }
        }
    }
}
