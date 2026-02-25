using System.Threading.Tasks;
using Mirror;
using Steamworks;          
using Steamworks.Data;    
using UnityEngine;

public class SteamLobbyManager : MonoBehaviour
{
    public static SteamLobbyManager Instance { get; private set; }

    [Header("Lobi Ayarlarý")]
    [SerializeField] private int maxPlayers = 4;

    public Lobby CurrentLobby { get; private set; }
    private bool _inLobby = false;

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (!SteamClient.IsValid) return;

        Steamworks.SteamNetworkingUtils.InitRelayNetworkAccess();

        RegisterCallbacks();
        Debug.Log($"[Steam] Giriþ yapýldý: {SteamClient.Name} ({SteamClient.SteamId})");
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
                    Debug.Log($"[Steam] Komut satýrýndan lobi bulundu: {lobbyId}");
                    
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

    #region Callback Kayýt
    
    private void RegisterCallbacks()
    {
        SteamMatchmaking.OnLobbyCreated += OnLobbyCreated;

        SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;

        SteamMatchmaking.OnLobbyGameCreated += OnLobbyGameCreated;

        SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequested;
    }

    private void UnregisterCallbacks()
    {
        SteamMatchmaking.OnLobbyCreated -= OnLobbyCreated;
        SteamMatchmaking.OnLobbyEntered -= OnLobbyEntered;
        SteamMatchmaking.OnLobbyGameCreated -= OnLobbyGameCreated;
        SteamFriends.OnGameLobbyJoinRequested -= OnGameLobbyJoinRequested;
    }

    #endregion

    #region Public API

    public async void CreateLobby()
    {
        if (!SteamClient.IsValid) return;

        if (NetworkServer.active || NetworkClient.active)
        {
            Debug.LogWarning("[Steam] Zaten host/client aktif, CreateLobby atlandý.");
            return;
        }

        if (_inLobby)
        {
            Debug.LogWarning("[Steam] Zaten bir lobidesin!");
            return;
        }

        Debug.Log("[Steam] Lobi oluþturuluyor...");

        Lobby? lobby = await SteamMatchmaking.CreateLobbyAsync(maxPlayers);

        if (!lobby.HasValue)
        {
            Debug.LogError("[Steam] Lobi oluþturulamadý!");
            return;
        }

        lobby.Value.SetFriendsOnly();
        // Public yapmak istersen: lobby.Value.SetPublic();

        lobby.Value.SetJoinable(true);

        lobby.Value.SetData("HostSteamId", SteamClient.SteamId.ToString());
        lobby.Value.SetData("GameName", "RichContractor");

        Debug.Log($"[Steam] Lobi oluþturuldu ve metadata yazýldý. ID: {lobby.Value.Id}");
    }

    public async void JoinLobby(SteamId lobbyId)
    {
        Debug.Log($"[Steam] Lobiye katýlýnýyor: {lobbyId}");
        await SteamMatchmaking.JoinLobbyAsync(lobbyId);
    }
 
    public void LeaveLobby()
    {
        if (!_inLobby) return;

        CurrentLobby.Leave();
        _inLobby = false;
        Debug.Log("[Steam] Lobiden ayrýldý.");
    }

    #endregion

    #region Steam Callbacks

    private void OnLobbyCreated(Result result, Lobby lobby)
    {
        if (result != Result.OK)
        {
            Debug.LogError($"[Steam] OnLobbyCreated hatasý: {result}");
            return;
        }

        CurrentLobby = lobby;
        _inLobby = true;
        Debug.Log($"[Steam] OnLobbyCreated tetiklendi. Lobi ID: {lobby.Id}");

        GameNetworkManager.Instance.StartHost();
        LobbyUI.NotifyLobbyJoined();
    }

    private void OnLobbyEntered(Lobby lobby)
    {
        CurrentLobby = lobby;
        _inLobby = true;
        Debug.Log($"[Steam] Lobiye girildi: {lobby.Id}");

        if (NetworkServer.active)
        {
            LobbyUI.NotifyLobbyJoined();
            return;
        }

        string hostSteamIdStr = lobby.GetData("HostSteamId");

        if (string.IsNullOrEmpty(hostSteamIdStr))
        {
            Debug.LogError("[Steam] HostSteamId metadata'sý okunamadý!");
            return;
        }

        GameNetworkManager.Instance.networkAddress = hostSteamIdStr;
        GameNetworkManager.Instance.StartClient();

        Debug.Log($"[Network] Client baþlatýldý. Host: {hostSteamIdStr}");
    }

    private void OnGameLobbyJoinRequested(Lobby lobby, SteamId friendSteamId)
    {
        Debug.Log($"[Steam] Lobiye katýlma isteði: {lobby.Id}, Arkadaþ: {friendSteamId}");

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

    #endregion
}