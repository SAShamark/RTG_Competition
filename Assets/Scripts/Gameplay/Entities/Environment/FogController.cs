using System;
using UnityEngine;

namespace Gameplay.Entities.Environment
{
    [Serializable]
    public sealed class FogController
    {
        [SerializeField] private SmokeZone[] _zones = Array.Empty<SmokeZone>();
        [SerializeField] private float _clearBlendSpeed = 1.5f;

        private bool _defaultFog;
        private Color _defaultColor;
        private float _defaultDensity;
        private FogMode _defaultMode;
        private SmokeZone _active;
        private Color _fromColor;
        private float _fromDensity;
        private float _blend = 1f;

        public void Init()
        {
            _defaultFog = RenderSettings.fog;
            _defaultColor = RenderSettings.fogColor;
            _defaultDensity = RenderSettings.fogDensity;
            _defaultMode = RenderSettings.fogMode;
        }

        public void Dispose()
        {
            RenderSettings.fog = _defaultFog;
            RenderSettings.fogMode = _defaultMode;
            RenderSettings.fogColor = _defaultColor;
            RenderSettings.fogDensity = _defaultDensity;
        }

        public void Tick(Vector3 viewer, float deltaTime)
        {
            SmokeZone zone = FindZone(viewer);
            if (zone != _active)
            {
                _active = zone;
                _fromColor = RenderSettings.fogColor;
                _fromDensity = RenderSettings.fog && RenderSettings.fogMode == FogMode.ExponentialSquared ? RenderSettings.fogDensity : 0f;
                _blend = 0f;
            }

            if (_blend >= 1f)
            {
                return;
            }

            _blend = Mathf.MoveTowards(_blend, 1f, deltaTime * (_active != null ? _active.BlendSpeed : _clearBlendSpeed));
            if (_active != null)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = Color.Lerp(_fromColor, _active.Color, _blend);
                RenderSettings.fogDensity = Mathf.Lerp(_fromDensity, _active.Density, _blend);
                return;
            }

            RenderSettings.fogColor = Color.Lerp(_fromColor, _defaultColor, _blend);
            RenderSettings.fogDensity = Mathf.Lerp(_fromDensity, _defaultFog ? _defaultDensity : 0f, _blend);
            if (_blend >= 1f)
            {
                RenderSettings.fog = _defaultFog;
                RenderSettings.fogMode = _defaultMode;
                RenderSettings.fogDensity = _defaultDensity;
            }
        }

        private SmokeZone FindZone(Vector3 viewer)
        {
            SmokeZone best = null;
            foreach (SmokeZone zone in _zones)
            {
                if (zone.Contains(viewer) && (best == null || zone.Priority > best.Priority))
                {
                    best = zone;
                }
            }

            return best;
        }
    }
}
