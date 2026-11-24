using Gameplay.Dialogue;
using Gameplay.Entities.Interactive;
using Services.Audio;
using System;
using UI.Screens.Variants.Gameplay;

namespace Gameplay.Mission.Floor3
{
    public sealed class ValveRoomController : IDisposable
    {
        private readonly GameplayScreen _screen;
        private readonly PipeValve[] _valves;
        private readonly Func<bool> _spendWheel;
        private readonly TextLibrary _texts;

        public ValveRoomController(GameplayScreen screen, PipeValve[] valves, Func<bool> spendWheel, TextLibrary texts)
        {
            _screen = screen;
            _valves = valves;
            _spendWheel = spendWheel;
            _texts = texts;
        }

        public bool IsSolved { get; private set; }
        public int WheelsUsed { get; private set; }

        public event Action WheelUsed;
        public event Action Solved;

        public void Init()
        {
            foreach (PipeValve valve in _valves)
            {
                valve.Setup(TakeWheel);
                valve.Turned += HandleTurned;
                valve.NoWheel += HandleNoWheel;
            }
        }

        public void Dispose()
        {
            foreach (PipeValve valve in _valves)
            {
                valve.Turned -= HandleTurned;
                valve.NoWheel -= HandleNoWheel;
            }
        }

        private bool TakeWheel()
        {
            if (!_spendWheel())
            {
                return false;
            }

            WheelsUsed++;
            _screen.ShowToast(_texts[TextId.ToastWheelPlaced]);
            WheelUsed?.Invoke();
            return true;
        }

        private void HandleNoWheel(PipeValve valve) => _screen.ShowToast(_texts[TextId.ToastNoWheel]);

        private void HandleTurned(PipeValve valve)
        {
            if (IsSolved)
            {
                return;
            }

            int turns = valve.Turns;
            int target = valve.TargetTurns;
            bool wrongWay = turns != 0 && Math.Sign(turns) != Math.Sign(target);
            bool tooFar = Math.Abs(turns) > Math.Abs(target);
            if (wrongWay || tooFar)
            {
                valve.ResetTurns();
                AudioService.Instance.PlayAt(SoundType.ValveRoomBurst, valve.transform.position);

                _screen.ShowToast(_texts[TextId.ToastValveWrongWay]);
                return;
            }

            foreach (PipeValve other in _valves)
            {
                if (other.Turns != other.TargetTurns)
                {
                    _screen.ShowToast(turns == target ? _texts[TextId.ToastValveClick] : _texts[TextId.ToastValveMore]);
                    return;
                }
            }

            IsSolved = true;
            _screen.ShowToast(_texts[TextId.ToastGasClosed]);
            Solved?.Invoke();
        }
    }
}
