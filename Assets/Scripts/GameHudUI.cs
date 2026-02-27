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
    private Button _rollButton;
    private Image _rollButtonImage;
    private Text _rollButtonText;
    private bool _uiBuilt;

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
        if (_uiBuilt && _turnText != null && _rollText != null && _youText != null && _statusText != null && _rollButton != null)
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
        // Yeni Unity surumlerinde Arial.ttf built-in degil.
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null) return font;
        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private void OnRollClicked()
    {
        if (_localPlayer == null) return;

        // Host tek basina veya host-client modunda Command zincirine bagli kalmadan direkt server'a ilet.
        if (NetworkServer.active && GameTurnManager.Instance != null)
        {
            GameTurnManager.Instance.ServerTryRoll(_localPlayer);
            return;
        }

        _localPlayer.CmdRequestRoll();
    }

    private void RefreshUi()
    {
        if (_turnText == null || _rollText == null || _youText == null || _statusText == null)
            return;

        var turn = GameTurnManager.Instance;
        if (turn == null)
        {
            _turnText.text = "Turn: waiting turn manager...";
            _rollText.text = "Last Roll: -";
            _youText.text = "You: -";
            _statusText.text = "Durum: sistem hazirlaniyor...";
            if (_rollButton != null) _rollButton.interactable = false;
            if (_rollButtonImage != null) _rollButtonImage.color = _buttonIdle;
            if (_rollButtonText != null) _rollButtonText.text = "ZAR BEKLE";
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
            if (_rollButton != null) _rollButton.interactable = false;
            if (_rollButtonImage != null) _rollButtonImage.color = _buttonIdle;
            if (_rollButtonText != null) _rollButtonText.text = "ZAR BEKLE";
            return;
        }

        _youText.text = $"You: P{_localPlayer.playerIndex} | Space: {_localPlayer.currentSpaceIndex}";
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
