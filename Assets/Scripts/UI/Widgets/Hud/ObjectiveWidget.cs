using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Widgets.Hud
{
    public class ObjectiveWidget : MonoBehaviour
    {
        [SerializeField] private Text _stepText;
        [SerializeField] private Text _text;
        [SerializeField] private GameObject _checkMark;
        [SerializeField] private UiPulse _pulse;
        [SerializeField] private CanvasGroup _group;

        private Coroutine _routine;

        public void SetObjective(int step, int total, string text, bool animate = true)
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
            }

            if (!animate || !isActiveAndEnabled)
            {
                Apply(step, total, text);
                return;
            }

            _routine = StartCoroutine(Swap(step, total, text));
        }

        private void Apply(int step, int total, string text)
        {
            _stepText.text = $"Завдання {step}/{total}";
            _text.text = text;
            _checkMark.SetActive(false);
        }

        private IEnumerator Swap(int step, int total, string text)
        {
            _checkMark.SetActive(true);
            _pulse.Play();
            yield return new WaitForSecondsRealtime(0.7f);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.2f)
            {
                _group.alpha = 1f - t;
                yield return null;
            }

            Apply(step, total, text);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.25f)
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
