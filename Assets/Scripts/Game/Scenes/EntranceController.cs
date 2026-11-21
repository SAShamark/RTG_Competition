using System;
using Game.Items;
using Game.World;
using UI.Screens.Variants.Gameplay;
using UnityEngine;

namespace Game.Scenes
{
    public class EntranceController : IDisposable
    {
        private const string FindCrowbarObjective = "Знайди лом біля машини";
        private const string ForceDoorObjective = "Зламай замок на дверях під'їзду";
        private const string RescueObjective = "Знайти й врятувати мешканців";

        private readonly Door[] _doors;
        private readonly Pickable _crowbar;
        private readonly ChainLock _chainLock;
        private readonly GameplayScreen _gameplayScreen;
        private readonly Vector3 _insidePoint;
        private bool _isTriggered;
        private bool _toldAboutLock;
        private bool _crowbarTaken;

        public string RadioHint { get; private set; } = "Махмуде, будинок №12! Двері під'їзду просто перед тобою.";

        public event Action OnSceneTriggered;

        private readonly NavigationGuide _nav;
        private readonly Transform _doorPoint;
        private readonly Transform _lockerPoint;

        public EntranceController(Door[] doors, Pickable crowbar, ChainLock chainLock, GameplayScreen gameplayScreen, Vector3 insidePoint,
            NavigationGuide nav, Transform doorPoint, Transform lockerPoint)
        {
            _nav = nav;
            _doorPoint = doorPoint;
            _lockerPoint = lockerPoint;
            _doors = doors;
            _crowbar = crowbar;
            _chainLock = chainLock;
            _gameplayScreen = gameplayScreen;
            _insidePoint = insidePoint;
        }

        public void Init()
        {
            foreach (Door door in _doors)
            {
                door.SetLocked(true);
                door.SetForceItem(ItemType.Crowbar);
                door.OnLockedAttempt += HandleLockedAttempt;
                door.OnForced += HandleForced;
            }

            _crowbar.OnTaken += HandleCrowbarTaken;
        }

        public void Dispose()
        {
            foreach (Door door in _doors)
            {
                door.OnLockedAttempt -= HandleLockedAttempt;
                door.OnForced -= HandleForced;
            }

            _crowbar.OnTaken -= HandleCrowbarTaken;
        }

        public bool IsOpen => _isTriggered;

        public void ForceOpen()
        {
            if (!_isTriggered && _doors.Length > 0)
            {
                HandleForced(_doors[0]);
            }
        }

        public void ShowRoute()
        {
            if (!_isTriggered)
            {
                bool toDoor = _crowbarTaken || !_toldAboutLock;
                Guide(toDoor ? _doorPoint : _lockerPoint, toDoor ? "Під'їзд" : "Машина");
            }
        }

        private void Guide(Transform point, string label)
        {
            if (point != null)
            {
                _nav.SetRoute(new[] { point }, null, label);
            }
        }

        private void HandleLockedAttempt(Door door)
        {
            if (_isTriggered || _crowbarTaken)
            {
                return;
            }

            _gameplayScreen.SetObjective(1, 3, FindCrowbarObjective);
            RadioHint = "Лом у відсіку збоку машини. Під речами пошукай!";
            if (!_toldAboutLock)
            {
                _toldAboutLock = true;
                _gameplayScreen.PartnerSay("Тут замок, візьми лом у відсіку зі спорядженням.", 3.5f);
            }

            Guide(_lockerPoint, "Машина");
        }

        private void HandleCrowbarTaken(Pickable pickable)
        {
            _crowbarTaken = true;
            if (_isTriggered)
            {
                return;
            }

            _gameplayScreen.SetObjective(1, 3, ForceDoorObjective);
            _gameplayScreen.ShowToast("Підібрано: лом");
            _gameplayScreen.Hotbar.SetAvailable(GameplayScreen.SlotCrowbar, true);
            _gameplayScreen.Hotbar.Select(GameplayScreen.SlotCrowbar);
            RadioHint = "Підходь до дверей і ламай замок ломом!";
            Guide(_doorPoint, "Під'їзд");
        }

        private void HandleForced(Door door)
        {
            if (_isTriggered)
            {
                return;
            }

            _isTriggered = true;
            _nav.Clear();
            _chainLock.Break((door.transform.position - _insidePoint).normalized);

            foreach (Door entranceDoor in _doors)
            {
                entranceDoor.Open(_insidePoint);
            }

            _gameplayScreen.SetObjective(2, 3, RescueObjective);
            _gameplayScreen.ShowToast("Зламано: замок");
            RadioHint = "Всередині ще люди! Шукай по поверхах, слухай крики.";
            OnSceneTriggered?.Invoke();
        }
    }
}
