using UnityEngine;
using UnityEngine.UI;

namespace UI.Widgets.Hud
{
    public class DamageWidget : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Text _text;
        [SerializeField] private UiPulse _pulse;

        private int _total;
        private float _shown;

        private void Awake()
        {
            _root.SetActive(false);
        }

        public void Add(int amount)
        {
            _total += amount;
            _root.SetActive(true);
            _pulse.Play();
        }

        private void Update()
        {
            if (!_root.activeSelf)
            {
                return;
            }

            _shown = Mathf.MoveTowards(_shown, _total, Mathf.Max(60f, Mathf.Abs(_total - _shown) * 3f) * Time.deltaTime);
            _text.text = "Знайдено: " + Mathf.RoundToInt(_shown).ToString("N0").Replace(',', ' ') + " ₴";
        }
    }
}
