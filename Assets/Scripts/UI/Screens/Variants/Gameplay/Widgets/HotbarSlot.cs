using Gameplay.Dialogue;
using UI.Widgets;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Screens.Variants.Gameplay.Widgets
{
    public sealed class HotbarSlot : MonoBehaviour
    {
        [SerializeField] private TextLibrary _texts;
        [SerializeField] private Image _frame;
        [SerializeField] private Image _icon;
        [SerializeField] private UiPulse _pulse;
        [SerializeField] private Color _selectedColor = new(1f, 0.82f, 0.25f, 1f);
        [SerializeField] private Color _normalColor = new(0.2f, 0.2f, 0.24f, 0.9f);

        private bool _isAvailable;
        [SerializeField] private Text _count;

        public void SetAvailable(bool isAvailable)
        {
            if (isAvailable && !_isAvailable)
            {
                _pulse.Play();
            }

            _isAvailable = isAvailable;
            _icon.enabled = isAvailable;
            if (!isAvailable)
            {
                SetCount(0);
            }
        }

        public void SetCount(int count)
        {
            _count.text = count > 1 ? _texts.Format(TextId.SlotCount, count) : string.Empty;
        }

        public void Pulse() => _pulse.Play();

        public void SetItem(Sprite icon, int count)
        {
            bool changed = _icon.sprite != icon || !_icon.enabled != (icon == null);
            _icon.sprite = icon;
            _icon.enabled = icon != null;
            _icon.preserveAspect = true;
            SetCount(icon != null ? count : 0);
            if (changed && icon != null)
            {
                _pulse.Play();
            }
        }

        public void SetSelected(bool isSelected)
        {
            _frame.color = isSelected ? _selectedColor : _normalColor;
        }

    }
}
