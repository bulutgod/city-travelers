using Mirror;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// GameScene icin hizli test HUD'u:
/// - Aktif oyuncu
/// - Son zar
/// - Roll butonu (sadece local sira sendeyse aktif)
/// - Ev dikme paneli
/// </summary>
public class GameHudUI : MonoBehaviour
{
    [Tooltip("HUD layout ayarlari. Bos birakilirsa varsayilan degerler kullanilir. Assets > Create > Brom City > Game HUD Layout Config ile olustur.")]
    [SerializeField] private GameHudLayoutConfig _layoutConfig;

    private GameHudLayoutConfig C => _layoutConfig;

    private Canvas _canvas;
    private Text _turnText;
    private Text _rollText;
    private Text _youText;
    private Text _statusText;
    private Text _moneyText;
    private Text _playerSummaryText;
    private Text _turnTimerText;
    private struct PlayerCornerHud { public GameObject root; public RawImage avatar; public Text nameText; public Text moneyText; }
    private PlayerCornerHud[] _playerCornerHuds;
    private GameObject _gameDurationPanel;
    private Text _gameDurationText;
    private Button _dur20Btn;
    private Button _dur60Btn;
    private Button _dur120Btn;
    private Button _rollButton;
    private Image _rollButtonImage;
    private Text _rollButtonText;
    private GameObject _buyPanel;
    private Text _buyPanelText;
    private Button _buyButton;
    private Button _declineButton;
    private Text _buyButtonText;
    private GameObject _buyPanelContent;
    private GameObject _buildPanelContent;
    private GameObject _rentOrBuyPanelContent;
    private Text _rentOrBuyInfoText;
    private Button _payRentButton;
    private Button _buyFromOwnerButton;
    private Toggle[] _houseToggles;
    private GameObject[] _houseBoxes;
    private Text[] _houseLabels;
    private Text _buildPriceText;
    private Button _buildBuyButton;
    private int _selectedHouseCount;
    private GameObject _notificationToast;
    private Text _notificationText;
    private GameObject _gameOverPanel;
    private Text _gameOverText;
    private Button _gameOverMenuButton;
    private Button _leaveGameButton;
    private GameObject _escapeMenuPanel;
    private GameObject _escapeMenuBackdrop;
    private GameObject _quickChatBar;
    private bool _uiBuilt;
    private int _lastPendingSpace = -1;
    private const float NotificationDuration = 4f;

    private PlayerObject _localPlayer;
    private readonly Color _buttonActive = new Color(0.15f, 0.7f, 0.2f, 0.95f);
    private readonly Color _buttonIdle = new Color(0.35f, 0.35f, 0.35f, 0.8f);
    private float _rollAnimTick;
    private int _rollAnimValue = 1;

    private void Start()
    {
        EnsureUiBuilt();
    }

    private void Update()
    {
        EnsureUiBuilt();
        if (_localPlayer == null)
            _localPlayer = FindLocalPlayer();

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            ToggleEscapeMenu();

#if UNITY_EDITOR
        if (Keyboard.current != null && Keyboard.current.f5Key.wasPressedThisFrame)
            ForceRebuildUI();
#endif

        RefreshUi();
    }

    /// <summary>
    /// Config degisikliklerini gormek icin: F5 (Editor) veya Inspector'da sag tik > Force Rebuild UI
    /// </summary>
    [ContextMenu("Force Rebuild UI")]
    public void ForceRebuildUI()
    {
        if (_canvas != null)
        {
            Destroy(_canvas.gameObject);
            _canvas = null;
        }
        _uiBuilt = false;
        _turnText = _rollText = _youText = _statusText = _moneyText = _playerSummaryText = _turnTimerText = null;
        _playerCornerHuds = null;
        _gameDurationPanel = null; _gameDurationText = null; _dur20Btn = _dur60Btn = _dur120Btn = null;
        _rollButton = null; _rollButtonImage = null; _rollButtonText = null;
        _buyPanel = null; _buyPanelText = null; _buyButton = _declineButton = null; _buyButtonText = null;
        _buyPanelContent = _buildPanelContent = _rentOrBuyPanelContent = null; _rentOrBuyInfoText = null;
        _payRentButton = _buyFromOwnerButton = null; _houseToggles = null; _houseBoxes = null; _houseLabels = null;
        _buildPriceText = null; _buildBuyButton = null; _notificationToast = null; _notificationText = null;
        _gameOverPanel = null; _gameOverText = null; _gameOverMenuButton = null; _leaveGameButton = null;
        _escapeMenuPanel = null; _escapeMenuBackdrop = null; _quickChatBar = null;
    }

    private void EnsureUiBuilt()
    {
        if (_uiBuilt && _turnText != null && _rollText != null && _youText != null && _statusText != null && _moneyText != null && _rollButton != null)
            return;
        BuildUi();
    }

    private void BuildUi()
    {
        if (_canvas != null)
        {
            _uiBuilt = true;
            return;
        }

        EnsureEventSystem();

        var canvasGo = new GameObject("GameHUD_Canvas");
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = C != null ? C.referenceResolution : new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        var firstPos = C != null ? C.leftPanelFirstTextPos : new Vector2(12, -12);
        var spacing = C != null ? C.leftPanelLineSpacing : 32f;
        var textSize = C != null ? C.leftPanelTextSize : new Vector2(500, 28);
        var fontSize = C != null ? C.leftPanelFontSize : 22;

        _turnText = CreateText(canvasGo.transform, "TurnText", firstPos, "Turn: -", textSize, fontSize);
        _rollText = CreateText(canvasGo.transform, "RollText", firstPos + new Vector2(0, -spacing), "Last Roll: -", textSize, fontSize);
        _youText = CreateText(canvasGo.transform, "YouText", firstPos + new Vector2(0, -spacing * 2), "You: -", textSize, fontSize);
        _statusText = CreateText(canvasGo.transform, "StatusText", firstPos + new Vector2(0, -spacing * 3), "Status: -", textSize, fontSize);
        _moneyText = CreateText(canvasGo.transform, "MoneyText", firstPos + new Vector2(0, -spacing * 4), "Para: 1500", textSize, fontSize);
        var summaryPos = C != null ? C.playerSummaryPos : new Vector2(12, -172);
        var summaryFontSize = C != null ? C.playerSummaryFontSize : 16;
        _playerSummaryText = CreateText(canvasGo.transform, "PlayerSummary", summaryPos, "", textSize, summaryFontSize);
        _playerSummaryText.color = new Color(0.85f, 0.85f, 0.9f, 1f);
        _playerSummaryText.gameObject.SetActive(false);

        CreatePlayerCornerHuds(canvasGo.transform);

        var timerPos = C != null ? C.turnTimerPos : new Vector2(12, -204);
        var timerFontSize = C != null ? C.turnTimerFontSize : 16;
        _turnTimerText = CreateText(canvasGo.transform, "TurnTimer", timerPos, "", textSize, timerFontSize);
        _turnTimerText.color = new Color(0.9f, 0.85f, 0.5f, 1f);

        _buyPanel = CreateBuyPanel(canvasGo.transform);
        _buyPanel.SetActive(false);
        _declineButton.onClick.AddListener(OnDeclineClicked);

        _notificationToast = CreateNotificationToast(canvasGo.transform);
        _notificationToast.SetActive(false);

        _gameOverPanel = CreateGameOverPanel(canvasGo.transform);
        _gameOverPanel.SetActive(false);

        var buttonGo = new GameObject("RollButton");
        buttonGo.transform.SetParent(canvasGo.transform, false);
        var buttonRt = buttonGo.AddComponent<RectTransform>();
        buttonRt.anchorMin = C != null ? C.rollButtonAnchorMin : new Vector2(1, 0);
        buttonRt.anchorMax = C != null ? C.rollButtonAnchorMax : new Vector2(1, 0);
        buttonRt.pivot = C != null ? C.rollButtonPivot : new Vector2(1, 0);
        buttonRt.anchoredPosition = C != null ? C.rollButtonPosition : new Vector2(-16, 16);
        buttonRt.sizeDelta = C != null ? C.rollButtonSize : new Vector2(180, 52);

        var img = buttonGo.AddComponent<Image>();
        img.color = _buttonIdle;
        _rollButtonImage = img;
        _rollButton = buttonGo.AddComponent<Button>();
        _rollButton.targetGraphic = img;
        _rollButton.onClick.AddListener(OnRollClicked);

        var txt = CreateText(buttonGo.transform, "RollButtonText", Vector2.zero, "ZAR BEKLE");
        _rollButtonText = txt;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontStyle = FontStyle.Bold;
        var txtRt = txt.rectTransform;
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;

        var leaveBtn = new GameObject("LeaveGameButton");
        leaveBtn.transform.SetParent(canvasGo.transform, false);
        var leaveRt = leaveBtn.AddComponent<RectTransform>();
        leaveRt.anchorMin = new Vector2(0, 0);
        leaveRt.anchorMax = new Vector2(0, 0);
        leaveRt.pivot = new Vector2(0, 0);
        leaveRt.anchoredPosition = C != null ? C.leaveButtonPosition : new Vector2(16, 16);
        leaveRt.sizeDelta = C != null ? C.leaveButtonSize : new Vector2(140, 40);
        var leaveImg = leaveBtn.AddComponent<Image>();
        leaveImg.color = new Color(0.5f, 0.25f, 0.25f, 0.9f);
        _leaveGameButton = leaveBtn.AddComponent<Button>();
        _leaveGameButton.targetGraphic = leaveImg;
        var leaveFontSize = C != null ? C.leaveButtonFontSize : 16;
        var leaveTxt = CreateText(leaveBtn.transform, "Text", Vector2.zero, "Oyunu Bırak", null, leaveFontSize);
        leaveTxt.alignment = TextAnchor.MiddleCenter;
        leaveTxt.rectTransform.anchorMin = Vector2.zero;
        leaveTxt.rectTransform.anchorMax = Vector2.one;
        leaveTxt.rectTransform.offsetMin = Vector2.zero;
        leaveTxt.rectTransform.offsetMax = Vector2.zero;
        _leaveGameButton.onClick.AddListener(OnLeaveGameClicked);

        var settingsBtn = new GameObject("SettingsButton");
        settingsBtn.transform.SetParent(canvasGo.transform, false);
        var setRt = settingsBtn.AddComponent<RectTransform>();
        setRt.anchorMin = C != null ? C.settingsButtonAnchorMin : new Vector2(1, 1);
        setRt.anchorMax = C != null ? C.settingsButtonAnchorMax : new Vector2(1, 1);
        setRt.pivot = C != null ? C.settingsButtonPivot : new Vector2(1, 1);
        setRt.anchoredPosition = C != null ? C.settingsButtonPosition : new Vector2(-16, -16);
        setRt.sizeDelta = C != null ? C.settingsButtonSize : new Vector2(80, 36);
        var setImg = settingsBtn.AddComponent<Image>();
        setImg.color = new Color(0.35f, 0.35f, 0.4f, 0.9f);
        var setBtn = settingsBtn.AddComponent<Button>();
        setBtn.targetGraphic = setImg;
        setBtn.onClick.AddListener(OnSettingsClicked);
        var setFontSize = C != null ? C.settingsButtonFontSize : 24;
        var setTxt = CreateText(settingsBtn.transform, "Text", Vector2.zero, "Ayarlar", null, setFontSize);
        setTxt.alignment = TextAnchor.MiddleCenter;
        setTxt.rectTransform.anchorMin = Vector2.zero;
        setTxt.rectTransform.anchorMax = Vector2.one;
        setTxt.rectTransform.offsetMin = Vector2.zero;
        setTxt.rectTransform.offsetMax = Vector2.zero;

        CreateEscapeMenu(canvasGo.transform);
        CreateQuickChatBar(canvasGo.transform);
        CreateGameDurationPanel(canvasGo.transform);

        _uiBuilt = true;
    }

