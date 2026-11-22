using Gameplay.Dialogue;
using Services.Audio;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UI.Popups.Variants.Puzzles
{
    public sealed class LockpickPopup : BasePopup
    {
        private const float FullTurn = 90f;
        private const float PickRange = 85f;
        [SerializeField] private TextLibrary _texts;
        [SerializeField] private float _sensitivity = 0.25f;
        [SerializeField] private float _tolerance = 9f;
        [SerializeField] private float _feelRange = 55f;
        [SerializeField] private float _turnSpeed = 140f;


        [Header("Layout")]
        [SerializeField] private RectTransform _cylinder;
        [SerializeField] private RectTransform _pick;
        [SerializeField] private RectTransform _hintRow;
        [SerializeField] private Text _status;
        [SerializeField] private float _sweetSpotRange = 70f;
        [SerializeField] private float _openDelay = 0.8f;
        [SerializeField] private float _stuckShakeFrequency = 70f;
        [SerializeField] private float _stuckShakeAngle = 2.5f;
        [SerializeField] private float _returnSpeedFactor = 1.5f;
        private Action _onOpened;
        private Action _onClosed;
        private float _pickAngle;
        private float _sweetSpot;
        private float _turn;
        private float _openTime = -1f;

        public void Open(Action onOpened, Action onClosed)
        {
            _onOpened = onOpened;
            _onClosed = onClosed;
            _sweetSpot = UnityEngine.Random.Range(-_sweetSpotRange, _sweetSpotRange);
            _pickAngle = 0f;
            _turn = 0f;
            _openTime = -1f;
            _canvas.gameObject.SetActive(true);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            _status.text = string.Empty;
            _hintRow.gameObject.SetActive(true);
        }

        private void Close(bool opened)
        {
            Action callback = opened ? _onOpened : _onClosed;
            _onOpened = _onClosed = null;
            callback?.Invoke();
            CloseTrigger();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_openTime >= 0f)
            {
                _openTime += dt;
                if (_openTime > _openDelay)
                {
                    Close(true);
                }

                return;
            }

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close(false);
                return;
            }

            Mouse mouse = Mouse.current;
            bool turning = mouse.leftButton.isPressed;
            if (!turning)
            {
                _pickAngle = Mathf.Clamp(_pickAngle + mouse.delta.ReadValue().x * _sensitivity, -PickRange, PickRange);
            }

            float miss = Mathf.Abs(_pickAngle - _sweetSpot);
            float allowed = miss <= _tolerance ? FullTurn : FullTurn * Mathf.Clamp01(1f - (miss - _tolerance) / _feelRange);
            float shake = 0f;
            if (turning)
            {
                _turn = Mathf.MoveTowards(_turn, allowed, _turnSpeed * dt);
                if (_turn >= allowed - 0.5f && allowed < FullTurn)
                {
                    shake = Mathf.Sin(Time.unscaledTime * _stuckShakeFrequency) * _stuckShakeAngle;   // stuck: the pick trembles
                }
            }
            else
            {
                _turn = Mathf.MoveTowards(_turn, 0f, _turnSpeed * _returnSpeedFactor * dt);
            }

            _cylinder.localRotation = Quaternion.Euler(0f, 0f, -_turn);
            _pick.localRotation = Quaternion.Euler(0f, 0f, -_pickAngle + shake);

            if (_turn >= FullTurn - 0.5f)
            {
                _openTime = 0f;
                _status.text = _texts[TextId.StatusLockOpened];
                _hintRow.gameObject.SetActive(false);
                AudioService.Instance.Play(SoundType.LockpickOpen);
            }
        }
    }
}
