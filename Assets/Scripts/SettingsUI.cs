using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Ayarlar paneli: ses ve grafik sekmeleri. Kod ile olusturulur, Inspector referansi gerekmez.
/// SettingsUI.Instance.Show() ile acilir.
/// </summary>
public class SettingsUI : MonoBehaviour
{
    public static SettingsUI Instance { get; private set; }

    [Header("Sekme Sprite'lari")]
    [Tooltip("Aktif sekme butonuna verilecek sprite. Bos kalirsa renk kullanilir.")]
    public Sprite activeTabSprite;
    [Tooltip("Pasif sekme butonuna verilecek sprite. Bos kalirsa renk kullanilir.")]
    public Sprite inactiveTabSprite;

    private GameObject _panel;
    private GameObject _backdrop;
    private GameObject _audioContent;
    private GameObject _graphicsContent;
    private Image _audioTabImage;
    private Image _graphicsTabImage;
    private TextMeshProUGUI _audioTabText;
    private TextMeshProUGUI _graphicsTabText;
    private Slider _sfxSlider;
    private Slider _musicSlider;
    private Toggle _fullscreenToggle;
    private TextMeshProUGUI _qualityButtonText;
    private SettingsTab _currentTab = SettingsTab.Audio;

    private enum SettingsTab
    {
        Audio,
        Graphics
    }

    public static SettingsUI EnsureInstance()
    {
        if (Instance != null) return Instance;

        var existing = FindFirstObjectByType<SettingsUI>();
        if (existing != null) return existing;

        var go = new GameObject("SettingsUI");
        return go.AddComponent<SettingsUI>();
    }

    public static void ShowPanel()
    {
        var settings = EnsureInstance();
        if (settings != null) settings.Show();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildPanel();
    }

    private void BuildPanel()
    {
        var canvasGo = new GameObject("SettingsCanvas");
        canvasGo.transform.SetParent(transform);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<GraphicRaycaster>();

        _panel = new GameObject("SettingsPanel");
        _panel.transform.SetParent(canvasGo.transform, false);

        var panelRt = _panel.AddComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(460, 360);

        var bg = _panel.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.15f, 0.98f);

