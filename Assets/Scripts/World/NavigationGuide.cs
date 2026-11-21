using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

namespace Game.World
{
    public class NavigationGuide : MonoBehaviour
    {
        [SerializeField] private Transform _diamond;
        [SerializeField] private float _reachRadius = 1.6f;
        [SerializeField] private float _diamondHeight = 0.9f;
        [SerializeField] private float _edgeMargin = 40f;
        [SerializeField] private Color _color = new(1f, 0.82f, 0.25f, 1f);
        [SerializeField] private Color _panelColor = new(0.08f, 0.05f, 0.04f, 0.93f);
        [SerializeField] private Sprite _diamondSprite;
        [SerializeField] private Sprite _chevronSprite;

        private Transform _viewer;
        private Transform[] _route = System.Array.Empty<Transform>();
        private Collider _zone;
        private string _label = "";
        private int _index;
        private bool _arrived;
        private RectTransform _hud;
        private Canvas _canvas;

        private RectTransform _marker;
        private RectTransform _icon;
        private RectTransform _panel;
        private Text _text;
        private RectTransform _chevron;

        public bool HasRoute => _route.Length > 0;

        public void Init(Transform viewer, RectTransform hud)
        {
            _viewer = viewer;
            _hud = hud;
            _canvas = hud.GetComponentInParent<Canvas>();
            CreateMarker();
            Clear();
        }

        public void SetRoute(Transform[] points, Collider zone, string label = "")
        {
            _route = points;
            _zone = zone;
            _label = label ?? "";
            _index = 0;
            _arrived = false;
        }

        public void Clear()
        {
            _route = System.Array.Empty<Transform>();
            _zone = null;
            Show(false, false);
        }

        private void OnEnable() => CinemachineCore.CameraUpdatedEvent.AddListener(HandleCameraUpdated);

        private void OnDisable() => CinemachineCore.CameraUpdatedEvent.RemoveListener(HandleCameraUpdated);

        private void HandleCameraUpdated(CinemachineBrain brain)
        {
            if (brain.OutputCamera == Camera.main)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            if (_diamond.gameObject.activeSelf)
            {
                _diamond.gameObject.SetActive(false);
            }

            if (_route.Length == 0)
            {
                Show(false, false);
                return;
            }

            if (_zone != null && _zone.bounds.Contains(_viewer.position + Vector3.up))
            {
                _arrived = true;
                Show(false, false);
                return;
            }

            if (_arrived)
            {
                _index = _route.Length - 1;
            }
            else
            {
                for (int i = _route.Length - 1; i >= _index; i--)
                {
                    if (Reached(_route[i].position) && i < _route.Length - 1)
                    {
                        _index = i + 1;
                        break;
                    }
                }

                while (_index < _route.Length - 1 && IsPassed(_index))
                {
                    _index++;
                }
            }

            Transform target = _route[Mathf.Clamp(_index, 0, _route.Length - 1)];
            Camera cam = Camera.main;
            if (cam == null)
            {
                Show(false, false);
                return;
            }

            Transform last = _route[_route.Length - 1];
            int meters = Mathf.Max(1, Mathf.RoundToInt(Vector3.Distance(_viewer.position, last.position)));

            Vector3 point = target.position + Vector3.up * _diamondHeight;
            Vector3 viewport = cam.WorldToViewportPoint(point);
            bool onScreen = viewport.z > 0f && viewport.x > 0.04f && viewport.x < 0.96f && viewport.y > 0.06f && viewport.y < 0.9f;
            Show(onScreen, !onScreen);

            if (onScreen)
            {
                PlaceOnScreen(cam.WorldToScreenPoint(point), meters);
            }
            else
            {
                PlaceOnEdge(cam, point, meters);
            }
        }

