using System.Collections.Generic;
using Mirror;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameNetworkManager : NetworkManager
{
    public static GameNetworkManager Instance => singleton as GameNetworkManager;

    [Tooltip("Lobiden 'Oyunu Baslat' ile gidilecek sahne adi (Build Settings'te olmali).")]
    [SerializeField] string gameSceneName = "GameScene";
    public string GameSceneName => gameSceneName;
    [Tooltip("Baglanti kopunca donulecek lobi/menu sahnesi.")]
    [SerializeField] string fallbackLobbySceneName = "SampleScene";
    [Tooltip("Kopan oyuncunun ayni slota donebilmesi icin tutulacak sure (sn).")]
    [SerializeField] float reconnectGraceSeconds = 180f;

    private readonly List<PlayerObject> _connectedPlayers = new List<PlayerObject>();
    private readonly List<PlayerObject> _botPlayers = new List<PlayerObject>();
    private readonly Dictionary<ulong, PersistedPlayerState> _disconnectedStates = new Dictionary<ulong, PersistedPlayerState>();
    private readonly HashSet<ulong> _allowedPlayerSteamIds = new HashSet<ulong>();
    private readonly HashSet<ulong> _voluntaryLeaveSteamIds = new HashSet<ulong>();

    private GameStateSnapshot _migrationSnapshot;
    private bool _isVoluntaryLeave;

    private struct PersistedPlayerState
    {
        public int playerIndex;
        public int currentSpaceIndex;
        public int selectedCharacterIndex;
        public int selectedDiceIndex;
        public int money;
        public float disconnectedAt;
        public string steamName;
        public bool hasPassedStart;
        public bool isInJail;
    }

    private struct PendingBotSpawn
    {
        public int playerIndex;
        public string steamName;
        public int selectedCharacterIndex;
        public int selectedDiceIndex;
    }

    private readonly List<PendingBotSpawn> _pendingBotsForGameScene = new List<PendingBotSpawn>();

    [Tooltip("Lobide host'un sectigi oyun suresi (sn). Oyun sahnesi acilinca GameTurnManager'a uygulanir.")]
    private float _pendingGameDurationSeconds;

    #region Mirror Lifecycle

    public override void Awake()
    {
        if (transform.parent != null)
            transform.SetParent(null);
        base.Awake();
    }

    public override void OnStartHost()
    {
        base.OnStartHost();
        Debug.Log("[Network] Host başlatıldı.");
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        Debug.Log("[Network] Sunucuya bağlanılıyor...");
    }

    public override void OnClientConnect()
    {
        // Sadece RECONNECT (sunucu GameScene'de) icin Ready() ertele - SceneMessage gelecek, sahne yuklenince
        // OnClientSceneChanged'da Ready() cagrilacak. Normal lobby join'de (sunucu SampleScene'de) hemen Ready() + AddPlayer.
        bool isReconnectToGame = SteamLobbyManager.Instance != null && SteamLobbyManager.Instance.IsReconnectingToGame;
        string lobbyScene = string.IsNullOrEmpty(offlineScene) ? fallbackLobbySceneName : offlineScene;
        bool inLobbyScene = !string.IsNullOrEmpty(lobbyScene) && SceneManager.GetActiveScene().name == lobbyScene;
        bool isClientOnly = NetworkClient.active && !NetworkServer.active;

        if (inLobbyScene && isClientOnly && isReconnectToGame)
        {
            Debug.Log("[Network] Sunucuya baglandi. GameScene yukleniyor...");
            StartCoroutine(ReconnectSceneFallback());
        }
        else
        {
            base.OnClientConnect();
            if (!NetworkClient.ready) NetworkClient.Ready();
        }

        LobbyUINew.NotifyLobbyJoined();
    }

    private System.Collections.IEnumerator ReconnectSceneFallback()
    {
        string lobbyScene = string.IsNullOrEmpty(offlineScene) ? fallbackLobbySceneName : offlineScene;
        string gameScene = !string.IsNullOrEmpty(gameSceneName) ? gameSceneName : "GameScene";
        float timeout = Time.realtimeSinceStartup + 2.5f;

        while (Time.realtimeSinceStartup < timeout && NetworkClient.active)
        {
            string active = SceneManager.GetActiveScene().name;
            if (active == gameScene)
                yield break;
            yield return null;
        }

        if (!NetworkClient.active) yield break;

        string stillActive = SceneManager.GetActiveScene().name;
        if (stillActive == lobbyScene)
        {
            Debug.Log("[Network] SceneMessage gelmedi veya sahne yuklenmedi. Manuel GameScene yukleniyor...");
            yield return SceneManager.LoadSceneAsync(gameScene);
            if (NetworkClient.connection != null && NetworkClient.connection.isAuthenticated)
            {
                if (!NetworkClient.ready) NetworkClient.Ready();
                if (autoCreatePlayer && NetworkClient.localPlayer == null)
                    NetworkClient.AddPlayer();
            }
            if (SteamLobbyManager.Instance != null)
                SteamLobbyManager.Instance.ClearReconnectFlag();
        }
    }

    public override void OnClientSceneChanged()
    {
        base.OnClientSceneChanged();
        // Reconnect basarili - GameScene yuklendi, flag temizle
        if (SteamLobbyManager.Instance != null && SteamLobbyManager.Instance.IsReconnectingToGame)
        {
            string active = SceneManager.GetActiveScene().name;
            string gameScene = !string.IsNullOrEmpty(gameSceneName) ? gameSceneName : "GameScene";
            if (active == gameScene)
                SteamLobbyManager.Instance.ClearReconnectFlag();
        }
    }

    public override void OnClientDisconnect()
    {
        try { base.OnClientDisconnect(); } catch (System.Exception ex) { Debug.LogWarning("[Network] OnClientDisconnect base: " + ex.Message); }
        Debug.Log("[Network] Baglanti kesildi.");

        // Voluntary leave: LeaveAfterDelay handles scene transition
        if (_isVoluntaryLeave)
        {
            _isVoluntaryLeave = false;
            return;
        }

        // Reconnect denemesi basarisiz (lobi kapali, host yok vb.) - 3 dk cooldown
        if (SteamLobbyManager.Instance != null && SteamLobbyManager.Instance.IsReconnectingToGame)
            SteamLobbyManager.Instance.NotifyReconnectFailed();

        bool isInGameScene = false;
        try
        {
            string active = SceneManager.GetActiveScene().name;
            string gameScene = !string.IsNullOrEmpty(gameSceneName) ? gameSceneName : "GameScene";
            isInGameScene = active == gameScene;
        }
        catch { }

        if (isInGameScene && HostMigrationManager.Instance != null)
        {
            HostMigrationManager.Instance.OnClientDisconnected();
            return;
        }

        try { SteamLobbyManager.Instance?.LeaveLobby(); } catch { }
        string targetScene = !string.IsNullOrWhiteSpace(offlineScene) ? offlineScene : fallbackLobbySceneName;
        if (!string.IsNullOrWhiteSpace(targetScene))
        {
            try
            {
                string active = SceneManager.GetActiveScene().name;
                if (active != targetScene)
                    SceneManager.LoadScene(targetScene);
            }
            catch (System.Exception ex) { Debug.LogWarning("[Network] Scene load: " + ex.Message); }
        }
    }

    /// <summary>
    /// Oyundayken "Terket" / "Oyundan Cik" butonundan cagirilir.
    /// Once sunucuya istekli ayrilisi bildirir (3 dk state tutulmaz), sonra baglantiyi kesip menuye doner.
    /// </summary>
    public void RequestVoluntaryLeaveAndReturnToMenu()
    {
        if (!NetworkClient.active) return;
        _isVoluntaryLeave = true;
        var localPlayer = NetworkClient.localPlayer?.GetComponent<PlayerObject>();
        if (localPlayer != null)
        {
            ulong id = localPlayer.steamId != 0 ? localPlayer.steamId : (SteamClient.IsValid ? SteamClient.SteamId.Value : 0);
            if (NetworkServer.active && id != 0)
                ServerMarkVoluntaryLeave(id);
            else
                localPlayer.CmdVoluntaryLeave();
        }
        StartCoroutine(LeaveAfterDelay());
    }

    private System.Collections.IEnumerator LeaveAfterDelay()
    {
        yield return new WaitForSeconds(0.25f);
        try { SteamLobbyManager.Instance?.LeaveLobby(); } catch { }
        try { SteamLobbyManager.Instance?.ClearReconnectData(); } catch { }

        string targetScene = !string.IsNullOrWhiteSpace(offlineScene) ? offlineScene : fallbackLobbySceneName;

        if (NetworkServer.active)
            StopHost();
        else
            StopClient();

        // StopHost/StopClient moves the NetworkManager out of DDOL when offlineScene is set.
        // Ensure it's out of DDOL so it gets destroyed with the old scene.
        if (gameObject != null && gameObject.scene.name == "DontDestroyOnLoad")
            SceneManager.MoveGameObjectToScene(gameObject, SceneManager.GetActiveScene());

        if (!string.IsNullOrWhiteSpace(targetScene))
        {
            Debug.Log($"[Network] Voluntary leave -> {targetScene}");
            SceneManager.LoadScene(targetScene);
        }
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        Debug.Log($"[Network] OnServerAddPlayer. ConnId:{conn.connectionId} " +
                  $"Address:{conn.address}");

        // Ayni connection icin ikinci kez player olusturma (scene gecislerinde gelebilir).
        if (conn.identity != null)
        {
            Debug.LogWarning($"[Network] ConnId:{conn.connectionId} zaten bir player'a sahip. Tekrar spawn atlandi.");
            return;
        }

        _connectedPlayers.RemoveAll(p => p == null);
        CleanupExpiredReconnectStates();

        ulong incomingSteamId = ResolveSteamId(conn);
        PersistedPlayerState restored = default;
        string activeScene = SceneManager.GetActiveScene().name;
        bool isGameScene = activeScene == (string.IsNullOrEmpty(gameSceneName) ? "GameScene" : gameSceneName);

        // Oyun sahnesi acilmissa:
        // - Lobi baslangicinda oyunda olan oyuncular (_allowedPlayerSteamIds) her zaman kabul edilir.
        // - Bu sette olmayan bir oyuncu sadece daha once bu oyundan dusmus ve 3 dk icinde
        //   geri geliyorsa (_disconnectedStates'te varsa) kabul edilir.
        // - Aksi halde (yeni davet edilen, hic oyunda olmamis oyuncu) reddedilir.
        if (isGameScene && _allowedPlayerSteamIds.Count > 0)
        {
            bool wasInitial = _allowedPlayerSteamIds.Contains(incomingSteamId);
            bool hasDisconnectedState = _disconnectedStates.TryGetValue(incomingSteamId, out restored);

            if (!wasInitial && !hasDisconnectedState)
            {
                Debug.LogWarning($"[Network] Oyuna giris reddedildi: SteamId {incomingSteamId} bu oyunun parcası degil.");
                conn.Disconnect();
                return;
            }
        }

        if (_migrationSnapshot != null && _migrationSnapshot.isValid)
            PrefillDisconnectedStatesFromSnapshot(conn);

        bool hasRestoredState2 = incomingSteamId != 0 &&
                                _disconnectedStates.TryGetValue(incomingSteamId, out restored);

        var bot = hasRestoredState2 ? GetBotByIndex(restored.playerIndex) : null;

        GameObject playerGo = Instantiate(playerPrefab);
        NetworkServer.AddPlayerForConnection(conn, playerGo);

        PlayerObject player = playerGo.GetComponent<PlayerObject>();
        if (player != null)
        {
            if (hasRestoredState2)
            {
                int restoredIndex = IsIndexInUse(restored.playerIndex) ? GetNextPlayerIndex() : restored.playerIndex;
                player.playerIndex = restoredIndex;
                if (bot != null)
                {
                    player.currentSpaceIndex = bot.currentSpaceIndex;
                    player.selectedCharacterIndex = bot.selectedCharacterIndex;
                    player.selectedDiceIndex = bot.selectedDiceIndex;
                    player.money = bot.money;
                    player.hasPassedStart = bot.hasPassedStart;
                    player.isInJail = bot.isInJail;
                    Debug.Log($"[Network] Reconnect restore BOT state -> Space:{bot.currentSpaceIndex} Money:{bot.money}");
                }
                else
                {
                    player.currentSpaceIndex = restored.currentSpaceIndex;
                    player.selectedCharacterIndex = restored.selectedCharacterIndex;
                    player.selectedDiceIndex = restored.selectedDiceIndex;
                    player.money = restored.money;
                    player.hasPassedStart = restored.hasPassedStart;
                    player.isInJail = restored.isInJail;
                }
                if (!string.IsNullOrWhiteSpace(restored.steamName))
                    player.steamName = restored.steamName;
                if (incomingSteamId != 0)
                    player.steamId = incomingSteamId;
                _disconnectedStates.Remove(incomingSteamId);

                if (bot != null)
                {
                    NetworkServer.Destroy(bot.gameObject);
                    _botPlayers.Remove(bot);
                    Debug.Log($"[Network] Bot kaldirildi, oyuncu geri donuyor: P{restored.playerIndex}");
                }

                Debug.Log($"[Network] Reconnect restore -> SteamId:{incomingSteamId} Index:{player.playerIndex} Space:{player.currentSpaceIndex} Money:{player.money}");
            }
            else if (_migrationSnapshot != null && _migrationSnapshot.isValid)
            {
                var entry = FindSnapshotEntryBySteamId(incomingSteamId);
                if (entry != null)
                {
                    int idx = IsIndexInUse(entry.playerIndex) ? GetNextPlayerIndex() : entry.playerIndex;
                    player.playerIndex = idx;
                    player.currentSpaceIndex = entry.currentSpaceIndex;
                    player.selectedCharacterIndex = entry.selectedCharacterIndex;
                    player.selectedDiceIndex = entry.selectedDiceIndex;
                    player.money = entry.money;
                    player.hasPassedStart = entry.hasPassedStart;
                    player.isInJail = entry.isInJail;
                    if (!string.IsNullOrWhiteSpace(entry.steamName)) player.steamName = entry.steamName;
                    if (entry.steamId != 0) player.steamId = entry.steamId;
                    Debug.Log($"[Network] Migration restore host player -> Index:{player.playerIndex}");
                }
                else
                {
                    player.playerIndex = GetNextPlayerIndex();
                }
            }
            else
            {
                player.playerIndex = GetNextPlayerIndex();
            }

            _connectedPlayers.Add(player);

            Debug.Log($"[Network] Spawn edildi → " +
                      $"Index:{player.playerIndex} " +
                      $"Name:{player.steamName} " +
                      $"SteamId:{player.steamId}");

            LobbyUINew.Instance?.RefreshLobby();
        }
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        try
        {
            if (conn != null && conn.identity != null)
            {
                PlayerObject player = conn.identity.GetComponent<PlayerObject>();
                if (player != null)
                {
                    ulong steamId = player.steamId != 0 ? player.steamId : ResolveSteamId(conn);
                    bool voluntary = steamId != 0 && _voluntaryLeaveSteamIds.Contains(steamId);
                    if (voluntary)
                        _voluntaryLeaveSteamIds.Remove(steamId);

                    if (steamId != 0 && !voluntary)
                    {
                        _disconnectedStates[steamId] = new PersistedPlayerState
                        {
                            playerIndex = player.playerIndex,
                            currentSpaceIndex = player.currentSpaceIndex,
                            selectedCharacterIndex = player.selectedCharacterIndex,
                            selectedDiceIndex = player.selectedDiceIndex,
                            money = player.money,
                            disconnectedAt = Time.unscaledTime,
                            steamName = player.steamName,
                            hasPassedStart = player.hasPassedStart,
                            isInJail = player.isInJail
                        };
                        StartCoroutine(SpawnBotAfterDisconnect(steamId, player));
                    }

                    _connectedPlayers.Remove(player);
                    Debug.Log($"[Network] Oyuncu ayrıldı ve listeden çıkarıldı. " +
                              $"Index: {player.playerIndex}" + (voluntary ? " (istemli)" : ""));

                    if (voluntary && NetworkServer.active && GameTurnManager.Instance != null)
                        GameTurnManager.Instance.ServerHandlePlayerDisconnected(player.playerIndex);
                }
            }
        }
        catch (System.Exception)
        {
            // conn/identity gecersiz olabiliyor; sessizce gec.
        }

        try
        {
            base.OnServerDisconnect(conn);
        }
        catch (System.Exception)
        {
            // Kapanis sirasinda Mirror icinde NullReferenceException olabiliyor; sessizce gec.
        }
    }

    public override void OnStopHost()
    {
        base.OnStopHost();
        _connectedPlayers.Clear();
        _botPlayers.Clear();
        _disconnectedStates.Clear();
        _allowedPlayerSteamIds.Clear();
        _voluntaryLeaveSteamIds.Clear();
        SteamLobbyManager.Instance?.LeaveLobby();
    }

    [Server]
    public void ServerAddBot()
    {
        string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        string gameScene = !string.IsNullOrEmpty(gameSceneName) ? gameSceneName : "GameScene";
        if (activeScene == gameScene)
        {
            Debug.LogWarning("[Network] Bot lobide eklenebilir, oyun icinde degil.");
            return;
        }

        int idx = GetNextPlayerIndex();
        const int maxPlayers = 4;
        if (idx >= maxPlayers)
        {
            Debug.LogWarning($"[Network] Maksimum {maxPlayers} oyuncu. Bot eklenemedi.");
            return;
        }

        var botGo = Instantiate(playerPrefab);
        var bot = botGo.GetComponent<PlayerObject>();
        if (bot == null) { Destroy(botGo); return; }

        bot.isBot = true;
        bot.playerIndex = idx;
        bot.steamName = $"Bot {idx + 1}";
        bot.steamId = 0;
        bot.currentSpaceIndex = 0;
        bot.money = 1500;
        bot.selectedCharacterIndex = idx % 5;
        bot.selectedDiceIndex = 0;
        bot.hasPassedStart = false;
        bot.isInJail = false;
        bot.isReady = true;

        NetworkServer.Spawn(botGo);
        _botPlayers.Add(bot);
        Debug.Log($"[Network] Lobiye bot eklendi: P{idx} ({bot.steamName})");
    }

    [Server]
    private System.Collections.IEnumerator SpawnBotAfterDisconnect(ulong steamId, PlayerObject oldPlayer)
    {
        yield return null;
        if (!NetworkServer.active) yield break;
        if (!_disconnectedStates.TryGetValue(steamId, out var state)) yield break;

        var botGo = Instantiate(playerPrefab);
        var bot = botGo.GetComponent<PlayerObject>();
        if (bot == null) { Destroy(botGo); yield break; }

        bot.isBot = true;
        bot.playerIndex = state.playerIndex;
        bot.steamName = state.steamName + " (Bot)";
        bot.steamId = steamId;
        bot.currentSpaceIndex = state.currentSpaceIndex;
        bot.money = state.money;
        bot.selectedCharacterIndex = state.selectedCharacterIndex;
        bot.selectedDiceIndex = state.selectedDiceIndex;
        bot.hasPassedStart = state.hasPassedStart;
        bot.isInJail = state.isInJail;

        NetworkServer.Spawn(botGo);
        _botPlayers.Add(bot);
        Debug.Log($"[Network] Bot spawn edildi: P{state.playerIndex} ({state.steamName})");

        if (GameTurnManager.Instance != null)
            GameTurnManager.Instance.ServerNotifyBotSpawned(bot);
    }

    public override void OnServerChangeScene(string newSceneName)
    {
        string gameScene = !string.IsNullOrEmpty(gameSceneName) ? gameSceneName : "GameScene";
        if (!string.IsNullOrEmpty(newSceneName) && newSceneName == gameScene)
        {
            _pendingBotsForGameScene.Clear();
            foreach (var b in _botPlayers)
            {
                if (b != null)
                {
                    _pendingBotsForGameScene.Add(new PendingBotSpawn
                    {
                        playerIndex = b.playerIndex,
                        steamName = b.steamName,
                        selectedCharacterIndex = b.selectedCharacterIndex,
                        selectedDiceIndex = b.selectedDiceIndex
                    });
                }
            }
            _botPlayers.Clear();
            if (_pendingBotsForGameScene.Count > 0)
                Debug.Log($"[Network] {_pendingBotsForGameScene.Count} bot oyun sahnesinde yeniden spawn edilecek.");
        }

        base.OnServerChangeScene(newSceneName);
        if (string.IsNullOrEmpty(newSceneName) || newSceneName != gameScene) return;
        _allowedPlayerSteamIds.Clear();
        foreach (var conn in NetworkServer.connections.Values)
        {
            if (conn == null) continue;
            ulong id = ResolveSteamId(conn);
            if (id != 0) _allowedPlayerSteamIds.Add(id);
        }
        if (SteamClient.IsValid)
            _allowedPlayerSteamIds.Add(SteamClient.SteamId.Value);
        Debug.Log($"[Network] Oyun sahnesi icin izinli oyuncu sayisi: {_allowedPlayerSteamIds.Count}");
    }

    public override void OnServerSceneChanged(string sceneName)
    {
        base.OnServerSceneChanged(sceneName);
        string gameScene = !string.IsNullOrEmpty(gameSceneName) ? gameSceneName : "GameScene";

        if (!string.IsNullOrEmpty(sceneName) && sceneName == gameScene && _pendingGameDurationSeconds > 0 && GameTurnManager.Instance != null)
        {
            GameTurnManager.Instance.ServerSetGameDuration(_pendingGameDurationSeconds);
            _pendingGameDurationSeconds = 0f;
            Debug.Log("[Network] Lobide secilen oyun suresi oyun sahnesine uygulandi.");
        }

        if (string.IsNullOrEmpty(sceneName) || sceneName != gameScene || _pendingBotsForGameScene.Count == 0)
            return;

        foreach (var info in _pendingBotsForGameScene)
        {
            if (IsIndexInUse(info.playerIndex)) continue;
            var botGo = Instantiate(playerPrefab);
            var bot = botGo.GetComponent<PlayerObject>();
            if (bot == null) { Destroy(botGo); continue; }

            bot.isBot = true;
            bot.playerIndex = info.playerIndex;
            bot.steamName = info.steamName;
            bot.steamId = 0;
            bot.currentSpaceIndex = 0;
            bot.money = 1500;
            bot.selectedCharacterIndex = info.selectedCharacterIndex;
            bot.selectedDiceIndex = info.selectedDiceIndex;
            bot.hasPassedStart = false;
            bot.isInJail = false;
            bot.isReady = true;

            NetworkServer.Spawn(botGo);
            _botPlayers.Add(bot);
            Debug.Log($"[Network] Bot oyun sahnesinde spawn edildi: P{info.playerIndex} ({info.steamName})");
        }
        _pendingBotsForGameScene.Clear();

        if (GameTurnManager.Instance != null)
        {
            foreach (var b in _botPlayers)
                if (b != null) GameTurnManager.Instance.ServerNotifyBotSpawned(b);
        }
    }

    [Server]
    public void ServerSetPendingGameDuration(float seconds)
    {
        _pendingGameDurationSeconds = seconds;
    }

    #endregion

    #region Yardımcı Metodlar

    public IReadOnlyList<PlayerObject> GetConnectedPlayers() => _connectedPlayers;

    public IReadOnlyList<PlayerObject> GetBotPlayers() => _botPlayers;

    public PlayerObject GetBotByIndex(int playerIndex)
    {
        foreach (var b in _botPlayers)
            if (b != null && b.playerIndex == playerIndex) return b;
        return null;
    }

    public bool IsBot(int playerIndex) => GetBotByIndex(playerIndex) != null;

    public System.Collections.Generic.IEnumerable<PlayerObject> GetAllActivePlayers()
    {
        foreach (var p in _connectedPlayers) if (p != null) yield return p;
        foreach (var p in _botPlayers) if (p != null) yield return p;
    }

    private int GetNextPlayerIndex()
    {
        int idx = 0;
        while (true)
        {
            if (!IsIndexInUse(idx)) return idx;
            idx++;
        }
    }

    private bool IsIndexInUse(int idx)
    {
        for (int i = 0; i < _connectedPlayers.Count; i++)
        {
            var p = _connectedPlayers[i];
            if (p != null && p.playerIndex == idx) return true;
        }
        for (int i = 0; i < _botPlayers.Count; i++)
        {
            var p = _botPlayers[i];
            if (p != null && p.playerIndex == idx) return true;
        }

        foreach (var kv in _disconnectedStates)
        {
            if (Time.unscaledTime - kv.Value.disconnectedAt > reconnectGraceSeconds) continue;
            if (kv.Value.playerIndex == idx) return true;
        }

        return false;
    }

    private ulong ResolveSteamId(NetworkConnectionToClient conn)
    {
        if (conn == null) return 0;

        if (ulong.TryParse(conn.address, out ulong parsed))
            return parsed;

        if ((conn.address == "localhost" || string.IsNullOrWhiteSpace(conn.address)) && SteamClient.IsValid)
            return SteamClient.SteamId.Value;

        return 0;
    }

    private void CleanupExpiredReconnectStates()
    {
        if (_disconnectedStates.Count == 0) return;
        var toRemove = new List<ulong>();
        foreach (var kv in _disconnectedStates)
        {
            if (Time.unscaledTime - kv.Value.disconnectedAt > reconnectGraceSeconds)
                toRemove.Add(kv.Key);
        }

        for (int i = 0; i < toRemove.Count; i++)
            _disconnectedStates.Remove(toRemove[i]);
    }

    private void PrefillDisconnectedStatesFromSnapshot(NetworkConnectionToClient excludeConn)
    {
        ulong excludeSteamId = ResolveSteamId(excludeConn);
        foreach (var entry in _migrationSnapshot.players)
        {
            if (entry.steamId == 0 || entry.steamId == excludeSteamId) continue;
            if (_disconnectedStates.ContainsKey(entry.steamId)) continue;
            _disconnectedStates[entry.steamId] = new PersistedPlayerState
            {
                playerIndex = entry.playerIndex,
                currentSpaceIndex = entry.currentSpaceIndex,
                selectedCharacterIndex = entry.selectedCharacterIndex,
                selectedDiceIndex = entry.selectedDiceIndex,
                money = entry.money,
                disconnectedAt = Time.unscaledTime,
                steamName = entry.steamName ?? "",
                hasPassedStart = entry.hasPassedStart,
                isInJail = entry.isInJail
            };
        }
    }

    private GameStateSnapshot.PlayerEntry FindSnapshotEntryBySteamId(ulong steamId)
    {
        if (_migrationSnapshot == null || _migrationSnapshot.players == null) return null;
        foreach (var e in _migrationSnapshot.players)
            if (e.steamId == steamId) return e;
        return null;
    }

    public void SetMigrationSnapshot(GameStateSnapshot snapshot)
    {
        _migrationSnapshot = snapshot;
    }

    public GameStateSnapshot GetMigrationSnapshot()
    {
        return _migrationSnapshot;
    }

    public void ClearMigrationSnapshot()
    {
        _migrationSnapshot = null;
    }

    /// <summary>
    /// Oyuncu "Terket" ile cikarken cagirilir; 3 dk state tutulmaz.
    /// </summary>
    public void ServerMarkVoluntaryLeave(ulong steamId)
    {
        if (steamId != 0)
        {
            _voluntaryLeaveSteamIds.Add(steamId);
            _disconnectedStates.Remove(steamId);
        }
    }

    #endregion
}