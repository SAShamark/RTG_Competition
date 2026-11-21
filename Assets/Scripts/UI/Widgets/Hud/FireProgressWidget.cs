using UnityEngine;
using UnityEngine.UI;

namespace UI.Widgets.Hud
{
    public class FireProgressWidget : MonoBehaviour
    {
        [SerializeField] private Image _fill;
        [SerializeField] private Text _percent;
        [SerializeField] private Gradient _colors;
        [SerializeField] private float _smoothing = 4f;

        private float _target;
        private float _shown;

        public void SetProgress(float value)
        {
            _target = Mathf.Clamp01(value);
        }

        private void Update()
        {
            _shown = Mathf.MoveTowards(_shown, _target, Time.deltaTime * Mathf.Max(0.05f, Mathf.Abs(_target - _shown) * _smoothing));
            _fill.fillAmount = _shown;
            if (_colors != null)
            {
                _fill.color = _colors.Evaluate(_shown);
            }

            _percent.text = Mathf.RoundToInt(_shown * 100f) + "%";
        }
    }
}
