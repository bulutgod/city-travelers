using Mirror;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// GameScene icin hizli test HUD'u:
/// - Aktif oyuncu
/// - Son zar
/// - Roll butonu (sadece local sira sendeyse aktif)
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
    private GameObject _notificationToast;
    private Text _notificationText;
    private bool _uiBuilt;
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
        _buyButton.onClick.AddListener(OnBuyClicked);
        _declineButton.onClick.AddListener(OnDeclineClicked);

        _notificationToast = CreateNotificationToast(canvasGo.transform);
        _notificationToast.SetActive(false);

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
        panelRt.sizeDelta = new Vector2(320, 140);

        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

        _buyPanelText = CreateText(panel.transform, "BuyText", new Vector2(0, 30), "Mülk satın al?");
        var txtRt = _buyPanelText.rectTransform;
        txtRt.anchorMin = new Vector2(0.5f, 0.5f);
        txtRt.anchorMax = new Vector2(0.5f, 0.5f);
        txtRt.pivot = new Vector2(0.5f, 0.5f);
        txtRt.anchoredPosition = new Vector2(0, 30);
        txtRt.sizeDelta = new Vector2(300, 40);
        _buyPanelText.alignment = TextAnchor.MiddleCenter;

        var buyBtn = new GameObject("BuyButton");
        buyBtn.transform.SetParent(panel.transform, false);
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
        buyTxt.alignment = TextAnchor.MiddleCenter;
        buyTxt.rectTransform.anchorMin = Vector2.zero;
        buyTxt.rectTransform.anchorMax = Vector2.one;
        buyTxt.rectTransform.offsetMin = Vector2.zero;
        buyTxt.rectTransform.offsetMax = Vector2.zero;

        var declineBtn = new GameObject("DeclineButton");
        declineBtn.transform.SetParent(panel.transform, false);
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

        return panel;
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

    private void OnBuyClicked()
    {
        if (_localPlayer == null || PropertyManager.Instance == null) return;
        int spaceIndex = PropertyManager.Instance.pendingSpaceIndex;
        if (spaceIndex < 0) return;
        _localPlayer.CmdBuyProperty(spaceIndex);
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
            return;
        }

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

        bool showBuyPanel = PropertyManager.Instance != null &&
            PropertyManager.Instance.pendingSpaceIndex >= 0 &&
            PropertyManager.Instance.pendingPlayerIndex == _localPlayer.playerIndex;
        if (_buyPanel != null)
        {
            _buyPanel.SetActive(showBuyPanel);
            if (showBuyPanel && _buyPanelText != null && BoardManager.Instance != null)
            {
                var info = BoardManager.Instance.GetSpaceInfo(PropertyManager.Instance.pendingSpaceIndex);
                string name = info != null ? info.displayName : "Kare";
                int price = info != null ? info.purchasePrice : 0;
                _buyPanelText.text = $"{name}\n{price} TL - Satın al?";
                _buyButton.interactable = _localPlayer.money >= price;
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
