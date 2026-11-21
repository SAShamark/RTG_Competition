using UnityEngine;
using UnityEngine.UI;

namespace UI.Widgets.Hud
{
    public class InteractionPromptWidget : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Text _key;
        [SerializeField] private Text _text;
        [SerializeField] private RectTransform _layout;

        public void Show(string hint)
        {
            if (string.IsNullOrEmpty(hint))
            {
                _root.SetActive(false);
                return;
            }

            string key = "E";
            string text = hint;
            int close = hint.IndexOf(']');
            if (hint.StartsWith("[") && close > 0)
            {
                key = hint.Substring(1, close - 1);
                text = hint.Substring(close + 1).Trim();
            }

            _key.text = key;
            _text.text = text;
            _root.SetActive(true);
            float width = Mathf.Max(280f, _text.preferredWidth + 130f);
            _layout.sizeDelta = new Vector2(width, _layout.sizeDelta.y);
        }
    }
}
