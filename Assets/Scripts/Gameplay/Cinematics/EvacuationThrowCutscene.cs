using Gameplay.Entities.Environment;
using Gameplay.Entities.Interactive;
using Services;
using System;
using Gameplay.Entities.Characters.Player;
using Gameplay.Entities.Characters.Residents;
using Gameplay.Entities.Characters;
using UnityEngine;

namespace Gameplay.Cinematics
{
    public sealed class EvacuationThrowCutscene : CinematicBase
    {
        private const float MinDirectionSqr = 0.01f;

        [SerializeField] private CinematicSettings _settings = new CinematicSettings(0.3f, 0.5f);

        [Header("Timing")]
        [SerializeField] private float _handOff = 0.7f;
        [SerializeField] private float _breakTime = 0.45f;
        [SerializeField] private float _hang = 0.25f;
        [SerializeField] private float _flightTime = 1.25f;
        [SerializeField] private float _bounceTime = 1.0f;
        [SerializeField] private float _endHold = 1.1f;

        [Header("Arcs")]
        [SerializeField] private float _flightApex = 0.9f;
        [SerializeField] private float _bounceApex = 1.6f;
        [SerializeField] private float _tumbleDegrees = 70f;

        [Header("Lines")]
        [SerializeField] private string _partnerSpeaker = "Напарник";
        [SerializeField] private Color _partnerColor = new(0.45f, 0.8f, 1f);
        [SerializeField] private float _landingLift = 0.25f;
        [SerializeField] private float _sillInset = 0.35f;
        [SerializeField] private float _sillLift = 0.1f;
        [SerializeField] private float _sillDrop = 0.3f;
        [SerializeField] private float _sillTilt = -40f;
        [SerializeField] private float _hangWobble = 0.06f;
        [SerializeField] private float _tumbleStart = 0.45f;
        [SerializeField] private float _settleSpeed = 1.3f;

        private FirstPersonController _player;
        private Rescuable _resident;
        private Trampoline _trampoline;
        private EvacuationWindow _window;
        private Vector3 _launch;
        private Vector3 _landing;
        private Vector3 _safe;
        private Vector3 _outward;
        private Quaternion _safeRotation;
        private Quaternion _flightRotation;
        private Quaternion _lyingRotation;
        private Vector3 _handOffFrom;
        private Quaternion _handOffFromRotation;
        private SubtitleCue[] _subtitles = Array.Empty<SubtitleCue>();
        private bool _hasBounced;

        public override CinematicSettings Settings => _settings;
        public override SubtitleCue[] Subtitles => _subtitles;

        private float CutTime => _handOff;
        private float LaunchTime => _handOff + _hang;
        private float LandTime => LaunchTime + _flightTime;
        private float SafeTime => LandTime + _bounceTime;
        protected override float Duration => SafeTime + _endHold;

        public void Play(FirstPersonController player, Rescuable resident, EvacuationWindow window)
        {
            if (!TryBegin())
            {
                return;
            }

            _hasBounced = false;
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

            _handOffFrom = resident.transform.position;
            _handOffFromRotation = resident.transform.rotation;
            resident.Launch();
            resident.Character.SetPose(CharacterPose.Carried);

            _player.enabled = false;

            BuildSubtitles();
            PlayClock();
        }

        private void BuildSubtitles()
        {
            _subtitles = new[]
            {
                new SubtitleCue(CutTime, _hang + _flightTime, _resident.FlyLineId),
            };
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
                float wobble = Mathf.Sin((t - CutTime) / _hang * Mathf.PI * 2f) * _hangWobble;
                _resident.Character.SetCenterPose(_launch + Vector3.up * wobble, _flightRotation);
            }
            else if (t < LandTime)
            {
                _resident.Character.SetPose(CharacterPose.Flailing);
                float u = (t - LaunchTime) / _flightTime;
                // slight backward tumble, ending flat on the back for the bed
                Quaternion tumble = Quaternion.Slerp(_flightRotation, _lyingRotation, Easing.Smooth(Mathf.InverseLerp(_tumbleStart, 1f, u)));
                _resident.Character.SetCenterPose(Arc(_launch, _landing, _flightApex, u), tumble);
            }
            else if (t < SafeTime)
            {
                if (!_hasBounced)
                {
                    _hasBounced = true;
                    _trampoline.Bounce();
                }

                float contact = _trampoline.ContactDuration;
                float since = t - LandTime;
                if (since < contact)
                {
                    _resident.Character.SetCenterPose(_landing + Vector3.up * _trampoline.SampleDent(_landing), _lyingRotation);
                }
                else
                {
                    float u = (since - contact) / (_bounceTime - contact);
                    Vector3 safeCenter = _safe + Vector3.up * _resident.Character.BodyCenterHeight;
                    _resident.Character.SetCenterPose(Arc(_landing, safeCenter, _bounceApex, u),
                        Quaternion.Slerp(_lyingRotation, _safeRotation, Easing.Smooth(u * _settleSpeed)));
                }
            }
            else
            {
                Land();
            }

            StopAtEnd();
        }

        private void Land()
        {
            _resident.transform.SetPositionAndRotation(_safe, _safeRotation);
            _resident.MarkRescued();
        }

        protected override void OnStopped()
        {
            _window.Break(_outward);
            Land();
            _player.enabled = true;
        }
    }
}