    private void CreateEscapeMenu(Transform parent)
    {
        _escapeMenuBackdrop = new GameObject("EscapeBackdrop");
        _escapeMenuBackdrop.transform.SetParent(parent, false);
        var backRt = _escapeMenuBackdrop.AddComponent<RectTransform>();
        backRt.anchorMin = Vector2.zero;
        backRt.anchorMax = Vector2.one;
        backRt.offsetMin = backRt.offsetMax = Vector2.zero;
        var backImg = _escapeMenuBackdrop.AddComponent<Image>();
        backImg.color = new Color(0, 0, 0, 0.6f);
        var backBtn = _escapeMenuBackdrop.AddComponent<Button>();
        backBtn.onClick.AddListener(HideEscapeMenu);

        _escapeMenuPanel = new GameObject("EscapeMenuPanel");
        _escapeMenuPanel.transform.SetParent(parent, false);
        var panelRt = _escapeMenuPanel.AddComponent<RectTransform>();
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = C != null ? C.escapeMenuPanelSize : new Vector2(320, 280);
        var panelImg = _escapeMenuPanel.AddComponent<Image>();
        panelImg.color = new Color(0.12f, 0.12f, 0.18f, 0.98f);

        var titlePos = C != null ? C.escapeMenuTitlePos : new Vector2(0, -24);
        var titleSize = C != null ? C.escapeMenuTitleSize : new Vector2(200, 36);
        var titleFontSize = C != null ? C.escapeMenuTitleFontSize : 28;
        var title = CreateText(_escapeMenuPanel.transform, "Title", Vector2.zero, "MENU", titleSize, titleFontSize);
        title.rectTransform.anchorMin = new Vector2(0.5f, 1);
        title.rectTransform.anchorMax = new Vector2(0.5f, 1);
        title.rectTransform.pivot = new Vector2(0.5f, 1);
        title.rectTransform.anchoredPosition = titlePos;
        title.rectTransform.sizeDelta = titleSize;
        title.alignment = TextAnchor.MiddleCenter;
        title.fontStyle = FontStyle.Bold;

        var btnY = C != null ? C.escapeMenuButtonY : -70f;
        var btnSpacing = C != null ? C.escapeMenuButtonSpacing : 50f;
        var continueBtn = CreateMenuButton(_escapeMenuPanel.transform, "Devam", btnY);
        continueBtn.onClick.AddListener(() => { if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick(); HideEscapeMenu(); });

        var settingsBtn2 = CreateMenuButton(_escapeMenuPanel.transform, "Ayarlar", btnY - btnSpacing);
        settingsBtn2.onClick.AddListener(() =>
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
            HideEscapeMenu();
            if (SettingsUI.Instance != null) SettingsUI.Instance.Show();
        });

        var leaveBtn2 = CreateMenuButton(_escapeMenuPanel.transform, "Oyunu Bırak", btnY - btnSpacing * 2);
        leaveBtn2.onClick.AddListener(OnLeaveGameClicked);

