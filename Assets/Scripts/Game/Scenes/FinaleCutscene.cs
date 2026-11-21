using System;
using Game.Fire;
using Game.Rescue;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Game.Scenes
{
    public class FinaleCutscene : MonoBehaviour, ICinematic
    {
        [SerializeField] private PlayableDirector _director;
        [SerializeField] private CinemachineCamera _camera;
        [SerializeField] private PosedActor _stunt;
        [SerializeField] private EvacuationWindow _window;
        [SerializeField] private GameObject _firefighters;
        [SerializeField] private FireSource[] _houseFires;
        [SerializeField] private Transform _wideFrom;
        [SerializeField] private Transform _wideTo;
        [SerializeField] private Transform _wideLookAt;

        [Header("The explosion behind him")]
        [SerializeField] private GameObject _explosion;
        [SerializeField] private AudioSource _boom;
        [SerializeField] private FireSource _blastFire;
        [SerializeField] private float _shakeTime = 0.8f;
        [SerializeField] private float _shakeAmount = 0.25f;

        [Header("The last flame (the uncle)")]
        [SerializeField] private PosedActor _uncle;
        [SerializeField] private Transform _uncleSpot;
        [SerializeField] private FireSource _lastFlame;
        [SerializeField] private ParticleSystem _stream;
        [SerializeField] private Transform _peeCamera;
        [SerializeField] private float _orbitDistance = 2.3f;
        [SerializeField] private float _orbitHeight = 1.55f;

        [SerializeField] private CinematicSettings _settings = new()
        {
            BarsInTime = 0.4f, BarsOutTime = 0f, TitleLine = "Пожежу загашено", TitleSubLine = "вул. Липова, 12 · усі на вулиці", TitleTime = 5.6f, TitleDuration = 3.8f,
        };

        [Header("Timing")]
        [SerializeField] private float _run = 1.1f;
        [SerializeField] private float _hang = 0.35f;
        [SerializeField] private float _fall = 1.35f;
        [SerializeField] private float _bounce = 1.0f;
        [SerializeField] private float _landHold = 1.0f;
        [SerializeField] private float _wide = 5.5f;
        [SerializeField] private float _runIn = 1.7f;
        [SerializeField] private Vector3 _runInFrom = new(5.0f, 0f, -3.0f);
        [SerializeField] private float _pee = 3.4f;
        [SerializeField] private float _orbit = 2.6f;
        [SerializeField] private float _poseHold = 0.9f;

        [Header("Arcs")]
        [SerializeField] private float _fallApex = 1.9f;
        [SerializeField] private float _bounceApex = 2.0f;

        private SubtitleCue[] _subtitles;
        private FirstPersonController _player;
        private TimelineAsset _clock;
        private bool _isPlaying;
        private bool _hasBounced;
        private int _shot = -1;
        private Vector3 _launch, _landing, _safe, _outward, _runFrom, _runTo;
        private Quaternion _safeRotation, _runRotation;

        public PlayableDirector Director => _director;
        public CinematicSettings Settings => _settings;
        public SubtitleCue[] Subtitles => _subtitles;
        public bool IsPlaying => _isPlaying;

        private float JumpTime => _run;
        private float FallTime => JumpTime + _hang;
        private float LandTime => FallTime + _fall;
        private float SafeTime => LandTime + _bounce;
        private float WideTime => SafeTime + _landHold;
        private float PeeTime => WideTime + _wide;
        private float OrbitTime => PeeTime + _runIn + _pee;
        private float Duration => OrbitTime + _orbit + _poseHold;

        private RenderTexture _frame;
        private Vector3 _orbitFrom;
        private Quaternion _orbitFromRotation;
        private bool _orbitStarted;
        private bool _streamOn;

        public Texture CapturedFrame => _frame;

        public event Action OnStarted;
        public event Action OnFinished;

        private void Awake()
        {
            _camera.gameObject.SetActive(false);
            _stunt.gameObject.SetActive(false);
            _firefighters.SetActive(false);
            _uncle.gameObject.SetActive(false);
        }

        public void Play(FirstPersonController player)
        {
            if (_isPlaying)
            {
                return;
            }

            _isPlaying = true;
            _hasBounced = false;
            _exploded = false;
            _shot = -1;
            _player = player;
            _launch = _window.LaunchPoint.position;
            _landing = _window.Trampoline.LandingPoint + Vector3.up * 0.25f;
            _safe = _window.Trampoline.SafePoint + _window.Trampoline.SafeRotation * Vector3.right * 1.1f;
            _safeRotation = _window.Trampoline.SafeRotation;
            _outward = Vector3.ProjectOnPlane(_landing - _launch, Vector3.up).normalized;
            _window.Open();
            _lastFlame.gameObject.SetActive(true);

            Transform head = player.Head;
            _runFrom = head.position;
            _runTo = new Vector3(_launch.x, _runFrom.y, _launch.z) - _outward * 0.9f;
            _runRotation = Quaternion.LookRotation(_outward + Vector3.down * 0.35f);
            player.enabled = false;

            _subtitles = new[]
            {
                new SubtitleCue { Time = SafeTime - 0.1f, Duration = _landHold + 0.6f, Speaker = "Напарник", SpeakerColor = new Color(0.45f, 0.8f, 1f), Text = "Та ти супергерой!" },
                new SubtitleCue { Time = PeeTime + _runIn + 0.3f, Duration = 2.6f, Speaker = "Дядько", SpeakerColor = new Color(1f, 0.55f, 0.25f), Text = "Відійдіть. Професіонал працює." },
                new SubtitleCue { Time = OrbitTime + 1.2f, Duration = 2.2f, Speaker = "Дядько", SpeakerColor = new Color(1f, 0.55f, 0.25f), Text = "Я всіх врятував! Фотографуйте!" },
            };

            _camera.transform.SetPositionAndRotation(head.position, head.rotation);
            _camera.gameObject.SetActive(true);

            if (_clock == null)
            {
                _clock = ScriptableObject.CreateInstance<TimelineAsset>();
                _clock.hideFlags = HideFlags.DontSave;
            }

            _clock.durationMode = TimelineAsset.DurationMode.FixedLength;
            _clock.fixedDuration = Duration;
            _director.playableAsset = _clock;
            _director.extrapolationMode = DirectorWrapMode.None;
            _director.stopped += HandleStopped;
            _director.time = 0;
            _director.RebuildGraph();
            _director.Play();
            OnStarted?.Invoke();
        }

        public void Skip()
        {
            if (_isPlaying)
            {
                _director.Stop();
            }
        }

        private void Update()
        {
            if (!_isPlaying)
            {
                return;
            }

            float t = (float)_director.time;
            Transform cam = _camera.transform;

            if (t < JumpTime)
            {
                float u = Smooth(t / _run);
                Vector3 bob = Vector3.up * Mathf.Abs(Mathf.Sin(t * 11f)) * 0.04f * (1f - u);
                cam.SetPositionAndRotation(Vector3.Lerp(_runFrom, _runTo, u) + bob, Quaternion.Slerp(cam.rotation, _runRotation, 1f - Mathf.Exp(-5f * Time.deltaTime)));
            }
            else
            {
                if (t < WideTime)
                {
                    Shot(1);
                }

                _stunt.gameObject.SetActive(true);
                Quaternion facing = Quaternion.LookRotation(_outward);
                if (t >= FallTime - 0.05f)
                {
                    Explode();
                }

                if (t < WideTime && _exploded)
                {
                    float since = t - FallTime;
                    float shake = since >= 0f && since < _shakeTime ? (1f - since / _shakeTime) * _shakeAmount : 0f;
                    cam.position = _shotPosition + UnityEngine.Random.insideUnitSphere * shake;
                }

                if (t < FallTime)
                {
                    _stunt.SetPose(ResidentPose.Carried);
                    _stunt.SetCenterPose(_launch + Vector3.up * 0.1f, facing * Quaternion.Euler(-10f, 0f, 0f));
                }
                else if (t < LandTime)
                {
                    _stunt.SetPose(ResidentPose.Flailing);
                    float u = (t - FallTime) / _fall;
                    Quaternion lying = Quaternion.LookRotation(Vector3.up, _outward);
                    _stunt.SetCenterPose(Arc(_launch, _landing, _fallApex, u), Quaternion.Slerp(facing, lying, Smooth(Mathf.InverseLerp(0.3f, 1f, u))));
                }
                else if (t < SafeTime)
                {
                    if (!_hasBounced)
                    {
                        _hasBounced = true;
                        _window.Trampoline.Bounce(1.3f);
                    }

                    float u = (t - LandTime) / _bounce;
                    Quaternion lying = Quaternion.LookRotation(Vector3.up, _outward);
                    _stunt.SetCenterPose(Arc(_landing, _safe + Vector3.up * _stunt.BodyCenterHeight, _bounceApex, u),
                        Quaternion.Slerp(lying, _safeRotation, Smooth(u * 1.4f)));
                }
                else
                {
                    _stunt.SetPose(ResidentPose.Animated);
                    _stunt.transform.SetPositionAndRotation(_safe, _safeRotation);
                }

                if (t >= PeeTime)
                {
                    UncleScene(t);
                }
                else if (t >= WideTime)
                {
                    Shot(2);
                    float u = Smooth((t - WideTime) / _wide);
                    cam.position = Vector3.Lerp(_wideFrom.position, _wideTo.position, u);

                    cam.rotation = Quaternion.LookRotation(_wideLookAt.position - cam.position, Vector3.up);

                    foreach (FireSource fire in _houseFires)
                    {
                        if (!fire.IsOut)
                        {
                            fire.SetReignite(false);
                            fire.Extinguish(0.35f, Time.deltaTime);
                        }
                    }
                }
                else
                {
                    Vector3 target = _stunt.transform.position + Vector3.up * 0.9f;
                    cam.rotation = Quaternion.Slerp(cam.rotation, Quaternion.LookRotation(target - cam.position, Vector3.up), 1f - Mathf.Exp(-7f * Time.deltaTime));
                }
            }

            if (t >= Duration - 0.02f)
            {
                _director.Stop();
            }
        }

        private void UncleScene(float t)
        {
            Transform cam = _camera.transform;
            if (_shot != 3)
            {
                _shot = 3;
                _firefighters.SetActive(true);

                Vector3 from = _uncleSpot.position + _runInFrom;
                _uncle.gameObject.SetActive(true);
                _uncle.SetPose(ResidentPose.Running);
                _uncle.transform.SetPositionAndRotation(from, Quaternion.LookRotation(Vector3.ProjectOnPlane(_uncleSpot.position - from, Vector3.up)));

                cam.SetPositionAndRotation(_peeCamera.position, _peeCamera.rotation);
            }

            Transform uncle = _uncle.transform;
            Vector3 chest = uncle.position + Vector3.up * 1.4f;
            float run = t - PeeTime;
            if (run < _runIn)
            {
                float k = Mathf.Clamp01(run / _runIn);
                float pace = k * (2f - k);
                Vector3 from = _uncleSpot.position + _runInFrom;
                uncle.position = Vector3.Lerp(from, _uncleSpot.position, pace);
                Quaternion heading = Quaternion.LookRotation(Vector3.ProjectOnPlane(_uncleSpot.position - from, Vector3.up));
                uncle.rotation = Quaternion.Slerp(heading, _uncleSpot.rotation, Smooth(Mathf.InverseLerp(0.6f, 1f, k)));
                if (k > 0.85f)
                {
                    _uncle.SetPose(ResidentPose.Animated);
                }

                cam.rotation = Quaternion.LookRotation(_uncleSpot.position + Vector3.up * 1.1f - cam.position, Vector3.up);
                return;
            }

            if (_uncle.transform.position != _uncleSpot.position && run - Time.deltaTime < _runIn)
            {
                uncle.SetPositionAndRotation(_uncleSpot.position, _uncleSpot.rotation);
                _uncle.SetPose(ResidentPose.Animated);
            }

            float local = run - _runIn;

            bool streamOn = local > 0.6f && local < _pee - 0.2f;
            if (streamOn != _streamOn)
            {
                _streamOn = streamOn;
                if (streamOn) _stream.Play(); else _stream.Stop();
            }

            if (!_lastFlame.IsOut && local > 1.0f)
            {
                _lastFlame.SetReignite(false);
                _lastFlame.Extinguish(0.6f, Time.deltaTime);
                if (local > _pee - 0.3f)
                {
                    _lastFlame.ForceOut();
                }
            }

            if (t < OrbitTime)
            {
                cam.rotation = Quaternion.LookRotation(_uncleSpot.position + Vector3.up * 1.1f - cam.position, Vector3.up);
                return;
            }

            if (!_orbitStarted)
            {
                _orbitStarted = true;
                _orbitFrom = cam.position;
                _orbitFromRotation = uncle.rotation;
            }

            float o = Mathf.Clamp01((t - OrbitTime) / _orbit);
            float ease = Smooth(o);
            Quaternion toCamera = Quaternion.LookRotation(Vector3.ProjectOnPlane(-_uncleSpot.forward, Vector3.up).normalized);
            uncle.rotation = Quaternion.Slerp(_orbitFromRotation, toCamera, Smooth(Mathf.InverseLerp(0.15f, 0.6f, o)));
            if (o > 0.45f)
            {
                _uncle.SetPose(ResidentPose.Victory);
            }

            Vector3 centre = uncle.position;
            Vector3 fromOffset = _orbitFrom - centre;
            Vector3 toOffset = Quaternion.AngleAxis(25f, Vector3.up) * (-_uncleSpot.forward * _orbitDistance) + Vector3.up * _orbitHeight;
            float fromAngle = Mathf.Atan2(fromOffset.x, fromOffset.z) * Mathf.Rad2Deg;
            float toAngle = Mathf.Atan2(toOffset.x, toOffset.z) * Mathf.Rad2Deg;
            float angle = Mathf.LerpAngle(fromAngle, toAngle, ease);
            float radius = Mathf.Lerp(new Vector2(fromOffset.x, fromOffset.z).magnitude, new Vector2(toOffset.x, toOffset.z).magnitude, ease);
            float height = Mathf.Lerp(fromOffset.y, toOffset.y, ease);
            cam.position = centre + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius + Vector3.up * height;
            cam.rotation = Quaternion.LookRotation(centre + Vector3.up * 1.25f - cam.position, Vector3.up);
        }

        private void CaptureFrame()
        {
            Camera main = Camera.main;
            if (main == null)
            {
                return;
            }

            if (_frame == null)
            {
                _frame = new RenderTexture(Screen.width, Screen.height, 24);
            }

            RenderTexture previous = main.targetTexture;
            main.targetTexture = _frame;
            main.Render();
            main.targetTexture = previous;
        }

        private bool _exploded;
        private Vector3 _shotPosition;

        private void Explode()
        {
            if (_exploded)
            {
                return;
            }

            _exploded = true;
            _explosion.SetActive(false);
            _explosion.SetActive(true);

            _boom.Play();

            _blastFire.gameObject.SetActive(true);
        }

        private void Shot(int shot)
        {
            if (_shot == shot)
            {
                return;
            }

            _shot = shot;
            Transform cam = _camera.transform;
            if (shot == 1)
            {
                cam.position = _window.CameraAnchor.position;
                _shotPosition = cam.position;
                cam.rotation = Quaternion.LookRotation(Vector3.Lerp(_launch, _landing, 0.4f) - cam.position, Vector3.up);
            }
            else if (shot == 2)
            {
                _firefighters.SetActive(true);

                cam.position = _wideFrom.position;
            }
        }

        private void HandleStopped(PlayableDirector director)
        {
            _director.stopped -= HandleStopped;
            _isPlaying = false;
            _stream.Stop();

            if (_uncle.gameObject.activeSelf)
            {
                _uncle.SetPose(ResidentPose.Victory);
            }

            CaptureFrame();
            foreach (FireSource fire in _houseFires)
            {
                fire.SetReignite(false);
                fire.ForceOut();
            }

            OnFinished?.Invoke();
        }

        private static Vector3 Arc(Vector3 from, Vector3 to, float apex, float u) =>
            Vector3.Lerp(from, to, u) + Vector3.up * (4f * apex * u * (1f - u));

        private static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        private void OnDisable()
        {
            if (_isPlaying)
            {
                _director.stopped -= HandleStopped;
                _isPlaying = false;
            }
        }

        private void OnDestroy()
        {
            if (_clock != null)
            {
                Destroy(_clock);
            }

            if (_frame != null)
            {
                _frame.Release();
            }
        }
    }
}
