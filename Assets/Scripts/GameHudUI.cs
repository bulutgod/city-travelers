using Mirror;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// GameScene icin hizli test HUD'u:
/// - Aktif oyuncu
/// - Son zar
/// - Roll butonu (sadece local sira sendeyse aktif)
/// - Ev dikme paneli
/// </summary>
public class GameHudUI : MonoBehaviour
{
    private Canvas _canvas;
    private Text _turnText;
    private Text _rollText;
    private Text _youText;
    private Text _statusText;
    private Text _moneyText;
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

        RefreshUi();
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
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<GraphicRaycaster>();

        _turnText = CreateText(canvasGo.transform, "TurnText", new Vector2(12, -12), "Turn: -");
        _rollText = CreateText(canvasGo.transform, "RollText", new Vector2(12, -44), "Last Roll: -");
        _youText = CreateText(canvasGo.transform, "YouText", new Vector2(12, -76), "You: -");
        _statusText = CreateText(canvasGo.transform, "StatusText", new Vector2(12, -108), "Status: -");
        _moneyText = CreateText(canvasGo.transform, "MoneyText", new Vector2(12, -140), "Para: 1500");

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
        buttonRt.anchorMin = new Vector2(1, 0);
        buttonRt.anchorMax = new Vector2(1, 0);
        buttonRt.pivot = new Vector2(1, 0);
        buttonRt.anchoredPosition = new Vector2(-16, 16);
        buttonRt.sizeDelta = new Vector2(180, 52);

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
        leaveRt.anchoredPosition = new Vector2(16, 16);
        leaveRt.sizeDelta = new Vector2(140, 40);
        var leaveImg = leaveBtn.AddComponent<Image>();
        leaveImg.color = new Color(0.5f, 0.25f, 0.25f, 0.9f);
        _leaveGameButton = leaveBtn.AddComponent<Button>();
        _leaveGameButton.targetGraphic = leaveImg;
        var leaveTxt = CreateText(leaveBtn.transform, "Text", Vector2.zero, "Oyunu Bırak");
        leaveTxt.alignment = TextAnchor.MiddleCenter;
        leaveTxt.rectTransform.anchorMin = Vector2.zero;
        leaveTxt.rectTransform.anchorMax = Vector2.one;
        leaveTxt.rectTransform.offsetMin = Vector2.zero;
        leaveTxt.rectTransform.offsetMax = Vector2.zero;
        leaveTxt.fontSize = 16;
        _leaveGameButton.onClick.AddListener(OnLeaveGameClicked);

        _uiBuilt = true;
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;

