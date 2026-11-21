using System;
using Game.Scenes;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Game.Rescue
{
    public class EvacuationThrowCutscene : MonoBehaviour, ICinematic
    {
        [SerializeField] private PlayableDirector _director;
        [SerializeField] private CinemachineCamera _camera;

        [SerializeField] private CinematicSettings _settings = new() { BarsInTime = 0.3f, BarsOutTime = 0.5f };

        [Header("Timing")]
        [SerializeField] private float _handOff = 0.7f;
        [SerializeField] private float _hang = 0.25f;
        [SerializeField] private float _flightTime = 1.25f;
        [SerializeField] private float _bounceTime = 1.0f;
        [SerializeField] private float _endHold = 1.1f;

        [Header("Arcs")]
        [SerializeField] private float _flightApex = 0.9f;
        [SerializeField] private float _bounceApex = 1.6f;
        [SerializeField] private float _tumbleDegrees = 70f;

        [Header("Camera")]
        [SerializeField] private float _lookSharpness = 7f;
        [SerializeField] private Vector3 _lookOffset = new(0f, 0.3f, 0f);

        [Header("Lines")]
        [SerializeField] private string _partnerSpeaker = "Напарник";
        [SerializeField] private Color _partnerColor = new(0.45f, 0.8f, 1f);
        [SerializeField] private Color _residentColor = new(1f, 0.85f, 0.55f);
        [SerializeField] private string[] _catchLines = { "Є! Прийняв!", "Ціле! Здається.", "Наступного!" };

        private FirstPersonController _player;
        private Rescuable _resident;
        private Trampoline _trampoline;
        private Vector3 _launch;
        private Vector3 _landing;
        private Vector3 _safe;
        private Vector3 _outward;
        private Vector3 _cameraAnchor;
        private Quaternion _safeRotation;
        private Quaternion _flightRotation;
        private Quaternion _lyingRotation;
        private Vector3 _handOffFrom;
        private Quaternion _handOffFromRotation;
        private Quaternion _viewFrom;
        private TimelineAsset _clock;
        private SubtitleCue[] _subtitles = Array.Empty<SubtitleCue>();
        private bool _isPlaying;
        private bool _isOutside;
        private bool _hasBounced;
        private int _catchIndex;

        public PlayableDirector Director => _director;
        public CinematicSettings Settings => _settings;
        public SubtitleCue[] Subtitles => _subtitles;
        public bool IsPlaying => _isPlaying;

        private float CutTime => _handOff;
        private float LaunchTime => _handOff + _hang;
        private float LandTime => LaunchTime + _flightTime;
        private float SafeTime => LandTime + _bounceTime;
        private float Duration => SafeTime + _endHold;

        public event Action OnStarted;
        public event Action OnFinished;

        private void Awake()
        {
            _camera.gameObject.SetActive(false);
        }

        public void Play(FirstPersonController player, Rescuable resident, EvacuationWindow window)
        {
            if (_isPlaying)
            {
                return;
            }

            _isPlaying = true;
            _isOutside = false;
            _hasBounced = false;
            _player = player;
            _resident = resident;
            _trampoline = window.Trampoline;

            _launch = window.LaunchPoint.position;
            _landing = _trampoline.LandingPoint + Vector3.up * 0.25f;
            _safe = _trampoline.SafePoint;
            _safeRotation = _trampoline.SafeRotation;
            _outward = Vector3.ProjectOnPlane(_landing - _launch, Vector3.up).normalized;
            if (_outward.sqrMagnitude < 0.01f)
            {
                _outward = window.LaunchPoint.forward;
            }

            _flightRotation = Quaternion.LookRotation(_outward) * Quaternion.Euler(resident.FlightEuler);
            _lyingRotation = Quaternion.LookRotation(Vector3.up, _outward);
            _cameraAnchor = window.CameraAnchor.position;

            window.Open();
            _handOffFrom = resident.transform.position;
            _handOffFromRotation = resident.transform.rotation;
            resident.Launch();
            resident.SetPose(ResidentPose.Carried);

            _player.enabled = false;

            BuildSubtitles();
            StartCamera();
            StartClock();
            OnStarted?.Invoke();
        }

        public void Skip()
        {
            if (_isPlaying)
            {
                _director.Stop();
            }
        }

        private void BuildSubtitles()
        {
            string catchLine = _catchLines[_catchIndex++ % _catchLines.Length];
            _subtitles = new[]
            {
                new SubtitleCue { Time = CutTime, Duration = _hang + _flightTime, Speaker = _resident.SpeakerName, SpeakerColor = _residentColor, Text = _resident.FlyLine },
                new SubtitleCue { Time = SafeTime - 0.15f, Duration = _endHold, Speaker = _partnerSpeaker, SpeakerColor = _partnerColor, Text = catchLine },
            };
        }

        private void StartCamera()
        {
            Transform head = _player.Head;
            _viewFrom = head.rotation;
            _camera.transform.SetPositionAndRotation(head.position, head.rotation);
            _camera.gameObject.SetActive(true);
        }

        private void StartClock()
        {
            if (_clock == null)
            {
                _clock = ScriptableObject.CreateInstance<TimelineAsset>();
                _clock.name = "EvacuationThrowClock";
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
        }

        private void Update()
        {
            if (!_isPlaying)
            {
                return;
            }

            float t = (float)_director.time;

            if (t < CutTime)
            {
                float u = Smooth(t / _handOff);
                Vector3 sill = _launch - _outward * 0.35f + Vector3.up * 0.1f;
                _resident.transform.SetPositionAndRotation(
                    Vector3.Lerp(_handOffFrom, sill - _resident.transform.rotation * Vector3.up * 0.3f, u),
                    Quaternion.Slerp(_handOffFromRotation, Quaternion.LookRotation(_outward) * Quaternion.Euler(-40f, 0f, 0f), u));
            }
            else if (t < LaunchTime)
            {
                CutOutside();
                float wobble = Mathf.Sin((t - CutTime) / _hang * Mathf.PI * 2f) * 0.06f;
                _resident.SetCenterPose(_launch + Vector3.up * wobble, _flightRotation);
            }
            else if (t < LandTime)
            {
                CutOutside();
                _resident.SetPose(ResidentPose.Flailing);
                float u = (t - LaunchTime) / _flightTime;
                // slight backward tumble, ending flat on the back for the bed
                Quaternion tumble = Quaternion.Slerp(_flightRotation, _lyingRotation, Smooth(Mathf.InverseLerp(0.45f, 1f, u)));
                _resident.SetCenterPose(Arc(_launch, _landing, _flightApex, u), tumble);
            }
            else if (t < SafeTime)
            {
                if (!_hasBounced)
                {
                    _hasBounced = true;
                    _trampoline.Bounce();
                }

                float u = (t - LandTime) / _bounceTime;
                Vector3 safeCenter = _safe + Vector3.up * _resident.BodyCenterHeight;
                _resident.SetCenterPose(Arc(_landing, safeCenter, _bounceApex, u),
                    Quaternion.Slerp(_lyingRotation, _safeRotation, Smooth(u * 1.3f)));
            }
            else
            {
                Land();
            }

            if (t >= Duration - 0.02f)
            {
                _director.Stop();
            }
        }

        private void LateUpdate()
        {
            if (!_isPlaying)
            {
                return;
            }

            Transform cam = _camera.transform;
            if (!_isOutside)
            {
                float u = Smooth((float)_director.time / _handOff);
                Quaternion toWindow = Quaternion.LookRotation(_launch + Vector3.down * 0.4f - cam.position, Vector3.up);
                cam.rotation = Quaternion.Slerp(_viewFrom, toWindow, u);
                return;
            }

            Vector3 target = _resident.transform.position + _resident.transform.rotation * Vector3.up * _resident.BodyCenterHeight
                + _lookOffset;
            Quaternion desired = Quaternion.LookRotation(target - cam.position, Vector3.up);
            cam.rotation = Quaternion.Slerp(cam.rotation, desired, 1f - Mathf.Exp(-_lookSharpness * Time.deltaTime));
        }

        private void CutOutside()
        {
            if (_isOutside)
            {
                return;
            }

            _isOutside = true;
            Transform cam = _camera.transform;
            cam.position = _cameraAnchor;
            cam.rotation = Quaternion.LookRotation(Vector3.Lerp(_launch, _landing, 0.4f) - cam.position, Vector3.up);
        }

        private void Land()
        {
            _resident.transform.SetPositionAndRotation(_safe, _safeRotation);
            _resident.MarkRescued();
        }

        private void HandleStopped(PlayableDirector director)
        {
            _director.stopped -= HandleStopped;
            _isPlaying = false;

            Land();

            _camera.gameObject.SetActive(false);

            _player.enabled = true;

            OnFinished?.Invoke();
        }

        private static Vector3 Arc(Vector3 from, Vector3 to, float apex, float u) =>
            Vector3.Lerp(from, to, u) + Vector3.up * (4f * apex * u * (1f - u));

        private static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        private void OnDestroy()
        {
            if (_clock != null)
            {
                Destroy(_clock);
            }
        }
    }
}
