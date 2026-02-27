using System.Collections;
using System.Threading.Tasks;
using Mirror;
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SteamLobbyManager : MonoBehaviour
{
    public static SteamLobbyManager Instance { get; private set; }

    private const string PrefLastLobbyId = "LastLobbyId";
    private const string PrefLastHostSteamId = "LastHostSteamId";

    [Header("Lobi Ayarlar�")]
    [SerializeField] private int maxPlayers = 4;

    public Lobby CurrentLobby { get; private set; }
    private bool _inLobby = false;
    private bool _weJustCreatedLobby = false;

    private bool _waitingForNewHostReconnect = false;
    public System.Action OnHostMigrationFinished;
    public bool InLobby => _inLobby;

    // SampleScene'den (oyun yeniden acildiginda) kullanilir
    private bool _reconnectToLastLobbyRequested = false;
    private bool _leaveAfterReconnectRequested = false;

    #region Unity Lifecycle

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

    private void Start()
    {
        if (!SteamClient.IsValid) return;

        Steamworks.SteamNetworkingUtils.InitRelayNetworkAccess();

        RegisterCallbacks();
        Debug.Log($"[Steam] Giri� yap�ld�: {SteamClient.Name} ({SteamClient.SteamId})");
        CheckCommandLineLobbyJoin();
    }

    private void CheckCommandLineLobbyJoin()
    {
        string[] args = System.Environment.GetCommandLineArgs();

        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "+connect_lobby")
            {
                if (ulong.TryParse(args[i + 1], out ulong lobbyId))
                {
                    Debug.Log($"[Steam] Komut sat�r�ndan lobi bulundu: {lobbyId}");
                    
                    StartCoroutine(JoinLobbyDelayed(lobbyId));
                }
                break;
            }
        }
    }

    private System.Collections.IEnumerator JoinLobbyDelayed(ulong lobbyId)
    {
      
        yield return null;
        JoinLobby(new Steamworks.SteamId { Value = lobbyId });
    }
    private void OnDestroy()
    {
        UnregisterCallbacks();
    }

    #endregion

    #region Callback Kay�t
    
    private void RegisterCallbacks()
    {
        SteamMatchmaking.OnLobbyCreated += OnLobbyCreated;
        SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
        SteamMatchmaking.OnLobbyGameCreated += OnLobbyGameCreated;
        SteamMatchmaking.OnLobbyDataChanged += OnLobbyDataChanged;
        SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequested;
    }

    private void UnregisterCallbacks()
    {
        SteamMatchmaking.OnLobbyCreated -= OnLobbyCreated;
        SteamMatchmaking.OnLobbyEntered -= OnLobbyEntered;
        SteamMatchmaking.OnLobbyGameCreated -= OnLobbyGameCreated;
        SteamMatchmaking.OnLobbyDataChanged -= OnLobbyDataChanged;
        SteamFriends.OnGameLobbyJoinRequested -= OnGameLobbyJoinRequested;
    }

    #endregion

    #region Public API

    public async void CreateLobby()
    {
        if (!SteamClient.IsValid) return;

        if (NetworkServer.active || NetworkClient.active)
        {
            Debug.LogWarning("[Steam] Zaten host/client aktif, CreateLobby atland�.");
            return;
        }

        if (_inLobby)
        {
            Debug.LogWarning("[Steam] Zaten bir lobidesin!");
            return;
        }

        Debug.Log("[Steam] Lobi olu�turuluyor...");

        Lobby? lobby = await SteamMatchmaking.CreateLobbyAsync(maxPlayers);

        if (!lobby.HasValue)
        {
            Debug.LogError("[Steam] Lobi olu�turulamad�!");
            return;
        }

        lobby.Value.SetFriendsOnly();
        // Public yapmak istersen: lobby.Value.SetPublic();

        lobby.Value.SetJoinable(true);

        lobby.Value.SetData("HostSteamId", SteamClient.SteamId.ToString());
        lobby.Value.SetData("GameName", "RichContractor");

        Debug.Log($"[Steam] Lobi olu�turuldu ve metadata yaz�ld�. ID: {lobby.Value.Id}");
    }

    public async void JoinLobby(SteamId lobbyId)
    {
        Debug.Log($"[Steam] Lobiye kat�l�n�yor: {lobbyId}");
        await SteamMatchmaking.JoinLobbyAsync(lobbyId);
    }
 
    public void LeaveLobby()
    {
        if (!_inLobby) return;

        CurrentLobby.Leave();
        _inLobby = false;
        _weJustCreatedLobby = false;
        Debug.Log("[Steam] Lobiden ayr�ld�.");
    }

    public bool HasLastLobbyToReconnect()
    {
        return PlayerPrefs.HasKey(PrefLastLobbyId) && ulong.TryParse(PlayerPrefs.GetString(PrefLastLobbyId, "0"), out ulong id) && id != 0;
    }

    public void TryReconnectToLastLobby()
    {
        if (!SteamClient.IsValid) return;
        if (!HasLastLobbyToReconnect())
        {
            Debug.LogWarning("[Steam] Reconnect icin kayitli lobi yok.");
            return;
        }
        if (NetworkServer.active || NetworkClient.active)
        {
            Debug.LogWarning("[Steam] Zaten host/client aktif, reconnect atlandi.");
            return;
        }

        _reconnectToLastLobbyRequested = true;
        _leaveAfterReconnectRequested = false;

        ulong lobbyId = ulong.Parse(PlayerPrefs.GetString(PrefLastLobbyId, "0"));
        Debug.Log("[Steam] Kayitli lobiye yeniden katiliniyor: " + lobbyId);
        JoinLobby(new SteamId { Value = lobbyId });
    }

    /// <summary>
    /// Oyunu yeniden actiktan sonra "Leave" ile bu oyundan cikmak:
    /// Lobiye katil -> host'a baglan -> CmdVoluntaryLeave gonder -> cik.
    /// Boylece sunucu state'i hemen siler ve diger oyuncular lobby listesinde degisikligi gorur.
    /// </summary>
    public void LeaveLastGamePermanently()
    {
        if (!SteamClient.IsValid) return;
        if (!HasLastLobbyToReconnect())
        {
            ClearLastLobbyPrefs();
            return;
        }
        if (NetworkServer.active || NetworkClient.active)
        {
            Debug.LogWarning("[Steam] Zaten bagli. Oyun icinden cikis icin GameNetworkManager.RequestVoluntaryLeaveAndReturnToMenu kullanin.");
            return;
        }

        _reconnectToLastLobbyRequested = true;
        _leaveAfterReconnectRequested = true;

        ulong lobbyId = ulong.Parse(PlayerPrefs.GetString(PrefLastLobbyId, "0"));
        Debug.Log("[Steam] Bu oyundan cikis icin lobiye katiliniyor: " + lobbyId);
        JoinLobby(new SteamId { Value = lobbyId });
    }

    #endregion

    #region Steam Callbacks

    private void OnLobbyCreated(Result result, Lobby lobby)
    {
        if (result != Result.OK)
        {
            Debug.LogError($"[Steam] OnLobbyCreated hatas�: {result}");
            return;
        }

        CurrentLobby = lobby;
        _inLobby = true;
        _weJustCreatedLobby = true;
        Debug.Log($"[Steam] OnLobbyCreated tetiklendi. Lobi ID: {lobby.Id}");

        SaveLastLobbyPrefs(lobby.Id.Value, SteamClient.SteamId.Value);

        GameNetworkManager.Instance.StartHost();
        LobbyUINew.NotifyLobbyJoined();
        StartCoroutine(NotifyLobbyUIAgainAfterDelay());
    }

    private IEnumerator NotifyLobbyUIAgainAfterDelay()
    {
        yield return new WaitForSeconds(0.3f);
        LobbyUINew.NotifyLobbyJoined();
        yield return new WaitForSeconds(0.5f);
        LobbyUINew.NotifyLobbyJoined();
    }

    private void OnLobbyEntered(Lobby lobby)
    {
        CurrentLobby = lobby;
        _inLobby = true;
        Debug.Log($"[Steam] Lobiye girildi: {lobby.Id}");

        // Build'de uygulama yeniden acildiginda SampleScene'den reconnect icin gerekli.
        ulong hostFromData = 0;
        ulong.TryParse(lobby.GetData("HostSteamId"), out hostFromData);
        SaveLastLobbyPrefs(lobby.Id.Value, hostFromData);

        // Host taraf?nda zaten lobi olu?turma ak???nda UI a�?l?yor.
        // Yine de g�venli olmas? i�in yeni lobi aray�z�n� tetikle.
        // Lobi sahibi bizsek host'uz; kendimize client ile baglanmayalim (You can't connect to yourself).
        if (_weJustCreatedLobby)
        {
            _weJustCreatedLobby = false;
            LobbyUINew.NotifyLobbyJoined();
            return;
        }
        if (NetworkServer.active)
        {
            LobbyUINew.NotifyLobbyJoined();
            return;
        }
        if (lobby.IsOwnedBy(SteamClient.SteamId))
        {
            LobbyUINew.NotifyLobbyJoined();
            return;
        }

        string hostSteamIdStr = lobby.GetData("HostSteamId");

        if (string.IsNullOrEmpty(hostSteamIdStr))
        {
            Debug.LogError("[Steam] HostSteamId metadata's� okunamad�!");
            return;
        }

        GameNetworkManager.Instance.networkAddress = hostSteamIdStr;
        GameNetworkManager.Instance.StartClient();

        Debug.Log($"[Network] Client ba�lat�ld�. Host: {hostSteamIdStr}");

        // SampleScene'den "Leave" istendiyse baglanir baglanmaz istegi gonderip cik.
        if (_reconnectToLastLobbyRequested && _leaveAfterReconnectRequested)
        {
            StartCoroutine(LeaveAfterReconnectFlow());
        }
    }

    private void OnGameLobbyJoinRequested(Lobby lobby, SteamId friendSteamId)
    {
        Debug.Log($"[Steam] Lobiye kat�lma iste�i: {lobby.Id}, Arkada�: {friendSteamId}");

        if (NetworkServer.active || NetworkClient.active)
        {
            GameNetworkManager.Instance.StopHost();
            LeaveLobby();
        }
        JoinLobby(lobby.Id);
    }

    private void OnLobbyGameCreated(Lobby lobby, uint ip, ushort port, SteamId serverId)
    {
        Debug.Log($"[Steam] OnLobbyGameCreated: ServerID={serverId}");
    }

    private void OnLobbyDataChanged(Lobby lobby)
    {
        // Host degisikligini kaydet (reconnect butonu icin)
        if (_inLobby)
        {
            string hostSteamId = lobby.GetData("HostSteamId");
            if (!string.IsNullOrEmpty(hostSteamId))
            {
                PlayerPrefs.SetString(PrefLastHostSteamId, hostSteamId);
                PlayerPrefs.Save();
            }
        }

        if (!_waitingForNewHostReconnect) return;
        string hostSteamIdStr = lobby.GetData("HostSteamId");
        if (string.IsNullOrEmpty(hostSteamIdStr)) return;
        if (hostSteamIdStr == SteamClient.SteamId.ToString()) return;
        _waitingForNewHostReconnect = false;
        Debug.Log("[Steam] Yeni host metadata'si alindi. Baglaniyor: " + hostSteamIdStr);
        GameNetworkManager.Instance.networkAddress = hostSteamIdStr;
        GameNetworkManager.Instance.StartClient();
        OnHostMigrationFinished?.Invoke();
    }

    public void BecomeNewHostAndStartServer(GameStateSnapshot snapshot)
    {
        if (NetworkServer.active || NetworkClient.active)
        {
            OnHostMigrationFinished?.Invoke();
            return;
        }
        if (!_inLobby || CurrentLobby.Id == 0)
        {
            OnHostMigrationFinished?.Invoke();
            return;
        }
        GameNetworkManager.Instance.SetMigrationSnapshot(snapshot);
        CurrentLobby.SetData("HostSteamId", SteamClient.SteamId.ToString());
        SaveLastLobbyPrefs(CurrentLobby.Id.Value, SteamClient.SteamId.Value);
        GameNetworkManager.Instance.StartHost();
        OnHostMigrationFinished?.Invoke();
        LobbyUINew.NotifyLobbyJoined();
        StartCoroutine(NotifyLobbyUIAgainAfterDelay());
        StartCoroutine(ChangeToGameSceneAfterHostReady());
    }

    private IEnumerator ChangeToGameSceneAfterHostReady()
    {
        yield return new WaitForSeconds(0.5f);
        if (!NetworkServer.active || GameNetworkManager.Instance == null) yield break;
        string sceneName = GameNetworkManager.Instance.GameSceneName;
        if (string.IsNullOrEmpty(sceneName)) yield break;
        // Zaten oyun sahnesindeysek tekrar yukleme (state restore kaybolur).
        if (SceneManager.GetActiveScene().name == sceneName)
            yield break;
        NetworkManager.singleton.ServerChangeScene(sceneName);
    }

    public void WaitForNewHostAndReconnect()
    {
        _waitingForNewHostReconnect = true;
    }

    private void SaveLastLobbyPrefs(ulong lobbyId, ulong hostSteamId)
    {
        if (lobbyId == 0) return;
        PlayerPrefs.SetString(PrefLastLobbyId, lobbyId.ToString());
        if (hostSteamId != 0) PlayerPrefs.SetString(PrefLastHostSteamId, hostSteamId.ToString());
        PlayerPrefs.Save();
    }

    private void ClearLastLobbyPrefs()
    {
        PlayerPrefs.DeleteKey(PrefLastLobbyId);
        PlayerPrefs.DeleteKey(PrefLastHostSteamId);
        PlayerPrefs.Save();
    }

    private IEnumerator LeaveAfterReconnectFlow()
    {
        // Local player spawn olana kadar bekle, sonra istemli ayrilis bildir.
        float timeout = Time.time + 8f;
        while (Time.time < timeout)
        {
            if (NetworkClient.active && NetworkClient.localPlayer != null)
                break;
            yield return null;
        }

        if (GameNetworkManager.Instance != null)
            GameNetworkManager.Instance.RequestVoluntaryLeaveAndReturnToMenu();

        _reconnectToLastLobbyRequested = false;
        _leaveAfterReconnectRequested = false;
        ClearLastLobbyPrefs();
    }

    #endregion
}