using Gameplay.Dialogue;
using Gameplay.Cinematics.Finale;
using Gameplay.Entities.Characters.Player;
using Gameplay.Entities.Interactive;
using Services.Motion;
using Services.Audio;
using UnityEngine;

namespace Gameplay.Cinematics
{
    public sealed class FinaleCutscene : CinematicBase, IFireProgressSource
    {
        private const float AlmostOut = 0.99f;
        private const int DepthBits = 24;
        private const float GroundProbe = 5f;

        [SerializeField] private EvacuationWindow _window;
        [SerializeField] private GameObject _firefighters;
        [SerializeField] private GameObject[] _firefighterGear;
        [SerializeField] private FireSource[] _houseFires;
        [SerializeField] private CinematicSettings _jumpSettings = new(0.4f, 0.4f);
        [SerializeField] private CinematicSettings _settings = new(0.4f, 0f, titleLine: "Пожежу загашено", titleSubLine: "вул. Липова, 12 · усі на вулиці", titleTime: 0.3f, titleDuration: 3.8f);

        [Header("Shots")]
        [SerializeField] private FinaleUncleScene _uncleScene = new();

        [Header("The explosion behind him")]
        [SerializeField] private GameObject _explosion;
        [SerializeField] private FireSource _blastFire;
        [SerializeField] private Light _blastLight;
        [SerializeField] private float _flashDuration = 1.2f;
        [SerializeField] private float _flashFlickerFrequency = 40f;
        [SerializeField] private float _flashFlickerAmount = 0.15f;
        [SerializeField, Range(0f, 1f)] private float _blastPulse = 1f;
        [SerializeField, Min(0.01f)] private float _blastPulseDuration = 0.45f;

        [Header("Jump (fade through black)")]
        [SerializeField] private float _blastTime = 0.25f;
        [SerializeField] private float _fadeOut = 0.6f;
        [SerializeField] private float _blackHold = 0.9f;
        [SerializeField, Range(0f, 1f)] private float _arriveShare = 0.5f;
        [SerializeField] private float _fadeIn = 0.8f;
        [SerializeField] private float _afterFade = 1.6f;
        [SerializeField] private float _safeOffset = 1.1f;
        [SerializeField] private float _poseHold = 0.9f;

        [Header("Subtitles and fire bar")]
        [SerializeField] private float _lastFlamePeeShare = 0.6f;
        [SerializeField] private float _lastFlameFade = 0.5f;
        [SerializeField] private float _safeLineDelay = 0.3f;
        [SerializeField] private float _safeLineDuration = 2f;
        [SerializeField] private float _uncleLineDelay = 0.3f;
        [SerializeField] private float _uncleLineDuration = 2.6f;
        [SerializeField] private float _bowLineDelay = 1.2f;
        [SerializeField] private float _bowLineDuration = 2.2f;

        private SubtitleCue[] _subtitles;
        private bool _exploded;
        private bool _hasArrived;
        private bool _isEnding;
        private FirstPersonController _player;
        private Vector3 _safe;
        private Quaternion _safeRotation;
        private RenderTexture _frame;
        private float _flashPeak;

        public override CinematicSettings Settings => _isEnding ? _settings : _jumpSettings;
        public override SubtitleCue[] Subtitles => _subtitles;
        public Texture CapturedFrame => _frame;

        private float BlackTime => _fadeOut + _blackHold;
        private float ArriveTime => _fadeOut + _blackHold * _arriveShare;
        private float JumpDuration => BlackTime + _fadeIn + _afterFade;
        private float BowTime => _uncleScene.RunIn + _uncleScene.Pee;
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
            _firefighters.SetActive(false);
            _uncleScene.Init();
            _flashPeak = _blastLight.intensity;
        }

        public void Play(FirstPersonController player)
        {
            if (!TryBegin())
            {
                return;
            }

            _isEnding = false;
            _player = player;
            _exploded = false;
            _hasArrived = false;
            _safeRotation = _window.Trampoline.SafeRotation;
            _safe = _window.Trampoline.SafePoint + _safeRotation * Vector3.right * _safeOffset;
            Vector3 outward = Vector3.ProjectOnPlane(_window.Trampoline.LandingPoint - _window.LaunchPoint.position, Vector3.up).normalized;
            _window.Break(outward);
            _uncleScene.Prepare();
            player.enabled = false;

            _subtitles = new[]
            {
                new SubtitleCue(BlackTime + _safeLineDelay, _safeLineDuration, DialogIds.PartnerSuperhero),
            };

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
            player.enabled = false;
            _firefighters.SetActive(true);
            player.Teleport(GroundBelow(_uncleScene.ViewSpot.position), Facing(_uncleScene.ViewSpot.position, _uncleScene.Spot.position));

            _subtitles = new[]
            {
                new SubtitleCue(_uncleScene.RunIn + _uncleLineDelay, _uncleLineDuration, DialogIds.UncleProfessional),
                new SubtitleCue(BowTime + _bowLineDelay, _bowLineDuration, DialogIds.UncleSavedEveryone),
            };

            PlayClock();
        }

        public void Release(FirstPersonController player) => player.Teleport(_safe, _safeRotation);

        private void Update()
        {
            if (!IsPlaying)
            {
                return;
            }

            float t = ElapsedTime;
            if (_isEnding)
            {
                _uncleScene.Tick(_player.Head, t);
            }
            else
            {
                TickJump(t);
            }

            StopAtEnd();
        }

        private void TickJump(float t)
        {
            if (t >= _blastTime)
            {
                Explode();
            }

            if (!_hasArrived && t >= ArriveTime)
            {
                _hasArrived = true;
                Release(_player);
            }

            _player.Rig.SetCurtain(Curtain(t));
        }

        private float Curtain(float t)
        {
            if (t < _fadeOut)
            {
                return t / _fadeOut;
            }

            return t < BlackTime ? 1f : 1f - (t - BlackTime) / _fadeIn;
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
            _player.Rig.Input.Pulse(true, _blastPulse, _blastPulseDuration);
            _player.Rig.Input.Pulse(false, _blastPulse, _blastPulseDuration);
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

            _player.Rig.RenderWorld(_frame);
        }

        protected override void OnStopped()
        {
            if (!_isEnding)
            {
                Explode();
                if (!_hasArrived)
                {
                    Release(_player);
                }

                _player.Rig.SetCurtain(0f);
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

        private static Vector3 GroundBelow(Vector3 point) =>
            Physics.Raycast(point, Vector3.down, out RaycastHit hit, GroundProbe, ~0, QueryTriggerInteraction.Ignore) ? hit.point : point;

        private static Quaternion Facing(Vector3 from, Vector3 to) =>
            Quaternion.LookRotation(Vector3.ProjectOnPlane(to - from, Vector3.up).normalized);
    }
}
