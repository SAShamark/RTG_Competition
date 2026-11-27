using Gameplay.Cinematics.UncleBurst;
using Gameplay.Dialogue;
using Gameplay.Entities.Characters.Player;
using Gameplay.Entities.Environment;
using Services.Audio;
using UnityEngine;

namespace Gameplay.Cinematics
{
    public sealed class UncleBurstCutscene : CinematicBase
    {
        private const float LineTime = 0.25f;
        private const float LineDuration = 2.6f;

        [SerializeField] private GameObject _actors;
        [SerializeField] private Transform _uncle;
        [SerializeField] private ProceduralCarryRun _runner;
        [SerializeField] private CatFollower[] _cats;
        [SerializeField] private Vector3 _doorCenter;
        [SerializeField] private float _duration = 5.6f;
        [SerializeField] private float[] _meowTimes = { 0.5f, 1.8f, 3.3f };

        [SerializeField] private CinematicSettings _settings = new CinematicSettings(0.45f, 1.2f);
        [SerializeField] private SubtitleCue[] _subtitles =
        {
            new SubtitleCue(LineTime, LineDuration, DialogIds.UncleMakeWay),
        };

        [SerializeField] private UncleBurstPath _path = new();
        [SerializeField] private UncleBurstExit _exit = new();

        private FirstPersonController _player;
        private Vector3[][] _lanes;
        private float _pathTime;
        private int _nextMeow;
        private bool _isRunning;

        public override CinematicSettings Settings => _settings;
        public override SubtitleCue[] Subtitles => _subtitles;
        protected override float Duration => _duration;

        public void Init(FirstPersonController player, Camera camera)
        {
            _player = player;
            _lanes = new Vector3[_cats.Length][];
            _exit.Init(camera, _actors);
            _runner.LookAt(player.Head);
            _actors.SetActive(false);
        }

        public void Play()
        {
            if (!TryBegin())
            {
                return;
            }

            _path.Build(_doorCenter, _player.transform.position);
            _pathTime = 0f;
            _nextMeow = 0;
            _uncle.position = _path.Sample(_pathTime, 0f);

            Vector3 heading = _path.Direction(_pathTime);
            for (int i = 0; i < _cats.Length; i++)
            {
                CatFollower cat = _cats[i];
                _lanes[i] = _path.BuildLane(cat.LaneOffsets);
                cat.Place(_path.SampleLane(_lanes[i], cat.RouteTime(_pathTime)), heading);
            }

            _player.enabled = false;
            _isRunning = true;

            PlayTimeline();
        }

        private void Update()
        {
            if (!_isRunning)
            {
                return;
            }

            _pathTime = IsPlaying ? ElapsedTime : _pathTime + Time.deltaTime;
            _uncle.position = _path.Sample(_pathTime, Time.deltaTime);
            for (int i = 0; i < _cats.Length; i++)
            {
                _cats[i].MoveTo(_path.SampleLane(_lanes[i], _cats[i].RouteTime(_pathTime)));
            }

            PlayMeows();

            if (IsPlaying)
            {
                StopAtEnd();
            }
            else if (_exit.TickIsGone(Time.deltaTime))
            {
                _isRunning = false;
                _actors.SetActive(false);
            }
        }

        private void PlayMeows()
        {
            while (_nextMeow < _meowTimes.Length && _pathTime >= _meowTimes[_nextMeow])
            {
                AudioService.Instance.PlayAt(SoundType.CatMeow, _uncle.position);
                _nextMeow++;
            }
        }

        protected override void OnStopped()
        {
            _actors.SetActive(true);
            _exit.Begin();
            _player.enabled = true;
        }
    }
}
