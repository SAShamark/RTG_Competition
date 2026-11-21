using System.Collections;
using Game.Rescue;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UI.Results
{
    public struct MissionResults
    {
        public int Rescued;
        public int RescueTotal;
        public int MoneyFound;
        public float TimeSeconds;
        public float FirePercent;
    }

    public class ResultsScreen : MonoBehaviour
    {
        [Header("Look")]
        [SerializeField] private Font _font;
        [SerializeField] private Sprite _paper;
        [SerializeField] private Sprite _banner;
        [SerializeField] private Sprite _crown;
        [SerializeField] private Sprite _laurel;
        [SerializeField] private Sprite _iconFire;
        [SerializeField] private Sprite _iconPeople;
        [SerializeField] private Sprite _iconMoney;
        [SerializeField] private Color _ink = new(0.22f, 0.13f, 0.08f, 1f);
        [SerializeField] private Color _red = new(0.72f, 0.12f, 0.1f, 1f);

        [Header("Photo")]
        [SerializeField] private Camera _photoCamera;
        [SerializeField] private GameObject _photoSet;
        [SerializeField] private PosedActor _photoActor;

        [Header("Timing")]
        [SerializeField] private float _fadeTime = 1.0f;

        private Canvas _canvas;
        private Image _black;
        private RectTransform _page;
        private CanvasGroup _pageGroup;
        private RawImage _photo;
        private Text _fireValue, _peopleValue, _moneyValue, _timeText;
        private MissionResults _results;
        private RenderTexture _photoTexture;
        private float _time = -1f;

        public void Show(MissionResults results, Texture photo = null)
        {
            _results = results;
            Build();
            _canvas.gameObject.SetActive(true);
            _time = 0f;
            _dimOnly = photo != null;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (photo != null)
            {
                _photo.texture = photo;
                _photo.color = Color.white;
                FitPhoto(photo);
            }
            else
            {
                StartCoroutine(TakePhoto());
            }
        }

        private bool _dimOnly;

        private void FitPhoto(Texture photo)
        {
            float slot = 1004f / 434f;
            float aspect = (float)photo.width / Mathf.Max(1, photo.height);
            _photo.uvRect = aspect > slot
                ? new Rect((1f - slot / aspect) * 0.5f, 0f, slot / aspect, 1f)
                : new Rect(0f, (1f - aspect / slot) * 0.5f, 1f, aspect / slot);
        }

        private IEnumerator TakePhoto()
        {
            if (_photoCamera == null)
            {
                yield break;
            }

            _photoSet.SetActive(true);
            _photoActor.SetPose(ResidentPose.Flailing);
            yield return new WaitForEndOfFrame();
            yield return null;
            _photoTexture = new RenderTexture(1200, 500, 24);
            _photoCamera.targetTexture = _photoTexture;
            _photoCamera.Render();
            _photoCamera.targetTexture = null;
            _photo.texture = _photoTexture;
            _photo.color = Color.white;
            _photoSet.SetActive(false);
        }

        private void Update()
        {
            if (_time < 0f)
            {
                return;
            }

            _time += Time.unscaledDeltaTime;
            _black.color = new Color(0.05f, 0.03f, 0.02f, Mathf.Clamp01(_time / _fadeTime) * (_dimOnly ? 0.35f : 0.8f));

            float k = Mathf.Clamp01((_time - _fadeTime * 0.7f) / 0.55f);
            float ease = 1f - Mathf.Pow(1f - k, 3f);
            _pageGroup.alpha = k;
            _page.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, ease);
            _page.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(9f, -1.2f, ease));

            float count = Mathf.Clamp01((_time - _fadeTime - 0.5f) / 1.3f);
            _fireValue.text = $"{Mathf.RoundToInt(_results.FirePercent * count)}%";
            _peopleValue.text = $"{Mathf.RoundToInt(_results.Rescued * count)}";
            _moneyValue.text = $"{Mathf.RoundToInt(_results.MoneyFound * count)} ₴";
            int seconds = Mathf.RoundToInt(_results.TimeSeconds * count);
            _timeText.text = $"Час проходження: {seconds / 60:00}:{seconds % 60:00}   ·   урятовано {_results.Rescued} з {_results.RescueTotal}";
        }

        private void Build()
        {
            if (_canvas != null)
            {
                return;
            }

            var root = new GameObject("ResultsCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            _canvas = root.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 500;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            }

            _black = Img("Fade", root.transform, null, Color.clear, Vector2.zero, Vector2.zero);
            Stretch(_black.rectTransform);

            _page = new GameObject("Page", typeof(RectTransform)).GetComponent<RectTransform>();
            _page.SetParent(root.transform, false);
            _page.sizeDelta = new Vector2(1180f, 1010f);
            _pageGroup = _page.gameObject.AddComponent<CanvasGroup>();
            _pageGroup.alpha = 0f;

            Img("SheetBack2", _page, _paper, new Color(0.86f, 0.8f, 0.7f), new Vector2(16f, -14f), new Vector2(1160f, 990f)).rectTransform.localRotation = Quaternion.Euler(0, 0, 2.2f);
            Img("SheetBack1", _page, _paper, new Color(0.93f, 0.88f, 0.78f), new Vector2(-12f, -8f), new Vector2(1160f, 990f)).rectTransform.localRotation = Quaternion.Euler(0, 0, -1.6f);
            var sheet = Img("Sheet", _page, _paper, Color.white, Vector2.zero, new Vector2(1160f, 990f)).rectTransform;

            Img("Rule", sheet, null, new Color(_ink.r, _ink.g, _ink.b, 0.35f), new Vector2(-300f, 448f), new Vector2(380f, 4f));
            Img("Rule", sheet, null, new Color(_ink.r, _ink.g, _ink.b, 0.35f), new Vector2(300f, 448f), new Vector2(380f, 4f));
            Img("Crown", sheet, _crown, Color.white, new Vector2(0f, 448f), new Vector2(84f, 84f));
            Img("Banner", sheet, _banner, Color.white, new Vector2(0f, 345f), new Vector2(880f, 150f));
            var title = Txt("Title", sheet, "НАШ ГЕРОЙ!", 104, FontStyle.Bold, new Color(1f, 0.97f, 0.92f), new Vector2(0f, 347f), new Vector2(860f, 140f));
            title.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(3f, -4f);
            Img("LaurelL", sheet, _laurel, Color.white, new Vector2(-505f, 345f), new Vector2(130f, 150f));
            Img("LaurelR", sheet, _laurel, Color.white, new Vector2(505f, 345f), new Vector2(130f, 150f)).rectTransform.localScale = new Vector3(-1f, 1f, 1f);

            Img("PhotoBorder", sheet, null, new Color(1f, 0.99f, 0.96f), new Vector2(0f, 30f), new Vector2(1030f, 460f));
            _photo = new GameObject("Photo", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            _photo.transform.SetParent(sheet, false);
            _photo.rectTransform.anchoredPosition = new Vector2(0f, 30f);
            _photo.rectTransform.sizeDelta = new Vector2(1004f, 434f);
            _photo.color = new Color(0.55f, 0.5f, 0.45f);
            Txt("Caption", sheet, "Мешканці дякують сміливцю в майці. Пожежник Махмуд у кадр не потрапив.", 26, FontStyle.Italic,
                new Color(_ink.r, _ink.g, _ink.b, 0.75f), new Vector2(0f, -225f), new Vector2(1030f, 40f));

            Img("RuleStats", sheet, null, new Color(_ink.r, _ink.g, _ink.b, 0.3f), new Vector2(0f, -258f), new Vector2(1040f, 3f));
            _fireValue = Column(sheet, -345f, _iconFire, "Загашено\nпожежі");
            _peopleValue = Column(sheet, 0f, _iconPeople, "Врятовано\nлюдей");
            _moneyValue = Column(sheet, 345f, _iconMoney, "Знайдено\nгрошей");
            Img("Divider", sheet, null, new Color(_ink.r, _ink.g, _ink.b, 0.3f), new Vector2(-172f, -345f), new Vector2(3f, 150f));
            Img("Divider", sheet, null, new Color(_ink.r, _ink.g, _ink.b, 0.3f), new Vector2(172f, -345f), new Vector2(3f, 150f));
            Img("Underline", sheet, null, _red, new Vector2(-345f, -415f), new Vector2(210f, 6f));

            _timeText = Txt("Time", sheet, "", 24, FontStyle.Normal, new Color(_ink.r, _ink.g, _ink.b, 0.8f), new Vector2(-190f, -458f), new Vector2(700f, 36f));
            _timeText.alignment = TextAnchor.MiddleLeft;
            var buttonImage = Img("Replay", sheet, null, _red, new Vector2(370f, -458f), new Vector2(330f, 62f));
            var button = buttonImage.gameObject.AddComponent<Button>();
            Txt("Label", buttonImage.rectTransform, "ЗІГРАТИ ЩЕ РАЗ", 30, FontStyle.Bold, Color.white, Vector2.zero, new Vector2(330f, 62f));
            var colors = button.colors;
            colors.highlightedColor = new Color(1f, 0.85f, 0.8f);
            colors.pressedColor = new Color(0.75f, 0.6f, 0.55f);
            button.colors = colors;
            button.onClick.AddListener(() => SceneManager.LoadScene(Mathf.Max(0, SceneManager.GetActiveScene().buildIndex)));
        }

        private Text Column(RectTransform sheet, float x, Sprite icon, string label)
        {
            Img("Icon", sheet, icon, Color.white, new Vector2(x - 110f, -300f), new Vector2(70f, 70f));
            var l = Txt("Label", sheet, label, 30, FontStyle.Bold, _ink, new Vector2(x + 40f, -300f), new Vector2(220f, 80f));
            l.alignment = TextAnchor.MiddleLeft;
            l.lineSpacing = 0.9f;
            return Txt("Value", sheet, "0", 92, FontStyle.Bold, _red, new Vector2(x, -372f), new Vector2(320f, 100f));
        }

        private static Image Img(string name, Transform parent, Sprite sprite, Color color, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            image.rectTransform.anchoredPosition = position;
            image.rectTransform.sizeDelta = size;
            return image;
        }

        private Text Txt(string name, Transform parent, string text, int size, FontStyle style, Color color, Vector2 position, Vector2 box)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.font = _font;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = color;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.raycastTarget = false;
            label.rectTransform.anchoredPosition = position;
            label.rectTransform.sizeDelta = box;
            return label;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void OnDestroy()
        {
            if (_photoTexture != null)
            {
                _photoTexture.Release();
            }
        }
    }
}
