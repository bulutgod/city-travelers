using System;
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
    private const string PrefLastLobbySavedAtUnix = "LastLobbySavedAtUnix";
    private const long ReconnectDataLifetimeSeconds = 180;

    [Header("Lobi Ayarlar�")]
    [SerializeField] private int maxPlayers = 4;

    public Lobby CurrentLobby { get; private set; }
    private bool _inLobby = false;
    private bool _weJustCreatedLobby = false;

    private bool _waitingForNewHostReconnect = false;
    public System.Action OnHostMigrationFinished;
    public bool InLobby => _inLobby;

    /// <summary>Matchmaking lobisi (LobbyType metadata). Arkadas davet paneli bunu gizlemek icin kullanir.</summary>
    public bool IsCurrentLobbyMatchmaking()
    {
        if (!_inLobby) return false;
        string type = CurrentLobby.GetData("LobbyType");
        return string.Equals(type, "Matchmaking", StringComparison.OrdinalIgnoreCase);
    }

    private bool _isMatchmaking = false;
    public bool IsMatchmaking => _isMatchmaking;

    private bool _reconnectToLastLobbyRequested = false;
    /// <summary>Reconnect akisinda miyiz (sunucu GameScene'de, Ready sahne yuklenince cagrilacak).</summary>
    public bool IsReconnectingToGame => _reconnectToLastLobbyRequested;
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

    private void OnApplicationQuit()
    {
        // Uygulama kapanirken reconnect kaydini "simdi"ye cek.
        // Boylece panel maksimum 3 dakika gecerli kalir.
        TouchLastLobbyTimestamp();
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
        if (!SteamClient.IsValid)
        {
            Debug.LogWarning("[Steam] CreateLobby atlandi: SteamClient.IsValid degil");
            return;
        }

        if (NetworkServer.active || NetworkClient.active)
        {
            Debug.LogWarning($"[Steam] Zaten host/client aktif, CreateLobby atlandi. Server={NetworkServer.active} Client={NetworkClient.active}");
            return;
        }

        if (_inLobby)
        {
            Debug.LogWarning("[Steam] Zaten bir lobidesin!");
            return;
        }

        Debug.Log("[Steam] Lobi olusturuluyor...");

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

    /// <summary>
    /// Matchmaking: Acik lobi ara, bulursa katil, bulamazsa public lobi ac.
    /// </summary>
    public async void FindMatch()
    {
        if (!SteamClient.IsValid)
        {
            Debug.LogWarning("[Steam] FindMatch: SteamClient gecersiz.");
            return;
        }
        if (NetworkServer.active || NetworkClient.active)
        {
            Debug.LogWarning("[Steam] FindMatch: Zaten aktif baglanti var.");
            return;
        }
        if (_inLobby)
        {
            Debug.LogWarning("[Steam] FindMatch: Zaten lobidesin.");
            return;
        }

        _isMatchmaking = true;
        Debug.Log("[Steam] Matchmaking: Acik lobi araniyor...");

        try
        {
            var lobbies = await SteamMatchmaking.LobbyList
                .WithKeyValue("GameName", "RichContractor")
                .WithKeyValue("LobbyType", "Matchmaking")
                .WithSlotsAvailable(1)
                .WithMaxResults(10)
                .RequestAsync();

            if (lobbies != null && lobbies.Length > 0)
            {
                var target = lobbies[0];
                Debug.Log($"[Steam] Matchmaking: Lobi bulundu! ID={target.Id} Uye={target.MemberCount}/{target.MaxMembers}");
                _isMatchmaking = false;
                JoinLobby(target.Id);
                return;
            }

            Debug.Log("[Steam] Matchmaking: Uygun lobi bulunamadi, yeni public lobi olusturuluyor...");
            await CreateMatchmakingLobby();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Steam] Matchmaking hatasi: {ex.Message}");
            _isMatchmaking = false;
        }
    }

    public void CancelMatchmaking()
    {
        if (!_isMatchmaking) return;
        _isMatchmaking = false;
        if (_inLobby)
        {
            LeaveLobby();
            if (NetworkServer.active) GameNetworkManager.Instance?.StopHost();
        }
        Debug.Log("[Steam] Matchmaking iptal edildi.");
    }

    private async Task CreateMatchmakingLobby()
    {
        Lobby? lobby = await SteamMatchmaking.CreateLobbyAsync(maxPlayers);
        if (!lobby.HasValue)
        {
            Debug.LogError("[Steam] Matchmaking lobi olusturulamadi!");
            _isMatchmaking = false;
            return;
        }

        lobby.Value.SetPublic();
        lobby.Value.SetJoinable(true);
        lobby.Value.SetData("HostSteamId", SteamClient.SteamId.ToString());
        lobby.Value.SetData("GameName", "RichContractor");
        lobby.Value.SetData("LobbyType", "Matchmaking");

        Debug.Log($"[Steam] Matchmaking lobi olusturuldu (public). ID: {lobby.Value.Id}");
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

    /// <summary>
    /// Voluntary leave sirasinda reconnect verilerini temizler.
    /// PlayerPrefs'teki lobi/host bilgisi silinir, reconnect bayraklari sifirlanir.
    /// </summary>
    public void ClearReconnectData()
    {
        _reconnectToLastLobbyRequested = false;
        _leaveAfterReconnectRequested = false;
        ClearLastLobbyPrefs();
        Debug.Log("[Steam] Reconnect verileri temizlendi (voluntary leave).");
    }

    public bool HasLastLobbyToReconnect()
    {
        if (!PlayerPrefs.HasKey(PrefLastLobbyId) ||
            !ulong.TryParse(PlayerPrefs.GetString(PrefLastLobbyId, "0"), out ulong lobbyId) ||
            lobbyId == 0)
        {
            return false;
        }

        if (!PlayerPrefs.HasKey(PrefLastLobbySavedAtUnix) ||
            !long.TryParse(PlayerPrefs.GetString(PrefLastLobbySavedAtUnix, "0"), out long savedAtUnix) ||
            savedAtUnix <= 0)
        {
            ClearLastLobbyPrefs();
            return false;
        }

        long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long ageSeconds = Math.Max(0, nowUnix - savedAtUnix);
        if (ageSeconds > ReconnectDataLifetimeSeconds)
        {
            ClearLastLobbyPrefs();
            return false;
        }

        return true;
    }

    /// <summary>Reconnect basarili oldugunda GameNetworkManager tarafindan cagrilir.</summary>
    public static float ReconnectedAtTime { get; private set; } = -1f;

    public void ClearReconnectFlag()
    {
        if (_reconnectToLastLobbyRequested)
            ReconnectedAtTime = Time.realtimeSinceStartup;
        _reconnectToLastLobbyRequested = false;
    }

    private float _reconnectBlockedUntil;

    /// <summary>Reconnect basarisiz (timeout, baglanti koptu). 3 dk boyunca Reconnect paneli gizlenir.</summary>
    public void NotifyReconnectFailed()
    {
        _reconnectBlockedUntil = Time.realtimeSinceStartup + 180f;
        ClearReconnectFlag();
    }

    /// <summary>Reconnect basarisiz oldu, su an cooldown icindeyiz.</summary>
    public bool IsReconnectBlocked => Time.realtimeSinceStartup < _reconnectBlockedUntil;

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
    /// Ana menude "Leave" ile reconnect secenegini kaldir.
    /// Bagli degilsek sadece prefs temizlenir. Bagliysak oyun icinden RequestVoluntaryLeave kullanilmalidir.
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

        // Bagli degiliz - sadece prefs temizle. Lobiye katilip CmdVoluntaryLeave gerekmez.
        Debug.Log("[Steam] Reconnect kaydi temizlendi.");
        ClearLastLobbyPrefs();
    }

    #endregion

    #region Steam Callbacks

    private void OnLobbyCreated(Result result, Lobby lobby)
    {
        if (result != Result.OK)
        {
            Debug.LogError($"[Steam] OnLobbyCreated hatas�: {result}");
            _isMatchmaking = false;
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
        _isMatchmaking = false;
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

        // Migration-aware: Steam lobby owner != Mirror host. HostSteamId metadata karar verir.
        // Eski host reconnect ederse lobby.IsOwnedBy true olur ama HostSteamId yeni host'tir -> StartClient gerekli.
        string hostSteamIdStr = lobby.GetData("HostSteamId");
        if (string.IsNullOrEmpty(hostSteamIdStr))
        {
            Debug.LogError("[Steam] HostSteamId metadata'si okunamadi!");
            return;
        }

        // Reconnect: Eski host geri donuyorsa metadata bazen guncel olmayabilir (HostSteamId hala kendi id'si).
        // Bu durumda Steam lobby owner = yeni host, onu kullan.
        if (_reconnectToLastLobbyRequested && hostSteamIdStr == SteamClient.SteamId.ToString())
        {
            ulong ownerId = lobby.Owner.Id.Value;
            if (ownerId != 0 && ownerId != SteamClient.SteamId.Value)
            {
                hostSteamIdStr = ownerId.ToString();
                Debug.Log($"[Steam] Reconnect: HostSteamId metadata guncel degil, lobby owner kullanildi: {hostSteamIdStr}");
            }
        }

        bool weAreMirrorHost = hostSteamIdStr == SteamClient.SteamId.ToString();

        // LeaveLastGamePermanently: Lobi bos (host biziz), oyun bitti - StartHost yapma, sadece cik
        if (_leaveAfterReconnectRequested && weAreMirrorHost)
        {
            Debug.Log("[Steam] Lobi bos/oyun bitti. Prefs temizleniyor.");
            _reconnectToLastLobbyRequested = false;
            _leaveAfterReconnectRequested = false;
            ClearLastLobbyPrefs();
            LeaveLobby();
            return;
        }

        if (weAreMirrorHost)
        {
            // Metadata'da biz host'uz - StartHost (yeni host migration sonrasi veya nadir edge case)
            GameNetworkManager.Instance.StartHost();
            LobbyUINew.NotifyLobbyJoined();
            StartCoroutine(NotifyLobbyUIAgainAfterDelay());
        }
        else
        {
            // Metadata'da baska biri host - StartClient (normal join veya eski host reconnect)
            GameNetworkManager.Instance.networkAddress = hostSteamIdStr;
            GameNetworkManager.Instance.StartClient();
            Debug.Log($"[Network] Client baslatildi. Host: {hostSteamIdStr}");

            if (_reconnectToLastLobbyRequested && _leaveAfterReconnectRequested)
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
        PlayerPrefs.SetString(PrefLastLobbySavedAtUnix, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
        PlayerPrefs.Save();
    }

    private void TouchLastLobbyTimestamp()
    {
        if (!PlayerPrefs.HasKey(PrefLastLobbyId)) return;
        if (!ulong.TryParse(PlayerPrefs.GetString(PrefLastLobbyId, "0"), out ulong lobbyId) || lobbyId == 0) return;

        ulong hostSteamId = 0;
        string hostFromPrefs = PlayerPrefs.GetString(PrefLastHostSteamId, "0");
        if (!ulong.TryParse(hostFromPrefs, out hostSteamId))
            hostSteamId = 0;
        if (hostSteamId == 0 && SteamClient.IsValid)
            hostSteamId = SteamClient.SteamId.Value;

        SaveLastLobbyPrefs(lobbyId, hostSteamId);
    }

    private void ClearLastLobbyPrefs()
    {
        PlayerPrefs.DeleteKey(PrefLastLobbyId);
        PlayerPrefs.DeleteKey(PrefLastHostSteamId);
        PlayerPrefs.DeleteKey(PrefLastLobbySavedAtUnix);
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