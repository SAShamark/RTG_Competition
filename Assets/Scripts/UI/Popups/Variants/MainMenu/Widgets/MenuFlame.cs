using System;
using Services;
using UI.Widgets;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI.Popups.Variants.MainMenu.Widgets
{
    public sealed class MenuFlame : MonoBehaviour, IPointerClickHandler
    {
        private const float FullCircle = 360f;
        private const float GlowFlicker = 0.15f;

        [Header("Parts")]
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private RectTransform _visual;
        [SerializeField] private Image _glow;
        [SerializeField] private UiPulse _pulse;
        [SerializeField] private SparkBurst _sparks = new();

        [Header("Growth")]
        [SerializeField, Min(1)] private int _clicksToIgnite = 10;
        [SerializeField] private float _startScale = 1f;
        [SerializeField] private float _maxScale = 4.2f;
        [SerializeField] private float _growSharpness = 9f;
        [SerializeField] private Vector2 _glowAlpha = new(0.35f, 0.85f);

        [Header("Flicker")]
        [SerializeField] private float _flickerAmount = 0.07f;
        [SerializeField] private Vector2 _flickerSpeed = new(9f, 13f);
        [SerializeField] private float _swayDegrees = 4f;
        [SerializeField] private float _swaySpeed = 5f;

        [Header("Sparks")]
        [SerializeField] private Vector2Int _sparksPerClick = new(5, 12);
        [SerializeField] private float _clickSpread = 150f;
        [SerializeField] private Vector2 _clickPower = new(0.7f, 1.2f);
        [SerializeField] private float _ignitePower = 1.9f;
        [SerializeField, Range(0f, 1f)] private float _sparkHeight = 0.45f;

        [Header("Ignition")]
        [SerializeField, Min(0.05f)] private float _burstTime = 0.4f;
        [SerializeField] private float _burstGrow = 1.4f;

        private int _clicks;
        private float _scale;
        private float _burst = -1f;

        public event Action<float> Stoked;
        public event Action Ignited;

        private float Progress => (float)_clicks / _clicksToIgnite;
        private float TargetScale => Mathf.Lerp(_startScale, _maxScale, Easing.InQuad(Progress));

        private void Awake()
        {
            _sparks.Init();
            _scale = _startScale;
            Apply();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_burst >= 0f)
            {
                return;
            }

            _clicks++;
            _pulse.Play();

            if (_clicks < _clicksToIgnite)
            {
                int count = Mathf.RoundToInt(Mathf.Lerp(_sparksPerClick.x, _sparksPerClick.y, Progress));
                _sparks.Emit(SparkOrigin(), count, _clickSpread, Mathf.Lerp(_clickPower.x, _clickPower.y, Progress));
                Stoked?.Invoke(Progress);
                return;
            }

            _burst = 0f;
            _group.blocksRaycasts = false;
            _sparks.Emit(SparkOrigin(), _sparks.Capacity, FullCircle, _ignitePower);
            Ignited?.Invoke();
        }

        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;
            _sparks.Tick(deltaTime);

            if (_burst >= 0f)
            {
                _burst = Mathf.Min(_burst + deltaTime / _burstTime, 1f);
                _scale = _maxScale * (1f + _burstGrow * Easing.OutCubic(_burst));
                _group.alpha = 1f - _burst;
            }
            else
            {
                _scale = Mathf.Lerp(_scale, TargetScale, Easing.Damp(_growSharpness, deltaTime));
            }

            Apply();
        }

        private void Apply()
        {
            float time = Time.unscaledTime;
            transform.localScale = Vector3.one * _scale;
            _visual.localScale = new Vector3(
                1f + Mathf.Sin(time * _flickerSpeed.x) * _flickerAmount,
                1f + Mathf.Sin(time * _flickerSpeed.y) * _flickerAmount,
                1f);
            _visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * _swaySpeed) * _swayDegrees);

            Color glow = _glow.color;
            glow.a = Mathf.Lerp(_glowAlpha.x, _glowAlpha.y, Progress) * (1f - GlowFlicker + GlowFlicker * Mathf.Sin(time * _flickerSpeed.y));
            _glow.color = glow;
        }

        private Vector2 SparkOrigin()
        {
            var rect = (RectTransform)transform;
            return rect.anchoredPosition + Vector2.up * (rect.rect.height * _scale * _sparkHeight);
        }
    }
}