        var esGo = new GameObject("EventSystem");
        esGo.AddComponent<EventSystem>();
        esGo.AddComponent<StandaloneInputModule>();
    }

    private Text CreateText(Transform parent, string name, Vector2 anchoredPos, string content)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(500, 28);

        var txt = go.AddComponent<Text>();
        txt.font = GetRuntimeFont();
        txt.fontSize = 22;
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
        var panel = new GameObject("BuyPanel");
        panel.transform.SetParent(parent, false);
        var panelRt = panel.AddComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.anchoredPosition = Vector2.zero;
        panelRt.sizeDelta = new Vector2(560, 220);

        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

        _buyPanelContent = new GameObject("BuyContent");
        _buyPanelContent.transform.SetParent(panel.transform, false);
        var buyContentRt = _buyPanelContent.AddComponent<RectTransform>();
        buyContentRt.anchorMin = Vector2.zero;
        buyContentRt.anchorMax = Vector2.one;
        buyContentRt.offsetMin = Vector2.zero;
        buyContentRt.offsetMax = Vector2.zero;

        _buyPanelText = CreateText(_buyPanelContent.transform, "BuyText", new Vector2(0, 30), "Mülk satın al?");
        var txtRt = _buyPanelText.rectTransform;
        txtRt.anchorMin = new Vector2(0.5f, 0.5f);
        txtRt.anchorMax = new Vector2(0.5f, 0.5f);
        txtRt.pivot = new Vector2(0.5f, 0.5f);
        txtRt.anchoredPosition = new Vector2(0, 30);
        txtRt.sizeDelta = new Vector2(300, 40);
        _buyPanelText.alignment = TextAnchor.MiddleCenter;

        var buyBtn = new GameObject("BuyButton");
        buyBtn.transform.SetParent(_buyPanelContent.transform, false);
        var buyRt = buyBtn.AddComponent<RectTransform>();
        buyRt.anchorMin = new Vector2(0.5f, 0);
        buyRt.anchorMax = new Vector2(0.5f, 0);
        buyRt.pivot = new Vector2(0.5f, 0);
        buyRt.anchoredPosition = new Vector2(-70, 20);
        buyRt.sizeDelta = new Vector2(100, 36);
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
        declineRt.anchoredPosition = new Vector2(70, 20);
        declineRt.sizeDelta = new Vector2(100, 36);
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
        var root = new GameObject("BuildContent");
        root.transform.SetParent(parent, false);
        var rootRt = root.AddComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        var title = CreateText(root.transform, "Title", new Vector2(0, 70), "Ev dik (1=Yer, 2-4=Ev)");
        var titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0.5f, 1);
        titleRt.anchorMax = new Vector2(0.5f, 1);
        titleRt.pivot = new Vector2(0.5f, 1);
        titleRt.anchoredPosition = new Vector2(0, 70);
        titleRt.sizeDelta = new Vector2(340, 24);
        title.alignment = TextAnchor.MiddleCenter;

        var row = new GameObject("HouseRow");
        row.transform.SetParent(root.transform, false);
        var rowRt = row.AddComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0.5f, 0.5f);
        rowRt.anchorMax = new Vector2(0.5f, 0.5f);
        rowRt.pivot = new Vector2(0.5f, 0.5f);
        rowRt.anchoredPosition = new Vector2(0, 20);
        rowRt.sizeDelta = new Vector2(530, 110);

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12;
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
            var slot = CreateHouseSlot(row.transform, i + 1);
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
        bottomRt.anchoredPosition = new Vector2(0, 8);
        bottomRt.sizeDelta = new Vector2(0, 44);
        var bottomHg = bottomRow.AddComponent<HorizontalLayoutGroup>();
        bottomHg.spacing = 16;
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
        var root = new GameObject("RentOrBuyContent");
        root.transform.SetParent(parent, false);
        var rootRt = root.AddComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        _rentOrBuyInfoText = CreateText(root.transform, "Info", new Vector2(0, 40), "");
        var infoRt = _rentOrBuyInfoText.rectTransform;
        infoRt.anchorMin = new Vector2(0.5f, 0.5f);
        infoRt.anchorMax = new Vector2(0.5f, 0.5f);
        infoRt.pivot = new Vector2(0.5f, 0.5f);
        infoRt.anchoredPosition = new Vector2(0, 40);
        infoRt.sizeDelta = new Vector2(500, 60);
        _rentOrBuyInfoText.alignment = TextAnchor.MiddleCenter;
        _rentOrBuyInfoText.fontSize = 18;

        var skipGo = new GameObject("SkipBuyButton");
        skipGo.transform.SetParent(root.transform, false);
        var skipRt = skipGo.AddComponent<RectTransform>();
        skipRt.anchorMin = new Vector2(0.5f, 0);
        skipRt.anchorMax = new Vector2(0.5f, 0);
        skipRt.pivot = new Vector2(0.5f, 0);
        skipRt.anchoredPosition = new Vector2(-90, 20);
        skipRt.sizeDelta = new Vector2(160, 40);
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
        buyFromOwnerRt.anchoredPosition = new Vector2(90, 20);
        buyFromOwnerRt.sizeDelta = new Vector2(160, 40);
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
        int spaceIndex = PropertyManager.Instance.pendingSpaceIndex;
        if (spaceIndex < 0) return;
        _localPlayer.CmdDeclineBuy(spaceIndex);
    }

    private void OnBuyFromOwnerClicked()
    {
        if (_localPlayer == null || PropertyManager.Instance == null) return;
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

    private GameObject CreateHouseSlot(Transform parent, int houseNum)
    {
        var slot = new GameObject($"HouseSlot_{houseNum}");
        slot.transform.SetParent(parent, false);
        var slotRt = slot.AddComponent<RectTransform>();
        slotRt.sizeDelta = new Vector2(95, 100);

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
        int spaceIndex = PropertyManager.Instance.pendingSpaceIndex;
        if (spaceIndex < 0 || _selectedHouseCount < 1) return;
        _localPlayer.CmdBuyOrBuild(spaceIndex, _selectedHouseCount);
    }

    private GameObject CreateNotificationToast(Transform parent)
    {
        var go = new GameObject("NotificationToast");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.85f);
        rt.anchorMax = new Vector2(0.5f, 0.85f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(500, 50);

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.2f, 0.92f);

        _notificationText = CreateText(go.transform, "Text", Vector2.zero, "");
        var txtRt = _notificationText.rectTransform;
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = new Vector2(10, 5);
        txtRt.offsetMax = new Vector2(-10, -5);
        _notificationText.alignment = TextAnchor.MiddleCenter;
        _notificationText.fontSize = 20;
        return go;
    }

    private GameObject CreateGameOverPanel(Transform parent)
    {
        var go = new GameObject("GameOverPanel");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.05f, 0.1f, 0.92f);

        var title = CreateText(go.transform, "Title", Vector2.zero, "KAZANAN");
        var titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0.5f, 0.6f);
        titleRt.anchorMax = new Vector2(0.5f, 0.6f);
        titleRt.pivot = new Vector2(0.5f, 0.5f);
        titleRt.sizeDelta = new Vector2(600, 50);
        title.alignment = TextAnchor.MiddleCenter;
        title.fontSize = 28;
        title.fontStyle = FontStyle.Bold;
        title.color = new Color(0.9f, 0.85f, 0.3f, 1f);

        _gameOverText = CreateText(go.transform, "WinnerName", Vector2.zero, "");
        var winnerRt = _gameOverText.rectTransform;
        winnerRt.anchorMin = new Vector2(0.5f, 0.48f);
        winnerRt.anchorMax = new Vector2(0.5f, 0.48f);
        winnerRt.pivot = new Vector2(0.5f, 0.5f);
        winnerRt.sizeDelta = new Vector2(600, 80);
        _gameOverText.alignment = TextAnchor.MiddleCenter;
        _gameOverText.fontSize = 42;
        _gameOverText.fontStyle = FontStyle.Bold;
        _gameOverText.color = new Color(0.3f, 0.9f, 0.4f, 1f);

        var menuBtn = new GameObject("MenuButton");
        menuBtn.transform.SetParent(go.transform, false);
        var menuRt = menuBtn.AddComponent<RectTransform>();
        menuRt.anchorMin = new Vector2(0.5f, 0.25f);
        menuRt.anchorMax = new Vector2(0.5f, 0.25f);
        menuRt.pivot = new Vector2(0.5f, 0.5f);
        menuRt.sizeDelta = new Vector2(200, 48);
        var menuImg = menuBtn.AddComponent<Image>();
        menuImg.color = new Color(0.25f, 0.5f, 0.7f, 1f);
        _gameOverMenuButton = menuBtn.AddComponent<Button>();
        _gameOverMenuButton.targetGraphic = menuImg;
        var menuTxt = CreateText(menuBtn.transform, "Text", Vector2.zero, "Menüye Dön");
        menuTxt.alignment = TextAnchor.MiddleCenter;
        menuTxt.rectTransform.anchorMin = Vector2.zero;
        menuTxt.rectTransform.anchorMax = Vector2.one;
        menuTxt.rectTransform.offsetMin = Vector2.zero;
        menuTxt.rectTransform.offsetMax = Vector2.zero;
        menuTxt.fontSize = 22;
        _gameOverMenuButton.onClick.AddListener(OnGameOverMenuClicked);

        return go;
    }

    private void OnGameOverMenuClicked()
    {
        if (GameNetworkManager.Instance != null)
            GameNetworkManager.Instance.RequestVoluntaryLeaveAndReturnToMenu();
    }

    private void OnLeaveGameClicked()
    {
        if (GameNetworkManager.Instance != null)
            GameNetworkManager.Instance.RequestVoluntaryLeaveAndReturnToMenu();
    }

    private void OnRollClicked()
    {
        if (_localPlayer == null) return;

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
        int spaceIndex = PropertyManager.Instance.pendingSpaceIndex;
        if (spaceIndex < 0) return;
        _localPlayer.CmdDeclineBuy(spaceIndex);
    }

    private void RefreshUi()
    {
        if (_turnText == null || _rollText == null || _youText == null || _statusText == null || _moneyText == null)
            return;

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
