using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sahnedeki hazir ayarlar panelini acar/kapatir ve Ses/Grafik sekmelerini SetActive ile degistirir.
/// Panel, icerikler ve butonlar Inspector'dan atanir.
/// </summary>
public class SettingsUI : MonoBehaviour
{
    public static SettingsUI Instance { get; private set; }

    [Header("Panel")]
    [Tooltip("Ayarlar butonuna basilinca acilacak ana panel GameObject'i.")]
    [SerializeField] private GameObject settingsPanel;
    [Tooltip("Opsiyonel karartma/arka plan objesi. Bos kalabilir.")]
    [SerializeField] private GameObject backdrop;

    [Header("Sekmeler")]
    [Tooltip("Ses icerigini tasiyan GameObject. Ses sekmesinde aktif olur.")]
    [SerializeField] private GameObject audioPanel;
    [Tooltip("Grafik icerigini tasiyan GameObject. Grafik sekmesinde aktif olur.")]
    [SerializeField] private GameObject graphicsPanel;
    [Tooltip("Ses sekmesine gecirecek buton.")]
    [SerializeField] private Button audioTabButton;
    [Tooltip("Grafik sekmesine gecirecek buton.")]
    [SerializeField] private Button graphicsTabButton;
    [Tooltip("Paneli kapatacak buton. Bos kalirsa sadece koddan Hide cagrilir.")]
    [SerializeField] private Button closeButton;
    [Tooltip("Backdrop uzerine tiklayinca panel kapansin.")]
    [SerializeField] private Button backdropButton;

    [Header("Baslangic")]
    [SerializeField] private bool openAudioTabOnShow = true;

    private SettingsTab _currentTab = SettingsTab.Audio;
    private bool _listenersBound;

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

        Debug.LogWarning("[SettingsUI] Sahnede SettingsUI bulunamadi. Hazir ayarlar paneline SettingsUI component'i ekleyip alanlari Inspector'dan atayin.");
        return null;
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
        ResolveMissingReferences();
        BindListeners();
        Hide();
    }

    public void Show()
    {
        ResolveMissingReferences();
        BindListeners();

        if (settingsPanel == null)
        {
            Debug.LogWarning("[SettingsUI] settingsPanel atanmamis.");
            return;
        }

        if (openAudioTabOnShow)
            _currentTab = SettingsTab.Audio;

        settingsPanel.SetActive(true);
        if (backdrop != null) backdrop.SetActive(true);
        ApplyCurrentTab();
    }

    public bool IsVisible => settingsPanel != null && settingsPanel.activeSelf;

    public void Hide()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (backdrop != null) backdrop.SetActive(false);
    }

    public void ShowAudioTab()
    {
        SelectTab(SettingsTab.Audio);
    }

    public void ShowGraphicsTab()
    {
        SelectTab(SettingsTab.Graphics);
    }

    private void SelectTab(SettingsTab tab)
    {
        _currentTab = tab;
        ApplyCurrentTab();
    }

    private void ApplyCurrentTab()
    {
        if (audioPanel != null) audioPanel.SetActive(_currentTab == SettingsTab.Audio);
        if (graphicsPanel != null) graphicsPanel.SetActive(_currentTab == SettingsTab.Graphics);
    }

    private void BindListeners()
    {
        if (_listenersBound) return;

        if (audioTabButton != null)
            audioTabButton.onClick.AddListener(() => OnTabClicked(SettingsTab.Audio));
        if (graphicsTabButton != null)
            graphicsTabButton.onClick.AddListener(() => OnTabClicked(SettingsTab.Graphics));
        if (closeButton != null)
            closeButton.onClick.AddListener(OnCloseClicked);
        if (backdropButton != null)
            backdropButton.onClick.AddListener(OnCloseClicked);

        _listenersBound = true;
    }

    private void OnTabClicked(SettingsTab tab)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
        SelectTab(tab);
    }

    private void OnCloseClicked()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
        Hide();
    }

    private void ResolveMissingReferences()
    {
        if (settingsPanel == null)
            settingsPanel = FindChildGameObject("SettingsPanel");
        if (settingsPanel == null && GetComponent<RectTransform>() != null)
            settingsPanel = gameObject;

        var root = settingsPanel != null ? settingsPanel.transform : transform;

        if (audioPanel == null)
            audioPanel = FindChildGameObject(root, "AudioPanel") ?? FindChildGameObject(root, "AudioContent");
        if (graphicsPanel == null)
            graphicsPanel = FindChildGameObject(root, "GraphicsPanel") ?? FindChildGameObject(root, "GraphicsContent");
        if (backdrop == null)
            backdrop = FindChildGameObject("SettingsBackdrop") ?? FindChildGameObject("Backdrop");

        if (audioTabButton == null)
            audioTabButton = FindButton(root, "AudioTabButton") ?? FindButton(root, "Ses");
        if (graphicsTabButton == null)
            graphicsTabButton = FindButton(root, "GraphicsTabButton") ?? FindButton(root, "Grafik");
        if (closeButton == null)
            closeButton = FindButton(root, "CloseButton") ?? FindButton(root, "Kapat");
        if (backdropButton == null && backdrop != null)
            backdropButton = backdrop.GetComponent<Button>();
    }

    private GameObject FindChildGameObject(string childName)
    {
        return FindChildGameObject(transform, childName);
    }

    private static GameObject FindChildGameObject(Transform root, string childName)
    {
        var found = FindRecursive(root, childName);
        return found != null ? found.gameObject : null;
    }

    private static Button FindButton(Transform root, string childName)
    {
        var found = FindRecursive(root, childName);
        return found != null ? found.GetComponent<Button>() : null;
    }

    private static Transform FindRecursive(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName)) return null;
        if (root.name == childName) return root;

        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            var found = FindRecursive(child, childName);
            if (found != null) return found;
        }

        return null;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
