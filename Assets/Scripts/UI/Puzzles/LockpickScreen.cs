using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UI.Puzzles
{
    public class LockpickScreen : MonoBehaviour
    {
        private const float FullTurn = 90f;
        private const float PickRange = 85f;
        private const float PlateSize = 780f;

        [SerializeField] private Font _font;
        [SerializeField] private Color _gold = new(1f, 0.68f, 0.18f, 1f);
        [SerializeField] private Color _textColor = new(1f, 0.93f, 0.82f);
        [SerializeField] private float _sensitivity = 0.25f;
        [SerializeField] private float _tolerance = 9f;
        [SerializeField] private float _feelRange = 55f;
        [SerializeField] private float _turnSpeed = 140f;
        [SerializeField] private AudioSource _openAudio;

        [Header("Art")]
        [SerializeField] private Sprite _plateSprite;
        [SerializeField] private Sprite _cylinderSprite;
        [SerializeField] private Sprite _pickSprite;
        [SerializeField] private Sprite _tensionSprite;
        [SerializeField] private Sprite _titlePlate;
        [SerializeField] private Sprite _hintPlate;
        [SerializeField] private float _plateHoleRadius = 0.28f;
        [SerializeField] private float _cylinderRadius = 0.58f;
        [SerializeField] private float _pickTip = 0.022f;
        [SerializeField] private Sprite _mouseMoveIcon;
        [SerializeField] private Sprite _mouseLeftIcon;
        [SerializeField] private Sprite _escIcon;

        private Canvas _canvas;
        private RectTransform _cylinder;
        private RectTransform _pick;
        private RectTransform _hintRow;
        private Text _status;
        private Action _onOpened;
        private Action _onClosed;
        private float _pickAngle;
        private float _sweetSpot;
        private float _turn;
        private float _openTime = -1f;

        public bool IsOpen => _canvas != null && _canvas.gameObject.activeSelf;

        public void Show(Action onOpened, Action onClosed)
        {
            _onOpened = onOpened;
            _onClosed = onClosed;
            if (_canvas == null)
            {
                Build();
            }

            _sweetSpot = UnityEngine.Random.Range(-70f, 70f);
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
            _canvas.gameObject.SetActive(false);
            Action callback = opened ? _onOpened : _onClosed;
            _onOpened = _onClosed = null;
            callback?.Invoke();
        }

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            float dt = Time.unscaledDeltaTime;
            if (_openTime >= 0f)
            {
                _openTime += dt;
                if (_openTime > 0.8f)
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
                    shake = Mathf.Sin(Time.unscaledTime * 70f) * 2.5f;   // stuck: the pick trembles
                }
            }
            else
            {
                _turn = Mathf.MoveTowards(_turn, 0f, _turnSpeed * 1.5f * dt);
            }

            _cylinder.localRotation = Quaternion.Euler(0f, 0f, -_turn);
            _pick.localRotation = Quaternion.Euler(0f, 0f, -_pickAngle + shake);

            if (_turn >= FullTurn - 0.5f)
            {
                _openTime = 0f;
                _status.text = "Клац! Відчинено";
                _hintRow.gameObject.SetActive(false);
                _openAudio.Play();
            }
        }

        private void Build()
        {
            var canvasGo = new GameObject("LockpickCanvas", typeof(Canvas), typeof(CanvasScaler));
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
            titlePlate.rectTransform.sizeDelta = new Vector2(640f, 112f);
            titlePlate.rectTransform.anchoredPosition = new Vector2(0f, 438f);

            Text title = NewText("Title", root, "Відкрити замок", 58, _gold);
            title.rectTransform.anchoredPosition = new Vector2(0f, 440f);
            title.fontStyle = FontStyle.Bold;

            Image lockPlate = NewImage("Plate", root, _plateSprite, Color.white);
            lockPlate.rectTransform.sizeDelta = Vector2.one * PlateSize;
            lockPlate.rectTransform.anchoredPosition = new Vector2(0f, -20f);
            RectTransform lockCenter = lockPlate.rectTransform;

            float hole = _plateHoleRadius * PlateSize * 0.5f;
            Image cylinder = NewImage("Cylinder", lockCenter, _cylinderSprite, Color.white);
            cylinder.rectTransform.sizeDelta = Vector2.one * (hole * 1.04f / _cylinderRadius) * 2f;
            _cylinder = cylinder.rectTransform;

            Image key = NewImage("Wrench", _cylinder, _tensionSprite, Color.white);
            key.rectTransform.sizeDelta = Vector2.one * 300f;
            key.rectTransform.anchoredPosition = new Vector2(80f, -hole * 0.75f);
            key.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);

            _pick = NewRect("PickPivot", lockCenter);
            _pick.sizeDelta = Vector2.zero;
            Image pick = NewImage("Pick", _pick, _pickSprite, Color.white);
            pick.rectTransform.pivot = new Vector2(0.5f, _pickTip);
            pick.rectTransform.sizeDelta = Vector2.one * 520f;
            pick.rectTransform.anchoredPosition = Vector2.zero;

            Image hintPlate = NewImage("HintPlate", root, _hintPlate, Color.white);
            hintPlate.type = Image.Type.Sliced;
            hintPlate.rectTransform.sizeDelta = new Vector2(980f, 112f);
            hintPlate.rectTransform.anchoredPosition = new Vector2(0f, -440f);

            _status = NewText("Status", root, string.Empty, 30, _textColor);
            _status.rectTransform.anchoredPosition = new Vector2(0f, -440f);
            _status.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            _hintRow = PuzzleHintRow.Build(root, new Vector2(0f, -440f), _font, _textColor,
                new PuzzleHintRow.Entry(_mouseMoveIcon, "положення відмички"),
                new PuzzleHintRow.Entry(_mouseLeftIcon, "утримуй — повернути замок"),
                new PuzzleHintRow.Entry(_escIcon, "назад", "Esc"));

            _canvas.gameObject.SetActive(false);
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
