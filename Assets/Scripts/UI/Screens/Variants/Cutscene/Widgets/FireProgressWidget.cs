using UnityEngine;
using UnityEngine.UI;

namespace UI.Screens.Variants.Cutscene.Widgets
{
    public sealed class FireProgressWidget : MonoBehaviour
    {
        [SerializeField] private Image _fill;
        [SerializeField] private Text _percent;
        [SerializeField] private Gradient _colors;
        [SerializeField] private float _smoothing = 4f;
        [SerializeField] private float _minSpeed = 0.05f;

        private float _target;
        private float _shown;

        public void SetProgress(float value)
        {
            _target = Mathf.Clamp01(value);
        }

        public void Snap(float value)
        {
            SetProgress(value);
            _shown = _target;
        }

        private void Update()
        {
            _shown = Mathf.MoveTowards(_shown, _target, Time.deltaTime * Mathf.Max(_minSpeed, Mathf.Abs(_target - _shown) * _smoothing));
            _fill.fillAmount = _shown;
            _fill.color = _colors.Evaluate(_shown);

            _percent.text = Mathf.RoundToInt(_shown * 100f) + "%";
        }
    }
}
