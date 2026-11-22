using Gameplay.Dialogue;
using Services.Audio;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UI.Popups.Variants.Puzzles
{
    public sealed class SafePuzzlePopup : BasePopup
    {
        private const int RingCount = 3;

        private const int Steps = 12;
        private const float StepAngle = 360f / Steps;
        [SerializeField] private TextLibrary _texts;
        [SerializeField] private Color _gold = new(1f, 0.68f, 0.18f, 1f);
        [SerializeField] private float _turnTime = 0.15f;

        [SerializeField] private float[] _ringInner = { 0.54f, 0.38f, 0f };
        [SerializeField] private float[] _ringOuter = { 1f, 0.63f, 0.38f };

        private static readonly int[] StartStep = { 4, 9, 7 };

        [Header("Layout")]
        [SerializeField] private RectTransform _dial;
        [SerializeField] private RectTransform[] _rings = new RectTransform[3];
        private readonly int[] _step = new int[RingCount];
        private readonly float[] _angle = new float[RingCount];
        [SerializeField] private Text _status;
        [SerializeField] private Image _glow;
        [SerializeField] private RectTransform _hintRow;
        [SerializeField] private float _dialRadius = 235f;
        [SerializeField] private float _glowSpeed = 1.6f;
        [SerializeField] private float _glowMax = 0.6f;
        [SerializeField] private float _solvedDelay = 1.6f;
        private Action _onSolved;
        private Action _onClosed;
        private bool _isSolved;
        private float _solvedTime = -1f;

        private int _dragRing = -1;
        private int _dragStartStep;
        private float _dragDelta;
        private float _dragLastAngle;

        private void Awake()
        {
            for (int i = 0; i < _rings.Length; i++)
            {
                _step[i] = StartStep[i];
                _angle[i] = -_step[i] * StepAngle;
            }
        }

        public void Open(Action onSolved, Action onClosed)
        {
            _onSolved = onSolved;
            _onClosed = onClosed;
            _dragRing = -1;
            _canvas.gameObject.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _status.text = string.Empty;
            _hintRow.gameObject.SetActive(true);
        }

        private void Close(bool solved)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Action callback = solved ? _onSolved : _onClosed;
            _onSolved = _onClosed = null;
            callback?.Invoke();
            CloseTrigger();
        }

        private void Update()
        {
            for (int i = 0; i < RingCount; i++)
            {
                if (i == _dragRing)
                {
                    _angle[i] = -(_dragStartStep * StepAngle + _dragDelta);
                }
                else
                {
                    _angle[i] = Mathf.MoveTowardsAngle(_angle[i], -_step[i] * StepAngle, StepAngle / _turnTime * Time.unscaledDeltaTime);
                }

                _rings[i].localRotation = Quaternion.Euler(0f, 0f, _angle[i]);
            }

            if (_isSolved)
            {
                _solvedTime += Time.unscaledDeltaTime;
                _glow.color = new Color(_gold.r, _gold.g, _gold.b, Mathf.PingPong(_solvedTime * _glowSpeed, _glowMax));
                if (_solvedTime > _solvedDelay)
                {
                    Close(true);
                }

                return;
            }

            Mouse mouse = Mouse.current;
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                _dragRing = -1;
                Close(false);
                return;
            }

            Vector2 pointer = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame)
            {
                _dragRing = RingAt(pointer);
                if (_dragRing >= 0)
                {
                    _dragStartStep = _step[_dragRing];
                    _dragDelta = 0f;
                    _dragLastAngle = PointerAngle(pointer);
                }
            }

            if (_dragRing < 0)
            {
                return;
            }

            float pointerAngle = PointerAngle(pointer);
            _dragDelta += Mathf.DeltaAngle(_dragLastAngle, pointerAngle);
            _dragLastAngle = pointerAngle;
            int step = ((_dragStartStep + Mathf.RoundToInt(_dragDelta / StepAngle)) % Steps + Steps) % Steps;
            _step[_dragRing] = step;

            if (mouse.leftButton.isPressed)
            {
                return;
            }

            _dragRing = -1;
            if (_step[0] == _step[1] && _step[1] == _step[2])
            {
                // the drawing is whole (in any rotation): it turns upright as one piece
                _step[0] = _step[1] = _step[2] = 0;
                _isSolved = true;
                _solvedTime = 0f;
                AudioService.Instance.Play(SoundType.SafeSolved);
                _hintRow.gameObject.SetActive(false);

                _status.text = _texts[TextId.StatusSafeSolved];
            }
        }

        private float PointerAngle(Vector2 screenPoint)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_dial, screenPoint, null, out Vector2 local);
            return Mathf.Atan2(local.x, local.y) * Mathf.Rad2Deg;
        }

        private int RingAt(Vector2 screenPoint)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_dial, screenPoint, null, out Vector2 local);
            float r = local.magnitude / _dialRadius;
            for (int i = 2; i >= 0; i--)
            {
                if (r >= _ringInner[i] && r <= _ringOuter[i])
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
