using UnityEngine;
using UnityEngine.UI;

namespace UI.Widgets
{
    public sealed class ScreenFade : MonoBehaviour
    {
        [SerializeField] private Canvas _canvas;
        [SerializeField] private Image _image;

        public void SetAlpha(float alpha)
        {
            Color color = _image.color;
            color.a = alpha;
            _image.color = color;
            _canvas.enabled = alpha > 0f;
        }
    }
}
