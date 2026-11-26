using System;
using Gameplay.Dialogue;
using Services.Audio;
using Services;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace UI.Popups.Variants.Puzzles
{
    public abstract class PuzzlePopup : BasePopup
    {
        private const float Idle = -1f;

        [SerializeField] private TextLibrary _texts;
        [SerializeField] private RectTransform _hintRow;
        [SerializeField] private Text _status;
        [FormerlySerializedAs("_openDelay")]
        [SerializeField] private float _solvedDelay = 1f;

        private Action _onSolved;
        private Action _onClosed;
        private float _solvedTime = Idle;
        private bool _isFinished;

        protected abstract bool ShowsCursor { get; }

        public void Open(Action onSolved, Action onClosed)
        {
            _onSolved = onSolved;
            _onClosed = onClosed;
            _solvedTime = Idle;
            _isFinished = false;
            _status.text = string.Empty;
            _hintRow.gameObject.SetActive(true);
            SetCursorVisible(ShowsCursor);
            OnOpened();
        }

        public override void CloseTrigger() => Finish(false);

        private void Update()
        {
            if (_isFinished)
            {
                return;
            }

            float deltaTime = Time.unscaledDeltaTime;
            if (_solvedTime >= 0f)
            {
                _solvedTime += deltaTime;
                OnSolvedTick(_solvedTime, deltaTime);
                if (_solvedTime > _solvedDelay)
                {
                    Finish(true);
                }

                return;
            }

            if (GameInput.CancelPressed)
            {
                Finish(false);
                return;
            }

            Tick(deltaTime);
        }

        protected abstract void OnOpened();

        protected abstract void Tick(float deltaTime);

        protected virtual void OnSolvedTick(float solvedTime, float deltaTime)
        {
        }

        protected void Solve(TextId status, SoundType sound)
        {
            _solvedTime = 0f;
            _status.text = _texts[status];
            _hintRow.gameObject.SetActive(false);
            AudioService.Instance.Play(sound);
        }

        private void Finish(bool solved)
        {
            if (_isFinished)
            {
                return;
            }

            _isFinished = true;
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