        var title = CreateTmp(canvasGo.transform, "Title", "AYARLAR", 28);
        var titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0.5f, 1);
        titleRt.anchorMax = new Vector2(0.5f, 1);
        titleRt.pivot = new Vector2(0.5f, 1);
        titleRt.anchoredPosition = new Vector2(0, -20);
        titleRt.sizeDelta = new Vector2(360, 40);
        title.transform.SetParent(_panel.transform, false);

        CreateTabs(_panel.transform);

        _audioContent = CreateContentRoot(_panel.transform, "AudioContent");
        _graphicsContent = CreateContentRoot(_panel.transform, "GraphicsContent");

        _sfxSlider = CreateSlider(_audioContent.transform, "SFX", -20, AudioManager.Instance != null ? AudioManager.Instance.sfxVolume : 0.7f);
        _musicSlider = CreateSlider(_audioContent.transform, "Muzik", -80, AudioManager.Instance != null ? AudioManager.Instance.musicVolume : 0.35f);

        _fullscreenToggle = CreateToggle(_graphicsContent.transform, "Tam Ekran", -20);
        CreateQualityButton(_graphicsContent.transform, "Grafik Kalitesi", -80);

        var closeBtn = CreateButton(_panel.transform, "Kapat", -286);
        closeBtn.onClick.AddListener(() =>
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
            Hide();
        });

        var backdrop = new GameObject("Backdrop");
        backdrop.transform.SetParent(canvasGo.transform, false);
        var backdropRt = backdrop.AddComponent<RectTransform>();
        backdropRt.anchorMin = Vector2.zero;
        backdropRt.anchorMax = Vector2.one;
        backdropRt.offsetMin = Vector2.zero;
        backdropRt.offsetMax = Vector2.zero;
        var backdropImg = backdrop.AddComponent<Image>();
        backdropImg.color = new Color(0, 0, 0, 0.5f);
        var backdropBtn = backdrop.AddComponent<Button>();
        backdropBtn.onClick.AddListener(Hide);
        backdrop.transform.SetAsFirstSibling();
        _backdrop = backdrop;

        _panel.SetActive(false);
        _backdrop.SetActive(false);
        SelectTab(SettingsTab.Audio, false);
    }

    private void UpdateSliderValues()
    {
        if (AudioManager.Instance == null) return;
        if (_sfxSlider != null) _sfxSlider.SetValueWithoutNotify(AudioManager.Instance.sfxVolume);
        if (_musicSlider != null) _musicSlider.SetValueWithoutNotify(AudioManager.Instance.musicVolume);
    }

    private void UpdateGraphicsValues()
    {
        if (_fullscreenToggle != null) _fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
        if (_qualityButtonText != null) _qualityButtonText.text = GetQualityLabel();
    }

    private void CreateTabs(Transform parent)
    {
        CreateTabButton(parent, "AudioTabButton", "Ses", new Vector2(-105, -74), SettingsTab.Audio, out _audioTabImage, out _audioTabText);
        CreateTabButton(parent, "GraphicsTabButton", "Grafik", new Vector2(105, -74), SettingsTab.Graphics, out _graphicsTabImage, out _graphicsTabText);
    }

    private Button CreateTabButton(Transform parent, string name, string text, Vector2 pos, SettingsTab tab, out Image image, out TextMeshProUGUI label)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1);
        rt.anchorMax = new Vector2(0.5f, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(180, 42);

        image = go.AddComponent<Image>();
        image.color = new Color(0.22f, 0.26f, 0.34f, 0.95f);

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() =>
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
            SelectTab(tab, true);
        });

        label = CreateTmp(go.transform, "Text", text, 20);
        var labelRt = label.rectTransform;
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;
        label.alignment = TextAlignmentOptions.Center;

        return button;
    }

    private GameObject CreateContentRoot(Transform parent, string name)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);

        var rt = root.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1);
        rt.anchorMax = new Vector2(0.5f, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(0, -122);
        rt.sizeDelta = new Vector2(400, 145);

        return root;
    }

    private Slider CreateSlider(Transform parent, string label, float y, float value)
    {
        var row = new GameObject($"Row_{label}");
        row.transform.SetParent(parent, false);
        var rowRt = row.AddComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0.5f, 1);
        rowRt.anchorMax = new Vector2(0.5f, 1);
        rowRt.pivot = new Vector2(0.5f, 1);
        rowRt.anchoredPosition = new Vector2(0, y);
        rowRt.sizeDelta = new Vector2(340, 40);

        var lbl = CreateTmp(row.transform, "Label", label, 18);
        var lblRt = lbl.rectTransform;
        lblRt.anchorMin = new Vector2(0, 0.5f);
        lblRt.anchorMax = new Vector2(0, 0.5f);
        lblRt.pivot = new Vector2(0, 0.5f);
        lblRt.anchoredPosition = new Vector2(16, 0);
        lblRt.sizeDelta = new Vector2(100, 36);

        var sliderGo = new GameObject("Slider");
        sliderGo.transform.SetParent(row.transform, false);
        var sliderRt = sliderGo.AddComponent<RectTransform>();
        sliderRt.anchorMin = new Vector2(0, 0.5f);
        sliderRt.anchorMax = new Vector2(1, 0.5f);
        sliderRt.pivot = new Vector2(0.5f, 0.5f);
        sliderRt.anchorMin = new Vector2(0.25f, 0.5f);
        sliderRt.anchorMax = new Vector2(1, 0.5f);
        sliderRt.offsetMin = new Vector2(5, -10);
        sliderRt.offsetMax = new Vector2(-16, 10);

        var bgImg = sliderGo.AddComponent<Image>();
        bgImg.color = new Color(0.2f, 0.2f, 0.25f, 1f);

        var fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderGo.transform, false);
        var fillAreaRt = fillArea.AddComponent<RectTransform>();
        fillAreaRt.anchorMin = Vector2.zero;
        fillAreaRt.anchorMax = Vector2.one;
        fillAreaRt.offsetMin = new Vector2(5, 5);
        fillAreaRt.offsetMax = new Vector2(-5, -5);

        var fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        var fillRt = fill.AddComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = fillRt.offsetMax = Vector2.zero;
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.3f, 0.7f, 0.95f, 1f);

        var handleArea = new GameObject("Handle Slide Area");
        handleArea.transform.SetParent(sliderGo.transform, false);
        var handleAreaRt = handleArea.AddComponent<RectTransform>();
        handleAreaRt.anchorMin = Vector2.zero;
        handleAreaRt.anchorMax = Vector2.one;
        handleAreaRt.offsetMin = new Vector2(10, 0);
        handleAreaRt.offsetMax = new Vector2(-10, 0);

        var handle = new GameObject("Handle");
        handle.transform.SetParent(handleArea.transform, false);
        var handleRt = handle.AddComponent<RectTransform>();
        handleRt.sizeDelta = new Vector2(20, 0);
        var handleImg = handle.AddComponent<Image>();
        handleImg.color = Color.white;

        var slider = sliderGo.AddComponent<Slider>();
        slider.fillRect = fillRt;
        slider.handleRect = handleRt;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = value;

        if (label.Contains("SFX"))
        {
            slider.onValueChanged.AddListener(v =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.SetSfxVolume(v);
            });
        }
        else
        {
            slider.onValueChanged.AddListener(v =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.SetMusicVolume(v);
            });
        }

        return slider;
    }

    private Toggle CreateToggle(Transform parent, string label, float y)
    {
        var row = new GameObject($"Toggle_{label}");
        row.transform.SetParent(parent, false);
        var rowRt = row.AddComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0.5f, 1);
        rowRt.anchorMax = new Vector2(0.5f, 1);
        rowRt.pivot = new Vector2(0.5f, 1);
        rowRt.anchoredPosition = new Vector2(0, y);
        rowRt.sizeDelta = new Vector2(340, 36);

        var lbl = CreateTmp(row.transform, "Label", label, 18);
        var lblRt = lbl.rectTransform;
        lblRt.anchorMin = new Vector2(0, 0.5f);
        lblRt.anchorMax = new Vector2(0, 0.5f);
        lblRt.pivot = new Vector2(0, 0.5f);
        lblRt.anchoredPosition = new Vector2(16, 0);
        lblRt.sizeDelta = new Vector2(200, 30);

        var toggleGo = new GameObject("Toggle");
        toggleGo.transform.SetParent(row.transform, false);
        var toggleRt = toggleGo.AddComponent<RectTransform>();
        toggleRt.anchorMin = new Vector2(0.85f, 0.5f);
        toggleRt.anchorMax = new Vector2(0.85f, 0.5f);
        toggleRt.pivot = new Vector2(0.5f, 0.5f);
        toggleRt.anchoredPosition = Vector2.zero;
        toggleRt.sizeDelta = new Vector2(50, 28);

        var bgImg = toggleGo.AddComponent<Image>();
        bgImg.color = new Color(0.25f, 0.25f, 0.3f, 1f);

        var checkmark = new GameObject("Checkmark");
        checkmark.transform.SetParent(toggleGo.transform, false);
        var checkRt = checkmark.AddComponent<RectTransform>();
        checkRt.anchorMin = Vector2.zero;
        checkRt.anchorMax = Vector2.one;
        checkRt.offsetMin = new Vector2(5, 5);
        checkRt.offsetMax = new Vector2(-5, -5);
        var checkImg = checkmark.AddComponent<Image>();
        checkImg.color = new Color(0.2f, 0.8f, 0.3f, 1f);

        var toggle = toggleGo.AddComponent<Toggle>();
        toggle.targetGraphic = bgImg;
        toggle.graphic = checkImg;
        toggle.isOn = Screen.fullScreen;

        toggle.onValueChanged.AddListener(v =>
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
            Screen.fullScreen = v;
        });

        return toggle;
    }

    private Button CreateQualityButton(Transform parent, string label, float y)
    {
        var row = new GameObject($"Quality_{label}");
        row.transform.SetParent(parent, false);

        var rowRt = row.AddComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0.5f, 1);
        rowRt.anchorMax = new Vector2(0.5f, 1);
        rowRt.pivot = new Vector2(0.5f, 1);
        rowRt.anchoredPosition = new Vector2(0, y);
        rowRt.sizeDelta = new Vector2(340, 42);

        var lbl = CreateTmp(row.transform, "Label", label, 18);
        var lblRt = lbl.rectTransform;
        lblRt.anchorMin = new Vector2(0, 0.5f);
        lblRt.anchorMax = new Vector2(0, 0.5f);
        lblRt.pivot = new Vector2(0, 0.5f);
        lblRt.anchoredPosition = new Vector2(16, 0);
        lblRt.sizeDelta = new Vector2(160, 36);

        var buttonGo = new GameObject("QualityButton");
        buttonGo.transform.SetParent(row.transform, false);
        var buttonRt = buttonGo.AddComponent<RectTransform>();
        buttonRt.anchorMin = new Vector2(1, 0.5f);
        buttonRt.anchorMax = new Vector2(1, 0.5f);
        buttonRt.pivot = new Vector2(1, 0.5f);
        buttonRt.anchoredPosition = new Vector2(-16, 0);
        buttonRt.sizeDelta = new Vector2(150, 34);

        var image = buttonGo.AddComponent<Image>();
        image.color = new Color(0.22f, 0.24f, 0.3f, 1f);

        var button = buttonGo.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() =>
        {
            var qualityNames = QualitySettings.names;
            if (qualityNames == null || qualityNames.Length == 0) return;

            int next = (QualitySettings.GetQualityLevel() + 1) % qualityNames.Length;
            QualitySettings.SetQualityLevel(next, true);
            UpdateGraphicsValues();
            if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
        });

        _qualityButtonText = CreateTmp(buttonGo.transform, "Text", GetQualityLabel(), 16);
        var textRt = _qualityButtonText.rectTransform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(8, 0);
        textRt.offsetMax = new Vector2(-8, 0);
        _qualityButtonText.alignment = TextAlignmentOptions.Center;

        return button;
    }

    private Button CreateButton(Transform parent, string text, float y)
    {
        var go = new GameObject("Button");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0);
        rt.anchorMax = new Vector2(0.5f, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.anchoredPosition = new Vector2(0, y);
        rt.sizeDelta = new Vector2(160, 44);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.25f, 0.5f, 0.7f, 1f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var txt = CreateTmp(go.transform, "Text", text, 20);
        var txtRt = txt.rectTransform;
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;

        return btn;
    }

    private void SelectTab(SettingsTab tab, bool updateContent)
    {
        _currentTab = tab;
        if (_audioContent != null) _audioContent.SetActive(tab == SettingsTab.Audio);
        if (_graphicsContent != null) _graphicsContent.SetActive(tab == SettingsTab.Graphics);

        ApplyTabVisual(_audioTabImage, _audioTabText, tab == SettingsTab.Audio);
        ApplyTabVisual(_graphicsTabImage, _graphicsTabText, tab == SettingsTab.Graphics);

        if (updateContent)
        {
            UpdateSliderValues();
            UpdateGraphicsValues();
        }
    }

    private static string GetQualityLabel()
    {
        var qualityNames = QualitySettings.names;
        if (qualityNames == null || qualityNames.Length == 0)
            return "Varsayilan";

        int index = Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, qualityNames.Length - 1);
        return qualityNames[index];
    }

    private void ApplyTabVisual(Image image, TextMeshProUGUI text, bool isActive)
    {
        if (image != null)
        {
            image.sprite = isActive ? activeTabSprite : inactiveTabSprite;
            image.color = isActive
                ? new Color(0.86f, 0.88f, 0.92f, 1f)
                : new Color(0.22f, 0.26f, 0.34f, 0.95f);
        }

        if (text != null)
        {
            text.color = isActive
                ? new Color(0.05f, 0.06f, 0.08f, 1f)
                : new Color(0.78f, 0.82f, 0.9f, 1f);
            text.fontStyle = isActive ? FontStyles.Bold : FontStyles.Normal;
        }
    }

    private static TextMeshProUGUI CreateTmp(Transform parent, string name, string text, int fontSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(200, 30);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        return tmp;
    }

    public void Show()
    {
        if (_panel == null) return;
        UpdateSliderValues();
        UpdateGraphicsValues();
        SelectTab(_currentTab, false);
        _panel.SetActive(true);
        if (_backdrop != null) _backdrop.SetActive(true);
    }

    public bool IsVisible => _panel != null && _panel.activeSelf;

    public void Hide()
    {
        if (_panel != null) _panel.SetActive(false);
        if (_backdrop != null) _backdrop.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
