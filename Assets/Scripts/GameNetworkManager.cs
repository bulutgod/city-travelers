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
    private readonly Dictionary<ulong, PersistedPlayerState> _disconnectedStates = new Dictionary<ulong, PersistedPlayerState>();
    private readonly HashSet<ulong> _allowedPlayerSteamIds = new HashSet<ulong>();
    private readonly HashSet<ulong> _voluntaryLeaveSteamIds = new HashSet<ulong>();

    private GameStateSnapshot _migrationSnapshot;

    private struct PersistedPlayerState
    {
        public int playerIndex;
        public int currentSpaceIndex;
        public int selectedCharacterIndex;
        public int selectedDiceIndex;
        public float disconnectedAt;
        public string steamName;
    }

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
        base.OnClientConnect();

        
        if (!NetworkClient.ready)
        {
            NetworkClient.Ready();
        }

        Debug.Log("[Network] Sunucuya bağlandı.");
        LobbyUINew.NotifyLobbyJoined();
    }
    public override void OnClientDisconnect()
    {
        try { base.OnClientDisconnect(); } catch (System.Exception ex) { Debug.LogWarning("[Network] OnClientDisconnect base: " + ex.Message); }
        Debug.Log("[Network] Baglanti kesildi.");

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
        var localPlayer = NetworkClient.localPlayer?.GetComponent<PlayerObject>();
        if (localPlayer != null)
            localPlayer.CmdVoluntaryLeave();
        StartCoroutine(LeaveAfterDelay());
    }

    private System.Collections.IEnumerator LeaveAfterDelay()
    {
        yield return new WaitForSeconds(0.25f);
        try { SteamLobbyManager.Instance?.LeaveLobby(); } catch { }
        StopClient();
        string target = !string.IsNullOrWhiteSpace(offlineScene) ? offlineScene : fallbackLobbySceneName;
        if (!string.IsNullOrWhiteSpace(target))
            SceneManager.LoadScene(target);
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

        GameObject playerGo = Instantiate(playerPrefab);
        NetworkServer.AddPlayerForConnection(conn, playerGo);

        PlayerObject player = playerGo.GetComponent<PlayerObject>();
        if (player != null)
        {
            if (hasRestoredState2)
            {
                // Oyuncu geri geldiyse ayni slota ve ayni tahta pozisyonuna donsun.
                int restoredIndex = IsIndexInUse(restored.playerIndex) ? GetNextPlayerIndex() : restored.playerIndex;
                player.playerIndex = restoredIndex;
                player.currentSpaceIndex = restored.currentSpaceIndex;
                player.selectedCharacterIndex = restored.selectedCharacterIndex;
                player.selectedDiceIndex = restored.selectedDiceIndex;
                if (!string.IsNullOrWhiteSpace(restored.steamName))
                    player.steamName = restored.steamName;
                _disconnectedStates.Remove(incomingSteamId);

                Debug.Log($"[Network] Reconnect restore -> SteamId:{incomingSteamId} Index:{player.playerIndex} Space:{player.currentSpaceIndex}");
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
                            disconnectedAt = Time.unscaledTime,
                            steamName = player.steamName
                        };
                    }

                    _connectedPlayers.Remove(player);
                    Debug.Log($"[Network] Oyuncu ayrıldı ve listeden çıkarıldı. " +
                              $"Index: {player.playerIndex}" + (voluntary ? " (istemli)" : ""));

                    if (NetworkServer.active && GameTurnManager.Instance != null)
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
        _disconnectedStates.Clear();
        _allowedPlayerSteamIds.Clear();
        _voluntaryLeaveSteamIds.Clear();
        SteamLobbyManager.Instance?.LeaveLobby();
    }

    public override void OnServerChangeScene(string newSceneName)
    {
        base.OnServerChangeScene(newSceneName);
        if (string.IsNullOrEmpty(newSceneName) || newSceneName != gameSceneName) return;
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

    #endregion

    #region Yardımcı Metodlar

    public IReadOnlyList<PlayerObject> GetConnectedPlayers() => _connectedPlayers;

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
                disconnectedAt = Time.unscaledTime,
                steamName = entry.steamName ?? ""
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