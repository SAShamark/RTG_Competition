using Gameplay.Cinematics;
using Gameplay.Dialogue;
using Services;
using UI.Screens.Variants.Cutscene.Widgets;
using System;
using UI.Screens.Base;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UI.Screens.Variants.Cutscene
{
    public sealed class CutsceneScreen : BaseScreen
    {
        private const float BackOvershoot = 0.9f;

        [Header("Letterbox")]
        [SerializeField] private RectTransform _topBar;
        [SerializeField] private RectTransform _bottomBar;
        [SerializeField] private RectTransform _canvasRect;
        [SerializeField] private float _aspect = 2.35f;
        [SerializeField] private Image _vignette;
        [SerializeField] private float _vignetteAlpha = 0.55f;

        [Header("Title card")]
        [SerializeField] private CanvasGroup _titleGroup;
        [SerializeField] private Text _titleLine;
        [SerializeField] private Text _titleSubLine;
        [SerializeField] private RectTransform _titleRule;

        [Header("Subtitles")]
        [SerializeField] private CanvasGroup _subtitleGroup;
        [SerializeField] private RectTransform _subtitleRect;
        [SerializeField] private Text _speaker;
        [SerializeField] private Text _line;

        [Header("Skip")]
        [SerializeField] private FireProgressWidget _fireProgress;
        [SerializeField] private DialogLibrary _dialogs;
        [SerializeField] private CanvasGroup _skipGroup;
        [SerializeField] private Image _skipRing;
        [SerializeField] private float _skipHoldTime = 0.7f;
        [SerializeField] private float _skipHintDelay = 1.0f;
        [SerializeField] private float _titleFadeSpeed = 3f;
        [SerializeField] private float _titleFadeIn = 0.25f;
        [SerializeField] private float _titleCharsPerSecond = 28f;
        [SerializeField] private float _subLinePause = 0.15f;
        [SerializeField] private float _subCharsPerSecond = 34f;
        [SerializeField] private float _ruleGrowTime = 0.6f;
        [SerializeField] private float _subtitleFadeIn = 0.2f;
        [SerializeField] private float _subtitleFadeOut = 0.3f;
        [SerializeField] private float _subtitleHideSpeed = 4f;
        [SerializeField] private float _subtitleDrop = 24f;
        [SerializeField] private float _skipFadeSpeed = 3f;

        private ICinematic _cinematic;
        private float _skipHold;
        private float _barsShown;
        private int _cueIndex = -1;
        private bool _finished;
        private Vector2 _subtitleRest;

        public event Action PresentationFinished;

        public override void Initialize()
        {
            base.Initialize();
            _subtitleRest = _subtitleRect.anchoredPosition;
        }

        public void Present(ICinematic cinematic)
        {
            _cinematic = cinematic;
            _finished = false;
            _skipHold = 0f;
            _cueIndex = -1;
            _barsShown = cinematic.Settings.BarsInTime <= 0f ? 1f : 0f;
            _subtitleGroup.alpha = 0f;
            _titleGroup.alpha = 0f;
            _skipGroup.alpha = 0f;
            _skipRing.fillAmount = 0f;
            _cinematic.Finished += HandleFinished;
            ApplyBars(_barsShown);
        }

        private void HandleFinished()
        {
            _cinematic.Finished -= HandleFinished;
            _finished = true;
            PresentationFinished?.Invoke();
        }

        private void Update()
        {
            if (_cinematic == null || _finished)
            {
                return;
            }

            float t = (float)_cinematic.Director.time;
            float duration = (float)_cinematic.Director.duration;
            UpdateFireProgress();
            CinematicSettings settings = _cinematic.Settings;

            float barsIn = settings.BarsInTime <= 0f ? 1f : Easing.Smooth(t / settings.BarsInTime);
            float barsOut = settings.BarsOutTime <= 0f ? 1f : Easing.Smooth((duration - t) / settings.BarsOutTime);
            _barsShown = Mathf.Min(barsIn, barsOut);
            ApplyBars(_barsShown);

            UpdateTitle(t, settings);
            UpdateSubtitles(t);
            UpdateSkip(t);
        }

        private void ApplyBars(float k)
        {
            Rect canvas = _canvasRect.rect;
            float height = Mathf.Max(0f, (canvas.height - canvas.width / _aspect) * 0.5f);
            float h = height * Easing.OutBack(k, BackOvershoot);
            _topBar.sizeDelta = new Vector2(0f, h);
            _bottomBar.sizeDelta = new Vector2(0f, h);
            Color c = _vignette.color;
            c.a = _vignetteAlpha * k;
            _vignette.color = c;
        }

        private void UpdateFireProgress()
        {
            float progress = _cinematic is IFireProgressSource source ? source.FireProgress : -1f;
            bool show = progress >= 0f;
            if (_fireProgress.gameObject.activeSelf != show)
            {
                _fireProgress.gameObject.SetActive(show);
            }

            if (show)
            {
                _fireProgress.SetProgress(progress);
            }
        }

        private void UpdateTitle(float t, CinematicSettings settings)
        {
            if (string.IsNullOrEmpty(settings.TitleLine))
            {
                _titleGroup.alpha = 0f;
                return;
            }

            float local = t - settings.TitleTime;
            if (local < 0f || local > settings.TitleDuration)
            {
                _titleGroup.alpha = Mathf.MoveTowards(_titleGroup.alpha, 0f, Time.deltaTime * _titleFadeSpeed);
                return;
            }

            _titleGroup.alpha = Mathf.Min(Easing.Smooth(local / _titleFadeIn), Easing.Smooth((settings.TitleDuration - local) / 0.5f));
            int chars = Mathf.Min(settings.TitleLine.Length, Mathf.FloorToInt(local * _titleCharsPerSecond));
            _titleLine.text = settings.TitleLine.Substring(0, chars);
            float subStart = settings.TitleLine.Length / _titleCharsPerSecond + _subLinePause;
            int subChars = string.IsNullOrEmpty(settings.TitleSubLine) ? 0 : Mathf.Clamp(Mathf.FloorToInt((local - subStart) * _subCharsPerSecond), 0, settings.TitleSubLine.Length);
            _titleSubLine.text = subChars > 0 ? settings.TitleSubLine.Substring(0, subChars) : string.Empty;
            _titleRule.localScale = new Vector3(Easing.Smooth(local / _ruleGrowTime), 1f, 1f);
        }

        private void UpdateSubtitles(float t)
        {
            SubtitleCue[] cues = _cinematic.Subtitles;
            int active = -1;
            for (int i = 0; i < cues.Length; i++)
            {
                if (t >= cues[i].Time && t <= cues[i].Time + CueDuration(cues[i]))
                {
                    active = i;
                }
            }

            if (active != _cueIndex && active >= 0)
            {
                SubtitleCue cue = cues[active];
                DialogPhrase phrase = _dialogs.Get(cue.LineId);
                _speaker.text = phrase.Speaker;
                _speaker.color = phrase.Color;
                _line.text = phrase.Text;
                _dialogs.PlayVoice(phrase.Text);
            }

            _cueIndex = active;
            float local = active >= 0 ? t - cues[active].Time : 0f;
            float target = active >= 0 ? Mathf.Min(Easing.Smooth(local / _subtitleFadeIn), Easing.Smooth((CueDuration(cues[active]) - local) / _subtitleFadeOut)) : 0f;
            _subtitleGroup.alpha = active >= 0 ? target : Mathf.MoveTowards(_subtitleGroup.alpha, 0f, Time.deltaTime * _subtitleHideSpeed);
            _subtitleRect.anchoredPosition = _subtitleRest + Vector2.down * (1f - _subtitleGroup.alpha) * _subtitleDrop;
        }

        private float CueDuration(SubtitleCue cue) =>
            _dialogs.TryGetClip(_dialogs.Get(cue.LineId).Text, out AudioClip clip) ? clip.length : cue.Duration;

        private void UpdateSkip(float t)
        {
            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;
            bool holding = kb.spaceKey.isPressed || kb.enterKey.isPressed || kb.escapeKey.isPressed || mouse.leftButton.isPressed;
            bool anyPress = kb.anyKey.wasPressedThisFrame || mouse.leftButton.wasPressedThisFrame;

            _skipHold = holding ? _skipHold + Time.unscaledDeltaTime : Mathf.MoveTowards(_skipHold, 0f, Time.unscaledDeltaTime * 2f);
            _skipRing.fillAmount = Mathf.Clamp01(_skipHold / _skipHoldTime);

            float hintTarget = t > _skipHintDelay || anyPress || holding ? 1f : 0f;
            _skipGroup.alpha = Mathf.MoveTowards(_skipGroup.alpha, hintTarget * _barsShown, Time.unscaledDeltaTime * _skipFadeSpeed);

            if (_skipHold >= _skipHoldTime)
            {
                _skipHold = 0f;
                _cinematic.Skip();
            }
        }


    }
}
