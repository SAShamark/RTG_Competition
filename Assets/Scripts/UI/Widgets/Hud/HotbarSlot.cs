using UnityEngine;
using UnityEngine.UI;

namespace UI.Widgets.Hud
{
    public class HotbarSlot : MonoBehaviour
    {
        [SerializeField] private Image _frame;
        [SerializeField] private Image _icon;
        [SerializeField] private Image _charge;
        [SerializeField] private GameObject _chargeRoot;
        [SerializeField] private UiPulse _pulse;
        [SerializeField] private Color _selectedColor = new(1f, 0.82f, 0.25f, 1f);
        [SerializeField] private Color _normalColor = new(0.2f, 0.2f, 0.24f, 0.9f);

        private bool _isAvailable;

        public void SetAvailable(bool isAvailable)
        {
            if (isAvailable && !_isAvailable)
            {
                _pulse.Play();
            }

            _isAvailable = isAvailable;
            _icon.color = isAvailable ? Color.white : new Color(1f, 1f, 1f, 0.22f);
        }

        public void SetSelected(bool isSelected)
        {
            _frame.color = isSelected ? _selectedColor : _normalColor;
        }

        public void SetCharge(float? value)
        {
            _chargeRoot.SetActive(value.HasValue);
            if (value.HasValue)
            {
                _charge.fillAmount = Mathf.Clamp01(value.Value);
            }
        }
    }
}
