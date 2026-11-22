using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Dialogue
{
    [CreateAssetMenu(fileName = "TextLibrary", menuName = "Configs/Text Library")]
    public sealed class TextLibrary : ScriptableObject
    {
        [Serializable]
        private sealed class TextEntry
        {
            [SerializeField] private TextId _id;
            [SerializeField, TextArea] private string _text;

            public TextId Id => _id;
            public string Text => _text;
        }

        [SerializeField] private TextEntry[] _texts = Array.Empty<TextEntry>();

        private Dictionary<TextId, string> _byId;

        public string this[TextId id]
        {
            get
            {
                if (_byId == null)
                {
                    _byId = new Dictionary<TextId, string>();
                    foreach (TextEntry entry in _texts)
                    {
                        _byId[entry.Id] = entry.Text;
                    }
                }

                if (_byId.TryGetValue(id, out string text))
                {
                    return text;
                }

                Debug.LogError($"Text '{id}' is missing in {name}", this);
                return id.ToString();
            }
        }

        public string Format(TextId id, params object[] args) => string.Format(this[id], args);

        private void OnValidate() => _byId = null;
    }
}
