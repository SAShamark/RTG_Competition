using UI.Widgets;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Screens.Variants.Gameplay.Widgets
{
    public sealed class ObjectiveWidget : MonoBehaviour
    {
        [SerializeField] private Text _text;
        [SerializeField] private GameObject _checkMark;
        [SerializeField] private UiPulse _pulse;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private float _checkHold = 0.7f;
        [SerializeField] private float _fadeOutTime = 0.2f;
        [SerializeField] private float _fadeInTime = 0.25f;

        private Coroutine _routine;

        public void SetObjective(string text, bool animate = true)
        {
            if (_text.text == text)
            {
                return;
            }

            if (_routine != null)
            {
                StopCoroutine(_routine);
            }

            if (!animate || !isActiveAndEnabled)
            {
                Apply(text);
                return;
            }

            _routine = StartCoroutine(Swap(text));
        }

        public void UpdateText(string text)
        {
            _text.text = text;
            _pulse.Play();
        }

        private void Apply(string text)
        {
            _text.text = text;
            _checkMark.SetActive(false);
        }

        private IEnumerator Swap(string text)
        {
            _checkMark.SetActive(true);
            _pulse.Play();
            yield return new WaitForSecondsRealtime(_checkHold);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / _fadeOutTime)
            {
                _group.alpha = 1f - t;
                yield return null;
            }

            Apply(text);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / _fadeInTime)
            {
                _group.alpha = t;
                yield return null;
            }

            _group.alpha = 1f;
            _pulse.Play();
            _routine = null;
        }
    }
}
