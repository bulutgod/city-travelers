using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class GameNetworkManager : NetworkManager
{
    public static GameNetworkManager Instance => singleton as GameNetworkManager;

    private readonly List<PlayerObject> _connectedPlayers = new List<PlayerObject>();

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
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        Debug.Log($"[Network] OnServerAddPlayer. ConnId:{conn.connectionId} " +
                  $"Address:{conn.address}");

        GameObject playerGo = Instantiate(playerPrefab);
        NetworkServer.AddPlayerForConnection(conn, playerGo);

        PlayerObject player = playerGo.GetComponent<PlayerObject>();
        if (player != null)
        {
            player.playerIndex = _connectedPlayers.Count;
            _connectedPlayers.Add(player);

            Debug.Log($"[Network] Spawn edildi → " +
                      $"Index:{player.playerIndex} " +
                      $"Name:{player.steamName} " +
                      $"SteamId:{player.steamId}");
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
                    _connectedPlayers.Remove(player);
                    Debug.Log($"[Network] Oyuncu ayrıldı ve listeden çıkarıldı. " +
                              $"Index: {player.playerIndex}");
                }
            }
        }
        catch (System.Exception)
        {
            // Kapanis sirasinda conn/identity bazen gecersiz olabiliyor; sessizce gec.
        }

        base.OnServerDisconnect(conn);
    }

    public override void OnStopHost()
    {
        base.OnStopHost();
        _connectedPlayers.Clear();
        SteamLobbyManager.Instance?.LeaveLobby();
    }

    #endregion

    #region Yardımcı Metodlar

    public IReadOnlyList<PlayerObject> GetConnectedPlayers() => _connectedPlayers;

    #endregion
}