using System;
using Gameplay.Entities.Characters.Player;
using UI.Popups;
using UI.Popups.Variants.Puzzles;
using UI.Screens.Variants.Gameplay;

namespace Gameplay.Mission.Systems
{
    public sealed class PuzzlePresenter
    {
        private readonly FirstPersonController _player;
        private readonly GameplayScreen _screen;
        private readonly IPopupsManager _popups;

        public PuzzlePresenter(FirstPersonController player, GameplayScreen screen, IPopupsManager popups)
        {
            _player = player;
            _screen = screen;
            _popups = popups;
        }

        public bool IsBusy { get; private set; }

        public void Present(PopupTypes type, Action solved)
        {
            IsBusy = true;
            SetPlayerActive(false);
            _screen.ClearDialog();
            _popups.ShowPopup<PuzzlePopup>(type).Open(() =>
            {
                Finish();
                solved();
            }, Finish);
        }

        private void Finish()
        {
            IsBusy = false;
            SetPlayerActive(true);
        }

        private void SetPlayerActive(bool isActive)
        {
            _player.enabled = isActive;
            _screen.SetCrosshairVisible(isActive);
            _screen.SetHudVisible(isActive);
        }
    }
}
