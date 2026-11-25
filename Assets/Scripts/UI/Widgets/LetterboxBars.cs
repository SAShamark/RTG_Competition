using UnityEngine;

namespace UI.Widgets
{
    public sealed class LetterboxBars : MonoBehaviour
    {
        private const float Half = 0.5f;

        [SerializeField] private Canvas _canvas;
        [SerializeField] private RectTransform _top;
        [SerializeField] private RectTransform _bottom;
        [SerializeField, Min(1f)] private float _aspect = 2.39f;

        public void SetShown(float shown)
        {
            // share of the screen height one bar covers so the picture between them has the film aspect
            float bar = Mathf.Max(0f, 1f - (float)Screen.width / Screen.height / _aspect) * Half * shown;
            _top.anchorMin = new Vector2(0f, 1f - bar);
            _bottom.anchorMax = new Vector2(1f, bar);
            _canvas.enabled = bar > 0f;
        }
    }
}
