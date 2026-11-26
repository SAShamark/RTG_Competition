using Services;
using Services.Audio;
using UI.Widgets;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Popups.Variants.Settings
{
    public sealed class SettingsPopup : BasePopup
    {
        [SerializeField] private Slider _music;
        [SerializeField] private Slider _sounds;
        [SerializeField] private MenuButton _back;

        [Header("Audio")]
        [SerializeField] private AudioSource _audio;
        [SerializeField] private AudioClip _hoverClip;
        [SerializeField] private AudioClip _clickClip;
        [SerializeField, Range(0f, 1f)] private float _hoverVolume = 0.25f;
        [SerializeField, Min(0f)] private float _previewInterval = 0.12f;

        private float _lastPreview;

        private void Awake()
        {
            _audio.ignoreListenerPause = true;
            _music.SetValueWithoutNotify(AudioService.Instance.MusicLevel);
            _sounds.SetValueWithoutNotify(AudioService.Instance.SoundsLevel);

            _music.onValueChanged.AddListener(HandleMusicChanged);
            _sounds.onValueChanged.AddListener(HandleSoundsChanged);
            _back.Clicked += HandleBack;
            _back.Hovered += HandleHovered;
        }

        private void OnDestroy()
        {
            _music.onValueChanged.RemoveListener(HandleMusicChanged);
            _sounds.onValueChanged.RemoveListener(HandleSoundsChanged);
            _back.Clicked -= HandleBack;
            _back.Hovered -= HandleHovered;
        }

        private void Update()
        {
            if (GameInput.CancelPressed)
            {
                CloseTrigger();
            }
        }

        private void HandleMusicChanged(float level) => AudioService.Instance.SetMusicLevel(level);

        private void HandleSoundsChanged(float level)
        {
            AudioService.Instance.SetSoundsLevel(level);
            if (Time.unscaledTime - _lastPreview >= _previewInterval)
            {
                _lastPreview = Time.unscaledTime;
                _audio.PlayOneShot(_clickClip);
            }
        }

        private void HandleBack()
        {
            _audio.PlayOneShot(_clickClip);
            CloseTrigger();
        }

        private void HandleHovered() => _audio.PlayOneShot(_hoverClip, _hoverVolume);
    }
}
