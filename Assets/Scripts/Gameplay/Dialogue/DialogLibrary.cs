using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Dialogue
{
    [Serializable]
    public sealed class DialogLine
    {
        [SerializeField] private string _id;
        [TextArea, SerializeField] private string _text;
        [SerializeField] private AudioClip _clip;
        [SerializeField, Min(0.5f)] private float _hold = 3f;

        public string Id => _id;
        public string Text => _text;
        public AudioClip Clip => _clip;
        public float Hold => _hold;
    }

    [Serializable]
    public sealed class CharacterDialogs
    {
        [SerializeField] private CharactersType _character;
        [SerializeField] private string _displayName;
        [SerializeField] private Color _color = new(1f, 0.76f, 0.29f);
        [SerializeField] private List<DialogLine> _lines = new();

        public CharactersType Character => _character;
        public string DisplayName => _displayName;
        public Color Color => _color;
        public List<DialogLine> Lines => _lines;
    }

    [CreateAssetMenu(fileName = "VoiceLibrary", menuName = "Game/Voice Library")]
    public sealed class DialogLibrary : ScriptableObject
    {
        private const float MissingLineHold = 3f;

        [SerializeField] private List<CharacterDialogs> _characters = new();

        private Dictionary<string, DialogPhrase> _byId;

        public IReadOnlyList<CharacterDialogs> Characters => _characters;

        public DialogPhrase Get(string id)
        {
            if (_byId == null)
            {
                Build();
            }

            if (_byId.TryGetValue(id, out DialogPhrase phrase))
            {
                return phrase;
            }

            Debug.LogError($"Dialog line '{id}' is missing in {name}", this);
            return new DialogPhrase(CharactersType.Partner, id, Color.white, id, null, MissingLineHold);
        }

        private void OnValidate()
        {
            _byId = null;
        }

        private void Build()
        {
            _byId = new Dictionary<string, DialogPhrase>();
            foreach (CharacterDialogs character in _characters)
            {
                foreach (DialogLine line in character.Lines)
                {
                    _byId[line.Id] = new DialogPhrase(character.Character, character.DisplayName, character.Color, line.Text, line.Clip, line.Hold);
                }
            }
        }
    }

    public readonly struct DialogPhrase
    {
        public DialogPhrase(CharactersType character, string speaker, Color color, string text, AudioClip clip, float hold)
        {
            Hold = hold;
            Character = character;
            Speaker = speaker;
            Color = color;
            Text = text;
            Clip = clip;
        }

        public static DialogPhrase Radio(string text, float hold) => new(CharactersType.Partner, null, Color.white, text, null, hold);

        public DialogPhrase WithHold(float hold) => new(Character, Speaker, Color, Text, Clip, hold);

        public CharactersType Character { get; }
        public string Speaker { get; }
        public Color Color { get; }
        public string Text { get; }
        public AudioClip Clip { get; }
        public float Hold { get; }
    }
}
