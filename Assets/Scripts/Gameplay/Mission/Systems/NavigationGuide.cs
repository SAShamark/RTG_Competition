using Gameplay.Dialogue;
using System;
using UI.Screens.Variants.Gameplay.Widgets;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

namespace Gameplay.Mission.Systems
{
    [Serializable]
    public sealed class NavigationGuide
    {
        private const float ViewportMarginX = 0.04f;
        private const float ViewportMaxX = 0.96f;
        private const float ViewportMinY = 0.06f;
        private const float ViewportMaxY = 0.9f;
        private const float MinDirectionSqr = 0.0001f;
        [SerializeField] private float _reachRadius = 1.6f;
        [SerializeField] private float _diamondHeight = 0.9f;
        [SerializeField] private float _edgeMargin = 40f;
        [SerializeField] private float _bobFrequency = 3f;
        [SerializeField] private float _panelGap = 26f;
        [SerializeField] private float _chevronSpace = 34f;
        [SerializeField] private float _chevronGap = 16f;
        [SerializeField] private float _pulseFrequency = 5f;
        [SerializeField] private float _pulseAmount = 0.1f;
        [SerializeField] private float _pulseDistance = 20f;
        [SerializeField] private float _panelPaddingX = 28f;
        [SerializeField] private float _panelPaddingY = 10f;
        [SerializeField] private float _floorClimbHeight = 1.2f;
        [SerializeField] private float _passSlack = 0.3f;
        [SerializeField] private float _sameFloorHeight = 2.5f;
        [SerializeField] private float _reachHeight = 1.4f;

        private Transform _viewer;
        private Camera _camera;
        private Transform[] _route = Array.Empty<Transform>();
        private Collider _zone;
        private string _label = "";
        private int _index;
        private bool _arrived;
        private RectTransform _hud;
        private Canvas _canvas;

        private NavMarkerWidget _view;
        private TextLibrary _texts;
        private RectTransform _marker;
        private RectTransform _icon;
        private RectTransform _panel;
        private Text _text;
        private RectTransform _chevron;

        public bool HasRoute => _route.Length > 0;

        public void Init(Transform viewer, Camera camera, NavMarkerWidget view, TextLibrary texts)
        {
            _camera = camera;
            _texts = texts;
            _viewer = viewer;
            _view = view;
            _hud = view.Area;
            _canvas = _hud.GetComponentInParent<Canvas>();
            _marker = view.Marker;
            _icon = view.Icon;
            _panel = view.Panel;
            _text = view.Text;
            _chevron = view.Chevron;
            CinemachineCore.CameraUpdatedEvent.AddListener(HandleCameraUpdated);
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
            _route = Array.Empty<Transform>();
            _zone = null;
            Show(false, false);
        }

        public void Dispose() => CinemachineCore.CameraUpdatedEvent.RemoveListener(HandleCameraUpdated);

        private void HandleCameraUpdated(CinemachineBrain brain)
        {
            if (brain.OutputCamera == _camera)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
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

            Transform last = _route[_route.Length - 1];
            int meters = Mathf.Max(1, Mathf.RoundToInt(Vector3.Distance(_viewer.position, last.position)));

            Vector3 point = target.position + Vector3.up * _diamondHeight;
            Vector3 viewport = _camera.WorldToViewportPoint(point);
            bool onScreen = viewport.z > 0f && viewport.x > ViewportMarginX && viewport.x < ViewportMaxX && viewport.y > ViewportMinY && viewport.y < ViewportMaxY;
            Show(onScreen, !onScreen);

            if (onScreen)
            {
                PlaceOnScreen(_camera.WorldToScreenPoint(point), meters);
            }
            else
            {
                PlaceOnEdge(point, meters);
            }
        }

        private void PlaceOnScreen(Vector3 screen, int meters)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_hud, screen, UiCamera(), out Vector2 local);
            _marker.anchoredPosition = local;
            float bob = Mathf.Sin(Time.time * _bobFrequency) * _bobFrequency;
            _icon.anchoredPosition = new Vector2(0f, bob);
            SetText(string.IsNullOrEmpty(_label) ? _texts.Format(TextId.NavDistance, meters) : _texts.Format(TextId.NavLabelStacked, _label, meters));
            _panel.anchoredPosition = new Vector2(0f, _panelGap + _panel.sizeDelta.y * 0.5f + bob);
        }

        private void PlaceOnEdge(Vector3 point, int meters)
        {
            Vector3 local = _camera.transform.InverseTransformPoint(point);
            Vector2 dir = new Vector2(local.x, local.y);
            if (local.z < 0f)
            {
                dir.y = Mathf.Min(dir.y, 0f);
                if (dir.sqrMagnitude < MinDirectionSqr)
                {
                    dir = Vector2.right;
                }
            }

            dir = dir.normalized;
            SetText(string.IsNullOrEmpty(_label) ? _texts.Format(TextId.NavDistance, meters) : _texts.Format(TextId.NavLabelInline, _label, meters));

            Rect rect = _hud.rect;
            Vector2 chip = _panel.sizeDelta + new Vector2(_chevronSpace, 0f);
            Vector2 half = new Vector2(rect.width * 0.5f - _edgeMargin - chip.x * 0.5f, rect.height * 0.5f - _edgeMargin - chip.y * 0.5f);
            float scale = Mathf.Min(half.x / Mathf.Max(MinDirectionSqr, Mathf.Abs(dir.x)), half.y / Mathf.Max(MinDirectionSqr, Mathf.Abs(dir.y)));
            _marker.anchoredPosition = dir * scale;
            _panel.anchoredPosition = Vector2.zero;

            Vector2 side = new Vector2(dir.x * (_panel.sizeDelta.x * 0.5f + _chevronGap), dir.y * (_panel.sizeDelta.y * 0.5f + _chevronGap));
            float pulse = 1f + Mathf.Sin(Time.time * _pulseFrequency) * _pulseAmount;
            _chevron.anchoredPosition = side + dir * (pulse - 1f) * _pulseDistance;
            _chevron.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        }

        private void SetText(string text)
        {
            if (_text.text != text)
            {
                _text.text = text;
                _panel.sizeDelta = new Vector2(_text.preferredWidth + _panelPaddingX, _text.preferredHeight + _panelPaddingY);
            }
        }

        private Camera UiCamera() => _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;

        private bool IsPassed(int index)
        {
            Vector3 current = _route[index].position;
            Vector3 next = _route[index + 1].position;
            Vector3 viewer = _viewer.position;
            if (next.y > current.y + 0.5f && viewer.y > current.y + _floorClimbHeight)
            {
                return true;
            }

            return Vector3.Distance(viewer, next) < Vector3.Distance(current, next) - _passSlack && Mathf.Abs(viewer.y - next.y) < _sameFloorHeight;
        }

        private bool Reached(Vector3 point)
        {
            Vector3 d = point - _viewer.position;
            return Mathf.Abs(d.y) < _reachHeight && new Vector2(d.x, d.z).magnitude < _reachRadius;
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

    }
}
