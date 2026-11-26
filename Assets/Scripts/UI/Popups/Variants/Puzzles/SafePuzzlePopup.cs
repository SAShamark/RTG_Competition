using Gameplay.Dialogue;
using Services.Audio;
using Services;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Popups.Variants.Puzzles
{
    public sealed class SafePuzzlePopup : PuzzlePopup
    {
        private const int RingCount = 3;
        private const int Steps = 12;
        private const float StepAngle = 360f / Steps;
        private const int NoRing = -1;

        private static readonly int[] StartStep = { 4, 9, 7 };

        [Header("Feel")]
        [SerializeField] private float _turnTime = 0.15f;
        [SerializeField] private float[] _ringInner = { 0.54f, 0.38f, 0f };
        [SerializeField] private float[] _ringOuter = { 1f, 0.63f, 0.38f };

        [Header("Solved Glow")]
        [SerializeField] private Color _gold = new(1f, 0.68f, 0.18f, 1f);
        [SerializeField] private float _glowSpeed = 1.6f;
        [SerializeField] private float _glowMax = 0.6f;

        [Header("Layout")]
        [SerializeField] private RectTransform _dial;
        [SerializeField] private RectTransform[] _rings = new RectTransform[RingCount];
        [SerializeField] private Image _glow;
        [SerializeField] private float _dialRadius = 235f;

        private readonly int[] _step = new int[RingCount];
        private readonly float[] _angle = new float[RingCount];

        private int _dragRing = NoRing;
        private int _dragStartStep;
        private float _dragDelta;
        private float _dragLastAngle;

        protected override bool ShowsCursor => true;

        private void Awake()
        {
            for (int i = 0; i < RingCount; i++)
            {
                _step[i] = StartStep[i];
                _angle[i] = -_step[i] * StepAngle;
            }
        }

        protected override void OnOpened() => _dragRing = NoRing;

        protected override void Tick(float deltaTime)
        {
            UpdateRings(deltaTime);

            Vector2 pointer = GameInput.PointerPosition;
            if (GameInput.PrimaryPressed)
            {
                BeginDrag(pointer);
            }

            if (_dragRing == NoRing)
            {
                return;
            }

            Drag(pointer);
            if (!GameInput.IsPrimaryHeld)
            {
                EndDrag();
            }
        }

        protected override void OnSolvedTick(float solvedTime, float deltaTime)
        {
            UpdateRings(deltaTime);
            Color glow = _gold;
            glow.a = Mathf.PingPong(solvedTime * _glowSpeed, _glowMax);
            _glow.color = glow;
        }

        private void UpdateRings(float deltaTime)
        {
            for (int i = 0; i < RingCount; i++)
            {
                _angle[i] = i == _dragRing
                    ? -(_dragStartStep * StepAngle + _dragDelta)
                    : Mathf.MoveTowardsAngle(_angle[i], -_step[i] * StepAngle, StepAngle / _turnTime * deltaTime);
                _rings[i].localRotation = Quaternion.Euler(0f, 0f, _angle[i]);
            }
        }

        private void BeginDrag(Vector2 pointer)
        {
            _dragRing = RingAt(pointer);
            if (_dragRing == NoRing)
            {
                return;
            }

            _dragStartStep = _step[_dragRing];
            _dragDelta = 0f;
            _dragLastAngle = PointerAngle(pointer);
        }

        // step = (start + round(delta / stepAngle)) mod Steps, kept non-negative
        private void Drag(Vector2 pointer)
        {
            float pointerAngle = PointerAngle(pointer);
            _dragDelta += Mathf.DeltaAngle(_dragLastAngle, pointerAngle);
            _dragLastAngle = pointerAngle;
            _step[_dragRing] = ((_dragStartStep + Mathf.RoundToInt(_dragDelta / StepAngle)) % Steps + Steps) % Steps;
        }

        private void EndDrag()
        {
            _dragRing = NoRing;
            if (!IsAligned())
            {
                return;
            }

            for (int i = 0; i < RingCount; i++)
            {
                _step[i] = 0;
            }

            Solve(TextId.StatusSafeSolved, SoundType.SafeSolved);
        }

        private bool IsAligned()
        {
            for (int i = 1; i < RingCount; i++)
            {
                if (_step[i] != _step[0])
                {
                    return false;
                }
            }

            return true;
        }

        private float PointerAngle(Vector2 screenPoint)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_dial, screenPoint, null, out Vector2 local);
            return Mathf.Atan2(local.x, local.y) * Mathf.Rad2Deg;
        }

        private int RingAt(Vector2 screenPoint)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_dial, screenPoint, null, out Vector2 local);
            float radius = local.magnitude / _dialRadius;
            for (int i = RingCount - 1; i >= 0; i--)
            {
                if (radius >= _ringInner[i] && radius <= _ringOuter[i])
                {
                    return i;
                }
            }

            return NoRing;
        }
    }
}
