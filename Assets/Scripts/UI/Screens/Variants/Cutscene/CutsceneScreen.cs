using System;
using Gameplay.Cinematics;
using Gameplay.Dialogue;
using Services;
using Services.Audio;
using UI.Screens.Base;
using UI.Screens.Variants.Cutscene.Widgets;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Screens.Variants.Cutscene
{
    public sealed class CutsceneScreen : BaseScreen
    {
        private const float BackOvershoot = 0.9f;
        private const int NoCue = -1;
        private const float NoProgress = -1f;

        [Header("Letterbox")]
        [SerializeField] private RectTransform _topBar;
        [SerializeField] private RectTransform _bottomBar;
        [SerializeField] private float _aspect = 2.35f;
        [SerializeField] private Image _vignette;
        [SerializeField] private float _vignetteAlpha = 0.55f;

        [Header("Title Card")]
        [SerializeField] private CanvasGroup _titleGroup;
        [SerializeField] private Text _titleLine;
        [SerializeField] private Text _titleSubLine;
        [SerializeField] private RectTransform _titleRule;
        [SerializeField] private float _titleFadeIn = 0.25f;
        [SerializeField] private float _titleFadeOut = 0.5f;
        [SerializeField] private float _titleFadeSpeed = 3f;
        [SerializeField] private float _titleCharsPerSecond = 28f;
        [SerializeField] private float _subLinePause = 0.15f;
        [SerializeField] private float _subCharsPerSecond = 34f;
        [SerializeField] private float _ruleGrowTime = 0.6f;

        [Header("Subtitles")]
        [SerializeField] private DialogLibrary _dialogs;
        [SerializeField] private CanvasGroup _subtitleGroup;
        [SerializeField] private RectTransform _subtitleRect;
        [SerializeField] private Text _speaker;
        [SerializeField] private Text _line;
        [SerializeField] private float _subtitleFadeIn = 0.2f;
        [SerializeField] private float _subtitleFadeOut = 0.3f;
        [SerializeField] private float _subtitleHideSpeed = 4f;
        [SerializeField] private float _subtitleDrop = 24f;

        [Header("Skip")]
        [SerializeField] private CanvasGroup _skipGroup;
        [SerializeField] private Image _skipRing;
        [SerializeField] private float _skipHoldTime = 0.7f;
        [SerializeField] private float _skipHintDelay = 1.0f;
        [SerializeField] private float _skipFadeSpeed = 3f;
        [SerializeField] private float _skipDecaySpeed = 2f;

        [Header("Widgets")]
        [SerializeField] private FireProgressWidget _fireProgress;

        private ICinematic _cinematic;
        private Action _finishedHandler;
        private Vector2 _subtitleRest;
        private float _skipHold;
        private float _barsShown;
        private int _cueIndex = NoCue;

        public override void Initialize()
        {
            base.Initialize();
            _subtitleRest = _subtitleRect.anchoredPosition;
        }

        private void OnDestroy() => Release(_cinematic);

        public void Present(ICinematic cinematic)
        {
            Release(_cinematic);
            _cinematic = cinematic;
            _finishedHandler = () => Release(cinematic);
            _cinematic.Finished += _finishedHandler;
            _skipHold = 0f;
            _cueIndex = NoCue;
            _barsShown = cinematic.Settings.BarsInTime <= 0f ? 1f : 0f;
            _subtitleGroup.alpha = 0f;
            _titleGroup.alpha = 0f;
            _skipGroup.alpha = 0f;
            _skipRing.fillAmount = 0f;
            ApplyBars(_barsShown);
        }

        private void Release(ICinematic cinematic)
        {
            if (cinematic == null || cinematic != _cinematic)
            {
                return;
            }

            _cinematic.Finished -= _finishedHandler;
            _cinematic = null;
            _finishedHandler = null;
        }

        private void Update()
        {
            if (_cinematic == null)
            {
                return;
            }

            float time = (float)_cinematic.Director.time;
            float duration = (float)_cinematic.Director.duration;
            CinematicSettings settings = _cinematic.Settings;

            _barsShown = Mathf.Min(FadeIn(time, settings.BarsInTime), FadeIn(duration - time, settings.BarsOutTime));
            ApplyBars(_barsShown);
            UpdateFireProgress();
            UpdateTitle(time, settings);
            UpdateSubtitles(time);
            UpdateSkip(time);
        }

        private static float FadeIn(float time, float length) => length <= 0f ? 1f : Easing.Smooth(time / length);

        // bar height = (canvasHeight − canvasWidth / aspect) / 2
        private void ApplyBars(float shown)
        {
            Rect canvas = ((RectTransform)transform).rect;
            float height = Mathf.Max(0f, (canvas.height - canvas.width / _aspect) * 0.5f) * Easing.OutBack(shown, BackOvershoot);
            _topBar.sizeDelta = new Vector2(0f, height);
            _bottomBar.sizeDelta = new Vector2(0f, height);

            Color vignette = _vignette.color;
            vignette.a = _vignetteAlpha * shown;
            _vignette.color = vignette;
        }

        private void UpdateFireProgress()
        {
            float progress = _cinematic is IFireProgressSource source ? source.FireProgress : NoProgress;
            bool isShown = progress >= 0f;
            if (_fireProgress.gameObject.activeSelf != isShown)
            {
                _fireProgress.gameObject.SetActive(isShown);
                _fireProgress.Snap(Mathf.Max(0f, progress));
            }

            if (isShown)
            {
                _fireProgress.SetProgress(progress);
            }
        }

        private void UpdateTitle(float time, CinematicSettings settings)
        {
            if (string.IsNullOrEmpty(settings.TitleLine))
            {
                _titleGroup.alpha = 0f;
                return;
            }

            float local = time - settings.TitleTime;
            if (local < 0f || local > settings.TitleDuration)
            {
                _titleGroup.alpha = Mathf.MoveTowards(_titleGroup.alpha, 0f, Time.deltaTime * _titleFadeSpeed);
                return;
            }

            _titleGroup.alpha = Mathf.Min(FadeIn(local, _titleFadeIn), FadeIn(settings.TitleDuration - local, _titleFadeOut));
            _titleLine.text = TypeText(settings.TitleLine, local * _titleCharsPerSecond);
            float subStart = settings.TitleLine.Length / _titleCharsPerSecond + _subLinePause;
            _titleSubLine.text = TypeText(settings.TitleSubLine, (local - subStart) * _subCharsPerSecond);
            _titleRule.localScale = new Vector3(FadeIn(local, _ruleGrowTime), 1f, 1f);
        }

        private static string TypeText(string text, float chars)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return text.Substring(0, Mathf.Clamp(Mathf.FloorToInt(chars), 0, text.Length));
        }

        private void UpdateSubtitles(float time)
        {
            SubtitleCue[] cues = _cinematic.Subtitles;
            int active = ActiveCue(cues, time);
            if (active != _cueIndex && active != NoCue)
            {
                ShowPhrase(_dialogs.Get(cues[active].LineId));
            }

            _cueIndex = active;
            if (active == NoCue)
            {
                _subtitleGroup.alpha = Mathf.MoveTowards(_subtitleGroup.alpha, 0f, Time.deltaTime * _subtitleHideSpeed);
            }
            else
            {
                float local = time - cues[active].Time;
                _subtitleGroup.alpha = Mathf.Min(FadeIn(local, _subtitleFadeIn), FadeIn(CueDuration(cues[active]) - local, _subtitleFadeOut));
            }

            _subtitleRect.anchoredPosition = _subtitleRest + Vector2.down * (1f - _subtitleGroup.alpha) * _subtitleDrop;
        }

        private int ActiveCue(SubtitleCue[] cues, float time)
        {
            int active = NoCue;
            for (int i = 0; i < cues.Length; i++)
            {
                if (time >= cues[i].Time && time <= cues[i].Time + CueDuration(cues[i]))
                {
                    active = i;
                }
            }

            return active;
        }

        private void ShowPhrase(DialogPhrase phrase)
        {
            _speaker.text = phrase.Speaker;
            _speaker.color = phrase.Color;
            _line.text = phrase.Text;
            if (phrase.Clip != null)
            {
                AudioService.Instance.PlayVoice(phrase.Clip);
            }
        }

        private float CueDuration(SubtitleCue cue)
        {
            AudioClip clip = _dialogs.Get(cue.LineId).Clip;
            return clip != null ? clip.length : cue.Duration;
        }

        private void UpdateSkip(float time)
        {
            bool isHolding = GameInput.IsSkipHeld && !global::Gameplay.PauseController.IsPaused;
            bool isAnyPress = GameInput.AnyPressed;
            float deltaTime = Time.unscaledDeltaTime;

            _skipHold = isHolding ? _skipHold + deltaTime : Mathf.MoveTowards(_skipHold, 0f, deltaTime * _skipDecaySpeed);
            _skipRing.fillAmount = Mathf.Clamp01(_skipHold / _skipHoldTime);

            float hint = time > _skipHintDelay || isAnyPress || isHolding ? _barsShown : 0f;
            _skipGroup.alpha = Mathf.MoveTowards(_skipGroup.alpha, hint, deltaTime * _skipFadeSpeed);

            if (_skipHold >= _skipHoldTime)
            {
                _skipHold = 0f;
                _cinematic.Skip();
            }
        }
    }
}
