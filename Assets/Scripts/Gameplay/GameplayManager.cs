using Gameplay.Entities.Environment;
using System;
using Debugging;
using Gameplay.Cinematics;
using Gameplay.Dialogue;
using Gameplay.Entities.Interactive;
using Gameplay.Entities.Interactive.Items;
using Gameplay.Entities.Player;
using Gameplay.Mission;
using Gameplay.Mission.Floor3;
using UI.Managers;
using UI.Popups;
using UI.Popups.Variants.Puzzles;
using UI.Popups.Variants.Results;
using UI.Screens;
using UI.Screens.Variants.Gameplay;
using UnityEngine;

namespace Gameplay
{
    [Serializable]
    public sealed class GameplayManager
    {
        private const float AllFiresOutPercent = 100f;

        [SerializeField] private FirstPersonController _player;
        [SerializeField] private Transform _playerBody;
        [SerializeField] private TextLibrary _texts;
        [SerializeField] private FireHeatLogic _fireHeat = new();
        [SerializeField] private DialogueDirector _dialogue = new();
        [SerializeField] private NavigationGuide _navigation = new();
        [SerializeField] private FogController _fog = new();
        [SerializeField] private MissionStats _stats = new();

        [Header("Stages")]
        [SerializeField] private EntranceSetup _entranceSetup = new();
        [SerializeField] private Floor1Setup _floor1Setup = new();
        [SerializeField] private Floor2Setup _floor2Setup = new();
        [SerializeField] private Floor3Setup _floor3Setup = new();

        [Header("Evacuation")]
        [SerializeField] private EvacuationWindow[] _evacuationWindows;
        [SerializeField, Min(1)] private int _rescueTotal = 3;

        [Header("Cinematics")]
        [SerializeField] private IntroCutscene _introCutscene;
        [SerializeField] private UncleBurstCutscene _uncleBurstCutscene;
        [SerializeField] private EvacuationThrowCutscene _throwCutscene;
        [SerializeField] private FinaleCutscene _finale;
        [SerializeField] private GasBurstCutscene _gasBurstCutscene;
        [SerializeField] private ResultsPhotoRig _photoRig = new();

        private GameplayScreen _screen;
        private IPopupsManager _popups;
        private CinematicPresenter _cinematics;
        private PuzzlePresenter _puzzles;
        private RadioController _radio;
        private EvacuationController _evacuation;
        private EntranceController _entrance;
        private Floor1Controller _floor1;
        private Floor2Controller _floor2;
        private Floor3Controller _floor3;
        private ValveRoomController _valveRoom;
        private WheelSearchController _wheelSearch;
        private StageController[] _stages;
        private bool _finaleStarted;

        public GameplayCheats Cheats { get; private set; }

        public void Initialize(UIManager uiManager)
        {
            _screen = (GameplayScreen)uiManager.ScreensManager.GetScreen(ScreenTypes.Gameplay);
            _screen.SetObjective(_texts[TextId.ObjectiveEnterBuilding]);
            _popups = uiManager.PopupsManager;
            _cinematics = new CinematicPresenter(uiManager);
            _puzzles = new PuzzlePresenter(_player, _screen, _popups);
            _stats.Init(_screen, _texts);
            _fog.Init();

            _fireHeat.Init(_player.Head);
            _dialogue.Init(_screen, _player, _playerBody);
            _navigation.Init(_player.Head, _screen.NavMarker, _texts);
            _evacuation = new EvacuationController(_player, _evacuationWindows, _throwCutscene, PresentThrow);
            _evacuation.Init();

            var context = new MissionContext(_screen, _player, _dialogue, _navigation, _evacuation, _texts);
            InitializeEntrance(context);
            InitializeFloor1(context);
            InitializeFloor2(context);
            InitializeFloor3(context);
            _stages = new StageController[] { _floor3, _floor2, _floor1 };

            _screen.Init(_player, _fireHeat, _evacuation, _rescueTotal, _texts);
            _radio = new RadioController(_player, _screen, CurrentRadioHint);
            Cheats = new GameplayCheats(_player, _introCutscene, _uncleBurstCutscene, _entrance, _evacuation,
                _floor1Setup, _floor2Setup, _floor3Setup, OpenPantry);

            InitializeIntro();
        }

