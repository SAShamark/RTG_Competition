using System;
using Game.Scenes;
using UI.Screens.Base;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UI.Screens.Variants.Cutscene
{
    public class CutsceneScreen : BaseScreen
    {
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
        [SerializeField] private CanvasGroup _skipGroup;
        [SerializeField] private Image _skipRing;
        [SerializeField] private float _skipHoldTime = 0.7f;
        [SerializeField] private float _skipHintDelay = 1.0f;

        private ICinematic _cinematic;
        private float _skipHold;
        private float _barsShown;
        private int _cueIndex = -1;
        private bool _finished;
        private Vector2 _subtitleRest;

        public event Action OnPresentationFinished;

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
            _cinematic.OnFinished += HandleFinished;
            ApplyBars(_barsShown);
        }

        private void HandleFinished()
        {
            _cinematic.OnFinished -= HandleFinished;
            _finished = true;
            OnPresentationFinished?.Invoke();
        }

        private void Update()
        {
            if (_cinematic == null || _finished)
            {
                return;
            }

            float t = (float)_cinematic.Director.time;
            float duration = (float)_cinematic.Director.duration;
            CinematicSettings settings = _cinematic.Settings;

            float barsIn = settings.BarsInTime <= 0f ? 1f : Smooth(t / settings.BarsInTime);
            float barsOut = settings.BarsOutTime <= 0f ? 1f : Smooth((duration - t) / settings.BarsOutTime);
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
            float h = height * EaseOutBack(k);
            _topBar.sizeDelta = new Vector2(0f, h);
            _bottomBar.sizeDelta = new Vector2(0f, h);
            Color c = _vignette.color;
            c.a = _vignetteAlpha * k;
            _vignette.color = c;
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
                _titleGroup.alpha = Mathf.MoveTowards(_titleGroup.alpha, 0f, Time.deltaTime * 3f);
                return;
            }

            _titleGroup.alpha = Mathf.Min(Smooth(local / 0.25f), Smooth((settings.TitleDuration - local) / 0.5f));
            int chars = Mathf.Min(settings.TitleLine.Length, Mathf.FloorToInt(local * 28f));
            _titleLine.text = settings.TitleLine.Substring(0, chars);
            float subStart = settings.TitleLine.Length / 28f + 0.15f;
            int subChars = string.IsNullOrEmpty(settings.TitleSubLine) ? 0 : Mathf.Clamp(Mathf.FloorToInt((local - subStart) * 34f), 0, settings.TitleSubLine.Length);
            _titleSubLine.text = subChars > 0 ? settings.TitleSubLine.Substring(0, subChars) : string.Empty;
            _titleRule.localScale = new Vector3(Smooth(local / 0.6f), 1f, 1f);
        }

        private void UpdateSubtitles(float t)
        {
            SubtitleCue[] cues = _cinematic.Subtitles;
            int active = -1;
            for (int i = 0; i < cues.Length; i++)
            {
                if (t >= cues[i].Time && t <= cues[i].Time + cues[i].Duration)
                {
                    active = i;
                }
            }

            if (active != _cueIndex && active >= 0)
            {
                SubtitleCue cue = cues[active];
                _speaker.text = cue.Speaker;
                _speaker.color = cue.SpeakerColor.a > 0f ? cue.SpeakerColor : new Color(1f, 0.76f, 0.29f);
                _line.text = cue.Text;
                Game.Scenes.DialogueDirector.Instance?.PlayVoice(cue.Text);
            }

            _cueIndex = active;
            float local = active >= 0 ? t - cues[active].Time : 0f;
            float target = active >= 0 ? Mathf.Min(Smooth(local / 0.2f), Smooth((cues[active].Duration - local) / 0.3f)) : 0f;
            _subtitleGroup.alpha = active >= 0 ? target : Mathf.MoveTowards(_subtitleGroup.alpha, 0f, Time.deltaTime * 4f);
            _subtitleRect.anchoredPosition = _subtitleRest + Vector2.down * (1f - _subtitleGroup.alpha) * 24f;
        }

        private void UpdateSkip(float t)
        {
            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;
            bool holding = kb.spaceKey.isPressed || kb.enterKey.isPressed || kb.escapeKey.isPressed || mouse.leftButton.isPressed;
            bool anyPress = kb.anyKey.wasPressedThisFrame || mouse.leftButton.wasPressedThisFrame;

            _skipHold = holding ? _skipHold + Time.unscaledDeltaTime : Mathf.MoveTowards(_skipHold, 0f, Time.unscaledDeltaTime * 2f);
            _skipRing.fillAmount = Mathf.Clamp01(_skipHold / _skipHoldTime);

            float hintTarget = t > _skipHintDelay || anyPress || holding ? 1f : 0f;
            _skipGroup.alpha = Mathf.MoveTowards(_skipGroup.alpha, hintTarget * _barsShown, Time.unscaledDeltaTime * 3f);

            if (_skipHold >= _skipHoldTime)
            {
                _skipHold = 0f;
                _cinematic.Skip();
            }
        }

        private static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        private static float EaseOutBack(float x)
        {
            if (x <= 0f || x >= 1f) return Mathf.Clamp01(x);
            const float c1 = 0.9f, c3 = c1 + 1f;
            float p = x - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }
    }
}
