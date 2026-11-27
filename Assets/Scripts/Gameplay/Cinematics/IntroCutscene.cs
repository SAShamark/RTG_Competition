using Gameplay.Dialogue;
using Gameplay.Cinematics.IntroDrift;
using Gameplay.Entities.Characters.Player;
using Gameplay.Entities.Interactive;
using UnityEngine;

namespace Gameplay.Cinematics
{
    public sealed class IntroCutscene : CinematicBase
    {
        private const float PartnerLineTime = 2.45f;

        [SerializeField] private FirstPersonController _player;
        [SerializeField] private bool _playOnStart = true;
        [SerializeField] private GameObject[] _enableOnFinish;
        [SerializeField] private IntroDriftControl _drift;
        [SerializeField] private Hatch _truckShutter;
        [SerializeField] private CinematicSettings _settings = new CinematicSettings(0f, 2f, titleLine: "вул. Липова, 12", titleSubLine: "18:47  ·  виклик №112  ·  «щось горить»", titleTime: 0.4f, titleDuration: 1.95f);
        [SerializeField] private SubtitleCue[] _subtitles =
        {
            new SubtitleCue(PartnerLineTime, PartnerLineTime, DialogIds.PartnerSlowDown),
        };

        public override CinematicSettings Settings => _settings;
        public override SubtitleCue[] Subtitles => _subtitles;
        public override bool IsInWorld => false;

        private void Start()
        {
            _player.enabled = false;
            foreach (GameObject target in _enableOnFinish)
            {
                target.SetActive(false);
            }

            if (!_playOnStart)
            {
                OnStopped();
                return;
            }

            TryBegin();
            _drift.Finished += HandleDriftFinished;
            _drift.Play();
        }

        public override void Skip()
        {
            _drift.Skip();
            base.Skip();
        }

        private void Update()
        {
            if (IsPlaying)
            {
                StopAtEnd();
            }
        }

        private void HandleDriftFinished()
        {
            _drift.Finished -= HandleDriftFinished;
            PlayTimeline();
            _director.Evaluate();
        }

        protected override void OnStopped()
        {
            _drift.StopEffects();
            _player.enabled = true;
            foreach (GameObject target in _enableOnFinish)
            {
                target.SetActive(true);
            }

            _truckShutter.Open();
        }
    }
}