        public void Tick(float deltaTime)
        {
            _fireHeat.Tick(deltaTime);
            _dialogue.Tick(deltaTime);
            _entrance.Tick(deltaTime);
            foreach (StageController stage in _stages)
            {
                stage.Tick(deltaTime);
            }

            _stats.Tick(deltaTime);
            _fog.Tick(Camera.main.transform.position, deltaTime);
            _radio.Tick();
        }

        public void LateTick() => _dialogue.LateTick();

        public void Dispose()
        {
            _navigation.Dispose();
            foreach (FireSource fire in _floor1Setup.Fires)
            {
                fire.Extinguished -= HandleFloor1FireOut;
            }

            _introCutscene.Finished -= HandleIntroFinished;
            _uncleBurstCutscene.Finished -= HandleUncleBurstFinished;
            _entrance.SceneTriggered -= HandleEntranceTriggered;
            _floor2.Finished -= _floor3.Begin;
            _floor2.SafePuzzleRequested -= ShowSafePuzzle;
            _floor3.Finished -= PlayGasBurst;
            _gasBurstCutscene.Finished -= PlayFinale;
            _valveRoom.Solved -= _floor3Setup.Valve.ForceClose;
            _floor3Setup.PantryDoor.LockedAttempt -= HandlePantryLocked;

            _screen.Dispose();
            _wheelSearch.Dispose();
            _valveRoom.Dispose();
            _floor3.Dispose();
            _floor2.Dispose();
            _evacuation.Dispose();
            _floor1.Dispose();
            _entrance.Dispose();
            _stats.Dispose();
            _fog.Dispose();
            _cinematics.Dispose();
        }

        private void PlayGasBurst()
        {
            _screen.SetCrosshairVisible(false);
            _gasBurstCutscene.Play(_player);
            _cinematics.Present(_gasBurstCutscene);
        }

        public void PlayFinale()
        {
            if (_finaleStarted)
            {
                return;
            }

            _finaleStarted = true;
            _stats.Stop();
            _screen.SetCrosshairVisible(false);
            _finale.Finished += HandleFinaleFinished;
            _finale.SetFireStart(_stats.FirePercentOut / 100f);
            _finale.Play(_player);
            _cinematics.Present(_finale);
        }

        public void ShowResults() => ShowResults(null, _stats.FirePercentOut);

        private void InitializeEntrance(MissionContext context)
        {
            _entrance = new EntranceController(_entranceSetup, context);
            _entrance.Init();
            _entrance.SceneTriggered += HandleEntranceTriggered;

            _uncleBurstCutscene.Init(_player);
            _uncleBurstCutscene.Finished += HandleUncleBurstFinished;
        }

        private void InitializeFloor1(MissionContext context)
        {
            _floor1 = new Floor1Controller(_floor1Setup, context);
            _floor1.Init();
            foreach (FireSource fire in _floor1Setup.Fires)
            {
                fire.Extinguished += HandleFloor1FireOut;
            }
        }

        private void InitializeFloor2(MissionContext context)
        {
            _floor2 = new Floor2Controller(_floor2Setup, context);
            _floor2.Init();
            _floor2.SafePuzzleRequested += ShowSafePuzzle;
        }

