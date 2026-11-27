using Services.Audio;
using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    public sealed class SafeKeypad : MonoBehaviour
    {
        public const string ClearKey = "C";
        public const string EnterKey = "OK";

        [SerializeField] private WallSafe _safe;
        [SerializeField] private TextMesh _display;
        [SerializeField] private string _emptySlot = "_";
        [SerializeField] private string _slotSeparator = " ";
        [SerializeField] private string _okText = "ВІДКРИТО";
        [SerializeField] private string _errorText = "ПОМИЛКА";
        [SerializeField] private Color _typingColor = new(0.45f, 1f, 0.55f);
        [SerializeField] private Color _okColor = new(0.45f, 1f, 0.55f);
        [SerializeField] private Color _errorColor = new(1f, 0.3f, 0.25f);
        [SerializeField, Min(0.1f)] private float _errorHold = 1.2f;

        private string _entered = string.Empty;
        private float _errorTimer;

        public void Press(string key)
        {
            if (_safe.IsUnlocked)
            {
                return;
            }

            _errorTimer = 0f;
            if (key == ClearKey)
            {
                _entered = string.Empty;
            }
            else if (key == EnterKey)
            {
                Check();
                return;
            }
            else if (_entered.Length < _safe.Code.Length)
            {
                _entered += key;
                if (_entered.Length == _safe.Code.Length)
                {
                    Check();
                    return;
                }
            }

            Refresh();
        }

        private void Awake() => Refresh();

        private void Update()
        {
            if (_errorTimer <= 0f)
            {
                return;
            }

            _errorTimer -= Time.deltaTime;
            if (_errorTimer <= 0f)
            {
                Refresh();
            }
        }

        private void Check()
        {
            if (_entered == _safe.Code)
            {
                _safe.Unlock();
                Show(_okText, _okColor);
                return;
            }

            _entered = string.Empty;
            _errorTimer = _errorHold;
            Show(_errorText, _errorColor);
            AudioService.Instance.PlayAt(SoundType.DoorLocked, transform.position);
        }

        private void Refresh()
        {
            var slots = new string[_safe.Code.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = i < _entered.Length ? _entered[i].ToString() : _emptySlot;
            }

            Show(string.Join(_slotSeparator, slots), _typingColor);
        }

        private void Show(string text, Color color)
        {
            _display.text = text;
            _display.color = color;
        }
    }
}
