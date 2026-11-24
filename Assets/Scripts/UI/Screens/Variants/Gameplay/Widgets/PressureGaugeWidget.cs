using Gameplay.Entities.Interactive.Items;
using Services;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Screens.Variants.Gameplay.Widgets
{
    public sealed class PressureGaugeWidget : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Image _fill;
        [SerializeField] private Gradient _colors;
        [SerializeField] private float _fillSmoothing = 14f;
        [SerializeField] private float _fadeSpeed = 6f;
        [SerializeField, Range(0f, 1f)] private float _lowThreshold = 0.2f;
        [SerializeField] private float _lowPulseFrequency = 9f;
        [SerializeField, Range(0f, 1f)] private float _lowPulseAmount = 0.35f;

        [Header("Icons")]
        [SerializeField] private GameObject _extinguisherIcon;
        [SerializeField] private GameObject _axeIcon;

        private ExtinguisherLogic _extinguisher;
        private AxeLogic _axe;
        private ItemType _item;
        private bool _isShown;
        private float _shown = 1f;

        public void Init(ExtinguisherLogic extinguisher, AxeLogic axe)
        {
            _extinguisher = extinguisher;
            _axe = axe;
            _group.alpha = 0f;
        }

        public void SetItem(ItemType item)
        {
            _isShown = item == ItemType.Extinguisher || item == ItemType.Axe;
            if (!_isShown)
            {
                return;
            }

            if (item != _item)
            {
                _shown = Value(item);
            }

            _item = item;
            _extinguisherIcon.SetActive(item == ItemType.Extinguisher);
            _axeIcon.SetActive(item == ItemType.Axe);
        }

        private float Value(ItemType item) => item == ItemType.Axe ? _axe.Stamina : _extinguisher.Pressure;

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            _group.alpha = Mathf.MoveTowards(_group.alpha, _isShown ? 1f : 0f, _fadeSpeed * deltaTime);
            if (!_isShown || _extinguisher == null)
            {
                return;
            }

            _shown = Mathf.Lerp(_shown, Value(_item), Easing.Damp(_fillSmoothing, deltaTime));
            _fill.fillAmount = _shown;

            Color color = _colors.Evaluate(_shown);
            if (_shown < _lowThreshold)
            {
                color.a *= 1f - _lowPulseAmount * (0.5f + 0.5f * Mathf.Sin(Time.time * _lowPulseFrequency));
            }

            _fill.color = color;
        }
    }
}
