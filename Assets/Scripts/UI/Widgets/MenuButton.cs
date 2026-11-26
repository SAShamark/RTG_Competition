using System;
using Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI.Widgets
{
    public sealed class MenuButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Button _button;
        [SerializeField] private RectTransform _content;
        [SerializeField] private Image _activePlate;
        [SerializeField] private Image _icon;
        [SerializeField] private Color _iconIdle = new(0.85f, 0.5f, 0.15f, 1f);
        [SerializeField] private Color _iconActive = new(1f, 0.72f, 0.2f, 1f);
        [SerializeField] private float _hoverScale = 1.04f;
        [SerializeField] private float _sharpness = 14f;

        private bool _isHovered;
        private bool _isPrimary;
        private float _glow;

        public event Action Clicked;
        public event Action Hovered;

        private void Awake() => _button.onClick.AddListener(HandleClick);

        private void OnDestroy() => _button.onClick.RemoveListener(HandleClick);

        public void SetPrimary(bool isPrimary) => _isPrimary = isPrimary;

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovered = true;
            Hovered?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData) => _isHovered = false;

        private void Update()
        {
            float target = _isHovered || _isPrimary ? 1f : 0f;
            _glow = Mathf.Lerp(_glow, target, Easing.Damp(_sharpness, Time.unscaledDeltaTime));

            Color plate = _activePlate.color;
            plate.a = _glow;
            _activePlate.color = plate;
            _icon.color = Color.Lerp(_iconIdle, _iconActive, _glow);

            float scale = Mathf.Lerp(_content.localScale.x, _isHovered ? _hoverScale : 1f, Easing.Damp(_sharpness, Time.unscaledDeltaTime));
            _content.localScale = new Vector3(scale, scale, 1f);
        }

        private void HandleClick() => Clicked?.Invoke();
    }
}
