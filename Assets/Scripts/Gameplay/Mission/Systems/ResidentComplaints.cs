using System;
using Gameplay.Dialogue;
using Gameplay.Entities.Characters;
using UI.Screens.Variants.Gameplay;
using UnityEngine;

namespace Gameplay.Mission.Systems
{
    [Serializable]
    public sealed class ResidentComplaints
    {
        [SerializeField] private CharacterReactions[] _characters = Array.Empty<CharacterReactions>();

        private GameplayScreen _screen;
        private TextLibrary _texts;
        private DialogLibrary _dialogs;
        private float _reactionEnd;

        public int Count { get; private set; }

        public void Init(GameplayScreen screen, TextLibrary texts, DialogLibrary dialogs)
        {
            _screen = screen;
            _texts = texts;
            _dialogs = dialogs;
            foreach (CharacterReactions character in _characters)
            {
                character.Bothered += HandleBothered;
            }
        }

        public void Dispose()
        {
            foreach (CharacterReactions character in _characters)
            {
                character.Bothered -= HandleBothered;
            }
        }

        private void HandleBothered(CharacterReactions character, string lineId)
        {
            // A newer reaction takes the window over from an older one, but never cuts into any other line.
            if (_screen.IsDialogBusy)
            {
                if (Time.unscaledTime >= _reactionEnd)
                {
                    return;
                }

                _screen.ClearDialog();
            }

            Count++;
            _screen.ShowToast(_texts.Format(TextId.ToastComplaint, Count));
            DialogPhrase phrase = _dialogs.Get(lineId);
            _reactionEnd = Time.unscaledTime + phrase.Hold;
            _screen.CharacterSay(phrase.Speaker, phrase.Text, phrase.Hold, character.Head);
        }
    }
}
