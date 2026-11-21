using UnityEngine;
using UnityEngine.UI;

namespace UI.Puzzles
{
    public static class PuzzleHintRow
    {
        public struct Entry
        {
            public Sprite Icon;
            public string KeyCap;
            public string Text;

            public Entry(Sprite icon, string text, string keyCap = null)
            {
                Icon = icon;
                Text = text;
                KeyCap = keyCap;
            }
        }

        public static RectTransform Build(RectTransform parent, Vector2 position, Font font, Color color, params Entry[] entries)
        {
            var row = (RectTransform)new GameObject("HintRow", typeof(RectTransform)).transform;
            row.SetParent(parent, false);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 0.5f);
            row.anchoredPosition = position;
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fitter = row.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            for (int i = 0; i < entries.Length; i++)
            {
                Entry entry = entries[i];
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                iconGo.transform.SetParent(row, false);
                var image = iconGo.GetComponent<Image>();
                image.sprite = entry.Icon;
                image.preserveAspect = true;
                image.raycastTarget = false;
                var size = iconGo.GetComponent<LayoutElement>();
                float aspect = entry.Icon.rect.width / entry.Icon.rect.height;
                size.preferredHeight = 48f;
                size.preferredWidth = 48f * aspect;
                if (!string.IsNullOrEmpty(entry.KeyCap))
                {
                    Text cap = NewText(iconGo.transform, entry.KeyCap, 18, color, font);
                    var capRect = cap.rectTransform;
                    capRect.anchorMin = Vector2.zero;
                    capRect.anchorMax = Vector2.one;
                    capRect.offsetMin = capRect.offsetMax = Vector2.zero;
                    cap.fontStyle = FontStyle.Bold;
                }

                Text label = NewText(row, entry.Text + (i < entries.Length - 1 ? "    " : ""), 28, color, font);
                label.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            }

            return row;
        }

        private static Text NewText(Transform parent, string text, int size, Color color, Font font)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }
    }
}
