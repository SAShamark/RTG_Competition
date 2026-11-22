using Gameplay.Entities.Player;
using UnityEngine;
using UnityEngine.Serialization;

namespace Gameplay.Cinematics
{
    public sealed class IntroCutscene : CinematicBase
    {
        private const float PartnerLineTime = 2.45f;

        [SerializeField, FormerlySerializedAs("Player")] private FirstPersonController _player;
        [SerializeField, FormerlySerializedAs("PlayOnStart")] private bool _playOnStart = true;
        [SerializeField, FormerlySerializedAs("EnableOnFinish")] private GameObject[] _enableOnFinish;
        [SerializeField] private CinematicSettings _settings = new CinematicSettings(0f, 2f, titleLine: "вул. Липова, 12", titleSubLine: "18:47  ·  виклик №112  ·  «щось горить»", titleTime: 0.4f, titleDuration: 1.95f);
        [SerializeField] private SubtitleCue[] _subtitles =
        {
            new SubtitleCue(PartnerLineTime, PartnerLineTime, "P01"),
        };

        public override CinematicSettings Settings => _settings;
        public override SubtitleCue[] Subtitles => _subtitles;

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
            PlayTimeline();
        }

        private void Update()
        {
            if (IsPlaying)
            {
                StopAtEnd();
            }
        }

        protected override void OnStopped()
        {
            _player.enabled = true;
            foreach (GameObject target in _enableOnFinish)
            {
                target.SetActive(true);
            }
        }
    }
}
