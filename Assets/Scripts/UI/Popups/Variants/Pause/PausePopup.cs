using System;
using UI.Widgets;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Popups.Variants.Pause
{
    public sealed class PausePopup : BasePopup
    {
        private const int MaxDots = 3;
        private const string HiddenDot = "<color=#00000000>.</color>";

        [SerializeField] private MenuButton _continue;
        [SerializeField] private MenuButton _restart;
        [SerializeField] private MenuButton _mainMenu;

        [Header("Loading Curtain")]
        [SerializeField] private CanvasGroup _curtain;
        [SerializeField, Min(0.05f)] private float _curtainTime = 0.25f;
        [SerializeField] private Text _curtainLabel;
        [SerializeField] private string _loadingText = "ЗАВАНТАЖЕННЯ";
        [SerializeField, Min(0.05f)] private float _dotInterval = 0.3f;

        [Header("Audio")]
        [SerializeField] private AudioSource _audio;
        [SerializeField] private AudioClip _hoverClip;
        [SerializeField] private AudioClip _clickClip;
        [SerializeField, Range(0f, 1f)] private float _hoverVolume = 0.25f;

        private Action _leaving;
        private bool _isLoading;
        private float _loadingTime;

        public event Action Continued;
        public event Action RestartChosen;
        public event Action MainMenuChosen;

        private void Awake()
        {
            _audio.ignoreListenerPause = true;
            _continue.SetPrimary(true);
            _curtain.alpha = 0f;

            _continue.Clicked += HandleContinue;
            _restart.Clicked += HandleRestart;
            _mainMenu.Clicked += HandleMainMenu;
            _continue.Hovered += HandleHovered;
            _restart.Hovered += HandleHovered;
            _mainMenu.Hovered += HandleHovered;
        }

        private void OnDestroy()
        {
            _continue.Clicked -= HandleContinue;
            _restart.Clicked -= HandleRestart;
            _mainMenu.Clicked -= HandleMainMenu;
            _continue.Hovered -= HandleHovered;
            _restart.Hovered -= HandleHovered;
            _mainMenu.Hovered -= HandleHovered;
        }

        private void Update()
        {
            if (!_isLoading)
            {
                return;
            }

            _loadingTime += Time.unscaledDeltaTime;
            int dots = Mathf.FloorToInt(_loadingTime / _dotInterval) % (MaxDots + 1);
            _curtainLabel.text = _loadingText + new string('.', dots) + string.Concat(System.Linq.Enumerable.Repeat(HiddenDot, MaxDots - dots));

            if (_leaving != null && _curtain.alpha >= 1f)
            {
                Action leaving = _leaving;
                _leaving = null;
                leaving.Invoke();
                return;
            }

            _curtain.alpha = Mathf.MoveTowards(_curtain.alpha, 1f, Time.unscaledDeltaTime / _curtainTime);
        }

        private void HandleContinue()
        {
            if (_isLoading)
            {
                return;
            }

            _audio.PlayOneShot(_clickClip);
            Continued?.Invoke();
        }

        private void HandleRestart() => Leave(() => RestartChosen?.Invoke());

        private void HandleMainMenu() => Leave(() => MainMenuChosen?.Invoke());

        private void Leave(Action leaving)
        {
            if (_isLoading)
            {
                return;
            }

            _audio.PlayOneShot(_clickClip);
            _curtain.blocksRaycasts = true;
            _leaving = leaving;
            _isLoading = true;
        }

        private void HandleHovered() => _audio.PlayOneShot(_hoverClip, _hoverVolume);
    }
}
