using System.Collections;
using Mirror;
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Host düştüğünde: graceful disconnect, 5 sn bekleme, yeni host seçimi (lobide kalan en düşük SteamId),
/// state restore ve diğer client'ların yeni host'a bağlanması.
/// </summary>
public class HostMigrationManager : MonoBehaviour
{
    public static HostMigrationManager Instance { get; private set; }

    [Tooltip("Host koptuktan sonra migration denemeden önce beklenecek süre (sn).")]
    [SerializeField] float migrationWaitSeconds = 2f;

    [Tooltip("Migration sırasında gösterilecek mesaj (opsiyonel UI).")]
    [SerializeField] string messageHostDisconnected = "Host baglantisi koptu. Yeni host bekleniyor...";

    private bool _waitingForMigration;
    private GameStateSnapshot _savedSnapshot;
    private Coroutine _waitCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        if (transform.parent != null)
            transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (_waitCoroutine != null)
            StopCoroutine(_waitCoroutine);
        if (SteamLobbyManager.Instance != null)
            SteamLobbyManager.Instance.OnHostMigrationFinished -= OnMigrationFinished;
    }

    /// <summary>
    /// GameNetworkManager.OnClientDisconnect'tan cagirilir: baglanti koptu, migration akisini baslat.
    /// </summary>
    public void OnClientDisconnected()
    {
        if (_waitingForMigration) return;
        _waitingForMigration = true;

        try
        {
            var props = PropertyManager.Instance?.spaceOwners;
            _savedSnapshot = GameStateSnapshot.Capture(props);
            if (_savedSnapshot != null && !_savedSnapshot.isValid)
                Debug.Log("[HostMigration] Snapshot alindi ama gecersiz (oyuncu yok). Yeni host yine de acilacak.");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[HostMigration] Snapshot alinamadi: " + e.Message);
        }

        if (SteamLobbyManager.Instance != null)
            SteamLobbyManager.Instance.OnHostMigrationFinished += OnMigrationFinished;

        Debug.Log("[HostMigration] Baglanti koptu. " + migrationWaitSeconds + " sn icinde yeni host secilecek.");
        if (_waitCoroutine != null) StopCoroutine(_waitCoroutine);
        _waitCoroutine = StartCoroutine(WaitThenDecideNewHost());
    }

    private void OnMigrationFinished()
    {
        _waitingForMigration = false;
        _savedSnapshot = null;
        if (_waitCoroutine != null)
        {
            StopCoroutine(_waitCoroutine);
            _waitCoroutine = null;
        }
        if (SteamLobbyManager.Instance != null)
            SteamLobbyManager.Instance.OnHostMigrationFinished -= OnMigrationFinished;
    }

    private IEnumerator WaitThenDecideNewHost()
    {
        yield return new WaitForSecondsRealtime(migrationWaitSeconds);

        _waitCoroutine = null;

        if (!SteamClient.IsValid || SteamLobbyManager.Instance == null)
        {
            Debug.Log("[HostMigration] Steam veya lobi yok. Ana menuye donuluyor.");
            ReturnToMenu();
            yield break;
        }

        if (!SteamLobbyManager.Instance.InLobby)
        {
            Debug.Log("[HostMigration] Lobi yok. Ana menuye donuluyor.");
            ReturnToMenu();
            yield break;
        }

        var lobby = SteamLobbyManager.Instance.CurrentLobby;
        ulong? smallestSteamId = null;
        foreach (var member in lobby.Members)
        {
            if (!member.Id.IsValid) continue;
            ulong id = member.Id.Value;
            if (!smallestSteamId.HasValue || id < smallestSteamId.Value)
                smallestSteamId = id;
        }

        if (!smallestSteamId.HasValue)
        {
            Debug.Log("[HostMigration] Lobide uye yok. Ana menuye donuluyor.");
            ReturnToMenu();
            yield break;
        }

        bool weAreNewHost = SteamClient.SteamId.Value == smallestSteamId.Value;
        if (weAreNewHost)
        {
            Debug.Log("[HostMigration] Bu client yeni host secildi. StartHost + state restore yapiliyor.");
            SteamLobbyManager.Instance.BecomeNewHostAndStartServer(_savedSnapshot);
        }
        else
        {
            Debug.Log("[HostMigration] Yeni host diger bir oyuncu. Lobi metadata degisikligi bekleniyor.");
            SteamLobbyManager.Instance.WaitForNewHostAndReconnect();
        }
    }

    private void ReturnToMenu()
    {
        _waitingForMigration = false;
        if (SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.OnHostMigrationFinished -= OnMigrationFinished;
            SteamLobbyManager.Instance.LeaveLobby();
        }
        if (GameNetworkManager.Instance != null)
            GameNetworkManager.Instance.StopClient();

        string target = GameNetworkManager.singleton != null && !string.IsNullOrEmpty(GameNetworkManager.singleton.offlineScene)
            ? GameNetworkManager.singleton.offlineScene
            : "SampleScene";
        SceneManager.LoadScene(target);
    }

    public bool IsWaitingForMigration => _waitingForMigration;
    public string MessageHostDisconnected => messageHostDisconnected;

    /// <summary>
    /// Baglanti koptugunda "Yeniden baglan" ile mevcut host'a tekrar dene (3 dk icinde).
    /// </summary>
    public void TryReconnectToCurrentHost()
    {
        if (!_waitingForMigration || SteamLobbyManager.Instance == null || !SteamLobbyManager.Instance.InLobby)
            return;
        var lobby = SteamLobbyManager.Instance.CurrentLobby;
        string hostStr = lobby.GetData("HostSteamId");
        if (string.IsNullOrEmpty(hostStr))
            return;
        if (GameNetworkManager.Instance == null) return;
        GameNetworkManager.Instance.StopClient();
        GameNetworkManager.Instance.networkAddress = hostStr;
        GameNetworkManager.Instance.StartClient();
        Debug.Log("[HostMigration] Yeniden baglaniyor: " + hostStr);
    }

    /// <summary>
    /// "Terket" butonu: lobiden cik, menuye don.
    /// </summary>
    public void LeaveAndReturnToMenu()
    {
        ReturnToMenu();
    }
}
