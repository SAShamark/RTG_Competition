using Gameplay.Dialogue;
using System;
using Gameplay.Entities.Characters.Player;
using UI.Screens.Variants.Gameplay;
using Services;
using Vr;

namespace Gameplay.Mission.Systems
{
    public sealed class RadioController
    {
        private const float HintHold = 3.5f;

        private readonly FirstPersonController _player;
        private readonly GameplayScreen _screen;
        private readonly Func<string> _currentHint;
        private readonly VrRadio _radio;

        public RadioController(FirstPersonController player, GameplayScreen screen, Func<string> currentHint)
        {
            _player = player;
            _screen = screen;
            _currentHint = currentHint;
            _radio = player.Rig.Radio;
        }

        public void Tick()
        {
            bool isUsed = _radio.ConsumeUse() | GameInput.RadioPressed;
            if (!isUsed || !_player.enabled)
            {
                return;
            }

            _radio.ShowTalking();
            _screen.Say(DialogPhrase.Radio(_currentHint(), HintHold));
        }
    }
}
