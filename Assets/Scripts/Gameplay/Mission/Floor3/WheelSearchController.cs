using Gameplay.Dialogue;
using Gameplay.Entities.Interactive.Items;
using System;
using UI.Screens.Variants.Gameplay;

namespace Gameplay.Mission.Floor3
{
    public sealed class WheelSearchController : IDisposable
    {
        private const int WheelsTotal = 2;

        private readonly GameplayScreen _screen;
        private readonly StackItem[] _wheels;
        private readonly ValveRoomController _valveRoom;
        private readonly Floor3Controller _floor3;
        private bool _isNeeded;
        private bool _isShown;

        private readonly TextLibrary _texts;

        public WheelSearchController(GameplayScreen screen, StackItem[] wheels, ValveRoomController valveRoom, Floor3Controller floor3, TextLibrary texts)
        {
            _texts = texts;
            _screen = screen;
            _wheels = wheels;
            _valveRoom = valveRoom;
            _floor3 = floor3;
        }

        public void Initialize()
        {
            foreach (StackItem wheel in _wheels)
            {
                wheel.Taken += HandleTaken;
            }

            _valveRoom.WheelUsed += Refresh;
            _floor3.WheelsNeeded += HandleNeeded;
        }

        public void Dispose()
        {
            foreach (StackItem wheel in _wheels)
            {
                wheel.Taken -= HandleTaken;
            }

            _valveRoom.WheelUsed -= Refresh;
            _floor3.WheelsNeeded -= HandleNeeded;
        }

        private void HandleTaken(ItemPickup item)
        {
            if (item == _wheels[0])
            {
                _screen.ShowToast(_texts[TextId.ToastFlywheelFound]);
            }

            Refresh();
        }

        private void HandleNeeded()
        {
            _isNeeded = true;
            Refresh();
        }

        private void Refresh()
        {
            if (!_isNeeded || _valveRoom.IsSolved)
            {
                return;
            }

            int found = 0;
            foreach (StackItem wheel in _wheels)
            {
                found += wheel.IsTaken ? 1 : 0;
            }

            if (found < WheelsTotal)
            {
                string text = _texts.Format(TextId.ObjectiveFindWheels, found, WheelsTotal);
                if (_isShown)
                {
                    _screen.UpdateObjectiveText(text);
                }
                else
                {
                    _isShown = true;
                    _screen.SetObjective(text);
                }
            }
            else
            {
                _screen.SetObjective(_valveRoom.WheelsUsed < WheelsTotal ? _texts[TextId.ObjectivePlaceWheels] : _texts[TextId.ObjectiveTurnValves]);
            }
        }
    }
}
