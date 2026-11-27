using Gameplay.Cinematics;
using Gameplay.Dialogue;
using Gameplay.Entities.Interactive;
using System;
using Gameplay.Entities.Characters.Player;
using Gameplay.Entities.Characters.Residents;
using Services.Audio;
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
        private readonly DialogLibrary _dialogs;
        private readonly int _rescueTotal;

        private Rescuable _carried;

        public EvacuationController(FirstPersonController player, EvacuationWindow[] windows, int rescueTotal,
            EvacuationThrowCutscene cutscene, Action<ICinematic> present, GameplayScreen screen, TextLibrary texts,
            DialogLibrary dialogs)
        {
            _dialogs = dialogs;
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
            _player.Carry.CarryChanged += HandleCarryChanged;
        }

        public void Dispose()
        {
            foreach (EvacuationWindow window in _windows)
            {
                window.ThrowRequested -= HandleThrowRequested;
            }

            _cutscene.Finished -= HandleThrowFinished;
            _player.Carry.CarryChanged -= HandleCarryChanged;
            HandleCarryChanged(null);
        }

        private void HandleCarryChanged(Rescuable resident)
        {
            if (_carried != null)
            {
                _carried.HeadBumped -= HandleHeadBumped;
            }

            _carried = resident;
            if (_carried != null)
            {
                _carried.HeadBumped += HandleHeadBumped;
            }
        }

        private void HandleHeadBumped(Rescuable resident)
        {
            AudioService.Instance.PlayAt(SoundType.PropImpact, resident.HeadCenter);
            if (!_screen.IsDialogBusy && !string.IsNullOrEmpty(resident.BumpLineId))
            {
                _screen.Say(_dialogs.Get(resident.BumpLineId));
            }
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
            _screen.ShowToast(_texts.Format(TextId.ToastRescuedCount, Rescued, _rescueTotal));
        }
    }
}
