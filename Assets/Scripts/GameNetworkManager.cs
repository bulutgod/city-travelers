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
        base.OnClientDisconnect();
        Debug.Log("[Network] Bağlantı kesildi.");
        SteamLobbyManager.Instance?.LeaveLobby();

        // Host dusunce client'i oyunda yarim birakma; lobi/menu sahnesine geri don.
        string targetScene = !string.IsNullOrWhiteSpace(offlineScene)
            ? offlineScene
            : fallbackLobbySceneName;

        if (!string.IsNullOrWhiteSpace(targetScene))
        {
            string active = SceneManager.GetActiveScene().name;
            if (active != targetScene)
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
        bool hasRestoredState = incomingSteamId != 0 &&
                                _disconnectedStates.TryGetValue(incomingSteamId, out restored);

        GameObject playerGo = Instantiate(playerPrefab);
        NetworkServer.AddPlayerForConnection(conn, playerGo);

        PlayerObject player = playerGo.GetComponent<PlayerObject>();
        if (player != null)
        {
            if (hasRestoredState)
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
                    if (steamId != 0)
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
                              $"Index: {player.playerIndex}");

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
        SteamLobbyManager.Instance?.LeaveLobby();
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

    #endregion
}