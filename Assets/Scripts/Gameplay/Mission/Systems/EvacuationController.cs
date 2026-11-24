using Gameplay.Cinematics;
using Gameplay.Dialogue;
using Gameplay.Entities.Interactive;
using System;
using Gameplay.Entities.Characters.Player;
using Gameplay.Entities.Characters.Residents;
using UI.Screens.Variants.Gameplay;

namespace Gameplay.Mission.Systems
{
    public sealed class EvacuationController : IDisposable
    {
        private readonly FirstPersonController _player;
        private readonly EvacuationWindow[] _windows;
        private readonly EvacuationThrowCutscene _cutscene;
        private readonly Action<ICinematic> _present;
        private readonly GameplayScreen _screen;
        private readonly TextLibrary _texts;
        private readonly int _rescueTotal;

        public EvacuationController(FirstPersonController player, EvacuationWindow[] windows, int rescueTotal,
            EvacuationThrowCutscene cutscene, Action<ICinematic> present, GameplayScreen screen, TextLibrary texts)
        {
            _player = player;
            _windows = windows;
            _rescueTotal = rescueTotal;
            _cutscene = cutscene;
            _present = present;
            _screen = screen;
            _texts = texts;
        }

        public int Rescued { get; private set; }
        public int RescueTotal => _rescueTotal;

        public event Action<Rescuable, int> ResidentRescued;

        public void Init()
        {
            foreach (EvacuationWindow window in _windows)
            {
                window.Init(_player.Carry);
                window.ThrowRequested += HandleThrowRequested;
            }

            _cutscene.Finished += HandleThrowFinished;
        }

        public void Dispose()
        {
            foreach (EvacuationWindow window in _windows)
            {
                window.ThrowRequested -= HandleThrowRequested;
            }

            _cutscene.Finished -= HandleThrowFinished;
        }

        private void HandleThrowRequested(EvacuationWindow window)
        {
            if (_cutscene.IsPlaying || !_player.Carry.IsCarrying)
            {
                return;
            }

            Rescuable resident = _player.Carry.Release();
            resident.Rescued += HandleResidentRescued;
            _cutscene.Play(_player, resident, window);
            _present(_cutscene);
        }

        private void HandleResidentRescued(Rescuable resident)
        {
            resident.Rescued -= HandleResidentRescued;
            Rescued++;
            _screen.ShowRescued(Rescued, _rescueTotal);
            ResidentRescued?.Invoke(resident, Rescued);
        }

        private void HandleThrowFinished()
        {
            _screen.SetCrosshairVisible(true);
            _screen.ShowToast(_texts.Format(TextId.ToastRescuedCount, Rescued, _rescueTotal));
        }
    }
}
