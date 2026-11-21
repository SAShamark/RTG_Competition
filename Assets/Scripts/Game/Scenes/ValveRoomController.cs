using System;
using Game.World;
using UI.Screens.Variants.Gameplay;
using UnityEngine;

namespace Game.Scenes
{
    public class ValveRoomController
    {
        private readonly GameplayScreen _screen;
        private readonly PipeValve[] _valves;
        private readonly AudioSource _hissAudio;
        private readonly Func<int> _wheelsFound;

        private int _wheelsUsed;
        private bool _solved;

        public bool IsSolved => _solved;

        public event Action OnSolved;

        public ValveRoomController(GameplayScreen screen, PipeValve[] valves, ParticleSystem unused, AudioSource hissAudio,
            Func<int> wheelsFound)
        {
            _screen = screen;
            _valves = valves;
            _hissAudio = hissAudio;
            _wheelsFound = wheelsFound;
        }

        public void Init()
        {
            foreach (PipeValve valve in _valves)
            {
                valve.Setup(TakeWheel);
                valve.OnTurned += HandleTurned;
                valve.OnNoWheel += HandleNoWheel;
            }
        }

        public void Dispose()
        {
            foreach (PipeValve valve in _valves)
            {
                valve.OnTurned -= HandleTurned;
                valve.OnNoWheel -= HandleNoWheel;
            }
        }

        private bool TakeWheel()
        {
            if (_wheelsUsed >= _wheelsFound())
            {
                return false;
            }

            _wheelsUsed++;
            _screen.ShowToast("Маховик поставлено");
            return true;
        }

        private void HandleNoWheel(PipeValve valve) => _screen.ShowToast("Немає маховика");

        private void HandleTurned(PipeValve valve)
        {
            if (_solved)
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
                _hissAudio.Play();

                _screen.ShowToast("Шшш! Не туди — вентиль відскочив назад");
                return;
            }

            foreach (PipeValve other in _valves)
            {
                if (other.Turns != other.TargetTurns)
                {
                    _screen.ShowToast(turns == target ? "Клац!" : "Ще оберт...");
                    return;
                }
            }

            _solved = true;
            _screen.ShowToast("Газ перекрито");
            OnSolved?.Invoke();
        }
    }
}
