using System;
using Game.Scenes;

namespace Game.Rescue
{
    public class EvacuationController
    {
        private readonly FirstPersonController _player;
        private readonly EvacuationWindow[] _windows;
        private readonly EvacuationThrowCutscene _cutscene;
        private readonly Action<ICinematic> _present;

        private int _rescued;

        public int Rescued => _rescued;

        public event Action<Rescuable, int> OnRescued;
        public event Action<EvacuationWindow> OnWindowNotReady;
        public event Action OnThrowStarted;
        public event Action OnThrowFinished;

        public EvacuationController(FirstPersonController player, EvacuationWindow[] windows,
            EvacuationThrowCutscene cutscene, Action<ICinematic> present)
        {
            _player = player;
            _windows = windows;
            _cutscene = cutscene;
            _present = present;
        }

        public void Init()
        {
            foreach (EvacuationWindow window in _windows)
            {
                window.Init(_player.Carry);
                window.OnThrowRequested += HandleThrowRequested;
                window.OnNotReady += HandleNotReady;
            }

            _cutscene.OnFinished += HandleThrowFinished;
        }

        public void Dispose()
        {
            foreach (EvacuationWindow window in _windows)
            {
                window.OnThrowRequested -= HandleThrowRequested;
                window.OnNotReady -= HandleNotReady;
            }

            _cutscene.OnFinished -= HandleThrowFinished;
        }

        public void ForceRescue(Rescuable resident, EvacuationWindow window)
        {
            if (resident.State == RescueState.Rescued)
            {
                return;
            }

            if (_player.Carry.Carried == resident)
            {
                _player.Carry.Release();
            }

            resident.Launch();
            resident.transform.SetPositionAndRotation(window.Trampoline.SafePoint, window.Trampoline.SafeRotation);

            resident.OnRescued += HandleResidentRescued;
            resident.MarkRescued();
        }

        private void HandleThrowRequested(EvacuationWindow window)
        {
            if (_cutscene.IsPlaying || !_player.Carry.IsCarrying)
            {
                return;
            }

            Rescuable resident = _player.Carry.Release();
            resident.OnRescued += HandleResidentRescued;
            OnThrowStarted?.Invoke();
            _cutscene.Play(_player, resident, window);
            _present?.Invoke(_cutscene);
        }

        private void HandleResidentRescued(Rescuable resident)
        {
            resident.OnRescued -= HandleResidentRescued;
            _rescued++;
            OnRescued?.Invoke(resident, _rescued);
        }

        private void HandleNotReady(EvacuationWindow window) => OnWindowNotReady?.Invoke(window);

        private void HandleThrowFinished() => OnThrowFinished?.Invoke();
    }
}
