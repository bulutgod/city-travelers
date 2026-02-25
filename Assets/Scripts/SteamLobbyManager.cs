using System.Threading.Tasks;
using Mirror;
using Steamworks;          
using Steamworks.Data;    
using UnityEngine;

public class SteamLobbyManager : MonoBehaviour
{
    public static SteamLobbyManager Instance { get; private set; }

    [Header("Lobi Ayarlarù")]
    [SerializeField] private int maxPlayers = 4;

    public Lobby CurrentLobby { get; private set; }
    private bool _inLobby = false;
    private bool _weJustCreatedLobby = false;

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
        Debug.Log($"[Steam] Giriù yapùldù: {SteamClient.Name} ({SteamClient.SteamId})");
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
                    Debug.Log($"[Steam] Komut satùrùndan lobi bulundu: {lobbyId}");
                    
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

    #region Callback Kayùt
    
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
            Debug.LogWarning("[Steam] Zaten host/client aktif, CreateLobby atlandù.");
            return;
        }

        if (_inLobby)
        {
            Debug.LogWarning("[Steam] Zaten bir lobidesin!");
            return;
        }

        Debug.Log("[Steam] Lobi oluùturuluyor...");

        Lobby? lobby = await SteamMatchmaking.CreateLobbyAsync(maxPlayers);

        if (!lobby.HasValue)
        {
            Debug.LogError("[Steam] Lobi oluùturulamadù!");
            return;
        }

        lobby.Value.SetFriendsOnly();
        // Public yapmak istersen: lobby.Value.SetPublic();

        lobby.Value.SetJoinable(true);

        lobby.Value.SetData("HostSteamId", SteamClient.SteamId.ToString());
        lobby.Value.SetData("GameName", "RichContractor");

        Debug.Log($"[Steam] Lobi oluùturuldu ve metadata yazùldù. ID: {lobby.Value.Id}");
    }

    public async void JoinLobby(SteamId lobbyId)
    {
        Debug.Log($"[Steam] Lobiye katùlùnùyor: {lobbyId}");
        await SteamMatchmaking.JoinLobbyAsync(lobbyId);
    }
 
    public void LeaveLobby()
    {
        if (!_inLobby) return;

        CurrentLobby.Leave();
        _inLobby = false;
        _weJustCreatedLobby = false;
        Debug.Log("[Steam] Lobiden ayrùldù.");
    }

    #endregion

    #region Steam Callbacks

    private void OnLobbyCreated(Result result, Lobby lobby)
    {
        if (result != Result.OK)
        {
            Debug.LogError($"[Steam] OnLobbyCreated hatasù: {result}");
            return;
        }

        CurrentLobby = lobby;
        _inLobby = true;
        _weJustCreatedLobby = true;
        Debug.Log($"[Steam] OnLobbyCreated tetiklendi. Lobi ID: {lobby.Id}");

        GameNetworkManager.Instance.StartHost();
        LobbyUINew.NotifyLobbyJoined();
    }

    private void OnLobbyEntered(Lobby lobby)
    {
        CurrentLobby = lobby;
        _inLobby = true;
        Debug.Log($"[Steam] Lobiye girildi: {lobby.Id}");

        // Host taraf?nda zaten lobi olu?turma ak???nda UI aù?l?yor.
        // Yine de gùvenli olmas? iùin yeni lobi arayùzùnù tetikle.
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
            Debug.LogError("[Steam] HostSteamId metadata'sù okunamadù!");
            return;
        }

        GameNetworkManager.Instance.networkAddress = hostSteamIdStr;
        GameNetworkManager.Instance.StartClient();

        Debug.Log($"[Network] Client baùlatùldù. Host: {hostSteamIdStr}");
    }

    private void OnGameLobbyJoinRequested(Lobby lobby, SteamId friendSteamId)
    {
        Debug.Log($"[Steam] Lobiye katùlma isteùi: {lobby.Id}, Arkadaù: {friendSteamId}");

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