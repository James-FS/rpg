using System;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FogHarbor.UI
{
    /// <summary>Standalone title UI. Gameplay systems are initialized by Boot only after launch.</summary>
    public class TitleMenu : MonoBehaviour
    {
        public const string SceneName = "Title";
        public TMP_FontAsset chineseFont;
        public Texture2D background;
        public Texture2D shade;
        [SerializeField] private string bootScene = "Boot";
        private GameObject settings, confirmation;
        private Button continueButton, newButton;
        private TMP_Text saveHint, message, qualityLabel, fullscreenLabel;
        private CanvasGroup fade;
        private Slider volume;
        private bool launching;
        private string SaveFile(string suffix) => Path.Combine(Application.persistentDataPath, "fog_harbor_save." + suffix);
        public bool HasJourney => File.Exists(SaveFile("json")) || File.Exists(SaveFile("bak"));
        public bool IsLaunching => launching;
        public bool SettingsOpen => settings != null && settings.activeSelf;

        private void Awake()
        {
            if (transform.Find("TitleCanvas") == null) Build();
            settings = transform.Find("TitleCanvas/Settings").gameObject;
            confirmation = transform.Find("TitleCanvas/Confirmation").gameObject;
            continueButton = FindButton("ContinueJourney");
            newButton = FindButton("NewJourney");
            saveHint = FindText("SaveHint");
            message = FindText("Message");
            fade = transform.Find("TitleCanvas/Fade").GetComponent<CanvasGroup>();
            qualityLabel = FindText("QualityLabel");
            fullscreenLabel = FindText("FullscreenLabel");
            volume = transform.Find("TitleCanvas/Settings/Card/Volume").GetComponent<Slider>();
            continueButton.onClick.AddListener(ContinueJourney);
            newButton.onClick.AddListener(NewJourney);
            FindButton("OpenSettings").onClick.AddListener(() => OpenSettings(true));
            FindButton("QuitGame").onClick.AddListener(QuitGame);
            FindButton("CloseSettings").onClick.AddListener(() => OpenSettings(false));
            FindButton("Quality").onClick.AddListener(CycleQuality);
            FindButton("Fullscreen").onClick.AddListener(ToggleFullscreen);
            FindButton("CancelNew").onClick.AddListener(() => confirmation.SetActive(false));
            FindButton("ConfirmNew").onClick.AddListener(StartNewJourney);
            AudioListener.volume = PlayerPrefs.GetFloat("FogHarbor.Volume", 0.8f);
            if (PlayerPrefs.HasKey("FogHarbor.Quality"))
                QualitySettings.SetQualityLevel(Mathf.Clamp(PlayerPrefs.GetInt("FogHarbor.Quality"), 0, QualitySettings.names.Length - 1));
            volume.SetValueWithoutNotify(AudioListener.volume);
            volume.onValueChanged.AddListener(SetVolume);
            if (PlayerPrefs.HasKey("FogHarbor.Fullscreen")) Screen.fullScreen = PlayerPrefs.GetInt("FogHarbor.Fullscreen") != 0;
            RefreshSettings();
            continueButton.interactable = HasJourney;
            var continueGroup = continueButton.GetComponent<CanvasGroup>();
            if (continueGroup == null) continueGroup = continueButton.gameObject.AddComponent<CanvasGroup>();
            continueGroup.alpha = HasJourney ? 1f : 0.45f;
            saveHint.text = HasJourney ? "已有旅程 · 从小镇继续" : "尚无存档 · 开启你的第一段旅程";
            settings.SetActive(false);
            confirmation.SetActive(false);
            fade.alpha = 0;
            fade.blocksRaycasts = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Start()
        {
            EventSystem.current?.SetSelectedGameObject((HasJourney ? continueButton : newButton).gameObject);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && !launching)
            {
                if (confirmation.activeSelf) confirmation.SetActive(false);
                else if (settings.activeSelf) OpenSettings(false);
            }
        }

        public void ContinueJourney() { if (HasJourney && !launching) StartCoroutine(Launch()); }
        public void NewJourney()
        {
            if (launching) return;
            if (HasJourney) confirmation.SetActive(true);
            else StartNewJourney();
        }
        public void StartNewJourney()
        {
            if (launching) return;
            try
            {
                // Move, rather than delete, all files recognized by SaveSystem so backup fallback cannot restore the old journey.
                string archive = Path.Combine(Application.persistentDataPath, "JourneyArchives", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
                foreach (string suffix in new[] { "json", "bak", "tmp" })
                {
                    string source = SaveFile(suffix);
                    if (!File.Exists(source)) continue;
                    Directory.CreateDirectory(archive);
                    File.Move(source, Path.Combine(archive, Path.GetFileName(source)));
                }
                confirmation.SetActive(false);
                StartCoroutine(Launch());
            }
            catch (Exception ex)
            {
                message.text = "旧旅程备份失败，请稍后重试。";
                Debug.LogError("[TitleMenu] Cannot archive journey: " + ex.Message);
            }
        }
        private IEnumerator Launch()
        {
            launching = true;
            fade.blocksRaycasts = true;
            PlayerPrefs.Save();
            // 淡出期间后台预加载 Boot，淡出结束时才激活，避免激活后出现无相机的黑屏空窗
            var preload = SceneManager.LoadSceneAsync(bootScene);
            preload.allowSceneActivation = false;
            float elapsed = 0;
            while (elapsed < 0.55f)
            {
                elapsed += Time.unscaledDeltaTime;
                fade.alpha = Mathf.Clamp01(elapsed / 0.55f);
                yield return null;
            }
            // 标题画面已全黑，幕层（含加载文案）无缝接管，跨过 Boot→游戏场景的同步加载
            LoadingVeil.Show(chineseFont);
            preload.allowSceneActivation = true;
            while (!preload.isDone) yield return null;
        }
        public void OpenSettings(bool open)
        {
            settings.SetActive(open);
            if (open) EventSystem.current?.SetSelectedGameObject(FindButton("CloseSettings").gameObject);
            else { PlayerPrefs.Save(); EventSystem.current?.SetSelectedGameObject(FindButton("OpenSettings").gameObject); }
        }
        public void SetVolume(float value) { AudioListener.volume = value; PlayerPrefs.SetFloat("FogHarbor.Volume", value); }
        public void CycleQuality()
        {
            int level = (QualitySettings.GetQualityLevel() + 1) % QualitySettings.names.Length;
            QualitySettings.SetQualityLevel(level);
            PlayerPrefs.SetInt("FogHarbor.Quality", level);
            RefreshSettings();
        }
        public void ToggleFullscreen()
        {
            bool full = !Screen.fullScreen;
            Screen.fullScreen = full;
            PlayerPrefs.SetInt("FogHarbor.Fullscreen", full ? 1 : 0);
            fullscreenLabel.text = "显示  ·  " + (full ? "全屏" : "窗口");
        }
        private void RefreshSettings()
        {
            qualityLabel.text = "画质  ·  " + QualitySettings.names[QualitySettings.GetQualityLevel()];
            fullscreenLabel.text = "显示  ·  " + (Screen.fullScreen ? "全屏" : "窗口");
        }
        public void QuitGame()
        {
            PlayerPrefs.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        private Button FindButton(string name)
        {
            foreach (var item in GetComponentsInChildren<Button>(true)) if (item.name == name) return item;
            throw new InvalidOperationException("Missing title button " + name);
        }
        private TMP_Text FindText(string name)
        {
            foreach (var item in GetComponentsInChildren<TMP_Text>(true)) if (item.name == name) return item;
            throw new InvalidOperationException("Missing title text " + name);
        }

        // Builds a serialized, editable scene UI in the editor; Awake also provides a runtime fallback.
        public void Build()
        {
            var canvasObject = new GameObject("TitleCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            Transform root = canvasObject.transform;
            var backdrop = new GameObject("Background", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            backdrop.transform.SetParent(root, false);
            var br = backdrop.GetComponent<RectTransform>(); Stretch(br);
            backdrop.GetComponent<RawImage>().texture = background;
            backdrop.GetComponent<RawImage>().raycastTarget = false;
            var fitter = backdrop.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = background != null ? (float)background.width / background.height : 16f / 9f;
            var veil = new GameObject("MenuShade", typeof(RectTransform), typeof(RawImage));
            veil.transform.SetParent(root, false); Stretch(veil.GetComponent<RectTransform>());
            veil.GetComponent<RawImage>().texture = shade;
            veil.GetComponent<RawImage>().raycastTarget = false;
            Color ivory = new Color(0.94f, 0.9f, 0.78f);
            Text("Overline", root, "一段旅程，从一次相遇开始", 17, new Vector2(100, 608), new Vector2(400, 30), new Color(0.75f, 0.8f, 0.76f));
            Text("Title", root, "雾港小镇", 58, new Vector2(96, 534), new Vector2(440, 76), ivory);
            var english = Text("EnglishTitle", root, "F O G   H A R B O R", 19, new Vector2(101, 502), new Vector2(390, 30), new Color(0.8f, 0.73f, 0.52f));
            Text("Subtitle", root, "每一场相遇，都有一个故事。", 18, new Vector2(102, 453), new Vector2(410, 32), new Color(0.73f, 0.79f, 0.78f));
            Image("TitleRule", root, new Vector2(102, 432), new Vector2(58, 2), new Color(0.76f, 0.63f, 0.38f));
            MenuButton("ContinueJourney", root, "继续旅程", new Vector2(102, 349));
            MenuButton("NewJourney", root, "新的旅程", new Vector2(102, 281));
            MenuButton("OpenSettings", root, "设置", new Vector2(102, 213));
            MenuButton("QuitGame", root, "退出游戏", new Vector2(102, 145));
            Text("SaveHint", root, "从这里，走进雾港。", 14, new Vector2(105, 109), new Vector2(445, 25), new Color(0.65f, 0.72f, 0.7f));
            Text("Message", root, "", 15, new Vector2(105, 77), new Vector2(510, 28), ivory);
            Text("Footer", root, "FOG HARBOR  /  雾港小镇", 12, new Vector2(100, 27), new Vector2(420, 24), new Color(0.55f, 0.64f, 0.64f));
            var settingsRoot = Modal("Settings", root);
            var card = Image("Card", settingsRoot.transform, new Vector2(360, 135), new Vector2(560, 450), new Color(0.065f, 0.11f, 0.13f, 0.98f)).transform;
            Text("SettingsHeading", card, "设置", 32, new Vector2(46, 367), new Vector2(450, 50), ivory);
            Text("VolumeLabel", card, "总音量", 20, new Vector2(48, 303), new Vector2(440, 36), ivory);
            BuildSlider(card);
            // RefreshSettings writes the full "画质 · X" string into the button caption at Awake;
            // a second label here would double-draw the prefix (ghosting).
            MenuButton("Quality", card, "画质", new Vector2(48, 188));
            MenuButton("Fullscreen", card, "显示", new Vector2(48, 122));
            MenuButton("CloseSettings", card, "完成", new Vector2(48, 44));
            settingsRoot.SetActive(false);
            var confirmRoot = Modal("Confirmation", root);
            var confirmCard = Image("Card", confirmRoot.transform, new Vector2(345, 210), new Vector2(590, 300), new Color(0.065f, 0.11f, 0.13f, 0.98f)).transform;
            Text("ConfirmHeading", confirmCard, "开启新的旅程？", 30, new Vector2(42, 215), new Vector2(500, 50), ivory);
            Text("ConfirmDescription", confirmCard, "当前旅程将被替换，旧存档会自动备份。", 19, new Vector2(44, 139), new Vector2(505, 60), ivory);
            MenuButton("CancelNew", confirmCard, "返回", new Vector2(44, 48), 220);
            MenuButton("ConfirmNew", confirmCard, "开启旅程", new Vector2(320, 48), 220);
            confirmRoot.SetActive(false);
            var fading = Image("Fade", root, Vector2.zero, Vector2.zero, Color.black);
            Stretch(fading.rectTransform);
            var group = fading.gameObject.AddComponent<CanvasGroup>(); group.alpha = 0; group.blocksRaycasts = false;
        }
        private GameObject Modal(string name, Transform parent)
        {
            var result = Image(name, parent, Vector2.zero, Vector2.zero, new Color(0.015f, 0.025f, 0.035f, 0.78f));
            Stretch(result.rectTransform); return result.gameObject;
        }
        private void BuildSlider(Transform parent)
        {
            var track = Image("Volume", parent, new Vector2(48, 266), new Vector2(430, 24), new Color(0.2f, 0.28f, 0.29f));
            var slider = track.gameObject.AddComponent<Slider>(); slider.minValue = 0; slider.maxValue = 1; slider.value = 0.8f;
            var fill = Image("Fill", track.transform, Vector2.zero, Vector2.zero, new Color(0.72f, 0.61f, 0.4f)); Stretch(fill.rectTransform);
            slider.fillRect = fill.rectTransform;
            var area = new GameObject("HandleArea", typeof(RectTransform)); area.transform.SetParent(track.transform, false);
            Stretch(area.GetComponent<RectTransform>());
            var handle = Image("Handle", area.transform, Vector2.zero, new Vector2(18, 32), new Color(0.94f, 0.89f, 0.73f));
            slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
        }
        private Button MenuButton(string name, Transform parent, string label, Vector2 position, float width = 340)
        {
            var image = Image(name, parent, position, new Vector2(width, 54), new Color(0.08f, 0.13f, 0.15f, 0.5f));
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white; colors.highlightedColor = new Color(1.5f, 1.35f, 1.05f, 1);
            colors.selectedColor = colors.highlightedColor; colors.pressedColor = new Color(0.75f, 0.7f, 0.52f);
            colors.disabledColor = new Color(0.55f, 0.6f, 0.6f, 0.5f); colors.fadeDuration = 0.15f; button.colors = colors;
            Image("Accent", image.transform, new Vector2(0, 10), new Vector2(2, 34), new Color(0.72f, 0.61f, 0.4f));
            var caption = Text(name + "Label", image.transform, label, 23, new Vector2(23, 5), new Vector2(width - 36, 44), new Color(0.94f, 0.9f, 0.78f));
            return button;
        }
        private Image Image(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            Position(go.GetComponent<RectTransform>(), position, size);
            var result = go.GetComponent<Image>(); result.color = color; return result;
        }
        private TMP_Text Text(string name, Transform parent, string label, float size, Vector2 position, Vector2 dimensions, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            Position(go.GetComponent<RectTransform>(), position, dimensions);
            var text = go.GetComponent<TextMeshProUGUI>(); text.font = chineseFont;
            text.text = label; text.fontSize = size; text.color = color;
            text.alignment = TextAlignmentOptions.MidlineLeft; text.raycastTarget = false;
            text.enableWordWrapping = false; return text;
        }
        private static void Position(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = Vector2.zero; rect.pivot = Vector2.zero;
            rect.anchoredPosition = position; rect.sizeDelta = size;
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}