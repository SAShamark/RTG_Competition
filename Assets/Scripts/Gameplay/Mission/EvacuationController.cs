using Gameplay.Cinematics;
using Gameplay.Entities.Interactive;
using Gameplay.Entities.Player;
using Gameplay.Entities.Residents;
using System;

namespace Gameplay.Mission
{
    public sealed class EvacuationController : IDisposable
    {
        private readonly FirstPersonController _player;
        private readonly EvacuationWindow[] _windows;
        private readonly EvacuationThrowCutscene _cutscene;
        private readonly Action<ICinematic> _present;

        public int Rescued { get; private set; }

        public event Action<Rescuable, int> ResidentRescued;
        public event Action<EvacuationWindow> WindowNotReady;
        public event Action ThrowStarted;
        public event Action ThrowFinished;

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
                window.ThrowRequested += HandleThrowRequested;
                window.NotReady += HandleNotReady;
            }

            _cutscene.Finished += HandleThrowFinished;
        }

        public void Dispose()
        {
            foreach (EvacuationWindow window in _windows)
            {
                window.ThrowRequested -= HandleThrowRequested;
                window.NotReady -= HandleNotReady;
            }

            _cutscene.Finished -= HandleThrowFinished;
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

            resident.Rescued += HandleResidentRescued;
            resident.MarkRescued();
        }

        private void HandleThrowRequested(EvacuationWindow window)
        {
            if (_cutscene.IsPlaying || !_player.Carry.IsCarrying)
            {
                return;
            }

            Rescuable resident = _player.Carry.Release();
            resident.Rescued += HandleResidentRescued;
            ThrowStarted?.Invoke();
            _cutscene.Play(_player, resident, window);
            _present?.Invoke(_cutscene);
        }

        private void HandleResidentRescued(Rescuable resident)
        {
            resident.Rescued -= HandleResidentRescued;
            Rescued++;
            ResidentRescued?.Invoke(resident, Rescued);
        }

        private void HandleNotReady(EvacuationWindow window) => WindowNotReady?.Invoke(window);

        private void HandleThrowFinished() => ThrowFinished?.Invoke();
    }
}
