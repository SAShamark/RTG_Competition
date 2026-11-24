using Services;
using Services.Audio;
using System.Collections;
using Gameplay.Entities.Characters.Residents;
using UnityEngine;
using UnityEngine.SceneManagement;
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

        [Header("Timing")]
        [SerializeField] private float _fadeTime = 1.0f;

        [Header("Layout")]
        [SerializeField] private Image _black;
        [SerializeField] private RectTransform _page;
        [SerializeField] private CanvasGroup _pageGroup;
        [SerializeField] private RawImage _photo;
        [SerializeField] private Text _fireValue;
        [SerializeField] private Text _peopleValue;
        [SerializeField] private Text _timeText;
        [SerializeField] private Button _replay;
        [SerializeField] private int _photoWidth = 1320;
        [SerializeField] private int _photoHeight = 600;
        [SerializeField] private float _dimAlpha = 0.35f;
        [SerializeField] private Color _shadeColor = new(0.05f, 0.03f, 0.02f);
        [SerializeField] private float _fadeAlpha = 0.8f;
        [SerializeField] private float _pageDelayFactor = 0.7f;
        [SerializeField] private float _pageInTime = 0.55f;
        [SerializeField] private float _pageStartScale = 1.25f;
        [SerializeField] private float _pageStartTilt = 9f;
        [SerializeField] private float _pageEndTilt = -1.2f;
        [SerializeField] private float _countTime = 1.3f;
        private MissionResults _results;
        private RenderTexture _photoTexture;
        private ResultsPhotoRig _rig;
        private float _time = -1f;

        private void Awake()
        {
            _replay.onClick.AddListener(Replay);
        }

        public void Present(MissionResults results, Texture photo, ResultsPhotoRig rig)
        {
            _results = results;
            _rig = rig;
            _canvas.gameObject.SetActive(true);
            _time = 0f;
            _dimOnly = photo != null;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (photo != null)
            {
                _photo.texture = photo;
                _photo.color = Color.white;
                FitPhoto(photo);
            }
            else
            {
                StartCoroutine(TakePhoto());
            }
        }

        private bool _dimOnly;

        private void FitPhoto(Texture photo)
        {
            float slot = PhotoSlotAspect;
            float aspect = (float)photo.width / Mathf.Max(1, photo.height);
            _photo.uvRect = aspect > slot
                ? new Rect((1f - slot / aspect) * 0.5f, 0f, slot / aspect, 1f)
                : new Rect(0f, (1f - aspect / slot) * 0.5f, 1f, aspect / slot);
        }

        private IEnumerator TakePhoto()
        {
            _rig.Set.SetActive(true);
            _rig.Actor.SetPose(ResidentPose.Flailing);
            yield return new WaitForEndOfFrame();
            yield return null;
            _photoTexture = new RenderTexture(_photoWidth, _photoHeight, DepthBits);
            _rig.Camera.targetTexture = _photoTexture;
            _rig.Camera.Render();
            _rig.Camera.targetTexture = null;
            _photo.texture = _photoTexture;
            _photo.color = Color.white;
            _rig.Set.SetActive(false);
        }

        private void Update()
        {
            if (_time < 0f)
            {
                return;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _time += Time.unscaledDeltaTime;
            _black.color = new Color(_shadeColor.r, _shadeColor.g, _shadeColor.b, Mathf.Clamp01(_time / _fadeTime) * (_dimOnly ? _dimAlpha : _fadeAlpha));

            float k = Mathf.Clamp01((_time - _fadeTime * _pageDelayFactor) / _pageInTime);
            float ease = Easing.OutCubic(k);
            _pageGroup.alpha = k;
            _page.localScale = Vector3.one * Mathf.Lerp(_pageStartScale, 1f, ease);
            _page.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(_pageStartTilt, _pageEndTilt, ease));

            float count = Mathf.Clamp01((_time - _fadeTime - 0.5f) / _countTime);
            _fireValue.text = $"{Mathf.RoundToInt(_results.FirePercent * count)}%";
            _peopleValue.text = $"{Mathf.RoundToInt(_results.Rescued * count)}";
            int seconds = Mathf.RoundToInt(_results.TimeSeconds * count);
            _timeText.text = $"{seconds / 60:00}:{seconds % 60:00}";
        }

        private static void Replay()
        {
            AudioService.Instance.Play(SoundType.UiConfirm);
            SceneManager.LoadScene(SceneManager.GetActiveScene().path);
        }

        private void OnDestroy()
        {
            _replay.onClick.RemoveListener(Replay);
            if (_photoTexture != null)
            {
                _photoTexture.Release();
            }
        }
    }
}
