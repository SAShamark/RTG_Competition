using Gameplay.Dialogue;
using UI.Widgets;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Screens.Variants.Gameplay.Widgets
{
    public sealed class RescueNoticeWidget : MonoBehaviour
    {
        [SerializeField] private TextLibrary _texts;
        [SerializeField] private Text _value;
        [SerializeField] private UiPulse _pulse;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private float _hold = 2.4f;
        [SerializeField] private float _fade = 0.35f;
        [SerializeField] private Color _plusColor = new(0.55f, 1f, 0.45f, 1f);

        [SerializeField] private Text _plus;
        [SerializeField] private float _plusOffsetX = 40f;
        [SerializeField] private float _plusOffsetY = 10f;
        [SerializeField] private float _plusTime = 0.9f;
        [SerializeField] private float _plusRise = 60f;
        [SerializeField] private float _countSwapAt = 0.35f;
        private Coroutine _routine;

        public void Show(int value, int total)
        {
            gameObject.SetActive(true);
            if (_routine != null)
            {
                StopCoroutine(_routine);
            }

            _routine = StartCoroutine(Play(value, total));
        }

        public void Hide()
        {
            _group.alpha = 0f;
            gameObject.SetActive(false);
        }

        private IEnumerator Play(int value, int total)
        {
            _value.text = _texts.Format(TextId.RescuedCount, value - 1, total);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / _fade)
            {
                _group.alpha = t;
                yield return null;
            }

            _group.alpha = 1f;
            var plus = (RectTransform)_plus.transform;
            Vector2 start = new(_value.rectTransform.anchoredPosition.x + _plusOffsetX, _plusOffsetY);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / _plusTime)
            {
                plus.anchoredPosition = start + Vector2.up * (_plusRise * t);
                _plus.color = new Color(_plusColor.r, _plusColor.g, _plusColor.b, 1f - t * t);
                if (t > _countSwapAt && _value.text != _texts.Format(TextId.RescuedCount, value, total))
                {
                    _value.text = _texts.Format(TextId.RescuedCount, value, total);
                    _pulse.Play();
                }

                yield return null;
            }

            _plus.color = Color.clear;
            yield return new WaitForSecondsRealtime(_hold);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / _fade)
            {
                _group.alpha = 1f - t;
                yield return null;
            }

            Hide();
            _routine = null;
        }
    }
}
