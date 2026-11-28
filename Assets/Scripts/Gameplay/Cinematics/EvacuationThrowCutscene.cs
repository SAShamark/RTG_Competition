using Gameplay.Cinematics.EvacuationThrow;
using Gameplay.Entities.Environment;
using Gameplay.Entities.Interactive;
using Services;
using System;
using Gameplay.Entities.Characters.Player;
using Gameplay.Entities.Characters.Residents;
using Unity.Cinemachine;
using Gameplay.Entities.Characters;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Gameplay.Cinematics
{
    public sealed class EvacuationThrowCutscene : CinematicBase
    {
        private const float MinDirectionSqr = 0.01f;

        [SerializeField] private CinemachineCamera _camera;

        [SerializeField] private CinematicSettings _settings = new CinematicSettings(0.3f, 0.5f);

        [Header("Timing")]
        [SerializeField] private float _handOff = 0.7f;
        [SerializeField] private float _breakTime = 0.45f;
        [SerializeField] private float _hang = 0.25f;
        [SerializeField] private float _endHold = 1.1f;

        [SerializeField] private ThrowFlightPhysics _physics = new();

        [Header("Camera")]
        [SerializeField] private float _lookSharpness = 7f;
        [SerializeField] private Vector3 _lookOffset = new(0f, 0.3f, 0f);

        [Header("Lines")]
        [SerializeField] private string _partnerSpeaker = "Напарник";
        [SerializeField] private Color _partnerColor = new(0.45f, 0.8f, 1f);
        [SerializeField] private float _landingLift = 0.25f;
        [SerializeField] private float _sillInset = 0.35f;
        [SerializeField] private float _sillLift = 0.1f;
        [SerializeField] private float _sillDrop = 0.3f;
        [SerializeField] private float _sillTilt = -40f;
        [SerializeField] private float _hangWobble = 0.06f;
        [SerializeField] private float _windowAimDrop = 0.4f;
        [SerializeField] private float _landingAimBlend = 0.4f;

        private FirstPersonController _player;
        private Rescuable _resident;
        private Trampoline _trampoline;
        private EvacuationWindow _window;
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
        private SubtitleCue[] _subtitles = Array.Empty<SubtitleCue>();
        private bool _isOutside;

        public override CinematicSettings Settings => _settings;
        public override SubtitleCue[] Subtitles => _subtitles;

        private float CutTime => _handOff;
        private float LaunchTime => _handOff + _hang;
        private float SafeTime => LaunchTime + _physics.Duration;
        protected override float Duration => SafeTime + _endHold;

        private void Awake()
        {
            _camera.gameObject.SetActive(false);
        }

        public void Play(FirstPersonController player, Rescuable resident, EvacuationWindow window)
        {
            if (!TryBegin())
            {
                return;
            }

            _isOutside = false;
            _player = player;
            _resident = resident;
            _window = window;
            _trampoline = window.Trampoline;

            _launch = window.LaunchPoint.position;
            _landing = _trampoline.LandingPoint + Vector3.up * _landingLift;
            _safe = _trampoline.SafePoint;
            _safeRotation = _trampoline.SafeRotation;
            _outward = Vector3.ProjectOnPlane(_landing - _launch, Vector3.up).normalized;
            if (_outward.sqrMagnitude < MinDirectionSqr)
            {
                _outward = window.LaunchPoint.forward;
            }

            _flightRotation = Quaternion.LookRotation(_outward) * Quaternion.Euler(resident.FlightEuler);
            _lyingRotation = Quaternion.LookRotation(Vector3.up, _outward);
            _cameraAnchor = window.CameraAnchor.position;
            _physics.Build(_launch, _landing, _safe + Vector3.up * resident.Character.BodyCenterHeight, _flightRotation,
                _lyingRotation, _safeRotation, _trampoline.MaxDent);

            _handOffFrom = resident.transform.position;
            _handOffFromRotation = resident.transform.rotation;
            resident.Launch();
            resident.Character.SetPose(CharacterPose.Carried);

            _player.enabled = false;

            BuildSubtitles();
            StartCamera();
            PlayClock();
        }

        private void BuildSubtitles()
        {
            _subtitles = new[]
            {
                new SubtitleCue(CutTime, _hang + _physics.FlightTime, _resident.FlyLineId),
            };
        }

        private void StartCamera()
        {
            Transform head = _player.Head;
            _viewFrom = head.rotation;
            _camera.transform.SetPositionAndRotation(head.position, head.rotation);
            _camera.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (!IsPlaying)
            {
                return;
            }

            float t = ElapsedTime;

            if (t >= _breakTime)
            {
                _window.Break(_outward);
            }

            if (t < CutTime)
            {
                float u = Easing.Smooth(t / _handOff);
                Vector3 sill = _launch - _outward * _sillInset + Vector3.up * _sillLift;
                _resident.transform.SetPositionAndRotation(
                    Vector3.Lerp(_handOffFrom, sill - _resident.transform.rotation * Vector3.up * _sillDrop, u),
                    Quaternion.Slerp(_handOffFromRotation, Quaternion.LookRotation(_outward) * Quaternion.Euler(_sillTilt, 0f, 0f), u));
            }
            else if (t < LaunchTime)
            {
                CutOutside();
                float wobble = Mathf.Sin((t - CutTime) / _hang * Mathf.PI * 2f) * _hangWobble;
                _resident.Character.SetCenterPose(_launch + Vector3.up * wobble, _flightRotation);
            }
            else if (t < SafeTime)
            {
                CutOutside();
                _physics.Pose(t - LaunchTime, _resident.Character, _trampoline);
            }
            else
            {
                Land();
            }

            StopAtEnd();
        }

        private void LateUpdate()
        {
            if (!IsPlaying)
            {
                return;
            }

            Transform cam = _camera.transform;
            if (!_isOutside)
            {
                float u = Easing.Smooth((float)_director.time / _handOff);
                Quaternion toWindow = Quaternion.LookRotation(_launch + Vector3.down * _windowAimDrop - cam.position, Vector3.up);
                cam.rotation = Quaternion.Slerp(_viewFrom, toWindow, u);
                return;
            }

            Vector3 target = _resident.transform.position + _resident.transform.rotation * Vector3.up * _resident.Character.BodyCenterHeight
                + _lookOffset;
            Quaternion desired = Quaternion.LookRotation(target - cam.position, Vector3.up);
            cam.rotation = Quaternion.Slerp(cam.rotation, desired, Easing.Damp(_lookSharpness, Time.deltaTime));
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
            cam.rotation = Quaternion.LookRotation(Vector3.Lerp(_launch, _landing, _landingAimBlend) - cam.position, Vector3.up);
        }

        private void Land()
        {
            _resident.transform.SetPositionAndRotation(_safe, _safeRotation);
            _resident.MarkRescued();
        }

        protected override void OnStopped()
        {
            _window.Break(_outward);
            _physics.LetGoOfCloth(_trampoline);

            Land();

            _camera.gameObject.SetActive(false);

            _player.enabled = true;
        }
    }
}
