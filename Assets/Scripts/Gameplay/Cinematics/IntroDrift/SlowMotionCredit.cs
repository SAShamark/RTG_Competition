using System;
using Services;
using UI.Widgets;
using UnityEngine;

namespace Gameplay.Cinematics.IntroDrift
{
    [Serializable]
    public sealed class SlowMotionCredit
    {
        [SerializeField] private CreditPanel _panel;
        [SerializeField, Range(0.05f, 1f)] private float _timeScale = 0.15f;
        [SerializeField, Min(0.01f)] private float _rampIn = 0.25f;
        [SerializeField, Min(0.01f)] private float _slideIn = 0.45f;
        [SerializeField, Min(0f)] private float _hold = 2f;
        [SerializeField, Min(0.01f)] private float _slideOut = 0.45f;
        [SerializeField] private float _slideOvershoot = 1.2f;

        private float _time = -1f;

        public float Slowness { get; private set; }

        public event Action PanelArrived;

        public void Init() => _panel.SetShown(0f);

        public void Begin() => _time = 0f;

        public void Tick(float unscaledDeltaTime)
        {
            if (_time < 0f || PauseController.IsPaused)
            {
                return;
            }

            bool arriving = _time < _slideIn;
            _time += unscaledDeltaTime;
            if (arriving && _time >= _slideIn)
            {
                PanelArrived?.Invoke();
            }

            float leaving = _time - _slideIn - _hold;
            if (leaving >= _slideOut)
            {
                Stop();
                return;
            }

            float shown = leaving < 0f ? Easing.OutBack(_time / _slideIn, _slideOvershoot) : 1f - Easing.InQuad(leaving / _slideOut);
            Slowness = Mathf.Min(Easing.Smooth(_time / _rampIn), 1f - Easing.Smooth(leaving / _slideOut));
            Time.timeScale = Mathf.Lerp(1f, _timeScale, Slowness);
            _panel.SetShown(shown);
        }

        public void Stop()
        {
            _time = -1f;
            Slowness = 0f;
            Time.timeScale = 1f;
            _panel.SetShown(0f);
        }
    }
}
