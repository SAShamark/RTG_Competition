using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Gameplay.Dialogue
{
    [Serializable]
    public sealed class DialogLine
    {
        [SerializeField, FormerlySerializedAs("Id")] private string _id;
        [TextArea, SerializeField, FormerlySerializedAs("Text")] private string _text;
        [SerializeField, FormerlySerializedAs("Clip")] private AudioClip _clip;
        [SerializeField, Min(0.5f)] private float _hold = 3f;

        public string Id => _id;
        public string Text => _text;
        public AudioClip Clip => _clip;
        public float Hold => _hold;
    }

    [Serializable]
    public sealed class CharacterDialogs
    {
        [SerializeField, FormerlySerializedAs("Character")] private CharactersType _character;
        [SerializeField] private string _displayName;
        [SerializeField] private Color _color = new(1f, 0.76f, 0.29f);
        [SerializeField, FormerlySerializedAs("Lines")] private List<DialogLine> _lines = new();

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

        private Dictionary<string, AudioClip> _byText;
        private Dictionary<string, DialogPhrase> _byId;

        public IReadOnlyList<CharacterDialogs> Characters => _characters;

        public bool TryGetClip(string text, out AudioClip clip)
        {
            if (_byText == null)
            {
                Build();
            }

            return _byText.TryGetValue(text, out clip);
        }

        public void PlayVoice(string text)
        {
            if (!string.IsNullOrEmpty(text) && TryGetClip(text, out AudioClip clip))
            {
                Services.Audio.AudioService.Instance.PlayVoice(clip);
            }
        }

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
            _byText = null;
            _byId = null;
        }

        private void Build()
        {
            _byText = new Dictionary<string, AudioClip>();
            _byId = new Dictionary<string, DialogPhrase>();
            foreach (CharacterDialogs character in _characters)
            {
                foreach (DialogLine line in character.Lines)
                {
                    _byId[line.Id] = new DialogPhrase(character.Character, character.DisplayName, character.Color, line.Text, line.Clip, line.Hold);
                    if (line.Clip != null && !string.IsNullOrEmpty(line.Text))
                    {
                        _byText[line.Text] = line.Clip;
                    }
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

        public CharactersType Character { get; }
        public string Speaker { get; }
        public Color Color { get; }
        public string Text { get; }
        public AudioClip Clip { get; }
        public float Hold { get; }
    }
}
