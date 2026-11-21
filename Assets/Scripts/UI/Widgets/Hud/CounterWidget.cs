using UnityEngine;
using UnityEngine.UI;

namespace UI.Widgets.Hud
{
    public class CounterWidget : MonoBehaviour
    {
        [SerializeField] private Text _text;
        [SerializeField] private UiPulse _pulse;

        public void Set(int value, int total)
        {
            string next = $"{value}/{total}";
            if (_text.text != next)
            {
                _text.text = next;
                _pulse.Play();
            }
        }
    }
}