        _escapeMenuPanel.SetActive(false);
        _escapeMenuBackdrop.SetActive(false);
    }

    private Button CreateMenuButton(Transform parent, string text, float y)
    {
        var btnSize = C != null ? C.escapeMenuButtonSize : new Vector2(240, 44);
        var go = new GameObject("Btn_" + text);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1);
        rt.anchorMax = new Vector2(0.5f, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(0, y);
        rt.sizeDelta = btnSize;
        var img = go.AddComponent<Image>();
        img.color = new Color(0.3f, 0.35f, 0.45f, 0.95f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var txt = CreateText(go.transform, "Text", Vector2.zero, text);
        txt.alignment = TextAnchor.MiddleCenter;
        txt.rectTransform.anchorMin = Vector2.zero;
        txt.rectTransform.anchorMax = Vector2.one;
        txt.rectTransform.offsetMin = Vector2.zero;
        txt.rectTransform.offsetMax = Vector2.zero;
        return btn;
    }

    private void ToggleEscapeMenu()
    {
        if (SettingsUI.Instance != null && SettingsUI.Instance.IsVisible)
        {
            SettingsUI.Instance.Hide();
            return;
        }
        if (_gameOverPanel != null && _gameOverPanel.activeSelf) return;
        if (_escapeMenuPanel == null) return;
        bool isOn = _escapeMenuPanel.activeSelf;
        if (isOn) HideEscapeMenu();
        else ShowEscapeMenu();
    }

    private void ShowEscapeMenu()
    {
        if (_escapeMenuPanel != null) _escapeMenuPanel.SetActive(true);
        if (_escapeMenuBackdrop != null) _escapeMenuBackdrop.SetActive(true);
    }

    private void HideEscapeMenu()
    {
        if (_escapeMenuPanel != null) _escapeMenuPanel.SetActive(false);
        if (_escapeMenuBackdrop != null) _escapeMenuBackdrop.SetActive(false);
    }

    private void CreateGameDurationPanel(Transform parent)
    {
        var panelPos = C != null ? C.gameDurationPanelPos : new Vector2(0, -80);
        var panelSize = C != null ? C.gameDurationPanelSize : new Vector2(320, 100);
        var textPos = C != null ? C.gameDurationTextPos : new Vector2(0, -12);
        var textSize = C != null ? C.gameDurationTextSize : new Vector2(280, 32);
        var textFontSize = C != null ? C.gameDurationTextFontSize : 24;
        var rowPos = C != null ? C.gameDurationButtonRowPos : new Vector2(0, 16);
        var rowSize = C != null ? C.gameDurationButtonRowSize : new Vector2(280, 36);
        var btnSpacing = C != null ? C.gameDurationButtonSpacing : 95f;

        _gameDurationPanel = new GameObject("GameDurationPanel");
        _gameDurationPanel.transform.SetParent(parent, false);
        var rt = _gameDurationPanel.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = panelPos;
        rt.sizeDelta = panelSize;

        var bg = _gameDurationPanel.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.1f, 0.14f, 0.9f);

        _gameDurationText = CreateText(_gameDurationPanel.transform, "Timer", Vector2.zero, "", textSize, textFontSize);
        _gameDurationText.rectTransform.anchorMin = new Vector2(0.5f, 1);
        _gameDurationText.rectTransform.anchorMax = new Vector2(0.5f, 1);
        _gameDurationText.rectTransform.pivot = new Vector2(0.5f, 1);
        _gameDurationText.rectTransform.anchoredPosition = textPos;
        _gameDurationText.rectTransform.sizeDelta = textSize;
        _gameDurationText.alignment = TextAnchor.MiddleCenter;

        var btnRow = new GameObject("ButtonRow");
        btnRow.transform.SetParent(_gameDurationPanel.transform, false);
        var rowRt = btnRow.AddComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0.5f, 0);
        rowRt.anchorMax = new Vector2(0.5f, 0);
        rowRt.pivot = new Vector2(0.5f, 0);
        rowRt.anchoredPosition = rowPos;
        rowRt.sizeDelta = rowSize;

        _dur20Btn = CreateSmallButton(btnRow.transform, "20 dk", -btnSpacing, () => SetGameDuration(1200f));
        _dur60Btn = CreateSmallButton(btnRow.transform, "1 saat", 0, () => SetGameDuration(3600f));
        _dur120Btn = CreateSmallButton(btnRow.transform, "2 saat", btnSpacing, () => SetGameDuration(7200f));
    }

    private Button CreateSmallButton(Transform parent, string text, float x, System.Action onClick)
    {
        var btnSize = C != null ? C.gameDurationButtonSize : new Vector2(80, 32);
        var btnFontSize = C != null ? C.gameDurationButtonFontSize : 14;
        var go = new GameObject("Btn_" + text);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0);
        rt.sizeDelta = btnSize;
        var img = go.AddComponent<Image>();
        img.color = new Color(0.25f, 0.4f, 0.6f, 0.95f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() =>
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
            onClick?.Invoke();
        });
        var txt = CreateText(go.transform, "Text", Vector2.zero, text, null, btnFontSize);
        txt.alignment = TextAnchor.MiddleCenter;
        txt.rectTransform.anchorMin = Vector2.zero;
        txt.rectTransform.anchorMax = Vector2.one;
        txt.rectTransform.offsetMin = Vector2.zero;
        txt.rectTransform.offsetMax = Vector2.zero;
        return btn;
    }

    private void SetGameDuration(float seconds)
    {
        if (_localPlayer == null) return;
        _localPlayer.CmdSetGameDuration(seconds);
    }

    private void RefreshGameDurationUI()
    {
        if (_gameDurationPanel == null || GameTurnManager.Instance == null) return;
        var turn = GameTurnManager.Instance;
        if (turn.winnerPlayerIndex >= 0)
        {
            _gameDurationPanel.SetActive(false);
            return;
        }
        float duration = turn.gameDurationSeconds;
        bool isHost = NetworkServer.active;

        _gameDurationPanel.SetActive(true);
        if (duration <= 0f)
        {
            _gameDurationText.text = isHost ? "Oyun süresi seçin:" : "Host süre seçiyor...";
            if (_dur20Btn != null) _dur20Btn.gameObject.SetActive(isHost);
            if (_dur60Btn != null) _dur60Btn.gameObject.SetActive(isHost);
            if (_dur120Btn != null) _dur120Btn.gameObject.SetActive(isHost);
        }
        else
        {
            float remaining = turn.GetRemainingGameTime();
            int min = Mathf.FloorToInt(remaining / 60f);
            int sec = Mathf.FloorToInt(remaining % 60f);
            _gameDurationText.text = $"{min:D2}:{sec:D2}";
            if (_dur20Btn != null) _dur20Btn.gameObject.SetActive(false);
            if (_dur60Btn != null) _dur60Btn.gameObject.SetActive(false);
            if (_dur120Btn != null) _dur120Btn.gameObject.SetActive(false);
        }
    }

    private void CreateQuickChatBar(Transform parent)
    {
        var barPos = C != null ? C.quickChatBarPos : new Vector2(0, 56);
        var barSize = C != null ? C.quickChatBarSize : new Vector2(520, 40);
        var btnW = C != null ? C.quickChatButtonWidth : 72f;
        var btnH = C != null ? C.quickChatButtonHeight : 36f;
        var btnSpacing = C != null ? C.quickChatButtonSpacing : 8f;
        var btnFontSize = C != null ? C.quickChatButtonFontSize : 14;

        _quickChatBar = new GameObject("QuickChatBar");
        _quickChatBar.transform.SetParent(parent, false);
        var barRt = _quickChatBar.AddComponent<RectTransform>();
        barRt.anchorMin = new Vector2(0.5f, 0);
        barRt.anchorMax = new Vector2(0.5f, 0);
        barRt.pivot = new Vector2(0.5f, 0);
        barRt.anchoredPosition = barPos;
        barRt.sizeDelta = barSize;

        string[] msgs = { "Zar at!", "Bekle", "Hadi", "GG", "Şanslı!", "Hızlı!" };
        float totalW = msgs.Length * btnW + (msgs.Length - 1) * btnSpacing;
        float startX = -totalW / 2f + btnW / 2f;

        for (int i = 0; i < msgs.Length; i++)
        {
            int idx = i;
            var btn = new GameObject("Chat_" + i);
            btn.transform.SetParent(_quickChatBar.transform, false);
            var btnRt = btn.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.5f, 0.5f);
            btnRt.anchorMax = new Vector2(0.5f, 0.5f);
            btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.anchoredPosition = new Vector2(startX + i * (btnW + btnSpacing), 0);
            btnRt.sizeDelta = new Vector2(btnW, btnH);
            var img = btn.AddComponent<Image>();
            img.color = new Color(0.25f, 0.3f, 0.4f, 0.9f);
            var b = btn.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(() =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
                if (_localPlayer != null) _localPlayer.CmdSendQuickChat(idx);
            });
            var txt = CreateText(btn.transform, "Text", Vector2.zero, msgs[i], null, btnFontSize);
            txt.alignment = TextAnchor.MiddleCenter;
            txt.rectTransform.anchorMin = Vector2.zero;
            txt.rectTransform.anchorMax = Vector2.one;
            txt.rectTransform.offsetMin = Vector2.zero;
            txt.rectTransform.offsetMax = Vector2.zero;
        }
    }

    private void OnSettingsClicked()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
        if (SettingsUI.Instance != null) SettingsUI.Instance.Show();
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;

        var esGo = new GameObject("EventSystem");
        esGo.AddComponent<EventSystem>();
        esGo.AddComponent<StandaloneInputModule>();
    }

    private Text CreateText(Transform parent, string name, Vector2 anchoredPos, string content, Vector2? size = null, int? fontSize = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size ?? new Vector2(500, 28);

        var txt = go.AddComponent<Text>();
        txt.font = GetRuntimeFont();
        txt.fontSize = fontSize ?? 22;
        txt.color = Color.white;
        txt.alignment = TextAnchor.UpperLeft;
        txt.text = content;
        return txt;
    }

    private Font GetRuntimeFont()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null) return font;
        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private GameObject CreateBuyPanel(Transform parent)
    {
        var panelSize = C != null ? C.buyPanelSize : new Vector2(560, 220);
        var textPos = C != null ? C.buyPanelTextPos : new Vector2(0, 30);
        var textSize = C != null ? C.buyPanelTextSize : new Vector2(300, 40);
        var buyBtnPos = C != null ? C.buyButtonPos : new Vector2(-70, 20);
        var buyBtnSize = C != null ? C.buyButtonSize : new Vector2(100, 36);
        var declineBtnPos = C != null ? C.declineButtonPos : new Vector2(70, 20);
        var declineBtnSize = C != null ? C.declineButtonSize : new Vector2(100, 36);

        var panel = new GameObject("BuyPanel");
        panel.transform.SetParent(parent, false);
        var panelRt = panel.AddComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.anchoredPosition = Vector2.zero;
        panelRt.sizeDelta = panelSize;

        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

        _buyPanelContent = new GameObject("BuyContent");
        _buyPanelContent.transform.SetParent(panel.transform, false);
        var buyContentRt = _buyPanelContent.AddComponent<RectTransform>();
        buyContentRt.anchorMin = Vector2.zero;
        buyContentRt.anchorMax = Vector2.one;
        buyContentRt.offsetMin = Vector2.zero;
        buyContentRt.offsetMax = Vector2.zero;

        _buyPanelText = CreateText(_buyPanelContent.transform, "BuyText", textPos, "Mülk satın al?", textSize);
        var txtRt = _buyPanelText.rectTransform;
        txtRt.anchorMin = new Vector2(0.5f, 0.5f);
        txtRt.anchorMax = new Vector2(0.5f, 0.5f);
        txtRt.pivot = new Vector2(0.5f, 0.5f);
        txtRt.anchoredPosition = textPos;
        txtRt.sizeDelta = textSize;
        _buyPanelText.alignment = TextAnchor.MiddleCenter;

        var buyBtn = new GameObject("BuyButton");
        buyBtn.transform.SetParent(_buyPanelContent.transform, false);
        var buyRt = buyBtn.AddComponent<RectTransform>();
        buyRt.anchorMin = new Vector2(0.5f, 0);
        buyRt.anchorMax = new Vector2(0.5f, 0);
        buyRt.pivot = new Vector2(0.5f, 0);
        buyRt.anchoredPosition = buyBtnPos;
        buyRt.sizeDelta = buyBtnSize;
        var buyImg = buyBtn.AddComponent<Image>();
        buyImg.color = new Color(0.2f, 0.7f, 0.3f, 1f);
        _buyButton = buyBtn.AddComponent<Button>();
        _buyButton.targetGraphic = buyImg;
        var buyTxt = CreateText(buyBtn.transform, "Text", Vector2.zero, "SATIN AL");
        _buyButtonText = buyTxt;
        buyTxt.alignment = TextAnchor.MiddleCenter;
        buyTxt.rectTransform.anchorMin = Vector2.zero;
        buyTxt.rectTransform.anchorMax = Vector2.one;
        buyTxt.rectTransform.offsetMin = Vector2.zero;
        buyTxt.rectTransform.offsetMax = Vector2.zero;

        var declineBtn = new GameObject("DeclineButton");
        declineBtn.transform.SetParent(_buyPanelContent.transform, false);
        var declineRt = declineBtn.AddComponent<RectTransform>();
        declineRt.anchorMin = new Vector2(0.5f, 0);
        declineRt.anchorMax = new Vector2(0.5f, 0);
        declineRt.pivot = new Vector2(0.5f, 0);
        declineRt.anchoredPosition = declineBtnPos;
        declineRt.sizeDelta = declineBtnSize;
        var declineImg = declineBtn.AddComponent<Image>();
        declineImg.color = new Color(0.6f, 0.3f, 0.3f, 1f);
        _declineButton = declineBtn.AddComponent<Button>();
        _declineButton.targetGraphic = declineImg;
        var declineTxt = CreateText(declineBtn.transform, "Text", Vector2.zero, "GEÇ");
        declineTxt.alignment = TextAnchor.MiddleCenter;
        declineTxt.rectTransform.anchorMin = Vector2.zero;
        declineTxt.rectTransform.anchorMax = Vector2.one;
        declineTxt.rectTransform.offsetMin = Vector2.zero;
        declineTxt.rectTransform.offsetMax = Vector2.zero;

        _buildPanelContent = CreateBuildHousePanelContent(panel.transform);
        _buildPanelContent.SetActive(false);

        _rentOrBuyPanelContent = CreateRentOrBuyPanelContent(panel.transform);
        _rentOrBuyPanelContent.SetActive(false);

        return panel;
    }

    private GameObject CreateBuildHousePanelContent(Transform parent)
    {
        var titlePos = C != null ? C.buildTitlePos : new Vector2(0, 70);
        var titleSize = C != null ? C.buildTitleSize : new Vector2(340, 24);
        var rowPos = C != null ? C.buildHouseRowPos : new Vector2(0, 20);
        var rowSize = C != null ? C.buildHouseRowSize : new Vector2(530, 110);
        var slotSize = C != null ? C.buildHouseSlotSize : new Vector2(95, 100);
        var rowSpacing = C != null ? C.buildHouseRowSpacing : 12f;
        var bottomPos = C != null ? C.buildBottomRowPos : new Vector2(0, 8);
        var bottomHeight = C != null ? C.buildBottomRowHeight : 44f;
        var bottomSpacing = C != null ? C.buildBottomSpacing : 16f;

        var root = new GameObject("BuildContent");
        root.transform.SetParent(parent, false);
        var rootRt = root.AddComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        var title = CreateText(root.transform, "Title", titlePos, "Ev dik (1=Yer, 2-4=Ev)", titleSize);
        var titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0.5f, 1);
        titleRt.anchorMax = new Vector2(0.5f, 1);
        titleRt.pivot = new Vector2(0.5f, 1);
        titleRt.anchoredPosition = titlePos;
        titleRt.sizeDelta = titleSize;
        title.alignment = TextAnchor.MiddleCenter;

        var row = new GameObject("HouseRow");
        row.transform.SetParent(root.transform, false);
        var rowRt = row.AddComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0.5f, 0.5f);
        rowRt.anchorMax = new Vector2(0.5f, 0.5f);
        rowRt.pivot = new Vector2(0.5f, 0.5f);
        rowRt.anchoredPosition = rowPos;
        rowRt.sizeDelta = rowSize;

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = (int)rowSpacing;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        _houseBoxes = new GameObject[5];
        _houseToggles = new Toggle[5];
        _houseLabels = new Text[5];
        for (int i = 0; i < 5; i++)
        {
            var slot = CreateHouseSlot(row.transform, i + 1, slotSize);
            _houseBoxes[i] = slot;
            _houseToggles[i] = slot.GetComponentInChildren<Toggle>();
            var texts = slot.GetComponentsInChildren<Text>();
            _houseLabels[i] = texts.Length > 0 ? texts[0] : null;
            int idx = i;
            _houseToggles[i].onValueChanged.AddListener(_ => OnHouseToggleChanged(idx));
        }

        var bottomRow = new GameObject("BottomRow");
        bottomRow.transform.SetParent(root.transform, false);
        var bottomRt = bottomRow.AddComponent<RectTransform>();
        bottomRt.anchorMin = new Vector2(0, 0);
        bottomRt.anchorMax = new Vector2(1, 0);
        bottomRt.pivot = new Vector2(0.5f, 0);
        bottomRt.anchoredPosition = bottomPos;
        bottomRt.sizeDelta = new Vector2(0, bottomHeight);
        var bottomHg = bottomRow.AddComponent<HorizontalLayoutGroup>();
        bottomHg.spacing = (int)bottomSpacing;
        bottomHg.childAlignment = TextAnchor.MiddleLeft;
        bottomHg.padding = new RectOffset(20, 0, 0, 0);
        bottomHg.childControlWidth = false;
        bottomHg.childControlHeight = true;
        bottomHg.childForceExpandWidth = false;

        _buildPriceText = CreateText(bottomRow.transform, "Price", Vector2.zero, "0 TL");
        _buildPriceText.rectTransform.sizeDelta = new Vector2(80, 28);

        var buildBuyGo = new GameObject("BuildBuyButton");
        buildBuyGo.transform.SetParent(bottomRow.transform, false);
        var buildBuyRt = buildBuyGo.AddComponent<RectTransform>();
        buildBuyRt.sizeDelta = new Vector2(100, 36);
        var buildBuyImg = buildBuyGo.AddComponent<Image>();
        buildBuyImg.color = new Color(0.2f, 0.7f, 0.3f, 1f);
        _buildBuyButton = buildBuyGo.AddComponent<Button>();
        _buildBuyButton.targetGraphic = buildBuyImg;
        var buildBuyTxt = CreateText(buildBuyGo.transform, "Text", Vector2.zero, "EV DİK");
        buildBuyTxt.alignment = TextAnchor.MiddleCenter;
        buildBuyTxt.rectTransform.anchorMin = Vector2.zero;
        buildBuyTxt.rectTransform.anchorMax = Vector2.one;
        buildBuyTxt.rectTransform.offsetMin = Vector2.zero;
        buildBuyTxt.rectTransform.offsetMax = Vector2.zero;
        _buildBuyButton.onClick.AddListener(OnBuildBuyClicked);

        var buildDeclineBtn = new GameObject("BuildDeclineButton");
        buildDeclineBtn.transform.SetParent(bottomRow.transform, false);
        var buildDeclineRt = buildDeclineBtn.AddComponent<RectTransform>();
        buildDeclineRt.sizeDelta = new Vector2(100, 36);
        var buildDeclineImg = buildDeclineBtn.AddComponent<Image>();
        buildDeclineImg.color = new Color(0.6f, 0.3f, 0.3f, 1f);
        var buildDeclineBtnComp = buildDeclineBtn.AddComponent<Button>();
        buildDeclineBtnComp.targetGraphic = buildDeclineImg;
        buildDeclineBtnComp.onClick.AddListener(OnDeclineClicked);
        var buildDeclineTxt = CreateText(buildDeclineBtn.transform, "Text", Vector2.zero, "GEÇ");
        buildDeclineTxt.alignment = TextAnchor.MiddleCenter;
        buildDeclineTxt.rectTransform.anchorMin = Vector2.zero;
        buildDeclineTxt.rectTransform.anchorMax = Vector2.one;
        buildDeclineTxt.rectTransform.offsetMin = Vector2.zero;
        buildDeclineTxt.rectTransform.offsetMax = Vector2.zero;

        return root;
    }

    private GameObject CreateRentOrBuyPanelContent(Transform parent)
    {
        var infoPos = C != null ? C.rentOrBuyInfoPos : new Vector2(0, 40);
        var infoSize = C != null ? C.rentOrBuyInfoSize : new Vector2(500, 60);
        var infoFontSize = C != null ? C.rentOrBuyInfoFontSize : 18;
        var payRentPos = C != null ? C.payRentButtonPos : new Vector2(-90, 20);
        var payRentSize = C != null ? C.payRentButtonSize : new Vector2(160, 40);
        var buyFromOwnerPos = C != null ? C.buyFromOwnerButtonPos : new Vector2(90, 20);
        var buyFromOwnerSize = C != null ? C.buyFromOwnerButtonSize : new Vector2(160, 40);

        var root = new GameObject("RentOrBuyContent");
        root.transform.SetParent(parent, false);
        var rootRt = root.AddComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        _rentOrBuyInfoText = CreateText(root.transform, "Info", infoPos, "", infoSize, infoFontSize);
        var infoRt = _rentOrBuyInfoText.rectTransform;
        infoRt.anchorMin = new Vector2(0.5f, 0.5f);
        infoRt.anchorMax = new Vector2(0.5f, 0.5f);
        infoRt.pivot = new Vector2(0.5f, 0.5f);
        infoRt.anchoredPosition = infoPos;
        infoRt.sizeDelta = infoSize;
        _rentOrBuyInfoText.alignment = TextAnchor.MiddleCenter;

        var skipGo = new GameObject("SkipBuyButton");
        skipGo.transform.SetParent(root.transform, false);
        var skipRt = skipGo.AddComponent<RectTransform>();
        skipRt.anchorMin = new Vector2(0.5f, 0);
        skipRt.anchorMax = new Vector2(0.5f, 0);
        skipRt.pivot = new Vector2(0.5f, 0);
        skipRt.anchoredPosition = payRentPos;
        skipRt.sizeDelta = payRentSize;
        var skipImg = skipGo.AddComponent<Image>();
        skipImg.color = new Color(0.6f, 0.3f, 0.3f, 1f);
        _payRentButton = skipGo.AddComponent<Button>();
        _payRentButton.targetGraphic = skipImg;
        var skipTxt = CreateText(skipGo.transform, "Text", Vector2.zero, "GEÇ");
        skipTxt.alignment = TextAnchor.MiddleCenter;
        skipTxt.rectTransform.anchorMin = Vector2.zero;
        skipTxt.rectTransform.anchorMax = Vector2.one;
        skipTxt.rectTransform.offsetMin = Vector2.zero;
        skipTxt.rectTransform.offsetMax = Vector2.zero;
        _payRentButton.onClick.AddListener(OnSkipBuyFromOwnerClicked);

        var buyFromOwnerGo = new GameObject("BuyFromOwnerButton");
        buyFromOwnerGo.transform.SetParent(root.transform, false);
        var buyFromOwnerRt = buyFromOwnerGo.AddComponent<RectTransform>();
        buyFromOwnerRt.anchorMin = new Vector2(0.5f, 0);
        buyFromOwnerRt.anchorMax = new Vector2(0.5f, 0);
        buyFromOwnerRt.pivot = new Vector2(0.5f, 0);
        buyFromOwnerRt.anchoredPosition = buyFromOwnerPos;
        buyFromOwnerRt.sizeDelta = buyFromOwnerSize;
        var buyFromOwnerImg = buyFromOwnerGo.AddComponent<Image>();
        buyFromOwnerImg.color = new Color(0.2f, 0.7f, 0.3f, 1f);
        _buyFromOwnerButton = buyFromOwnerGo.AddComponent<Button>();
        _buyFromOwnerButton.targetGraphic = buyFromOwnerImg;
        var buyFromOwnerTxt = CreateText(buyFromOwnerGo.transform, "Text", Vector2.zero, "SATIN AL");
        buyFromOwnerTxt.alignment = TextAnchor.MiddleCenter;
        buyFromOwnerTxt.rectTransform.anchorMin = Vector2.zero;
        buyFromOwnerTxt.rectTransform.anchorMax = Vector2.one;
        buyFromOwnerTxt.rectTransform.offsetMin = Vector2.zero;
        buyFromOwnerTxt.rectTransform.offsetMax = Vector2.zero;
        _buyFromOwnerButton.onClick.AddListener(OnBuyFromOwnerClicked);

        return root;
    }

    private void OnSkipBuyFromOwnerClicked()
    {
        if (_localPlayer == null || PropertyManager.Instance == null) return;
        if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
        int spaceIndex = PropertyManager.Instance.pendingSpaceIndex;
        if (spaceIndex < 0) return;
        _localPlayer.CmdDeclineBuy(spaceIndex);
    }

    private void OnBuyFromOwnerClicked()
    {
        if (_localPlayer == null || PropertyManager.Instance == null) return;
        if (AudioManager.Instance != null) AudioManager.Instance.PlayBuy();
        int spaceIndex = PropertyManager.Instance.pendingSpaceIndex;
        if (spaceIndex < 0) return;
        _localPlayer.CmdBuyFromOwner(spaceIndex);
    }

    private void RefreshRentOrBuyPanel()
    {
        if (_rentOrBuyInfoText == null || _localPlayer == null || PropertyManager.Instance == null || BoardManager.Instance == null) return;
        int spaceIndex = PropertyManager.Instance.pendingSpaceIndex;
        if (spaceIndex < 0) return;

        var info = BoardManager.Instance.GetSpaceInfo(spaceIndex);
        int baseRent = info != null ? info.rent : 0;
        int rent = PropertyManager.Instance.GetRentWithHouses(spaceIndex, baseRent);
        int buyPrice = rent * 2;
        string spaceName = info != null && !string.IsNullOrWhiteSpace(info.displayName) ? info.displayName : $"Alan {spaceIndex}";

        _rentOrBuyInfoText.text = $"Kira ödendi: {rent} TL\n{spaceName} mülkünü {buyPrice} TL'ye satın almak ister misin?";

        if (_payRentButton != null)
            _payRentButton.interactable = true;
        if (_buyFromOwnerButton != null)
            _buyFromOwnerButton.interactable = _localPlayer.money >= buyPrice;
    }

    private GameObject CreateHouseSlot(Transform parent, int houseNum, Vector2 slotSize)
    {
        var slot = new GameObject($"HouseSlot_{houseNum}");
        slot.transform.SetParent(parent, false);
        var slotRt = slot.AddComponent<RectTransform>();
        slotRt.sizeDelta = slotSize;

        var box = new GameObject("Box");
        box.transform.SetParent(slot.transform, false);
        var boxRt = box.AddComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0.5f, 1);
        boxRt.anchorMax = new Vector2(0.5f, 1);
        boxRt.pivot = new Vector2(0.5f, 1);
        boxRt.anchoredPosition = new Vector2(0, 0);
        boxRt.sizeDelta = new Vector2(95, 68);
        var boxImg = box.AddComponent<Image>();
        boxImg.color = new Color(0.35f, 0.35f, 0.38f, 1f);

        var numLbl = CreateText(box.transform, "Num", new Vector2(0, -18), houseNum.ToString());
        var numRt = numLbl.rectTransform;
        numRt.anchorMin = new Vector2(0.5f, 0.5f);
        numRt.anchorMax = new Vector2(0.5f, 0.5f);
        numRt.pivot = new Vector2(0.5f, 0.5f);
        numRt.anchoredPosition = new Vector2(0, -18);
        numRt.sizeDelta = new Vector2(50, 30);
        numLbl.alignment = TextAnchor.MiddleCenter;
        numLbl.fontSize = 22;

        if (houseNum >= 4)
        {
            var restrictLbl = CreateText(box.transform, "Restrict", new Vector2(0, -12), "1 tur geçmeden\nalınamaz");
            restrictLbl.fontSize = 11;
            restrictLbl.alignment = TextAnchor.MiddleCenter;
            var restrictRt = restrictLbl.rectTransform;
            restrictRt.anchorMin = new Vector2(0.5f, 0.5f);
            restrictRt.anchorMax = new Vector2(0.5f, 0.5f);
            restrictRt.pivot = new Vector2(0.5f, 0.5f);
            restrictRt.anchoredPosition = new Vector2(0, -12);
            restrictRt.sizeDelta = new Vector2(85, 40);
            restrictLbl.gameObject.name = "RestrictLabel";
            restrictLbl.color = new Color(0.9f, 0.7f, 0.2f, 1f);
        }

        var toggleGo = new GameObject("Toggle");
        toggleGo.transform.SetParent(slot.transform, false);
        var toggleRt = toggleGo.AddComponent<RectTransform>();
        toggleRt.anchorMin = new Vector2(0.5f, 0);
        toggleRt.anchorMax = new Vector2(0.5f, 0);
        toggleRt.pivot = new Vector2(0.5f, 0);
        toggleRt.anchoredPosition = new Vector2(0, 6);
        toggleRt.sizeDelta = new Vector2(32, 32);
        var toggleBg = toggleGo.AddComponent<Image>();
        toggleBg.color = new Color(0.2f, 0.2f, 0.25f, 1f);
        var toggle = toggleGo.AddComponent<Toggle>();
        toggle.targetGraphic = toggleBg;
        var checkmark = new GameObject("Checkmark");
        checkmark.transform.SetParent(toggleGo.transform, false);
        var checkRt = checkmark.AddComponent<RectTransform>();
        checkRt.anchorMin = Vector2.zero;
        checkRt.anchorMax = Vector2.one;
        checkRt.offsetMin = new Vector2(5, 5);
        checkRt.offsetMax = new Vector2(-5, -5);
        var checkImg = checkmark.AddComponent<Image>();
        checkImg.color = new Color(0.2f, 0.9f, 0.3f, 1f);
        toggle.graphic = checkImg;
        toggle.isOn = false;

        return slot;
    }

    private void OnHouseToggleChanged(int index)
    {
        if (_houseToggles == null || index < 0 || index >= 5) return;
        bool isOn = _houseToggles[index].isOn;
        if (isOn)
        {
            for (int i = 0; i < index; i++)
                if (_houseToggles[i] != null) _houseToggles[i].SetIsOnWithoutNotify(true);
        }
        else
        {
            for (int i = index + 1; i < 5; i++)
                if (_houseToggles[i] != null) _houseToggles[i].SetIsOnWithoutNotify(false);
        }
        _selectedHouseCount = 0;
        for (int i = 0; i < 5; i++)
        {
            if (_houseToggles[i] != null && _houseToggles[i].isOn)
                _selectedHouseCount = i + 1;
            else
                break;
        }
        RefreshBuildPrice();
    }

    private void RefreshBuildPanel(int spaceIndex, SpaceInfo info)
    {
        if (_houseToggles == null || _houseBoxes == null || _localPlayer == null) return;
        int owner = PropertyManager.Instance.GetOwner(spaceIndex);
        bool isEmpty = owner < 0;
        int currentHouses = isEmpty ? 0 : PropertyManager.Instance.GetHouseCount(spaceIndex);
        bool isHotelPhase = !isEmpty && currentHouses == 4;

        int maxSlots;
        if (isHotelPhase)
            maxSlots = 1;
        else if (isEmpty)
            maxSlots = 4;
        else
            maxSlots = 4 - currentHouses;

        bool needAutoSelect = true;
        for (int i = 0; i < 5; i++)
        {
            if (_houseToggles[i] != null && _houseToggles[i].isOn) { needAutoSelect = false; break; }
        }
        for (int i = 0; i < 5; i++)
        {
            bool canSelect;
            if (isHotelPhase)
                canSelect = i == 0;
            else
            {
                bool needsPassedStart = isEmpty ? (i >= 3) : (currentHouses + i + 1 >= 4);
                canSelect = i < maxSlots && (!needsPassedStart || _localPlayer.hasPassedStart);
            }

            if (_houseToggles[i] != null)
            {
                _houseToggles[i].interactable = canSelect;
                if (!canSelect) _houseToggles[i].SetIsOnWithoutNotify(false);
            }
            if (_houseLabels[i] != null)
            {
                if (isHotelPhase && i == 0)
                    _houseLabels[i].text = "Otel";
                else if (isEmpty && i == 0)
                    _houseLabels[i].text = "Yer";
                else
                    _houseLabels[i].text = (i + 1).ToString();
            }
            if (_houseBoxes[i] != null)
            {
                _houseBoxes[i].SetActive(i < maxSlots || isHotelPhase);
                var restrict = _houseBoxes[i].transform.Find("Box/RestrictLabel");
                if (restrict != null)
                {
                    if (isHotelPhase)
                        restrict.gameObject.SetActive(false);
                    else
                    {
                        bool showRestrict = isEmpty ? (i == 3 && !_localPlayer.hasPassedStart) : (currentHouses + i + 1 == 4 && !_localPlayer.hasPassedStart);
                        restrict.gameObject.SetActive(showRestrict);
                    }
                }
            }
        }
        if (needAutoSelect && _houseToggles[0] != null && _houseToggles[0].interactable)
            _houseToggles[0].SetIsOnWithoutNotify(true);
        _selectedHouseCount = 0;
        for (int i = 0; i < 5; i++)
        {
            if (_houseToggles[i] != null && _houseToggles[i].isOn)
                _selectedHouseCount = i + 1;
            else
                break;
        }
        RefreshBuildPrice();
    }

    private void RefreshBuildPrice()
    {
        if (_buildPriceText == null || _buildBuyButton == null || PropertyManager.Instance == null || BoardManager.Instance == null || _localPlayer == null) return;
        int spaceIndex = PropertyManager.Instance.pendingSpaceIndex;
        if (spaceIndex < 0) return;
        var info = BoardManager.Instance.GetSpaceInfo(spaceIndex);
        int purchasePrice = info != null ? info.purchasePrice : 0;
        int housePrice = info != null ? (info.housePrice > 0 ? info.housePrice : (info.purchasePrice / 2)) : 0;
        int owner = PropertyManager.Instance.GetOwner(spaceIndex);
        bool isEmpty = owner < 0;
        int total;
        if (isEmpty)
        {
            int evSayisi = _selectedHouseCount > 0 ? _selectedHouseCount - 1 : 0;
            total = purchasePrice + housePrice * evSayisi;
        }
        else
        {
            total = housePrice * _selectedHouseCount;
        }
        _buildPriceText.text = $"{total} TL";
        bool canBuy = isEmpty ? (_selectedHouseCount >= 1 && _localPlayer.money >= total) : (_selectedHouseCount >= 1 && _localPlayer.money >= total);
        _buildBuyButton.interactable = canBuy;
    }

    private void OnBuildBuyClicked()
    {
        if (_localPlayer == null || PropertyManager.Instance == null) return;
        if (AudioManager.Instance != null) AudioManager.Instance.PlayBuild();
        int spaceIndex = PropertyManager.Instance.pendingSpaceIndex;
        if (spaceIndex < 0 || _selectedHouseCount < 1) return;
        _localPlayer.CmdBuyOrBuild(spaceIndex, _selectedHouseCount);
    }

    private GameObject CreateNotificationToast(Transform parent)
    {
        var anchorMin = C != null ? C.notificationAnchorMin : new Vector2(0.5f, 0.85f);
        var anchorMax = C != null ? C.notificationAnchorMax : new Vector2(0.5f, 0.85f);
        var size = C != null ? C.notificationSize : new Vector2(500, 50);
        var padding = C != null ? new RectOffset(C.notificationPaddingLeft, C.notificationPaddingRight, C.notificationPaddingTop, C.notificationPaddingBottom) : new RectOffset(10, 5, 10, 5);
        var fontSize = C != null ? C.notificationFontSize : 20;

        var go = new GameObject("NotificationToast");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.2f, 0.92f);

        _notificationText = CreateText(go.transform, "Text", Vector2.zero, "", null, fontSize);
        var txtRt = _notificationText.rectTransform;
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = new Vector2(padding.left, padding.bottom);
        txtRt.offsetMax = new Vector2(-padding.right, -padding.top);
        _notificationText.alignment = TextAnchor.MiddleCenter;
        return go;
    }

    private GameObject CreateGameOverPanel(Transform parent)
    {
        var titleAnchorY = C != null ? C.gameOverTitleAnchorY : 0.6f;
        var titleSize = C != null ? C.gameOverTitleSize : new Vector2(600, 50);
        var titleFontSize = C != null ? C.gameOverTitleFontSize : 28;
        var winnerAnchorY = C != null ? C.gameOverWinnerAnchorY : 0.48f;
        var winnerSize = C != null ? C.gameOverWinnerSize : new Vector2(600, 80);
        var winnerFontSize = C != null ? C.gameOverWinnerFontSize : 42;
        var menuAnchorY = C != null ? C.gameOverMenuButtonAnchorY : 0.25f;
        var menuSize = C != null ? C.gameOverMenuButtonSize : new Vector2(200, 48);
        var menuFontSize = C != null ? C.gameOverMenuButtonFontSize : 22;

        var go = new GameObject("GameOverPanel");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.05f, 0.1f, 0.92f);

        var title = CreateText(go.transform, "Title", Vector2.zero, "KAZANAN", titleSize, titleFontSize);
        var titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0.5f, titleAnchorY);
        titleRt.anchorMax = new Vector2(0.5f, titleAnchorY);
        titleRt.pivot = new Vector2(0.5f, 0.5f);
        titleRt.sizeDelta = titleSize;
        title.alignment = TextAnchor.MiddleCenter;
        title.fontStyle = FontStyle.Bold;
        title.color = new Color(0.9f, 0.85f, 0.3f, 1f);

        _gameOverText = CreateText(go.transform, "WinnerName", Vector2.zero, "", winnerSize, winnerFontSize);
        var winnerRt = _gameOverText.rectTransform;
        winnerRt.anchorMin = new Vector2(0.5f, winnerAnchorY);
        winnerRt.anchorMax = new Vector2(0.5f, winnerAnchorY);
        winnerRt.pivot = new Vector2(0.5f, 0.5f);
        winnerRt.sizeDelta = winnerSize;
        _gameOverText.alignment = TextAnchor.MiddleCenter;
        _gameOverText.fontStyle = FontStyle.Bold;
        _gameOverText.color = new Color(0.3f, 0.9f, 0.4f, 1f);

        var menuBtn = new GameObject("MenuButton");
        menuBtn.transform.SetParent(go.transform, false);
        var menuRt = menuBtn.AddComponent<RectTransform>();
        menuRt.anchorMin = new Vector2(0.5f, menuAnchorY);
        menuRt.anchorMax = new Vector2(0.5f, menuAnchorY);
        menuRt.pivot = new Vector2(0.5f, 0.5f);
        menuRt.sizeDelta = menuSize;
        var menuImg = menuBtn.AddComponent<Image>();
        menuImg.color = new Color(0.25f, 0.5f, 0.7f, 1f);
        _gameOverMenuButton = menuBtn.AddComponent<Button>();
        _gameOverMenuButton.targetGraphic = menuImg;
        var menuTxt = CreateText(menuBtn.transform, "Text", Vector2.zero, "Menüye Dön", null, menuFontSize);
        menuTxt.alignment = TextAnchor.MiddleCenter;
        menuTxt.rectTransform.anchorMin = Vector2.zero;
        menuTxt.rectTransform.anchorMax = Vector2.one;
        menuTxt.rectTransform.offsetMin = Vector2.zero;
        menuTxt.rectTransform.offsetMax = Vector2.zero;
        _gameOverMenuButton.onClick.AddListener(OnGameOverMenuClicked);

        return go;
    }

    private void OnGameOverMenuClicked()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
        if (GameNetworkManager.Instance != null)
            GameNetworkManager.Instance.RequestVoluntaryLeaveAndReturnToMenu();
    }

    private void OnLeaveGameClicked()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
        if (GameNetworkManager.Instance != null)
            GameNetworkManager.Instance.RequestVoluntaryLeaveAndReturnToMenu();
    }

    private void OnRollClicked()
    {
        if (_localPlayer == null) return;
        if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();

        if (NetworkServer.active && GameTurnManager.Instance != null)
        {
            GameTurnManager.Instance.ServerTryRoll(_localPlayer);
            return;
        }

        _localPlayer.CmdRequestRoll();
    }

    private void OnDeclineClicked()
    {
        if (_localPlayer == null || PropertyManager.Instance == null) return;
        if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
        int spaceIndex = PropertyManager.Instance.pendingSpaceIndex;
        if (spaceIndex < 0) return;
        _localPlayer.CmdDeclineBuy(spaceIndex);
    }

    private void RefreshUi()
    {
        if (_turnText == null || _rollText == null || _youText == null || _statusText == null || _moneyText == null)
            return;

        RefreshPlayerSummary();
        RefreshPlayerCornerHuds();
        RefreshGameDurationUI();

        if (_notificationToast != null && _notificationText != null)
        {
            float reconnectElapsed = Time.realtimeSinceStartup - SteamLobbyManager.ReconnectedAtTime;
            bool showReconnected = SteamLobbyManager.ReconnectedAtTime >= 0 && reconnectElapsed >= 0 && reconnectElapsed < 3f;
            if (showReconnected)
            {
                _notificationToast.SetActive(true);
                _notificationText.text = "Bağlandı!";
            }
            else if (GameTurnManager.Instance != null)
            {
                float elapsed = Time.time - GameTurnManager.LastNotificationTime;
                bool show = elapsed >= 0 && elapsed < NotificationDuration && !string.IsNullOrEmpty(GameTurnManager.LastNotification);
                _notificationToast.SetActive(show);
                if (show) _notificationText.text = GameTurnManager.LastNotification;
            }
        }

        var turn = GameTurnManager.Instance;
        if (turn == null)
        {
            _turnText.text = "Turn: waiting turn manager...";
            _rollText.text = "Last Roll: -";
            _youText.text = "You: -";
            _statusText.text = "Durum: sistem hazirlaniyor...";
            _moneyText.text = _localPlayer != null ? $"Para: {_localPlayer.money}" : "Para: -";
            if (_rollButton != null) _rollButton.interactable = false;
            if (_rollButtonImage != null) _rollButtonImage.color = _buttonIdle;
            if (_rollButtonText != null) _rollButtonText.text = "ZAR BEKLE";
            if (_buyPanel != null) _buyPanel.SetActive(false);
            if (_gameOverPanel != null) _gameOverPanel.SetActive(false);
            return;
        }

        if (turn.winnerPlayerIndex >= 0)
        {
            if (_gameOverPanel != null)
            {
                if (!_gameOverPanel.activeSelf && AudioManager.Instance != null)
                    AudioManager.Instance.PlayWin();
                _gameOverPanel.SetActive(true);
                if (_gameOverText != null)
                {
                    string name = string.IsNullOrEmpty(turn.winnerName) ? $"Oyuncu {turn.winnerPlayerIndex}" : turn.winnerName;
                    _gameOverText.text = $"{name} kazandı!";
                }
            }
            if (_buyPanel != null) _buyPanel.SetActive(false);
            if (_rollButton != null) _rollButton.interactable = false;
            if (_leaveGameButton != null) _leaveGameButton.gameObject.SetActive(false);
            return;
        }

        if (_gameOverPanel != null) _gameOverPanel.SetActive(false);
        if (_leaveGameButton != null) _leaveGameButton.gameObject.SetActive(true);

        int activeIndex = turn.currentTurnPlayerIndex;
        _turnText.text = $"Turn {turn.turnNumber} | Active Player: {activeIndex}";
        if (turn.isRolling)
        {
            _rollAnimTick -= Time.deltaTime;
            if (_rollAnimTick <= 0f)
            {
                _rollAnimTick = 0.07f;
                int maxFace = turn != null ? Mathf.Max(1, turn.MaxDice) : 6;
                _rollAnimValue = Random.Range(1, maxFace + 1);
            }
            _rollText.text = $"Rolling: P{turn.rollingPlayerIndex} -> {_rollAnimValue}";
        }
        else
        {
            _rollText.text = $"Last Roll: P{turn.lastRollPlayerIndex} -> {turn.lastRollValue}";
            _rollAnimTick = 0f;
        }

        if (_localPlayer == null)
        {
            _youText.text = "You: waiting local player...";
            _statusText.text = "Durum: local oyuncu bekleniyor...";
            _moneyText.text = "Para: -";
            if (_rollButton != null) _rollButton.interactable = false;
            if (_rollButtonImage != null) _rollButtonImage.color = _buttonIdle;
            if (_rollButtonText != null) _rollButtonText.text = "ZAR BEKLE";
            if (_buyPanel != null) _buyPanel.SetActive(false);
            return;
        }

        _youText.text = $"You: P{_localPlayer.playerIndex} | Space: {_localPlayer.currentSpaceIndex}";
        _moneyText.text = $"Para: {_localPlayer.money}";

        bool hasPending = PropertyManager.Instance != null &&
            PropertyManager.Instance.pendingSpaceIndex >= 0 &&
            PropertyManager.Instance.pendingPlayerIndex == _localPlayer.playerIndex;
        bool showBuild = hasPending && PropertyManager.Instance.pendingIsBuild;
        bool showRentOrBuy = hasPending && PropertyManager.Instance.pendingIsRentOrBuy;
        bool showAnyPanel = showBuild || showRentOrBuy;

        if (_buyPanel != null)
        {
            _buyPanel.SetActive(showAnyPanel);
            if (_buyPanelContent != null) _buyPanelContent.SetActive(false);
            if (_buildPanelContent != null) _buildPanelContent.SetActive(showBuild);
            if (_rentOrBuyPanelContent != null) _rentOrBuyPanelContent.SetActive(showRentOrBuy);

            if (showBuild && BoardManager.Instance != null)
            {
                int spaceIndex = PropertyManager.Instance.pendingSpaceIndex;
                var info = BoardManager.Instance.GetSpaceInfo(spaceIndex);
                if (_lastPendingSpace != spaceIndex)
                {
                    _lastPendingSpace = spaceIndex;
                    for (int i = 0; i < 5; i++)
                        if (_houseToggles != null && i < _houseToggles.Length && _houseToggles[i] != null)
                            _houseToggles[i].SetIsOnWithoutNotify(false);
                }
                RefreshBuildPanel(spaceIndex, info);
            }
            else if (showRentOrBuy && BoardManager.Instance != null)
            {
                RefreshRentOrBuyPanel();
            }
            else
            {
                _lastPendingSpace = -1;
            }
        }

        bool yourTurn = _localPlayer.playerIndex == activeIndex;
        if (turn.isRolling) yourTurn = false;
        if (_rollButton != null) _rollButton.interactable = yourTurn;

        if (turn.isRolling)
        {
            _statusText.text = "Durum: Zar donuyor...";
            if (_rollButtonText != null) _rollButtonText.text = "DONUYOR";
            if (_rollButtonImage != null) _rollButtonImage.color = _buttonIdle;
        }
        else if (yourTurn)
        {
            _statusText.text = "Durum: SIRA SENDE - ZAR AT";
            if (_rollButtonText != null) _rollButtonText.text = "ZAR AT";
            if (_rollButtonImage != null)
            {
                float pulse = 0.85f + 0.15f * Mathf.Abs(Mathf.Sin(Time.time * 5f));
                _rollButtonImage.color = new Color(_buttonActive.r * pulse, _buttonActive.g * pulse, _buttonActive.b * pulse, _buttonActive.a);
            }
        }
        else
        {
            _statusText.text = "Durum: Rakip zar atiyor...";
            if (_rollButtonText != null) _rollButtonText.text = "BEKLE";
            if (_rollButtonImage != null) _rollButtonImage.color = _buttonIdle;
        }

        if (_turnTimerText != null && turn != null)
        {
            float remaining = turn.GetRemainingTurnTime();
            if (remaining >= 0f && !turn.isRolling)
            {
                int sec = Mathf.CeilToInt(remaining);
                _turnTimerText.text = $"Sure: {sec} sn";
                _turnTimerText.color = sec <= 10 ? new Color(1f, 0.5f, 0.3f, 1f) : new Color(0.9f, 0.85f, 0.5f, 1f);
            }
            else
            {
                _turnTimerText.text = "";
            }
        }
    }

    private void CreatePlayerCornerHuds(Transform parent)
    {
        var hudSize = C != null ? C.cornerHudSize : new Vector2(140, 56);
        var hudOffset = C != null ? C.cornerHudOffset : new Vector2(16, 16);
        var avatarSize = C != null ? C.cornerHudAvatarSize : new Vector2(40, 40);
        var avatarOffset = C != null ? C.cornerHudAvatarOffset : 8f;
        var textPad = C != null ? new RectOffset(C.cornerHudPaddingLeft, C.cornerHudPaddingRight, C.cornerHudPaddingTop, C.cornerHudPaddingBottom) : new RectOffset(54, 8, 8, 8);
        var nameFontSize = C != null ? C.cornerHudNameFontSize : 14;
        var moneyFontSize = C != null ? C.cornerHudMoneyFontSize : 16;

        _playerCornerHuds = new PlayerCornerHud[4];
        var anchors = new[] {
            (new Vector2(1, 0), new Vector2(1, 0), new Vector2(-hudOffset.x, hudOffset.y)),
            (new Vector2(0, 0), new Vector2(0, 0), new Vector2(hudOffset.x, hudOffset.y)),
            (new Vector2(0, 1), new Vector2(0, 1), new Vector2(hudOffset.x, -hudOffset.y)),
            (new Vector2(1, 1), new Vector2(1, 1), new Vector2(-hudOffset.x, -hudOffset.y))
        };
        for (int i = 0; i < 4; i++)
        {
            var root = new GameObject($"PlayerHud_{i}");
            root.transform.SetParent(parent, false);
            var rt = root.AddComponent<RectTransform>();
            rt.anchorMin = anchors[i].Item1;
            rt.anchorMax = anchors[i].Item2;
            rt.pivot = anchors[i].Item1;
            rt.anchoredPosition = anchors[i].Item3;
            rt.sizeDelta = hudSize;

            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.15f, 0.85f);

            var avatarGo = new GameObject("Avatar");
            avatarGo.transform.SetParent(root.transform, false);
            var avRt = avatarGo.AddComponent<RectTransform>();
            avRt.anchorMin = new Vector2(0, 0.5f);
            avRt.anchorMax = new Vector2(0, 0.5f);
            avRt.pivot = new Vector2(0, 0.5f);
            avRt.anchoredPosition = new Vector2(avatarOffset, 0);
            avRt.sizeDelta = avatarSize;
            var avatar = avatarGo.AddComponent<RawImage>();
            avatar.color = new Color(0.3f, 0.3f, 0.35f, 1f);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(root.transform, false);
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0, 0);
            textRt.anchorMax = new Vector2(1, 1);
            textRt.offsetMin = new Vector2(textPad.left, textPad.bottom);
            textRt.offsetMax = new Vector2(-textPad.right, -textPad.top);
            var nameText = CreateText(textGo.transform, "Name", Vector2.zero, "", null, nameFontSize);
            nameText.rectTransform.anchorMin = Vector2.zero;
            nameText.rectTransform.anchorMax = new Vector2(1, 0.5f);
            nameText.rectTransform.offsetMin = Vector2.zero;
            nameText.rectTransform.offsetMax = Vector2.zero;

            var moneyGo = new GameObject("Money");
            moneyGo.transform.SetParent(textGo.transform, false);
            var moneyRt = moneyGo.AddComponent<RectTransform>();
            moneyRt.anchorMin = new Vector2(0, 0.5f);
            moneyRt.anchorMax = new Vector2(1, 1);
            moneyRt.offsetMin = Vector2.zero;
            moneyRt.offsetMax = Vector2.zero;
            var moneyText = CreateText(moneyGo.transform, "Money", Vector2.zero, "", null, moneyFontSize);
            moneyText.fontStyle = FontStyle.Bold;
            moneyText.rectTransform.anchorMin = Vector2.zero;
            moneyText.rectTransform.anchorMax = Vector2.one;
            moneyText.rectTransform.offsetMin = Vector2.zero;
            moneyText.rectTransform.offsetMax = Vector2.zero;

            _playerCornerHuds[i] = new PlayerCornerHud { root = root, avatar = avatar, nameText = nameText, moneyText = moneyText };
            root.SetActive(false);
        }
    }

    private void RefreshPlayerCornerHuds()
    {
        if (_playerCornerHuds == null) return;
        var players = GetOrderedPlayersForSummary();
        var ordered = new System.Collections.Generic.List<PlayerObject>();
        PlayerObject localFirst = null;
        foreach (var p in players)
        {
            if (p != null && p.isLocalPlayer) localFirst = p;
        }
        if (localFirst != null) ordered.Add(localFirst);
        foreach (var p in players)
        {
            if (p != null && p != localFirst) ordered.Add(p);
        }
        for (int slot = 0; slot < 4; slot++)
        {
            if (slot >= ordered.Count)
            {
                _playerCornerHuds[slot].root.SetActive(false);
                continue;
            }
            var p = ordered[slot];
            if (p == null) { _playerCornerHuds[slot].root.SetActive(false); continue; }

            _playerCornerHuds[slot].root.SetActive(true);
            _playerCornerHuds[slot].nameText.text = string.IsNullOrWhiteSpace(p.steamName) ? $"P{p.playerIndex}" : (p.steamName.Length > 12 ? p.steamName.Substring(0, 10) + ".." : p.steamName);
            _playerCornerHuds[slot].moneyText.text = $"{p.money} TL";
            if (_playerCornerHuds[slot].avatar != null)
            {
                AvatarCircleMask.ApplyTo(_playerCornerHuds[slot].avatar);
                if (p.avatarTexture != null)
                {
                    _playerCornerHuds[slot].avatar.texture = p.avatarTexture;
                    _playerCornerHuds[slot].avatar.color = Color.white;
                }
                else
                {
                    _playerCornerHuds[slot].avatar.texture = null;
                    _playerCornerHuds[slot].avatar.color = new Color(0.3f, 0.3f, 0.35f, 1f);
                }
            }
        }
    }

    private void RefreshPlayerSummary()
    {
        if (_playerSummaryText == null) return;
        var players = GetOrderedPlayersForSummary();
        if (players.Count == 0)
        {
            _playerSummaryText.text = "";
            return;
        }
        var parts = new System.Collections.Generic.List<string>();
        foreach (var p in players)
        {
            if (p == null) continue;
            string name = string.IsNullOrWhiteSpace(p.steamName) ? $"P{p.playerIndex}" : p.steamName;
            if (name.Length > 10) name = name.Substring(0, 8) + "..";
            parts.Add($"{name}: {p.money} TL");
        }
        _playerSummaryText.text = string.Join(" | ", parts);
    }

    private System.Collections.Generic.List<PlayerObject> GetOrderedPlayersForSummary()
    {
        var list = new System.Collections.Generic.List<PlayerObject>();
        if (GameNetworkManager.Instance != null)
        {
            foreach (var p in GameNetworkManager.Instance.GetAllActivePlayers())
                if (p != null && !list.Contains(p)) list.Add(p);
        }
        if (list.Count == 0)
        {
            foreach (var kv in NetworkClient.spawned)
            {
                var p = kv.Value?.GetComponent<PlayerObject>();
                if (p != null && !list.Contains(p)) list.Add(p);
            }
        }
        list.RemoveAll(p => p == null);
        list.Sort((a, b) => a.playerIndex.CompareTo(b.playerIndex));
        return list;
    }

    private PlayerObject FindLocalPlayer()
    {
        foreach (var kv in NetworkClient.spawned)
        {
            if (kv.Value == null) continue;
            var p = kv.Value.GetComponent<PlayerObject>();
            if (p != null && p.isLocalPlayer) return p;
        }
        return null;
    }
}
