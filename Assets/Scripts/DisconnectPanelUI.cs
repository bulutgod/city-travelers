using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Mirror;
using TMPro;

/// <summary>
/// "Yeniden baglan" ve "Terket" butonlari. SampleScene'e ekleyin; panel tum sahnelerde gosterilebilsin diye DontDestroyOnLoad ile kalir.
/// - GameScene'de baglanti koptugunda: migration mesaji + Yeniden baglan / Terket.
/// - SampleScene'de lobideyken oyuna bagli degilken: Oyuna baglan veya lobiden cik.
/// - Reconnect baslatildiginda: "Yeniden baglaniyor..." tam ekran overlay.
/// </summary>
public class DisconnectPanelUI : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] TextMeshProUGUI messageText;
    [SerializeField] Button reconnectButton;
    [SerializeField] Button leaveButton;

    [Header("Reconnect Yukleme")]
    [SerializeField] GameObject reconnectLoadingOverlay;
    [SerializeField] string reconnectLoadingMessage = "Bağlanıyor...";

    [Tooltip("SampleScene adi (lobi sahnesi).")]
    [SerializeField] string lobbySceneName = "SampleScene";

    private bool _reconnectOverlayCreated;
    private float _reconnectStartTime = -1f;
    private const float ReconnectTimeoutSeconds = 15f;

    private void Awake()
    {
        if (panel != null) panel.SetActive(false);
        if (reconnectButton != null)
            reconnectButton.onClick.AddListener(OnReconnectClicked);
        if (leaveButton != null)
            leaveButton.onClick.AddListener(OnLeaveClicked);
        if (reconnectLoadingOverlay != null)
            reconnectLoadingOverlay.SetActive(false);
    }

    private void Update()
    {
        bool showReconnectLoading = ShouldShowReconnectLoading();
        if (showReconnectLoading)
        {
            if (_reconnectStartTime < 0) _reconnectStartTime = Time.realtimeSinceStartup;

            if (Time.realtimeSinceStartup - _reconnectStartTime > ReconnectTimeoutSeconds)
            {
                if (SteamLobbyManager.Instance != null)
                    SteamLobbyManager.Instance.NotifyReconnectFailed();
                _reconnectStartTime = -1f;
            }
            else
            {
                EnsureReconnectOverlayExists();
                if (reconnectLoadingOverlay != null)
                {
                    reconnectLoadingOverlay.SetActive(true);
                    if (panel != null) panel.SetActive(false);
                }
            }
        }
        else
        {
            _reconnectStartTime = -1f;
            if (reconnectLoadingOverlay != null)
                reconnectLoadingOverlay.SetActive(false);

            if (panel == null) return;

            bool show = false;
            string msg = "";

            if (HostMigrationManager.Instance != null && HostMigrationManager.Instance.IsWaitingForMigration)
            {
                show = true;
                msg = HostMigrationManager.Instance.MessageHostDisconnected;
            }
            else
            {
                string activeScene = SceneManager.GetActiveScene().name;
                bool inLobbyScene = activeScene == lobbySceneName;
                bool notConnected = !NetworkClient.active && !NetworkServer.active;
                bool hasReconnect = SteamLobbyManager.Instance != null && SteamLobbyManager.Instance.HasLastLobbyToReconnect();
                bool reconnectBlocked = SteamLobbyManager.Instance != null && SteamLobbyManager.Instance.IsReconnectBlocked;
                if (inLobbyScene && notConnected && hasReconnect && !reconnectBlocked)
                {
                    show = true;
                    msg = "Oyuna yeniden baglanmak icin Reconnect, tamamen cikmak icin Leave.";
                }
            }

            if (panel.activeSelf != show)
                panel.SetActive(show);
            if (show && messageText != null && !string.IsNullOrEmpty(msg))
                messageText.text = msg;
        }
    }

    private bool ShouldShowReconnectLoading()
    {
        if (SteamLobbyManager.Instance == null || !SteamLobbyManager.Instance.IsReconnectingToGame)
            return false;
        if (!NetworkClient.active || NetworkServer.active)
            return false;
        if (SceneManager.GetActiveScene().name != lobbySceneName)
            return false;
        return true;
    }

    private void EnsureReconnectOverlayExists()
    {
        if (reconnectLoadingOverlay != null || _reconnectOverlayCreated) return;

        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        var root = new GameObject("ReconnectLoadingOverlay");
        root.transform.SetParent(canvas.transform, false);
        root.transform.SetAsLastSibling();

        var rect = root.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        var bg = root.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.1f, 0.15f, 1f);

        var textObj = new GameObject("Message");
        textObj.transform.SetParent(root.transform, false);

        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(600, 80);
        textRect.anchoredPosition = Vector2.zero;

        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = reconnectLoadingMessage;
        tmp.fontSize = 36;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 1f, 1f, 0.95f);

        reconnectLoadingOverlay = root;
        _reconnectOverlayCreated = true;
    }

    private void OnReconnectClicked()
    {
        if (HostMigrationManager.Instance != null && HostMigrationManager.Instance.IsWaitingForMigration)
        {
            HostMigrationManager.Instance.TryReconnectToCurrentHost();
            return;
        }
        SteamLobbyManager.Instance?.TryReconnectToLastLobby();
    }

    private void OnLeaveClicked()
    {
        if (HostMigrationManager.Instance != null && HostMigrationManager.Instance.IsWaitingForMigration)
        {
            HostMigrationManager.Instance.LeaveAndReturnToMenu();
            return;
        }
        SteamLobbyManager.Instance?.LeaveLastGamePermanently();
    }
}
