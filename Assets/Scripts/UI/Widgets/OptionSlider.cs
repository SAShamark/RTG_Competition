using System;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Widgets
{
    public sealed class OptionSlider : MonoBehaviour
    {
        [SerializeField] private Slider _slider;
        [SerializeField] private Text _value;
        [SerializeField] private string[] _options;

        public event Action<int> Changed;

        private void Awake() => _slider.onValueChanged.AddListener(HandleValueChanged);

        private void OnDestroy() => _slider.onValueChanged.RemoveListener(HandleValueChanged);

        public void Show(int index)
        {
            _slider.wholeNumbers = true;
            _slider.minValue = 0f;
            _slider.maxValue = _options.Length - 1;
            _slider.SetValueWithoutNotify(index);
            _value.text = _options[index];
        }

        private void HandleValueChanged(float value)
        {
            int index = Mathf.RoundToInt(value);
            _value.text = _options[index];
            Changed?.Invoke(index);
        }
    }
}
