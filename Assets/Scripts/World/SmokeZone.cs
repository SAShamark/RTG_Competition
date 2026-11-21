using UnityEngine;

namespace Game.World
{
    [RequireComponent(typeof(BoxCollider))]
    public class SmokeZone : MonoBehaviour
    {
        [SerializeField] private Color _color = new(0.55f, 0.5f, 0.47f);
        [SerializeField] private float _density = 0.05f;
        [SerializeField] private int _priority;
        [SerializeField] private float _blendSpeed = 1.5f;

        private static SmokeZone _active;
        private static bool _savedDefaults;
        private static bool _defaultFog;
        private static Color _defaultColor;
        private static float _defaultDensity;
        private static FogMode _defaultMode;
        private static float _blend;
        private static Color _fromColor;
        private static float _fromDensity;

        private BoxCollider _box;

        private void Awake()
        {
            _box = GetComponent<BoxCollider>();
            _box.isTrigger = true;
            if (!_savedDefaults)
            {
                _savedDefaults = true;
                _defaultFog = RenderSettings.fog;
                _defaultColor = RenderSettings.fogColor;
                _defaultDensity = RenderSettings.fogDensity;
                _defaultMode = RenderSettings.fogMode;
            }
        }

        private void OnDestroy()
        {
            if (_active == this)
            {
                _active = null;
            }

            _savedDefaults = false;
        }

        private void Update()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            bool inside = _box.bounds.Contains(cam.transform.position);
            if (inside && (_active == null || _active == this || _priority > _active._priority))
            {
                if (_active != this)
                {
                    _fromColor = RenderSettings.fogColor;
                    _fromDensity = RenderSettings.fogMode == FogMode.ExponentialSquared ? RenderSettings.fogDensity : 0f;
                    _blend = 0f;
                    _active = this;
                }
            }
            else if (!inside && _active == this)
            {
                _active = null;
                _fromColor = RenderSettings.fogColor;
                _fromDensity = RenderSettings.fogDensity;
                _blend = 0f;
            }

            if (_active != this && !(_active == null && this == FirstZone()))
            {
                return;
            }

            _blend = Mathf.MoveTowards(_blend, 1f, Time.deltaTime * _blendSpeed);
            if (_active != null)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = Color.Lerp(_fromColor, _active._color, _blend);
                RenderSettings.fogDensity = Mathf.Lerp(_fromDensity, _active._density, _blend);
            }
            else
            {
                RenderSettings.fogColor = Color.Lerp(_fromColor, _defaultColor, _blend);
                RenderSettings.fogDensity = Mathf.Lerp(_fromDensity, _defaultFog ? _defaultDensity : 0f, _blend);
                if (_blend >= 1f)
                {
                    RenderSettings.fog = _defaultFog;
                    RenderSettings.fogMode = _defaultMode;
                    RenderSettings.fogDensity = _defaultDensity;
                }
            }
        }

        private static SmokeZone _first;

        private static SmokeZone FirstZone()
        {
            if (_first == null)
            {
                _first = FindAnyObjectByType<SmokeZone>();
            }

            return _first;
        }
    }
}
