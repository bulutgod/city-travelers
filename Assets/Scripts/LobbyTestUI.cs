using Mirror;
using UnityEngine;

public class LobbyTestUI : MonoBehaviour
{
    private void OnGUI()
    {
        bool isActive = NetworkServer.active || NetworkClient.active;

        GUI.enabled = !isActive;
        if (GUI.Button(new Rect(10, 10, 150, 40), "HOST OL"))
            SteamLobbyManager.Instance.CreateLobby();
        GUI.enabled = true;

        if (GUI.Button(new Rect(10, 60, 150, 40), "AYRIL"))
            SteamLobbyManager.Instance.LeaveLobby();

        GUI.Label(new Rect(10, 110, 300, 20),
            $"Server: {NetworkServer.active} | Client: {NetworkClient.active}");
        GUI.Label(new Rect(10, 130, 300, 20),
            $"Lobi: {SteamLobbyManager.Instance?.CurrentLobby.Id}");

        int playerCount = NetworkServer.active
            ? GameNetworkManager.Instance.GetConnectedPlayers().Count
            : NetworkClient.spawned.Count;

        GUI.Label(new Rect(10, 150, 300, 20), $"Baðlý oyuncu: {playerCount}");
        GUI.Label(new Rect(10, 170, 300, 20),
            $"Ben: {(NetworkServer.active ? "HOST" : "CLIENT")}");

        
        int yOffset = 210;
        foreach (var spawned in NetworkClient.spawned.Values)
        {
            var player = spawned.GetComponent<PlayerObject>();
            if (player == null) continue;

            if (player.avatarTexture != null)
                GUI.DrawTexture(new Rect(10, yOffset, 64, 64), player.avatarTexture);

            GUI.Label(new Rect(84, yOffset + 10, 250, 20),
                $"[{player.playerIndex}] {player.steamName}");
            GUI.Label(new Rect(84, yOffset + 28, 250, 20),
                $"ID: {player.steamId}");

            yOffset += 74;
        }
    }
}