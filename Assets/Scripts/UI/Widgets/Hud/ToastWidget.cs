using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Widgets.Hud
{
    public class ToastWidget : MonoBehaviour
    {
        [SerializeField] private RectTransform _template;
        [SerializeField] private float _lifetime = 3f;
        [SerializeField] private float _spacing = 64f;
        [SerializeField] private int _maxVisible = 4;

        private class Item
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public float Age;
        }

        private readonly List<Item> _items = new();

        private void Awake()
        {
            _template.gameObject.SetActive(false);
        }

        public void Show(string message)
        {
            RectTransform rect = Instantiate(_template, _template.parent);
            rect.gameObject.SetActive(true);
            rect.GetComponentInChildren<Text>().text = message;
            _items.Insert(0, new Item { Rect = rect, Group = rect.GetComponent<CanvasGroup>() });
            while (_items.Count > _maxVisible)
            {
                Destroy(_items[_items.Count - 1].Rect.gameObject);
                _items.RemoveAt(_items.Count - 1);
            }
        }

        private void Update()
        {
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                Item item = _items[i];
                item.Age += Time.unscaledDeltaTime;
                if (item.Age > _lifetime)
                {
                    Destroy(item.Rect.gameObject);
                    _items.RemoveAt(i);
                    continue;
                }

                float fadeIn = Mathf.Clamp01(item.Age / 0.2f);
                float fadeOut = Mathf.Clamp01((_lifetime - item.Age) / 0.4f);
                item.Group.alpha = Mathf.Min(fadeIn, fadeOut);
                Vector2 target = _template.anchoredPosition + Vector2.down * (_spacing * i) + Vector2.left * (1f - fadeIn) * 60f;
                item.Rect.anchoredPosition = Vector2.Lerp(item.Rect.anchoredPosition, target, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            }
        }
    }
}
