using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Mirror;
using TMPro;

/// <summary>
/// "Yeniden baglan" ve "Terket" butonlari. SampleScene'e ekleyin; panel tum sahnelerde gosterilebilsin diye DontDestroyOnLoad ile kalir.
/// - GameScene'de baglanti koptugunda: migration mesaji + Yeniden baglan / Terket.
/// - SampleScene'de lobideyken oyuna bagli degilken: Oyuna baglan veya lobiden cik.
/// </summary>
public class DisconnectPanelUI : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] TextMeshProUGUI messageText;
    [SerializeField] Button reconnectButton;
    [SerializeField] Button leaveButton;

    [Tooltip("SampleScene adi (lobi sahnesi).")]
    [SerializeField] string lobbySceneName = "SampleScene";

    private void Awake()
    {
        if (panel != null) panel.SetActive(false);
        if (reconnectButton != null)
            reconnectButton.onClick.AddListener(OnReconnectClicked);
        if (leaveButton != null)
            leaveButton.onClick.AddListener(OnLeaveClicked);
    }

    private void Update()
    {
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
            if (inLobbyScene && notConnected && hasReconnect)
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
