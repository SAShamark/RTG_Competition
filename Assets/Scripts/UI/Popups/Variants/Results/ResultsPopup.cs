using System;
using System.Collections;
using Gameplay.Entities.Characters;
using Services;
using Services.Audio;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace UI.Popups.Variants.Results
{
    public struct MissionResults
    {
        public int Rescued;
        public int RescueTotal;
        public int MoneyFound;
        public float TimeSeconds;
        public float FirePercent;
    }

    public sealed class ResultsPopup : BasePopup
    {
        private const float PhotoSlotAspect = 1001f / 455f;
        private const int DepthBits = 24;
        private const int SecondsInMinute = 60;
        private const float Idle = -1f;

        [Header("Timing")]
        [FormerlySerializedAs("_fadeTime")]
        [SerializeField] private float _introTime = 1.0f;
        [SerializeField] private float _pageDelayFactor = 0.7f;
        [SerializeField] private float _pageInTime = 0.55f;
        [SerializeField] private float _countDelay = 0.5f;
        [SerializeField] private float _countTime = 1.3f;

        [Header("Layout")]
        [SerializeField] private RectTransform _page;
        [SerializeField] private CanvasGroup _pageGroup;
        [SerializeField] private RawImage _photo;
        [SerializeField] private Text _fireValue;
        [SerializeField] private Text _peopleValue;
        [SerializeField] private Text _timeText;
        [SerializeField] private Button _replay;
        [SerializeField] private Button _continue;

        [Header("Photo")]
        [SerializeField] private int _photoWidth = 1320;
        [SerializeField] private int _photoHeight = 600;

        [Header("Look")]
        [SerializeField] private float _pageStartScale = 1.25f;
        [SerializeField] private float _pageStartTilt = 9f;
        [SerializeField] private float _pageEndTilt = -1.2f;

        private MissionResults _results;
        private RenderTexture _photoTexture;
        private ResultsPhotoRig _rig;
        private float _time = Idle;

        public event Action Continued;

        private void Awake()
        {
            _replay.onClick.AddListener(Replay);
            _continue.onClick.AddListener(Continue);
        }

        private void OnDestroy()
        {
            _replay.onClick.RemoveListener(Replay);
            _continue.onClick.RemoveListener(Continue);
            if (_photoTexture != null)
            {
                _photoTexture.Release();
                Destroy(_photoTexture);
            }
        }

        public void Present(MissionResults results, Texture photo, ResultsPhotoRig rig)
        {
            _results = results;
            _rig = rig;
            _time = 0f;
            _canvasGroup.interactable = false;
            UnlockCursor();

            if (photo != null)
            {
                ShowPhoto(photo);
                FitPhoto(photo);
            }
            else
            {
                StartCoroutine(TakePhoto());
            }
        }

        private void Update()
        {
            if (_time < 0f)
            {
                return;
            }

            UnlockCursor();
            _time += Time.unscaledDeltaTime;
            AnimatePage();
            AnimateCounters();
        }

        private void AnimatePage()
        {
            float progress = Mathf.Clamp01((_time - _introTime * _pageDelayFactor) / _pageInTime);
            float ease = Easing.OutCubic(progress);
            _pageGroup.alpha = progress;
            _page.localScale = Vector3.one * Mathf.Lerp(_pageStartScale, 1f, ease);
            _page.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(_pageStartTilt, _pageEndTilt, ease));
            _canvasGroup.interactable = progress >= 1f;
        }

        private void AnimateCounters()
        {
            float count = Mathf.Clamp01((_time - _introTime - _countDelay) / _countTime);
            int seconds = Mathf.RoundToInt(_results.TimeSeconds * count);
            _fireValue.text = $"{Mathf.RoundToInt(_results.FirePercent * count)}%";
            _peopleValue.text = $"{Mathf.RoundToInt(_results.Rescued * count)}";
            _timeText.text = $"{seconds / SecondsInMinute:00}:{seconds % SecondsInMinute:00}";
        }

        private IEnumerator TakePhoto()
        {
            _rig.Set.SetActive(true);
            _rig.Actor.SetPose(CharacterPose.Flailing);
            yield return new WaitForEndOfFrame();
            yield return null;

            _photoTexture = new RenderTexture(_photoWidth, _photoHeight, DepthBits);
            _rig.Camera.targetTexture = _photoTexture;
            _rig.Camera.Render();
            _rig.Camera.targetTexture = null;
            _rig.Set.SetActive(false);
            ShowPhoto(_photoTexture);
        }

        private void ShowPhoto(Texture photo)
        {
            _photo.texture = photo;
            _photo.color = Color.white;
        }

        // center-crop: visible fraction = slot / aspect (wide) or aspect / slot (tall)
        private void FitPhoto(Texture photo)
        {
            float aspect = (float)photo.width / Mathf.Max(1, photo.height);
            _photo.uvRect = aspect > PhotoSlotAspect
                ? new Rect((1f - PhotoSlotAspect / aspect) * 0.5f, 0f, PhotoSlotAspect / aspect, 1f)
                : new Rect(0f, (1f - aspect / PhotoSlotAspect) * 0.5f, 1f, aspect / PhotoSlotAspect);
        }

        private static void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private static void Replay()
        {
            AudioService.Instance.Play(SoundType.UiConfirm);
            SceneManager.LoadScene(SceneManager.GetActiveScene().path);
        }

        private void Continue()
        {
            AudioService.Instance.Play(SoundType.UiConfirm);
            _time = Idle;
            Continued?.Invoke();
            CloseTrigger();
        }
    }
}
