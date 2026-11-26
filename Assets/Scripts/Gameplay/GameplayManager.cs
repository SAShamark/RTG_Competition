using Gameplay.Cinematics;
using Gameplay.Dialogue;
using Gameplay.Effects.Puddle;
using Gameplay.Entities.Environment;
using Gameplay.Entities.Interactive;
using Gameplay.Mission.Core;
using Gameplay.Mission.Systems;
using Services.Audio;
using System;
using Gameplay.Entities.Characters.Player;
using UI.Managers;
using UI.Popups;
using UI.Popups.Variants.Results;
using UI.Screens;
using UI.Screens.Variants.Gameplay;
using UnityEngine;

namespace Gameplay
{
    [Serializable]
    public sealed class GameplayManager
    {
        private const float FullPercent = 100f;

        [Header("Player")]
        [SerializeField] private FirstPersonController _player;
        [SerializeField] private Transform _playerBody;

        [Header("Systems")]
        [SerializeField] private TextLibrary _texts;
        [SerializeField] private FireHeatLogic _fireHeat = new();
        [SerializeField] private DialogueDirector _dialogue = new();
        [SerializeField] private NavigationGuide _navigation = new();
        [SerializeField] private FogController _fog = new();
        [SerializeField] private GrassController _grass = new();
        [SerializeField] private MissionStats _stats = new();
        [SerializeField] private FacadeFirefight _facade = new();
        [SerializeField] private ResidentComplaints _complaints = new();
        [SerializeField] private PuddleControl[] _puddles;

        [Header("Mission")]
        [SerializeField] private MissionFlow _mission = new();
        [SerializeField] private EvacuationWindow[] _evacuationWindows;
        [SerializeField, Min(1)] private int _rescueTotal = 3;

        [Header("Cinematics")]
        [SerializeField] private MissionCinematics _cinematics = new();
        [SerializeField] private float _facadeLinePause = 0.6f;

        private GameplayScreen _screen;
        private IPopupsManager _popups;
        private EvacuationController _evacuation;
        private RadioController _radio;
        private Camera _camera;
        private bool _isFacadeLinePending;
        private float _sinceVoice;
        private ResultsPopup _resultsPopup;

        public void Initialize(UIManager uiManager)
        {
            _screen = (GameplayScreen)uiManager.ScreensManager.GetScreen(ScreenTypes.Gameplay);
            _screen.SetObjective(_texts[TextId.ObjectiveEnterBuilding]);
            _popups = uiManager.PopupsManager;
            _camera = Camera.main;

            _stats.Init(_screen, _texts);
            _fog.Init();
            _grass.Init();
            _fireHeat.Init(_player.Head);
            _dialogue.Init(_screen, _player, _playerBody, AudioService.Instance);
            _navigation.Init(_player.Head, _camera, _screen.NavMarker, _texts);
            _cinematics.Initialize(uiManager, _screen, _player, _camera, AudioService.Instance);
            _facade.Init(_screen, _texts, _cinematics.HouseFires, _navigation);

            _evacuation = new EvacuationController(_player, _evacuationWindows, _rescueTotal, _cinematics.Throw, _cinematics.Present, _screen, _texts, _dialogue.Library);
            _evacuation.Init();
            _complaints.Init(_screen, _texts, _dialogue.Library);

            var puzzles = new PuzzlePresenter(_player, _screen, _popups);
            _mission.Initialize(new MissionContext(_screen, _player, _dialogue, _navigation, _evacuation, puzzles, _texts));

            _screen.Init(_player, _fireHeat, _texts, _camera);
            _radio = new RadioController(_player, _screen, () => _mission.RadioHint);

            _mission.EntranceOpened += _cinematics.PlayUncleBurst;
            _mission.Completed += _cinematics.PlayGasBurst;
            _cinematics.IntroFinished += HandleIntroFinished;
            _cinematics.UncleBurstFinished += HandleUncleBurstFinished;
            _cinematics.GasBurstFinished += _cinematics.PlayFinale;
            _cinematics.FinaleJumpFinished += HandleFinaleJumpFinished;
            _facade.Completed += HandleFacadeCompleted;
            _cinematics.FinaleFinished += HandleFinaleFinished;
            _player.Hose.WaterLanded += HandleWaterLanded;

            if (!_cinematics.TryPresentIntro())
            {
                StartMission();
            }
        }

