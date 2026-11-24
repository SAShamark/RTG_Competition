using Gameplay.Dialogue;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Popups.Variants.Puzzles
{
    public abstract class PuzzlePopup : BasePopup
    {
        [SerializeField] private TextLibrary _texts;
        [SerializeField] private RectTransform _hintRow;
        [SerializeField] private Text _status;

        private Action _onSolved;
        private Action _onClosed;

        protected abstract bool ShowsCursor { get; }

        public void Open(Action onSolved, Action onClosed)
        {
            _onSolved = onSolved;
            _onClosed = onClosed;
            _canvas.gameObject.SetActive(true);
            SetCursorVisible(ShowsCursor);
            _status.text = string.Empty;
            _hintRow.gameObject.SetActive(true);
            OnOpened();
        }

        public override void CloseTrigger() => Finish(false);

        protected abstract void OnOpened();

        protected void ShowSolved(TextId status)
        {
            _status.text = _texts[status];
            _hintRow.gameObject.SetActive(false);
        }

        protected void Finish(bool solved)
        {
            SetCursorVisible(false);
            Action callback = solved ? _onSolved : _onClosed;
            _onSolved = _onClosed = null;
            callback?.Invoke();
            base.CloseTrigger();
        }

        private static void SetCursorVisible(bool isVisible)
        {
            Cursor.lockState = isVisible ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = isVisible;
        }
    }
}
