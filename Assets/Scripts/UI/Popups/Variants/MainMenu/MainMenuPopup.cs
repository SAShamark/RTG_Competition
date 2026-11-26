using System;
using Services;
using UI.Popups.Variants.MainMenu.Widgets;
using UI.Widgets;
using UnityEngine;

namespace UI.Popups.Variants.MainMenu
{
    public sealed class MainMenuPopup : BasePopup
    {
        [Header("Buttons")]
        [SerializeField] private MenuButton _newGame;
        [SerializeField] private MenuButton _exit;

        [Header("Backdrop")]
        [SerializeField] private MenuFlame _flame;
        [SerializeField] private BurnWave _burnWave;

        [Header("Appearance")]
        [SerializeField] private CanvasGroup[] _introItems;
        [SerializeField] private float _introDelay = 0.15f;
        [SerializeField] private float _introStagger = 0.09f;
        [SerializeField, Min(0.05f)] private float _introItemTime = 0.45f;
        [SerializeField] private float _introSlide = 70f;
        [SerializeField, Min(0.05f)] private float _fadeOutTime = 0.35f;

        [Header("Audio")]
        [SerializeField] private AudioSource _audio;
        [SerializeField] private AudioClip _hoverClip;
        [SerializeField] private AudioClip _clickClip;
        [SerializeField] private AudioClip _stokeClip;
        [SerializeField] private AudioClip _igniteClip;
        [SerializeField, Range(0f, 1f)] private float _hoverVolume = 0.25f;
        [SerializeField] private Vector2 _stokePitch = new(0.9f, 1.7f);

        private Vector2[] _introRest;
        private float _introTime;
        private float _fadeOut = -1f;
        private Action _chosen;

        public event Action NewGameChosen;

        private void Awake()
        {
            _audio.ignoreListenerPause = true;
            _introRest = new Vector2[_introItems.Length];
            for (int i = 0; i < _introItems.Length; i++)
            {
                _introRest[i] = ((RectTransform)_introItems[i].transform).anchoredPosition;
            }

            ApplyIntro();
            _newGame.SetPrimary(true);

            _newGame.Clicked += HandleNewGame;
            _exit.Clicked += HandleExit;
            _newGame.Hovered += HandleHovered;
            _exit.Hovered += HandleHovered;
            _flame.Stoked += HandleStoked;
            _flame.Ignited += HandleIgnited;
        }

        private void OnDestroy()
        {
            _newGame.Clicked -= HandleNewGame;
            _exit.Clicked -= HandleExit;
            _newGame.Hovered -= HandleHovered;
            _exit.Hovered -= HandleHovered;
            _flame.Stoked -= HandleStoked;
            _flame.Ignited -= HandleIgnited;
        }

        private void Update()
        {
            _introTime += Time.unscaledDeltaTime;
            ApplyIntro();

            if (_fadeOut < 0f)
            {
                return;
            }

            _fadeOut += Time.unscaledDeltaTime / _fadeOutTime;
            _canvasGroup.alpha = 1f - Easing.Smooth(_fadeOut);
            if (_fadeOut >= 1f)
            {
                _fadeOut = -1f;
                _chosen?.Invoke();
            }
        }

        private void ApplyIntro()
        {
            for (int i = 0; i < _introItems.Length; i++)
            {
                float shown = Easing.OutCubic(Mathf.Clamp01((_introTime - _introDelay - i * _introStagger) / _introItemTime));
                _introItems[i].alpha = shown;
                ((RectTransform)_introItems[i].transform).anchoredPosition = _introRest[i] + Vector2.left * (_introSlide * (1f - shown));
            }
        }

        private void HandleNewGame() => Leave(NewGameChosen);

        private void Leave(Action chosen)
        {
            if (_fadeOut >= 0f)
            {
                return;
            }

            PlayFlat(_clickClip, 1f);
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            _chosen = chosen;
            _fadeOut = 0f;
        }

        private void HandleHovered() => PlayFlat(_hoverClip, _hoverVolume);

        private void HandleStoked(float progress)
        {
            _audio.pitch = Mathf.Lerp(_stokePitch.x, _stokePitch.y, progress);
            _audio.PlayOneShot(_stokeClip);
        }

        private void HandleIgnited()
        {
            PlayFlat(_igniteClip, 1f);
            _burnWave.Burn(_flame.transform.position);
        }

        private void HandleExit()
        {
            PlayFlat(_clickClip, 1f);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void PlayFlat(AudioClip clip, float volume)
        {
            _audio.pitch = 1f;
            _audio.PlayOneShot(clip, volume);
        }
    }
}
