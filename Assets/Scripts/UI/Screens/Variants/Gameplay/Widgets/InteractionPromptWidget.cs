using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Screens.Variants.Gameplay.Widgets
{
    public sealed class InteractionPromptWidget : MonoBehaviour
    {
        private static readonly Regex Entry = new(@"\[(\w+)\]\s*([^\[]*)");

        [SerializeField] private GameObject _root;
        [SerializeField] private Text _key;
        [SerializeField] private Text _text;
        [SerializeField] private RectTransform _layout;
        [SerializeField] private float _rowSpacing = 8f;
        [SerializeField] private float _keyPadding = 18f;
        [SerializeField] private float _minWidth = 280f;
        [SerializeField] private float _textPadding = 130f;

        private readonly List<GameObject> _extraRows = new();
        private float _keyWidth = -1f;
        private float _textX = float.MinValue;

        public void Show(string hint)
        {
            foreach (GameObject row in _extraRows)
            {
                row.SetActive(false);
            }

            if (string.IsNullOrEmpty(hint))
            {
                _root.SetActive(false);
                return;
            }

            MatchCollection entries = Entry.Matches(hint);
            if (entries.Count == 0)
            {
                Fill(_key, _text, _layout, "E", hint);
                _root.SetActive(true);
                return;
            }

            Fill(_key, _text, _layout, entries[0].Groups[1].Value, entries[0].Groups[2].Value);
            _root.SetActive(true);
            for (int i = 1; i < entries.Count; i++)
            {
                GameObject row = Row(i - 1);
                Fill(row.transform.Find(_key.transform.parent.name + "/" + _key.name).GetComponent<Text>(),
                    row.transform.Find(_text.name).GetComponent<Text>(), (RectTransform)row.transform,
                    entries[i].Groups[1].Value, entries[i].Groups[2].Value);
                row.SetActive(true);
            }
        }

        private void Fill(Text key, Text text, RectTransform layout, string keyText, string line)
        {
            key.text = keyText;
            var keyBox = (RectTransform)key.transform.parent;
            if (_keyWidth < 0f)
            {
                _keyWidth = keyBox.sizeDelta.x;
            }

            if (_textX == float.MinValue)
            {
                _textX = text.rectTransform.anchoredPosition.x;
            }

            float extra = Mathf.Max(0f, key.preferredWidth + _keyPadding - _keyWidth);
            keyBox.sizeDelta = new Vector2(_keyWidth + extra, keyBox.sizeDelta.y);
            text.rectTransform.anchoredPosition = new Vector2(_textX + extra, text.rectTransform.anchoredPosition.y);
            text.text = line.Trim();
            layout.sizeDelta = new Vector2(Mathf.Max(_minWidth, text.preferredWidth + _textPadding + extra), layout.sizeDelta.y);
        }

        private GameObject Row(int index)
        {
            while (_extraRows.Count <= index)
            {
                GameObject row = Instantiate(_root, _root.transform.parent);
                row.name = "PromptRow" + (_extraRows.Count + 2);
                var rect = (RectTransform)row.transform;
                var first = (RectTransform)_root.transform;
                rect.anchoredPosition = first.anchoredPosition - new Vector2(0f, (first.rect.height + _rowSpacing) * (_extraRows.Count + 1));
                _extraRows.Add(row);
            }

            return _extraRows[index];
        }
    }
}
