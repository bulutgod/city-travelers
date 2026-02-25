using System.Collections;
using Mirror;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

public class PlayerObject : NetworkBehaviour
{
    

    [SyncVar(hook = nameof(OnPlayerIndexChanged))]
    public int playerIndex = -1;

    [SyncVar(hook = nameof(OnSteamNameChanged))]
    public string steamName = "";

    [SyncVar(hook = nameof(OnSteamIdChanged))]
    public ulong steamId = 0;
    [SyncVar(hook = nameof(OnCharacterIndexChanged))]
    public int selectedCharacterIndex = 0;

    private void OnCharacterIndexChanged(int oldVal, int newVal)
    {
        // Kart rengini güncelle - LobbyUINew üzerinden
        LobbyUINew.Instance?.RefreshLobby();
    }

    public Texture2D avatarTexture { get; private set; }

    public override void OnStartServer()
    {
        // Host kontrolü: connectionToClient null veya address "localhost" ise host'uz
        if (connectionToClient == null ||
            connectionToClient.address == "localhost" ||
            !ulong.TryParse(connectionToClient.address, out ulong id))
        {
            // Bu obje host'a ait
            steamId = SteamClient.SteamId.Value;
            steamName = SteamClient.Name ?? "Host";
            Debug.Log($"[Player] Host bilgisi set edildi: {steamName}");
            return;
        }

        // Client'ın SteamID'si address'ten geliyor
        steamId = id;
        var friendId = new Steamworks.SteamId { Value = id };
        steamName = new Steamworks.Friend(friendId).Name ?? "Oyuncu";
        Debug.Log($"[Player] Client bilgisi set edildi: {steamName} ({steamId})");
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        gameObject.name = $"Player_{playerIndex}_{netId}";
        Debug.Log($"[Player] Client başladı. " +
                  $"NetId:{netId} Index:{playerIndex} Name:{steamName}");

        if (steamId != 0)
            StartCoroutine(FetchAvatar(steamId));
    }

    public override void OnStartLocalPlayer()
    {
        Debug.Log($"[Player] Yerel oyuncu: {steamName} ({steamId})");
    }

    private void OnPlayerIndexChanged(int oldVal, int newVal)
    {
        gameObject.name = $"Player_{newVal}_{netId}";
    }

    private void OnSteamNameChanged(string oldVal, string newVal)
    {
        Debug.Log($"[Player] İsim güncellendi: {oldVal} → {newVal}");
    }

    private void OnSteamIdChanged(ulong oldVal, ulong newVal)
    {
        if (newVal != 0)
            StartCoroutine(FetchAvatar(newVal));
    }

    private IEnumerator FetchAvatar(ulong id)
    {
        var steamId = new SteamId { Value = id };

        var task = SteamFriends.GetLargeAvatarAsync(steamId);

        yield return new WaitUntil(() => task.IsCompleted);

        if (task.Result.HasValue)
        {
            var image = task.Result.Value;
            avatarTexture = ConvertToTexture2D(image);
            Debug.Log($"[Player] Avatar yüklendi: {steamName} " +
                      $"({image.Width}x{image.Height})");
        }
        else
        {
            Debug.LogWarning($"[Player] Avatar yüklenemedi: {steamName}");
        }
    }

    private Texture2D ConvertToTexture2D(Steamworks.Data.Image image)
    {
        var texture = new Texture2D((int)image.Width, (int)image.Height,
            TextureFormat.RGBA32, false);

        var flipped = new byte[image.Data.Length];
        int rowSize = (int)image.Width * 4;

        for (int y = 0; y < image.Height; y++)
        {
            int srcRow = (int)(image.Height - 1 - y);
            System.Array.Copy(
                image.Data, srcRow * rowSize,
                flipped, y * rowSize,
                rowSize
            );
        }

        texture.LoadRawTextureData(flipped);
        texture.Apply();
        return texture;
    }
}