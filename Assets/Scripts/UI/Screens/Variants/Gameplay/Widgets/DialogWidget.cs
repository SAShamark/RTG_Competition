using Gameplay.Dialogue;
using Services.Audio;
using UI.Widgets;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Screens.Variants.Gameplay.Widgets
{
    public sealed class DialogWidget : MonoBehaviour
    {
        private const float NoVoice = -1f;

        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Text _text;
        [SerializeField] private UiPulse _pulse;
        [SerializeField] private float _charsPerSecond = 45f;
        [SerializeField] private GameObject[] _portraitParts;
        [SerializeField] private string _otherSpeakerColor = "#b4441c";
        [SerializeField] private string _partnerName = "Напарник";
        [SerializeField] private Vector3 _anchorOffset = new(0f, 0.32f, 0f);
        [SerializeField] private Vector2 _anchorScreenOffset = new(40f, 20f);
        [SerializeField] private float _fadeSpeed = 5f;
        [SerializeField] private float _fadeOutSpeed = 1.5f;
        [SerializeField] private float _textPadding = 6f;

        private struct Line
        {
            public DialogPhrase Phrase;
            public Transform Anchor;
        }

        private RectTransform _rect;
        private Vector2 _restPosition;
        private Transform _anchor;
        private Camera _camera;

        private readonly Queue<Line> _queue = new();
        private string _line;
        private string _prefix = string.Empty;
        private float _typed;
        private float _hold;
        private float _visible;

        private void Awake()
        {
            _group.alpha = 0f;
            _rect = (RectTransform)transform;
            _restPosition = _rect.anchoredPosition;
            _baseHeight = _rect.sizeDelta.y;
            _baseTextHeight = _text.rectTransform.rect.height;
        }

        public bool IsBusy => _line != null || _queue.Count > 0;

        public void Init(Camera camera) => _camera = camera;

        public void Say(DialogPhrase phrase, Transform anchor)
        {
            _queue.Enqueue(new Line { Phrase = phrase, Anchor = anchor });
        }

        public void Clear()
        {
            _queue.Clear();
            _line = null;
            _visible = 0f;
            _group.alpha = 0f;
        }

        private void Update()
        {
            if (_line == null && _queue.Count > 0)
            {
                Line next = _queue.Dequeue();
                DialogPhrase phrase = next.Phrase;
                _line = phrase.Text;
                _hold = phrase.Hold;
                _voiceLeft = phrase.Clip != null ? phrase.Clip.length : NoVoice;
                _typed = 0f;
                bool other = phrase.Character != CharactersType.Partner || next.Anchor != null;
                _prefix = $"<b><color={_otherSpeakerColor}>{(other ? phrase.Speaker : _partnerName)}:</color></b> ";
                foreach (GameObject part in _portraitParts)
                {
                    part.SetActive(!other);
                }

                FitToText(_prefix + _line);
                if (phrase.Clip != null)
                {
                    AudioService.Instance.PlayVoice(phrase.Clip);
                }

                _anchor = next.Anchor;
                if (_anchor == null)
                {
                    _rect.anchoredPosition = _restPosition;
                }

                _pulse.Play();
            }

            float targetAlpha = _line != null ? 1f : 0f;
            float fadeSpeed = _line != null ? _fadeSpeed : _fadeOutSpeed;
            _visible = Mathf.MoveTowards(_visible, targetAlpha, Time.unscaledDeltaTime * fadeSpeed);
            _group.alpha = FollowAnchor() ? _visible : 0f;

            if (_line == null)
            {
                return;
            }

            _typed += Time.unscaledDeltaTime * _charsPerSecond;
            int count = Mathf.Min(_line.Length, Mathf.FloorToInt(_typed));
            _text.text = _prefix + _line.Substring(0, count);
            if (_voiceLeft != NoVoice)
            {
                _voiceLeft -= Time.unscaledDeltaTime;
                if (_voiceLeft <= 0f)
                {
                    _line = null;
                }

                return;
            }

            if (count >= _line.Length)
            {
                _hold -= Time.unscaledDeltaTime;
                if (_hold <= 0f)
                {
                    _line = null;
                }
            }
        }

        private float _voiceLeft;
        private float _baseHeight;
        private float _baseTextHeight;

        private void FitToText(string fullText)
        {
            if (_baseTextHeight <= 0f)
            {
                return;
            }

            _text.text = fullText;
            float extra = Mathf.Max(0f, _text.preferredHeight - _baseTextHeight + _textPadding);
            _rect.sizeDelta = new Vector2(_rect.sizeDelta.x, _baseHeight + extra);
            _text.text = string.Empty;
        }

        // A line spoken by someone in the world stays above their head, pinned to the screen edge, and is hidden only while they are behind the camera.
        private bool FollowAnchor()
        {
            if (_anchor == null)
            {
                return true;
            }

            Vector3 screen = _camera.WorldToScreenPoint(_anchor.position + _anchorOffset);
            if (screen.z <= 0f)
            {
                return false;
            }

            var parent = (RectTransform)_rect.parent;
            Canvas canvas = GetComponentInParent<Canvas>();
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, (Vector2)screen + _anchorScreenOffset, uiCamera, out Vector2 local))
            {
                return false;
            }

            Rect parentRect = parent.rect;
            Vector2 anchorRef = new(Mathf.Lerp(parentRect.xMin, parentRect.xMax, (_rect.anchorMin.x + _rect.anchorMax.x) * 0.5f),
                Mathf.Lerp(parentRect.yMin, parentRect.yMax, (_rect.anchorMin.y + _rect.anchorMax.y) * 0.5f));
            Vector2 size = _rect.rect.size;
            Vector2 pivot = _rect.pivot;
            local.x = Mathf.Clamp(local.x, parentRect.xMin + size.x * pivot.x, parentRect.xMax - size.x * (1f - pivot.x));
            local.y = Mathf.Clamp(local.y, parentRect.yMin + size.y * pivot.y, parentRect.yMax - size.y * (1f - pivot.y));
            _rect.anchoredPosition = local - anchorRef;
            return true;
        }
    }
}