        private void PlaceOnScreen(Vector3 screen, int meters)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_hud, screen, UiCamera(), out Vector2 local);
            _marker.anchoredPosition = local;
            float bob = Mathf.Sin(Time.time * 3f) * 3f;
            _icon.anchoredPosition = new Vector2(0f, bob);
            SetText(string.IsNullOrEmpty(_label) ? $"{meters} м" : $"{_label}\n<size=19>{meters} м</size>");
            _panel.anchoredPosition = new Vector2(0f, 26f + _panel.sizeDelta.y * 0.5f + bob);
        }

        private void PlaceOnEdge(Camera cam, Vector3 point, int meters)
        {
            Vector3 local = cam.transform.InverseTransformPoint(point);
            Vector2 dir = new Vector2(local.x, local.y);
            if (local.z < 0f)
            {
                dir.y = Mathf.Min(dir.y, 0f);
                if (dir.sqrMagnitude < 0.0001f)
                {
                    dir = Vector2.right;
                }
            }

            dir = dir.normalized;
            SetText(string.IsNullOrEmpty(_label) ? $"{meters} м" : $"{_label} · {meters} м");

            Rect rect = _hud.rect;
            Vector2 chip = _panel.sizeDelta + new Vector2(34f, 0f);
            Vector2 half = new Vector2(rect.width * 0.5f - _edgeMargin - chip.x * 0.5f, rect.height * 0.5f - _edgeMargin - chip.y * 0.5f);
            float scale = Mathf.Min(half.x / Mathf.Max(0.0001f, Mathf.Abs(dir.x)), half.y / Mathf.Max(0.0001f, Mathf.Abs(dir.y)));
            _marker.anchoredPosition = dir * scale;
            _panel.anchoredPosition = Vector2.zero;

            Vector2 side = new Vector2(dir.x * (_panel.sizeDelta.x * 0.5f + 16f), dir.y * (_panel.sizeDelta.y * 0.5f + 16f));
            float pulse = 1f + Mathf.Sin(Time.time * 5f) * 0.1f;
            _chevron.anchoredPosition = side + dir * (pulse - 1f) * 20f;
            _chevron.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        }

        private void SetText(string text)
        {
            if (_text.text != text)
            {
                _text.text = text;
                _panel.sizeDelta = new Vector2(_text.preferredWidth + 28f, _text.preferredHeight + 10f);
            }
        }

        private Camera UiCamera() => _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;

        private bool IsPassed(int index)
        {
            Vector3 current = _route[index].position;
            Vector3 next = _route[index + 1].position;
            Vector3 viewer = _viewer.position;
            if (next.y > current.y + 0.5f && viewer.y > current.y + 1.2f)
            {
                return true;
            }

            return Vector3.Distance(viewer, next) < Vector3.Distance(current, next) - 0.3f && Mathf.Abs(viewer.y - next.y) < 2.5f;
        }

        private bool Reached(Vector3 point)
        {
            Vector3 d = point - _viewer.position;
            return Mathf.Abs(d.y) < 1.4f && new Vector2(d.x, d.z).magnitude < _reachRadius;
        }

        private void Show(bool onScreen, bool onEdge)
        {
            bool any = onScreen || onEdge;
            if (_marker.gameObject.activeSelf != any)
            {
                _marker.gameObject.SetActive(any);
            }

            if (_icon.gameObject.activeSelf != onScreen)
            {
                _icon.gameObject.SetActive(onScreen);
            }

            if (_chevron.gameObject.activeSelf != onEdge)
            {
                _chevron.gameObject.SetActive(onEdge);
            }
        }

        private void CreateMarker()
        {
            if (_marker != null)
            {
                return;
            }

            _marker = NewRect("NavMarker", _hud);
            _marker.sizeDelta = Vector2.zero;

            _icon = NewRect("Diamond", _marker);
            _icon.sizeDelta = new Vector2(34f, 34f);
            Image icon = _icon.gameObject.AddComponent<Image>();
            icon.sprite = _diamondSprite;
            icon.color = Color.white;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            Shadow(icon);

            _panel = NewRect("Panel", _marker);
            Image panel = _panel.gameObject.AddComponent<Image>();
            panel.sprite = MakeSprite(32, (u, v) =>
            {
                float dx = Mathf.Max(0f, Mathf.Abs(u - 0.5f) - 0.3f) / 0.2f;
                float dy = Mathf.Max(0f, Mathf.Abs(v - 0.5f) - 0.3f) / 0.2f;
                return Mathf.Clamp01((1f - Mathf.Sqrt(dx * dx + dy * dy)) * 6f);
            }, new Vector4(12f, 12f, 12f, 12f));
            panel.type = Image.Type.Sliced;
            panel.color = _panelColor;
            panel.raycastTarget = false;

            RectTransform textRect = NewRect("Text", _panel);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            var text = textRect.gameObject.AddComponent<Text>();
            Text hudText = _hud.GetComponentInChildren<Text>(true);
            text.font = hudText.font;
            text.fontSize = 24;
            text.fontStyle = FontStyle.Bold;
            text.color = _color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = true;
            text.raycastTarget = false;
            var outline = textRect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            _text = text;

            _chevron = NewRect("Chevron", _marker);
            _chevron.sizeDelta = new Vector2(26f, 26f);
            Image chevron = _chevron.gameObject.AddComponent<Image>();
            chevron.sprite = _chevronSprite;
            chevron.color = Color.white;
            chevron.preserveAspect = true;
            chevron.raycastTarget = false;
            Shadow(chevron);

            _marker.gameObject.SetActive(false);
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            return rect;
        }

        private static void Shadow(Graphic graphic)
        {
            var shadow = graphic.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
        }

        private static Sprite MakeSprite(int size, System.Func<float, float, float> alpha, Vector4 border = default)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha((x + 0.5f) / size, (y + 0.5f) / size)));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }
    }
}
