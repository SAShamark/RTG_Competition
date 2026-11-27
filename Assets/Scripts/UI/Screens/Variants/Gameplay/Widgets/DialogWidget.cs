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
        [SerializeField] private float _fadeSpeed = 5f;
        [SerializeField] private float _fadeOutSpeed = 1.5f;
        [SerializeField] private float _textPadding = 6f;

        private RectTransform _rect;

        private readonly Queue<DialogPhrase> _queue = new();
        private string _line;
        private string _prefix = string.Empty;
        private float _typed;
        private float _hold;
        private float _visible;

        private void Awake()
        {
            _group.alpha = 0f;
            _rect = (RectTransform)transform;
            _baseHeight = _rect.sizeDelta.y;
            _baseTextHeight = _text.rectTransform.rect.height;
        }

        public bool IsBusy => _line != null || _queue.Count > 0;

        public void Say(DialogPhrase phrase) => _queue.Enqueue(phrase);

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
                DialogPhrase phrase = _queue.Dequeue();
                _line = phrase.Text;
                _hold = phrase.Hold;
                _voiceLeft = phrase.Clip != null ? phrase.Clip.length : NoVoice;
                _typed = 0f;
                bool other = phrase.Character != CharactersType.Partner;
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

                _pulse.Play();
            }

            float targetAlpha = _line != null ? 1f : 0f;
            float fadeSpeed = _line != null ? _fadeSpeed : _fadeOutSpeed;
            _visible = Mathf.MoveTowards(_visible, targetAlpha, Time.unscaledDeltaTime * fadeSpeed);
            _group.alpha = _visible;

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
    }
}
