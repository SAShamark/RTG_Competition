using UnityEngine;

namespace UI.Widgets
{
    public sealed class CreditPanel : MonoBehaviour
    {
        [SerializeField] private Canvas _canvas;
        [SerializeField] private RectTransform _panel;
        [SerializeField] private float _hiddenGap = 60f;

        private Vector2 _shownPosition;
        private bool _hasShownPosition;

        public void SetShown(float shown)
        {
            if (!_hasShownPosition)
            {
                _shownPosition = _panel.anchoredPosition;
                _hasShownPosition = true;
            }

            float hidden = _panel.rect.width + _hiddenGap;
            _panel.anchoredPosition = _shownPosition + Vector2.right * (hidden * (1f - shown));
            _canvas.enabled = shown > 0f;
        }
    }
}
