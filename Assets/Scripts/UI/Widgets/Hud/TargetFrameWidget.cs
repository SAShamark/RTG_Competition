using UnityEngine;

namespace UI.Widgets.Hud
{
    public class TargetFrameWidget : MonoBehaviour
    {
        [SerializeField] private RectTransform _frame;
        [SerializeField] private float _padding = 14f;
        [SerializeField] private float _minSize = 70f;
        [SerializeField] private float _maxSize = 420f;

        private Transform _target;
        private Renderer[] _renderers;
        private RectTransform _canvasRect;
        private Vector2 _position;
        private Vector2 _size;

        private void Awake()
        {
            _canvasRect = (RectTransform)GetComponentInParent<Canvas>().rootCanvas.transform;
            _frame.gameObject.SetActive(false);
        }

        public void SetTarget(Component target)
        {
            _target = target != null ? target.transform : null;
            _renderers = _target != null ? _target.GetComponentsInChildren<Renderer>() : null;
            if (_target == null)
            {
                _frame.gameObject.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (_target == null || cam == null || _renderers == null || _renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = _renderers[0].bounds;
            foreach (Renderer r in _renderers)
            {
                bounds.Encapsulate(r.bounds);
            }

            Vector2 min = new(float.MaxValue, float.MaxValue);
            Vector2 max = new(float.MinValue, float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                Vector3 sign = new((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
                Vector3 screen = cam.WorldToScreenPoint(bounds.center + Vector3.Scale(bounds.extents, sign));
                if (screen.z < 0.05f)
                {
                    _frame.gameObject.SetActive(false);
                    return;
                }

                min = Vector2.Min(min, screen);
                max = Vector2.Max(max, screen);
            }

            float scale = _canvasRect.rect.width / Screen.width;
            Vector2 size = (max - min) * scale + Vector2.one * (_padding * 2f);
            size = new Vector2(Mathf.Clamp(size.x, _minSize, _maxSize), Mathf.Clamp(size.y, _minSize, _maxSize));
            Vector2 center = ((min + max) * 0.5f - new Vector2(Screen.width, Screen.height) * 0.5f) * scale;
            center = Vector2.ClampMagnitude(center, 160f);

            bool first = !_frame.gameObject.activeSelf;
            _frame.gameObject.SetActive(true);
            float k = first ? 1f : 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime);
            _position = Vector2.Lerp(_position, center, k);
            _size = Vector2.Lerp(_size, size, k);
            _frame.anchoredPosition = _position;
            _frame.sizeDelta = _size;
        }
    }
}
