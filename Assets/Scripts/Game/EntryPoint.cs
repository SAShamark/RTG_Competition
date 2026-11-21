using Game.Fire;
using Game.Items;
using Game.Items.Extinguisher;
using Game.Rescue;
using Game.Scenes;
using Game.World;
using UI.Managers;
using UI.Results;
using UI.Screens;
using UI.Screens.Variants.Cutscene;
using UI.Screens.Variants.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    [DefaultExecutionOrder(100)]
    public class EntryPoint : MonoBehaviour
    {
        [SerializeField] private IntroCutscene _introCutscene;
        [SerializeField] private FirstPersonController _player;
        [SerializeField] private string _startObjective = "Потрапити в під'їзд";

        [Header("Entrance (scene 02)")]
        [SerializeField] private Door[] _entranceDoors;
        [SerializeField] private Pickable _crowbar;
        [SerializeField] private Transform _entranceInside;
        [SerializeField] private Transform _entranceNavPoint;
        [SerializeField] private Transform _lockerNavPoint;
        [SerializeField] private ChainLock _chainLock;
        [SerializeField] private UncleBurstCutscene _uncleBurstCutscene;

        [Header("Floor 1")]
        [SerializeField] private Door _knockDoor;
        [SerializeField] private Door[] _emptyFlatDoors;
        [SerializeField] private ElevatorButton _elevatorButton;

        [Header("Fire")]
        [SerializeField] private FireHeatLogic _fireHeat = new();

        [Header("Evacuation")]
        [SerializeField] private EvacuationWindow[] _evacuationWindows;
        [SerializeField] private EvacuationThrowCutscene _throwCutscene;
        [SerializeField] private int _rescueTotal = 3;

        [Header("Floor 2")]
        [SerializeField] private DialogueDirector _dialogue;
        [SerializeField] private Transform _playerBody;
        [SerializeField] private TriggerZone _floor2Arrival;
        [SerializeField] private Door _floor2GrandmaDoor;

        [Header("Navigation & bonuses")]
        [SerializeField] private NavigationGuide _navigation;
        [SerializeField] private Hatch _extinguisherCabinet;
        [SerializeField] private FireSource[] _floor1Fires;
        [SerializeField] private Transform[] _routeToGrandma;
        [SerializeField] private Collider _grandmaFlatZone;
        [SerializeField] private Transform[] _routeToFloor3;

        [Header("Floor 3")]
        [SerializeField] private Transform[] _routeToNeighbor;
        [SerializeField] private Collider _neighborFlatZone;
        [SerializeField] private Transform[] _routeToPantry;
        [SerializeField] private Collider _pantryZone;
        [SerializeField] private Door _pantryDoor;
        [SerializeField] private UI.Puzzles.LockpickScreen _lockpick;
        [SerializeField] private PipeValve[] _pipeValves;
        [SerializeField] private ParticleSystem _valveWaterSpray;
        [SerializeField] private AudioSource _valveCough;
        [SerializeField] private Transform[] _routeToKitchen;
        [SerializeField] private Collider _kitchenZone;
        [SerializeField] private Transform[] _routeToFloor4;
        [SerializeField] private FireSource _stoveFire;
        [SerializeField] private GasValve _gasValve;
        [SerializeField] private QuestItemPickup _valveHandle;
        [SerializeField] private PushableBlocker _fridge;
        [SerializeField] private Rescuable _neighbor;
        [SerializeField] private EvacuationWindow _floor3Window;
        [SerializeField] private Transform _partner;
        [SerializeField] private Transform _partnerSpotFloor3;

        [Header("Finale")]
        [SerializeField] private FinaleCutscene _finale;
        [SerializeField] private ResultsScreen _results;
        [SerializeField] private FireSource _floor2DoorFire;
        [SerializeField] private Rescuable _grandma;
        [SerializeField] private WallSafe _grandmaSafe;
        [SerializeField] private UI.Puzzles.SafePuzzleScreen _safePuzzle;
        [SerializeField] private QuestItemPickup _grandmaFlywheel;
        [SerializeField] private WallPainting[] _grandmaPaintings;
        [SerializeField] private DocumentsStash _grandmaDocuments;
        [SerializeField] private EvacuationWindow _floor2Window;

        private UIManager _uiManager;
        private GameplayScreen _gameplayScreen;
        private EntranceController _entranceController;
        private ICinematic _presented;
        private EvacuationController _evacuation;
        private Floor2Controller _floor2;
        private Floor3Controller _floor3;
        private float _missionTime;
        private bool _missionRunning;
        private bool _finaleStarted;
        private bool _extinguisherHintShown;
        private bool _carryHintShown;

        private void Start()
        {
            InitializeUI();
            InitializeInteraction();
            InitializeEntrance();
            InitializeFire();
            InitializeFloor1();
            InitializeEvacuation();
            InitializeTutorial();
        }

        private void InitializeEvacuation()
        {
            _evacuation = new EvacuationController(_player, _evacuationWindows, _throwCutscene, PresentThrow);
            _evacuation.Init();
            _evacuation.OnRescued += HandleResidentRescued;
            _evacuation.OnWindowNotReady += HandleWindowNotReady;
            _evacuation.OnThrowFinished += HandleThrowFinished;
            _player.Carry.OnCarryChanged += HandleCarryChanged;

            _dialogue.Init(_gameplayScreen, _player, _playerBody);
            _navigation.Init(_player.Head, (RectTransform)_gameplayScreen.transform);

            _floor2 = new Floor2Controller(_gameplayScreen, _dialogue, _player.Head, _floor2Arrival, _floor2GrandmaDoor,
                _floor2DoorFire, _grandma, _grandmaSafe, _grandmaPaintings, _grandmaDocuments, _floor2Window, _evacuation, _navigation, _routeToGrandma, _grandmaFlatZone, _routeToFloor3);
            _floor2.Init();
            _floor2.OnSafePuzzleRequested += ShowSafePuzzle;
            _grandmaFlywheel.OnTaken += HandleFlywheelTaken;
            _valveHandle.OnTaken += HandleWheelTaken;
            WatchFloor1Fires();
            InitializeFloor3();
            InitializeMoney();
        }

        private void PresentThrow(ICinematic cinematic)
        {
            _gameplayScreen.SetCrosshairVisible(false);
            Present(cinematic);
        }

        private void HandleCarryChanged(Rescuable resident)
        {
            _gameplayScreen.Hotbar.Select(resident != null ? -1 : _player.Items.HeldItem switch
            {
                ItemType.Crowbar => GameplayScreen.SlotCrowbar,
                ItemType.Extinguisher => GameplayScreen.SlotExtinguisher,
                _ => -1,
            });

            if (resident != null && !_carryHintShown)
            {
                _carryHintShown = true;
                _gameplayScreen.ShowToast("Неси до вікна з батутом · [G] поставити");
            }
        }

        private void HandleResidentRescued(Rescuable resident, int rescued)
        {
            _gameplayScreen.SetRescued(rescued, _rescueTotal);
        }

        private void HandleThrowFinished()
        {
            _gameplayScreen.SetCrosshairVisible(true);
            _gameplayScreen.ShowToast($"Врятовано: {_evacuation.Rescued} / {_rescueTotal}");
        }

        private void HandleWindowNotReady(EvacuationWindow window) =>
            _gameplayScreen.PartnerSay("Стоп! Не кидай — батут ще не розклали!", 3f);

        private void InitializeUI()
        {
            _uiManager = UIManager.Instance;
            _uiManager.Init();
            _uiManager.ScreensManager.ShowScreen(ScreenTypes.Gameplay);
            _gameplayScreen = _uiManager.ScreensManager.GetScreen(ScreenTypes.Gameplay) as GameplayScreen;
            _gameplayScreen.SetObjective(1, 3, _startObjective);
            _gameplayScreen.SetRescued(0, _rescueTotal);
            _gameplayScreen.SetFireProgress(0f);
            _gameplayScreen.SetHeat(0f);
            HotbarWidgetInit();
        }

        private void HotbarWidgetInit()
        {
            var hotbar = _gameplayScreen.Hotbar;
            hotbar.SetAvailable(GameplayScreen.SlotCrowbar, false);
            hotbar.SetAvailable(GameplayScreen.SlotHose, false);
            hotbar.SetAvailable(GameplayScreen.SlotExtinguisher, false);
            hotbar.SetAvailable(GameplayScreen.SlotRadio, true);
            hotbar.SetCharge(GameplayScreen.SlotExtinguisher, 1f);
            hotbar.Select(-1);
        }

        private void InitializeFire()
        {
            _fireHeat.Init(_player.Head);
            _fireHeat.OnHeatChanged += _gameplayScreen.SetHeat;

            _player.Items.OnItemChanged += HandleItemChanged;
            _player.Extinguisher.OnChargeChanged += HandleExtinguisherCharge;
            _player.Extinguisher.OnEmpty += HandleExtinguisherEmpty;
        }

        private Floor1Controller _floor1;

        private void InitializeFloor1()
        {
            _floor1 = new Floor1Controller(_gameplayScreen, _extinguisherCabinet, _player.Items, _floor1Fires);
            _floor1.Init();

            _knockDoor.SetLocked(true);
            _knockDoor.SetLockedPrompt("Відчинити", "зачинено");

            _elevatorButton.OnPressed += HandleElevatorPressed;

            foreach (Door door in _emptyFlatDoors)
            {
                door.SetLocked(true);
                door.SetLockedPrompt("Відчинити", "квартира порожня");
            }
        }

        private static readonly string[] ElevatorLines =
        {
            "Махмуд, ліфт під час пожежі? Ти серйозно?",
            "Тиснеш утретє — думаєш, він передумає?",
            "Все, я вимикаю рацію, поки ти тиснеш кнопку.",
        };

        private void HandleElevatorPressed(ElevatorButton button)
        {
            int index = Mathf.Min(button.Presses - 1, ElevatorLines.Length - 1);
            _gameplayScreen.PartnerSay(ElevatorLines[index], 3f);
        }

        private void HandleItemChanged(ItemType item)
        {
            if (item == ItemType.Extinguisher && !_extinguisherHintShown)
            {
                _extinguisherHintShown = true;
                _gameplayScreen.ShowToast("Вогнегасник [3] · затисни ЛКМ, щоб гасити");
            }

            var hotbar = _gameplayScreen.Hotbar;
            hotbar.SetAvailable(GameplayScreen.SlotCrowbar, _player.Items.Owns(ItemType.Crowbar));
            hotbar.SetAvailable(GameplayScreen.SlotExtinguisher, _player.Items.Owns(ItemType.Extinguisher));
            hotbar.Select(item switch
            {
                ItemType.Crowbar => GameplayScreen.SlotCrowbar,
                ItemType.Extinguisher => GameplayScreen.SlotExtinguisher,
                _ => -1,
            });
        }

        private void HandleExtinguisherCharge(float charge) =>
            _gameplayScreen.Hotbar.SetCharge(GameplayScreen.SlotExtinguisher, charge);

        private void HandleExtinguisherEmpty() => _gameplayScreen.ShowToast("Вогнегасник порожній");

        private void Update()
        {
            _fireHeat.Tick(Time.deltaTime);
            _floor2.Tick(Time.deltaTime);
            _floor3.Tick(Time.deltaTime);
            if (_missionRunning)
            {
                _missionTime += Time.deltaTime;
            }

            if (Keyboard.current.digit4Key.wasPressedThisFrame && _player.enabled)
            {
                string hint = _floor3.IsActive ? _floor3.RadioHint
                    : _floor2.IsActive ? _floor2.RadioHint
                    : _floor1.IsActive ? _floor1.RadioHint
                    : _entranceController.RadioHint;
                _gameplayScreen.Hotbar.Select(GameplayScreen.SlotRadio);
                _gameplayScreen.PartnerSay(hint, 3.5f);
            }
        }

        private void InitializeInteraction()
        {
            _player.Interaction.OnHintChanged += _gameplayScreen.SetInteractionHint;
            _player.Interaction.OnTargetChanged += _gameplayScreen.SetInteractionTarget;
        }

        private void InitializeEntrance()
        {
            _entranceController = new EntranceController(_entranceDoors, _crowbar, _chainLock, _gameplayScreen, _entranceInside.position,
                _navigation, _entranceNavPoint, _lockerNavPoint);
            _entranceController.Init();

            _uncleBurstCutscene.Init(_player);
            _uncleBurstCutscene.OnFinished += HandleUncleBurstFinished;
            _entranceController.OnSceneTriggered += HandleEntranceTriggered;
        }

        private void HandleEntranceTriggered()
        {
            _gameplayScreen.SetCrosshairVisible(false);
            _uncleBurstCutscene.Play();
            Present(_uncleBurstCutscene);
        }

        private void HandleUncleBurstFinished()
        {
            _gameplayScreen.SetCrosshairVisible(true);
            _gameplayScreen.ShowToast("Котів урятовано: 6  (не тобою)");
            _gameplayScreen.PartnerSay("Це... він що, котів рятував?! Заходь! Вогонь біля сходів — вогнегасник у червоному щитку.", 4.5f);
            _floor1.Begin();
        }

        private void InitializeTutorial()
        {
            if (!_introCutscene.IsPlaying)
            {
                _missionRunning = true;
                _gameplayScreen.SetCrosshairVisible(true);
                return;
            }

            _introCutscene.OnFinished += HandleTutorialFinished;
            Present(_introCutscene);
        }

        private void HandleTutorialFinished()
        {
            _introCutscene.OnFinished -= HandleTutorialFinished;
            _missionRunning = true;
            _gameplayScreen.SetCrosshairVisible(true);
            _gameplayScreen.PartnerSay("Махмуде, будинок №12! Там люди — бігом!", 3.5f);
            _entranceController.ShowRoute();
        }

        private void Present(ICinematic cinematic)
        {
            _presented = cinematic;
            _uiManager.ScreensManager.ShowScreen(ScreenTypes.Intro);
            var cutsceneScreen = (CutsceneScreen)_uiManager.ScreensManager.GetScreen(ScreenTypes.Intro);
            cinematic.OnFinished += HandlePresentedFinished;
            cutsceneScreen.Present(cinematic);
        }

        private void HandlePresentedFinished()
        {
            _presented.OnFinished -= HandlePresentedFinished;
            _presented = null;
            _uiManager.ScreensManager.ShowScreen(ScreenTypes.Gameplay);
        }

        private void InitializePantryLock()
        {
            _pantryDoor.SetLocked(true);
            _pantryDoor.SetLockedPrompt("Відкрити відмичкою", "");
            _pantryDoor.OnLockedAttempt += HandlePantryLocked;
        }

        private void HandlePantryLocked(Door door)
        {
            if (_lockpick.IsOpen || !_player.enabled)
            {
                return;
            }

            _player.enabled = false;
            _gameplayScreen.SetCrosshairVisible(false);
            _gameplayScreen.SetHudVisible(false);
            _gameplayScreen.ClearDialog();
            _lockpick.Show(
                () =>
                {
                    RestorePlayer();
                    door.OnLockedAttempt -= HandlePantryLocked;
                    door.Open(_player.transform.position);
                    _gameplayScreen.ShowToast("Замок відкрито");
                },
                RestorePlayer);

            void RestorePlayer()
            {
                _player.enabled = true;
                _gameplayScreen.SetCrosshairVisible(true);
                _gameplayScreen.SetHudVisible(true);
            }
        }

        private ValveRoomController _valveRoom;

        private void InitializeValveRoom()
        {
            _valveRoom = new ValveRoomController(_gameplayScreen, _pipeValves, _valveWaterSpray, _valveCough, CountWheels);
            _valveRoom.Init();
            _valveRoom.OnSolved += HandleValveRoomSolved;
        }

        private int CountWheels() =>
            (_grandmaFlywheel.IsTaken ? 1 : 0) + (_valveHandle.IsTaken ? 1 : 0);

        private void HandleValveRoomSolved()
        {
            _gameplayScreen.HideWheels();
            _gasValve.ForceClose();
        }

        private void InitializeFloor3()
        {
            InitializePantryLock();
            InitializeValveRoom();
            _fridge.enabled = false;
            _floor3 = new Floor3Controller(_gameplayScreen, _dialogue, _player.Head, _player.Extinguisher, _navigation,
                _routeToNeighbor, _neighborFlatZone, _routeToPantry, _pantryZone, _routeToKitchen, _kitchenZone, _routeToFloor4,
                _stoveFire, _gasValve, _valveHandle, _neighbor, _floor3Window, _evacuation, _partner, _partnerSpotFloor3);
            _floor3.Init();
            _floor2.OnFinished += _floor3.Begin;
            _floor3.OnFinished += PlayFinale;
            _floor3.OnWheelsNeeded += UpdateWheelsHud;
        }

        private void WatchFloor1Fires()
        {
            foreach (FireSource fire in _floor1Fires)
            {
                fire.OnExtinguished += HandleFloor1FireOut;
            }
        }

        private void HandleFloor1FireOut(FireSource fire)
        {
            foreach (FireSource f in _floor1Fires)
            {
                if (!f.IsOut)
                {
                    return;
                }
            }

            _floor2.ShowRouteToFlat();
        }

        private void InitializeMoney()
        {
            foreach (MoneyPickup money in FindObjectsByType<MoneyPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                money.OnTaken += HandleMoneyTaken;
            }
        }

        private void HandleMoneyTaken(MoneyPickup money)
        {
            money.OnTaken -= HandleMoneyTaken;
            _moneyFound += money.Amount;
            _gameplayScreen.AddMoney(money.Amount);
            _gameplayScreen.ShowToast($"+{money.Amount} ₴");
        }

        private int _moneyFound;

        public int MoneyFound => _moneyFound;

        public void PlayFinale()
        {
            if (_finaleStarted)
            {
                return;
            }

            _finaleStarted = true;
            _missionRunning = false;
            _gameplayScreen.SetCrosshairVisible(false);
            _finale.OnFinished += HandleFinaleFinished;
            _finale.Play(_player);
            Present(_finale);
        }

        private void HandleFinaleFinished()
        {
            _finale.OnFinished -= HandleFinaleFinished;
            ShowResults(_finale.CapturedFrame);
        }

        public void ShowResults() => ShowResults(null);

        private void ShowResults(Texture photo)
        {
            _missionRunning = false;
            _player.enabled = false;

            _gameplayScreen.SetCrosshairVisible(false);
            _gameplayScreen.SetHudVisible(false);
            _results.Show(new MissionResults
            {
                Rescued = _evacuation.Rescued,
                RescueTotal = _rescueTotal,
                MoneyFound = _moneyFound,
                TimeSeconds = _missionTime,
                FirePercent = FirePercentOut(),
            }, photo);
        }

        private void HandleFlywheelTaken(QuestItemPickup item)
        {
            item.OnTaken -= HandleFlywheelTaken;
            _gameplayScreen.ShowToast("Знайдено: маховик вентиля");
            UpdateWheelsHud();
        }

        private void HandleWheelTaken(QuestItemPickup item)
        {
            item.OnTaken -= HandleWheelTaken;
            UpdateWheelsHud();
        }

        private void UpdateWheelsHud()
        {
            if (!_valveRoom.IsSolved)
            {
                _gameplayScreen.SetWheels(CountWheels(), 2);
            }
        }

        private void ShowSafePuzzle(WallSafe safe)
        {
            _player.enabled = false;
            _gameplayScreen.SetCrosshairVisible(false);
            _gameplayScreen.SetHudVisible(false);
            _gameplayScreen.ClearDialog();
            _safePuzzle.Show(
                () => { RestorePlayer(); safe.Open(); },
                () => { RestorePlayer(); _floor2.SafePuzzleCancelled(); });

            void RestorePlayer()
            {
                _player.enabled = true;
                _gameplayScreen.SetCrosshairVisible(true);
                _gameplayScreen.SetHudVisible(true);
            }
        }

        private float FirePercentOut()
        {
            FireSource[] fires = FindObjectsByType<FireSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (fires.Length == 0)
            {
                return 100f;
            }

            int outCount = 0;
            foreach (FireSource fire in fires)
            {
                if (fire.IsOut)
                {
                    outCount++;
                }
            }

            return 100f * outCount / fires.Length;
        }

        public void CheatSkipIntro()
        {
            if (_introCutscene.IsPlaying)
            {
                _introCutscene.Skip();
            }
        }

        public void CheatOpenEntrance()
        {
            CheatSkipIntro();
            if (_entranceController.IsOpen)
            {
                return;
            }

            _entranceController.ForceOpen();
            if (_uncleBurstCutscene.IsPlaying)
            {
                _uncleBurstCutscene.Skip();
            }
        }

        public void CheatPutOutFloor1()
        {
            foreach (FireSource fire in _floor1Fires)
            {
                fire.ForceOut();
            }
        }

        public void CheatGiveTools()
        {
            foreach (Pickable pickable in FindObjectsByType<Pickable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (pickable.CanInteract && !_player.Items.Owns(pickable.ItemType))
                {
                    pickable.Interact(_player.transform);
                }
            }

            foreach (ExtinguisherItem item in FindObjectsByType<ExtinguisherItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                item.Refill();
            }
        }

        public void CheatRescueGrandma()
        {
            CheatOpenEntrance();
            CheatPutOutFloor1();
            _evacuation.ForceRescue(_grandma, _floor2Window);
        }

        public void CheatGiveWheels()
        {
            foreach (QuestItemPickup wheel in new[] { _grandmaFlywheel, _valveHandle })
            {
                if (!wheel.IsTaken)
                {
                    wheel.Interact(_player.transform);
                }
            }
        }

        public void CheatOpenPantry()
        {
            if (_pantryDoor.IsLocked)
            {
                _pantryDoor.OnLockedAttempt -= HandlePantryLocked;
                _pantryDoor.Open(_player.transform.position);
            }
        }

        public void CheatRescueNeighbor()
        {
            CheatRescueGrandma();
            _gasValve.ForceClose();
            _stoveFire.SetReignite(false);
            _stoveFire.ForceOut();
            _evacuation.ForceRescue(_neighbor, _floor3Window);
        }

        public void CheatTeleport(Transform point)
        {
            var controller = _player.GetComponent<CharacterController>();
            controller.enabled = false;
            _player.transform.SetPositionAndRotation(point.position + Vector3.up * 0.1f, Quaternion.Euler(0f, point.eulerAngles.y, 0f));
            controller.enabled = true;
        }

        private void OnDestroy()
        {
            foreach (FireSource fire in _floor1Fires)
            {
                fire.OnExtinguished -= HandleFloor1FireOut;
            }

            _valveRoom.OnSolved -= HandleValveRoomSolved;
            _valveRoom.Dispose();
            _floor1.Dispose();
            _floor2.OnFinished -= _floor3.Begin;
            _floor2.OnSafePuzzleRequested -= ShowSafePuzzle;
            _floor3.OnFinished -= PlayFinale;
            _floor3.OnWheelsNeeded -= UpdateWheelsHud;
            _floor2.Dispose();
            _floor3.Dispose();
            _evacuation.OnRescued -= HandleResidentRescued;
            _evacuation.OnWindowNotReady -= HandleWindowNotReady;
            _evacuation.OnThrowFinished -= HandleThrowFinished;
            _evacuation.Dispose();
            _player.Carry.OnCarryChanged -= HandleCarryChanged;
            _elevatorButton.OnPressed -= HandleElevatorPressed;
            _fireHeat.OnHeatChanged -= _gameplayScreen.SetHeat;

            _player.Items.OnItemChanged -= HandleItemChanged;
            _player.Extinguisher.OnChargeChanged -= HandleExtinguisherCharge;
            _player.Extinguisher.OnEmpty -= HandleExtinguisherEmpty;

            _entranceController.OnSceneTriggered -= HandleEntranceTriggered;
            _entranceController.Dispose();
            _uncleBurstCutscene.OnFinished -= HandleUncleBurstFinished;
            _player.Interaction.OnHintChanged -= _gameplayScreen.SetInteractionHint;
            _player.Interaction.OnTargetChanged -= _gameplayScreen.SetInteractionTarget;
            _introCutscene.OnFinished -= HandleTutorialFinished;
            if (_presented != null)
            {
                _presented.OnFinished -= HandlePresentedFinished;
            }
        }
    }
}
