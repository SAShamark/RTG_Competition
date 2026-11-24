using System;
using Gameplay.Entities.Characters.Player;
using UI.Screens.Variants.Gameplay;
using UnityEngine.InputSystem;

namespace Gameplay.Mission.Systems
{
    public sealed class RadioController
    {
        private const float HintHold = 3.5f;

        private readonly FirstPersonController _player;
        private readonly GameplayScreen _screen;
        private readonly Func<string> _currentHint;

        public RadioController(FirstPersonController player, GameplayScreen screen, Func<string> currentHint)
        {
            _player = player;
            _screen = screen;
            _currentHint = currentHint;
        }

        public void Tick()
        {
            if (!Keyboard.current.digit4Key.wasPressedThisFrame || !_player.enabled)
            {
                return;
            }

            _screen.PulseRadio();
            _screen.PartnerSay(_currentHint(), HintHold);
        }
    }
}
