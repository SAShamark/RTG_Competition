using UnityEngine;
using UnityEngine.UI;

namespace UI.Screens.Variants.Gameplay.Widgets
{
    public sealed class NavMarkerWidget : MonoBehaviour
    {
        [SerializeField] private RectTransform _marker;
        [SerializeField] private RectTransform _icon;
        [SerializeField] private RectTransform _panel;
        [SerializeField] private Text _text;
        [SerializeField] private RectTransform _chevron;

        public RectTransform Marker => _marker;
        public RectTransform Icon => _icon;
        public RectTransform Panel => _panel;
        public Text Text => _text;
        public RectTransform Chevron => _chevron;
        public RectTransform Area => (RectTransform)_marker.parent;
    }
}