        private void InitializeFloor3(MissionContext context)
        {
            Door pantryDoor = _floor3Setup.PantryDoor;
            pantryDoor.SetLocked(true);
            pantryDoor.SetLockedPrompt(_texts[TextId.PromptLockpick], "");
            pantryDoor.LockedAttempt += HandlePantryLocked;

            _valveRoom = new ValveRoomController(_screen, _floor3Setup.PipeValves, () => _player.Items.RemoveOne(ItemType.Wheel), _texts);
            _valveRoom.Init();
            _valveRoom.Solved += _floor3Setup.Valve.ForceClose;

            _floor3Setup.Fridge.enabled = false;
            _floor3 = new Floor3Controller(_floor3Setup, context);
            _floor3.Init();
            _floor2.Finished += _floor3.Begin;
            _floor3.Finished += PlayGasBurst;
            _gasBurstCutscene.Finished += PlayFinale;

            _wheelSearch = new WheelSearchController(_screen, new[] { _floor2Setup.Flywheel, _floor3Setup.Handle }, _valveRoom, _floor3, _texts);
            _wheelSearch.Initialize();
        }

        private void InitializeIntro()
        {
            if (!_introCutscene.IsPlaying)
            {
                _stats.Begin();
                _screen.SetCrosshairVisible(true);
                return;
            }

            _introCutscene.Finished += HandleIntroFinished;
            _cinematics.Present(_introCutscene);
        }

        private string CurrentRadioHint()
        {
            foreach (StageController stage in _stages)
            {
                if (stage.IsActive)
                {
                    return stage.RadioHint;
                }
            }

            return _entrance.RadioHint;
        }

        private void PresentThrow(ICinematic cinematic)
        {
            _screen.SetCrosshairVisible(false);
            _cinematics.Present(cinematic);
        }

        private void HandleIntroFinished()
        {
            _introCutscene.Finished -= HandleIntroFinished;
            _stats.Begin();
            _screen.SetCrosshairVisible(true);
            _screen.Say(_dialogue.Library.Get("P02"));
            _entrance.ShowRoute();
        }

        private void HandleEntranceTriggered()
        {
            _screen.SetCrosshairVisible(false);
            _uncleBurstCutscene.Play();
            _cinematics.Present(_uncleBurstCutscene);
        }

        private void HandleUncleBurstFinished()
        {
            _screen.SetCrosshairVisible(true);
            _screen.ShowToast(_texts[TextId.ToastCatsRescued]);
            _screen.Say(_dialogue.Library.Get("P04"));
            _floor1.Begin();
        }

        private void HandleFloor1FireOut(FireSource fire)
        {
            foreach (FireSource floorFire in _floor1Setup.Fires)
            {
                if (!floorFire.IsOut)
                {
                    return;
                }
            }

            _floor2.ShowRouteToFlat();
        }

        private void HandlePantryLocked(Door door)
        {
            if (_puzzles.IsBusy || !_player.enabled)
            {
                return;
            }

            _puzzles.Present<LockpickPopup>(PopupTypes.Lockpick, (popup, solved, closed) => popup.Open(solved, closed), () =>
            {
                OpenPantry();
                _screen.ShowToast(_texts[TextId.ToastLockOpened]);
            });
        }

        private void OpenPantry()
        {
            Door door = _floor3Setup.PantryDoor;
            if (!door.IsLocked)
            {
                return;
            }

            door.LockedAttempt -= HandlePantryLocked;
            door.Open(_player.transform.position);
        }

        private void ShowSafePuzzle(WallSafe safe) =>
            _puzzles.Present<SafePuzzlePopup>(PopupTypes.SafePuzzle, (popup, solved, closed) => popup.Open(solved, closed), safe.Open);

        private void HandleFinaleFinished()
        {
            _finale.Finished -= HandleFinaleFinished;
            ShowResults(_finale.CapturedFrame, AllFiresOutPercent);
        }

        private void ShowResults(Texture photo, float firePercent)
        {
            _stats.Stop();
            _player.enabled = false;
            _screen.SetCrosshairVisible(false);
            _screen.SetHudVisible(false);
            _popups.ShowPopup<ResultsPopup>(PopupTypes.Result).Present(new MissionResults
            {
                Rescued = _evacuation.Rescued,
                RescueTotal = _rescueTotal,
                MoneyFound = _stats.MoneyFound,
                TimeSeconds = _stats.Time,
                FirePercent = firePercent,
            }, photo, _photoRig);
        }
    }
}
