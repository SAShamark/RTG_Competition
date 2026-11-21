using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UI.Puzzles
{
    public class SafePuzzleScreen : MonoBehaviour
    {
        private const int Steps = 12;
        private const float StepAngle = 360f / Steps;

        [SerializeField] private Font _font;
        [SerializeField] private Color _gold = new(1f, 0.68f, 0.18f, 1f);
        [SerializeField] private AudioSource _solvedAudio;
        [SerializeField] private float _turnTime = 0.15f;

        [Header("Art")]
        [SerializeField] private Sprite[] _pictureRings;
        [SerializeField] private float _pictureRadius = 0.92f;
        [SerializeField] private float[] _ringInner = { 0.54f, 0.38f, 0f };
        [SerializeField] private float[] _ringOuter = { 1f, 0.63f, 0.38f };
        [SerializeField] private float[] _ringScale = { 1f, 0.862f, 0.8f };
        [SerializeField] private Sprite _frame;
        [SerializeField] private float _frameHoleRadius = 0.54f;
        [SerializeField] private Sprite _titlePlate;
        [SerializeField] private Sprite _hintPlate;
        [SerializeField] private Sprite _mouseLeftIcon;
        [SerializeField] private Sprite _escIcon;

        private static readonly int[] StartStep = { 4, 9, 7 };

        private Canvas _canvas;
        private RectTransform _dial;
        private readonly RectTransform[] _rings = new RectTransform[3];
        private readonly int[] _step = new int[3];
        private readonly float[] _angle = new float[3];
        private Text _status;
        private Image _glow;
        private RectTransform _hintRow;
        private Action _onSolved;
        private Action _onClosed;
        private bool _isSolved;
        private float _solvedTime = -1f;
        private float _dialRadius;

        private int _dragRing = -1;
        private int _dragStartStep;
        private float _dragDelta;
        private float _dragLastAngle;

        public bool IsOpen => _canvas != null && _canvas.gameObject.activeSelf;

        public void Show(Action onSolved, Action onClosed)
        {
            _onSolved = onSolved;
            _onClosed = onClosed;
            if (_canvas == null)
            {
                Build();
            }

            _dragRing = -1;
            _canvas.gameObject.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _status.text = string.Empty;
            _hintRow.gameObject.SetActive(true);
        }

        private void Close(bool solved)
        {
            _canvas.gameObject.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Action callback = solved ? _onSolved : _onClosed;
            _onSolved = _onClosed = null;
            callback?.Invoke();
        }

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            for (int i = 0; i < 3; i++)
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
                _glow.color = new Color(_gold.r, _gold.g, _gold.b, Mathf.PingPong(_solvedTime * 1.6f, 0.6f));
                if (_solvedTime > 1.6f)
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
            if (_step[0] == 0 && _step[1] == 0 && _step[2] == 0)
            {
                _isSolved = true;
                _solvedTime = 0f;
                _solvedAudio.Play();
                _hintRow.gameObject.SetActive(false);

                _status.text = "Клац! Малюнок зійшовся";
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

        private void Build()
        {
            var canvasGo = new GameObject("SafePuzzleCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 900;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            var root = (RectTransform)canvasGo.transform;

            Image shade = NewImage("Shade", root, null, new Color(0.05f, 0.03f, 0.03f, 0.55f));
            shade.rectTransform.anchorMin = Vector2.zero;
            shade.rectTransform.anchorMax = Vector2.one;
            shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;

            Image titlePlate = NewImage("TitlePlate", root, _titlePlate, Color.white);
            titlePlate.type = Image.Type.Sliced;
            titlePlate.rectTransform.sizeDelta = new Vector2(620f, 112f);
            titlePlate.rectTransform.anchoredPosition = new Vector2(0f, 468f);

            Text title = NewText("Title", root, "Відкрити сейф", 58, _gold);
            title.rectTransform.anchoredPosition = new Vector2(0f, 470f);
            title.fontStyle = FontStyle.Bold;

            _dialRadius = 235f;
            _dial = NewRect("Dial", root);
            _dial.sizeDelta = Vector2.one * _dialRadius * 2f;
            _dial.anchoredPosition = new Vector2(0f, -10f);

            Image frame = NewImage("Frame", _dial, _frame, Color.white);
            frame.rectTransform.sizeDelta = Vector2.one * (_dialRadius * 1.02f / _frameHoleRadius) * 2f;

            _glow = NewImage("Glow", _dial, MakeGlowSprite(), new Color(_gold.r, _gold.g, _gold.b, 0f));
            _glow.rectTransform.sizeDelta = _dial.sizeDelta * 1.12f;

            float imageSize = _dialRadius / _pictureRadius * 2f;
            for (int i = 0; i < 3; i++)
            {
                RectTransform ring = NewRect("Ring" + i, _dial);
                ring.sizeDelta = _dial.sizeDelta;
                Image face = NewImage("Picture", ring, _pictureRings[i], Color.white);
                face.rectTransform.sizeDelta = Vector2.one * imageSize * _ringScale[i];
                _rings[i] = ring;
                _step[i] = StartStep[i];
                _angle[i] = -_step[i] * StepAngle;
            }

            Image hintPlate = NewImage("HintPlate", root, _hintPlate, Color.white);
            hintPlate.type = Image.Type.Sliced;
            hintPlate.rectTransform.sizeDelta = new Vector2(760f, 112f);
            hintPlate.rectTransform.anchoredPosition = new Vector2(0f, -470f);

            _status = NewText("Status", root, "", 28, new Color(1f, 0.93f, 0.82f));
            _status.rectTransform.anchoredPosition = new Vector2(0f, -470f);
            _status.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            _hintRow = PuzzleHintRow.Build(root, new Vector2(0f, -470f), _font, new Color(1f, 0.93f, 0.82f),
                new PuzzleHintRow.Entry(_mouseLeftIcon, "Тягни кільце, щоб скласти малюнок"),
                new PuzzleHintRow.Entry(_escIcon, "назад", "Esc"));

            _canvas.gameObject.SetActive(false);
        }

        private static Sprite MakeGlowSprite()
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - Mathf.Abs(r - 0.9f) * 10f)));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            return rect;
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            RectTransform rect = NewRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Text NewText(string name, Transform parent, string text, int size, Color color)
        {
            RectTransform rect = NewRect(name, parent);
            rect.sizeDelta = new Vector2(1400f, 120f);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = _font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }
    }
}
