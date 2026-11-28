using Gameplay.Entities.Characters.Player;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Screens.Variants.Gameplay.Widgets
{
    public sealed class FreezeWidget : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Text _title;
        [SerializeField] private Image _timer;
        [SerializeField, Min(0f)] private float _fadeSpeed = 5f;

        private FreezeLogic _freeze;

        public void Init(FreezeLogic freeze, string title)
        {
            _freeze = freeze;
            _title.text = title;
            _group.alpha = 0f;
            enabled = true;
        }

        private void Update()
        {
            _group.alpha = Mathf.MoveTowards(_group.alpha, _freeze.IsFrozen ? 1f : 0f, _fadeSpeed * Time.deltaTime);
            if (_freeze.IsFrozen)
            {
                _timer.fillAmount = _freeze.Remaining / _freeze.Duration;
            }
        }
    }
}
