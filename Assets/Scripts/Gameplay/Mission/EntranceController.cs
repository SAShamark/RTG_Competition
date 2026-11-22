using Gameplay.Dialogue;
using Gameplay.Entities.Environment;
using Gameplay.Entities.Interactive;
using Gameplay.Entities.Interactive.Items;
using System;
using UI.Screens.Variants.Gameplay;
using UnityEngine;

namespace Gameplay.Mission
{
    public sealed class EntranceController : StageController
    {

        private bool _isTriggered;
        private bool _toldAboutLock;
        private bool _axeTaken;

        private string _radioHint;
        private float _openTimer = -1f;

        public event Action SceneTriggered;




        private readonly EntranceSetup _setup;
        private readonly GameplayScreen _gameplayScreen;
        private readonly NavigationGuide _nav;
        private readonly DialogLibrary _dialogs;
        private readonly TextLibrary _texts;

        public EntranceController(EntranceSetup setup, MissionContext context)
        {
            _setup = setup;
            _gameplayScreen = context.Screen;
            _nav = context.Navigation;
            _dialogs = context.Dialogue.Library;
            _texts = context.Texts;
            _radioHint = _texts[TextId.RadioEntranceStart];
        }

        public override void Init()
        {
            foreach (Door door in _setup.Doors)
            {
                door.SetLocked(true);
                door.SetForceItem(ItemType.Axe);
                door.LockedAttempt += HandleLockedAttempt;
                door.Forced += HandleForced;
            }

            _setup.Axe.Taken += HandleAxeTaken;
        }

        public override void Dispose()
        {
            foreach (Door door in _setup.Doors)
            {
                door.LockedAttempt -= HandleLockedAttempt;
                door.Forced -= HandleForced;
            }

            _setup.Axe.Taken -= HandleAxeTaken;
        }

        public override bool IsActive => !_isTriggered;
        public override string RadioHint => _radioHint;
        public bool IsOpen => _isTriggered;

        public void ForceOpen()
        {
            if (!_isTriggered && _setup.Doors.Length > 0)
            {
                HandleForced(_setup.Doors[0]);
            }
        }

        public void ShowRoute()
        {
            if (!_isTriggered)
            {
                bool toDoor = _axeTaken || !_toldAboutLock;
                Guide(toDoor ? _setup.DoorPoint : _setup.LockerPoint, toDoor ? _texts[TextId.NavEntrance] : _texts[TextId.NavTruck]);
            }
        }

        private void Guide(Transform point, string label)
        {
            _nav.SetRoute(new[] { point }, null, label);
        }

        private void HandleLockedAttempt(Door door)
        {
            if (_isTriggered || _axeTaken)
            {
                return;
            }

            _gameplayScreen.SetObjective(_texts[TextId.ObjectiveFindAxe]);
            _radioHint = _texts[TextId.RadioFindAxe];
            if (!_toldAboutLock)
            {
                _toldAboutLock = true;
                _gameplayScreen.Say(_dialogs.Get("P03"));
            }

            Guide(_setup.LockerPoint, _texts[TextId.NavTruck]);
        }

        private void HandleAxeTaken(ItemPickup pickable)
        {
            _axeTaken = true;
            if (_isTriggered)
            {
                return;
            }

            _gameplayScreen.SetObjective(_texts[TextId.ObjectiveForceDoor]);
            _gameplayScreen.ShowToast(_texts[TextId.ToastAxeTaken]);
            _radioHint = _texts[TextId.RadioForceDoor];
            Guide(_setup.DoorPoint, _texts[TextId.NavEntrance]);
        }

        private void HandleForced(Door door)
        {
            if (_isTriggered)
            {
                return;
            }

            _isTriggered = true;
            _nav.Clear();
            _setup.ChainLock.Break((door.transform.position - _setup.InsidePoint.position).normalized);
            _gameplayScreen.ShowToast(_texts[TextId.ToastLockBroken]);
            _openTimer = _setup.CutsceneDelay;
        }

        public override void Tick(float deltaTime)
        {
            if (_openTimer < 0f)
            {
                return;
            }

            _openTimer -= deltaTime;
            if (_openTimer < 0f)
            {
                OpenDoors();
            }
        }

        private void OpenDoors()
        {
            foreach (Door entranceDoor in _setup.Doors)
            {
                entranceDoor.Open(_setup.InsidePoint.position);
            }

            _gameplayScreen.SetObjective(_texts[TextId.ObjectiveRescueResidents]);
            _radioHint = _texts[TextId.RadioSearchFloors];
            SceneTriggered?.Invoke();
        }
    }
}