        public void Tick(float deltaTime)
        {
            _fireHeat.Tick(deltaTime);
            _dialogue.Tick(deltaTime);
            _mission.Tick(deltaTime);
            _stats.Tick(deltaTime);
            _facade.Tick(deltaTime);
            _fog.Tick(_camera.transform.position, deltaTime);
            _grass.Tick(_camera.transform.position, _player.transform.position);
            _radio.Tick();
            TickFacadeLine(deltaTime);
        }

        // The partner is still finishing his line from the jump when the cutscene ends, so the next one waits for it.
        private void TickFacadeLine(float deltaTime)
        {
            if (!_isFacadeLinePending)
            {
                return;
            }

            _sinceVoice = AudioService.Instance.IsVoicePlaying ? 0f : _sinceVoice + deltaTime;
            if (_sinceVoice >= _facadeLinePause)
            {
                _isFacadeLinePending = false;
                _screen.Say(_dialogue.Library.Get(DialogIds.PartnerPutOutFire));
            }
        }

        public void LateTick() => _dialogue.LateTick();

        public void Dispose()
        {
            _mission.EntranceOpened -= _cinematics.PlayUncleBurst;
            _mission.Completed -= _cinematics.PlayGasBurst;
            _cinematics.IntroFinished -= HandleIntroFinished;
            _cinematics.UncleBurstFinished -= HandleUncleBurstFinished;
            _cinematics.GasBurstFinished -= _cinematics.PlayFinale;
            _cinematics.FinaleJumpFinished -= HandleFinaleJumpFinished;
            _facade.Completed -= HandleFacadeCompleted;
            _cinematics.FinaleFinished -= HandleFinaleFinished;
            _player.Hose.WaterLanded -= HandleWaterLanded;

            _navigation.Dispose();
            _screen.Dispose();
            _mission.Dispose();
            _evacuation.Dispose();
            _complaints.Dispose();
            _stats.Dispose();
            _facade.Dispose();
            _fog.Dispose();
            _cinematics.Dispose();
        }

        private void HandleFinaleJumpFinished()
        {
            _screen.SetCrosshairVisible(true);
            _facade.Begin();
            _isFacadeLinePending = true;
            _sinceVoice = 0f;
        }

        private void HandleWaterLanded(Vector3 point, float litres)
        {
            foreach (PuddleControl puddle in _puddles)
            {
                puddle.Pour(point, litres);
            }
        }

        private void HandleFacadeCompleted()
        {
            _stats.Stop();
            _cinematics.PlayFinaleEnding();
        }

        private void StartMission()
        {
            _stats.Begin();
            _screen.SetCrosshairVisible(true);
            _mission.Start();
        }

        private void HandleIntroFinished()
        {
            StartMission();
            _screen.Say(_dialogue.Library.Get(DialogIds.PartnerBriefing));
        }

        private void HandleUncleBurstFinished()
        {
            _screen.SetCrosshairVisible(true);
            _screen.ShowToast(_texts[TextId.ToastCatsRescued]);
            _screen.Say(_dialogue.Library.Get(DialogIds.PartnerAfterUncle));
            _mission.BeginFloor1();
        }

        private void HandleFinaleFinished(Texture photo)
        {
            _stats.Stop();
            _player.enabled = false;
            _screen.SetCrosshairVisible(false);
            _screen.SetHudVisible(false);
            _resultsPopup = _popups.ShowPopup<ResultsPopup>(PopupTypes.Result);
            _resultsPopup.Continued += HandleResultsContinued;
            _resultsPopup.Present(new MissionResults
            {
                Rescued = _evacuation.Rescued,
                RescueTotal = _evacuation.RescueTotal,
                MoneyFound = _stats.MoneyFound,
                TimeSeconds = _stats.Time,
                FirePercent = FullPercent,
            }, photo, _cinematics.PhotoRig);
        }

        private void HandleResultsContinued()
        {
            _resultsPopup.Continued -= HandleResultsContinued;
            _popups.HidePopup(PopupTypes.Result);
            _cinematics.ReleaseFinale();
            _screen.SetHudVisible(true);
            _screen.SetCrosshairVisible(true);
            _player.enabled = true;
        }
    }
}
