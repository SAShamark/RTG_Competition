using Gameplay.Entities.Player;
using System;
using UI.Popups;
using UI.Screens.Variants.Gameplay;

namespace Gameplay.Mission
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

        public void Present<T>(PopupTypes type, Action<T, Action, Action> open, Action solved) where T : BasePopup
        {
            IsBusy = true;
            SetPlayerActive(false);
            _screen.ClearDialog();
            open(_popups.ShowPopup<T>(type),
                () =>
                {
                    Finish();
                    solved();
                },
                Finish);
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
