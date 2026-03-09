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

    [SyncVar(hook = nameof(OnDiceIndexChanged))]
    public int selectedDiceIndex = 0;

    [SyncVar(hook = nameof(OnSpaceIndexChanged))]
    public int currentSpaceIndex = 0;

    [SyncVar(hook = nameof(OnMoneyChanged))]
    public int money = 1500; // Prefab varsayılanı; sunucu tarafında GameNetworkManager/GameTurnManager ile GameEconomy.StartingMoney yapılır.

    [SyncVar] public bool isBot = false;

    /// <summary>
    /// Start karesinden (0) en az bir kez geçmiş mi? 4. ev dikmek için gerekli.
    /// </summary>
    [SyncVar] public bool hasPassedStart = false;

    /// <summary>
    /// Üç çift zar veya "Hapishaneye Git" karesine basınca true. Çift zar atınca veya ödeyince çıkılır.
    /// </summary>
    [SyncVar] public bool isInJail = false;

    [SyncVar] public bool isReady = false;

    /// <summary> 2v2 modunda takım (0 veya 1). </summary>
    [SyncVar] public int teamIndex = 0;

    private void OnCharacterIndexChanged(int oldVal, int newVal)
    {
        LobbyUINew.Instance?.RefreshLobby();
    }

    private void OnDiceIndexChanged(int oldVal, int newVal)
    {
        LobbyUINew.Instance?.RefreshLobby();
    }

    private void OnSpaceIndexChanged(int oldVal, int newVal)
    {
        // Oyun sahnesinde piyon hareketi bu degere baglanacak.
    }

    private void OnMoneyChanged(int oldVal, int newVal)
    {
        // HUD para gosterimi bu degere baglanacak.
    }

    [Command]
    public void CmdSetDiceIndex(int index)
    {
        selectedDiceIndex = Mathf.Clamp(index, 0, 99);
    }

    [Command]
    public void CmdSetReady(bool ready)
    {
        isReady = ready;
    }

    [Command]
    public void CmdRequestRoll()
    {
        if (GameTurnManager.Instance == null) return;
        GameTurnManager.Instance.ServerTryRoll(this);
    }

    [Command]
    public void CmdPayToLeaveJail()
    {
        if (GameTurnManager.Instance == null) return;
        GameTurnManager.Instance.ServerPayToLeaveJail(this);
    }

    [Command]
    public void CmdBuyProperty(int spaceIndex)
    {
        if (PropertyManager.Instance != null)
            PropertyManager.Instance.ServerTryBuy(this, spaceIndex);
    }

    [Command]
    public void CmdDeclineBuy(int spaceIndex)
    {
        if (PropertyManager.Instance != null)
            PropertyManager.Instance.ServerDeclineBuy(this, spaceIndex);
    }

    [Command]
    public void CmdBuyOrBuild(int spaceIndex, int count)
    {
        if (PropertyManager.Instance != null)
            PropertyManager.Instance.ServerTryBuyOrBuild(this, spaceIndex, count);
    }

    [Command]
    public void CmdPayRentOnly(int spaceIndex)
    {
        if (PropertyManager.Instance != null)
            PropertyManager.Instance.ServerPayRentOnly(this, spaceIndex);
    }

    [Command]
    public void CmdBuyFromOwner(int spaceIndex)
    {
        if (PropertyManager.Instance != null)
            PropertyManager.Instance.ServerBuyFromOwner(this, spaceIndex);
    }

    /// <summary>
    /// Oyuncu oyun icinde "Terket"e bastiginda cagirilir; sunucu state'i 3 dk tutmaz.
    /// </summary>
    [Command]
    public void CmdSetGameDuration(float seconds)
    {
        if (GameTurnManager.Instance != null)
            GameTurnManager.Instance.ServerSetGameDuration(seconds);
    }

    /// <summary>
    /// Lobide host oyun suresini secer; oyun sahnesi acilinca uygulanir.
    /// </summary>
    [Command]
    public void CmdSetPendingGameDuration(float seconds)
    {
        if (GameNetworkManager.Instance != null)
            GameNetworkManager.Instance.ServerSetPendingGameDuration(seconds);
    }

    [Command]
    public void CmdSetPendingTeamMode(bool teamMode)
    {
        if (GameNetworkManager.Instance != null)
            GameNetworkManager.Instance.ServerSetPendingTeamMode(teamMode);
    }

    [Command]
    public void CmdRequestAddBot()
    {
        if (GameNetworkManager.Instance != null)
            GameNetworkManager.Instance.ServerAddBot();
    }

    [Command]
    public void CmdSendQuickChat(int messageIndex)
    {
        if (GameTurnManager.Instance != null)
            GameTurnManager.Instance.ServerBroadcastQuickChat(steamName, messageIndex);
    }

    [Command]
    public void CmdVoluntaryLeave()
    {
        ulong id = steamId != 0 ? steamId : (SteamClient.IsValid ? SteamClient.SteamId.Value : 0);
        if (GameNetworkManager.Instance != null)
            GameNetworkManager.Instance.ServerMarkVoluntaryLeave(id);
    }

    [Command]
    public void CmdRequestRematch()
    {
        if (GameNetworkManager.Instance != null && connectionToClient != null)
            GameNetworkManager.Instance.ServerRecordRematch(connectionToClient);
    }

    [Command]
    public void CmdRequestStats()
    {
        if (GameTurnManager.Instance != null && connectionToClient != null)
            GameTurnManager.Instance.ServerSendStatsTo(connectionToClient);
    }

    [Server]
    public void ServerMoveBy(int steps, int boardSpaceCount)
    {
        if (boardSpaceCount <= 0) boardSpaceCount = BoardManager.Instance != null ? BoardManager.Instance.SpaceCount : 36;
        int next = currentSpaceIndex + steps;
        currentSpaceIndex = ((next % boardSpaceCount) + boardSpaceCount) % boardSpaceCount;
    }

    public Texture2D avatarTexture { get; private set; }

    public override void OnStartServer()
    {
        if (isBot) return;

        // Host kontrolü: connectionToClient null veya address "localhost" ise host'uz
        if (connectionToClient == null ||
            string.IsNullOrEmpty(connectionToClient.address) ||
            connectionToClient.address == "localhost" ||
            !ulong.TryParse(connectionToClient.address, out ulong id))
        {
            // Bu obje host'a ait
            if (SteamClient.IsValid)
            {
                steamId = SteamClient.SteamId.Value;
                steamName = SteamClient.Name ?? "Host";
            }
            else
            {
                steamName = "Host";
            }
            Debug.Log($"[Player] Host bilgisi set edildi: {steamName} (steamId:{steamId})");
            return;
        }

        // Client'ın SteamID'si address'ten geliyor (FizzySteam genelde Steam ID string gonderir)
        steamId = id;
        try
        {
            var friendId = new Steamworks.SteamId { Value = id };
            steamName = new Steamworks.Friend(friendId).Name ?? "Oyuncu";
        }
        catch
        {
            steamName = "Oyuncu";
        }

        // Eger hala bos kaldiysa ve bu baglanti kendimizse (host) SteamClient'dan doldur
        if (SteamClient.IsValid && (string.IsNullOrEmpty(steamName) || steamId == 0) &&
            steamId == SteamClient.SteamId.Value)
        {
            steamId = SteamClient.SteamId.Value;
            steamName = SteamClient.Name ?? "Host";
        }

        Debug.Log($"[Player] Client bilgisi set edildi: {steamName} ({steamId})");
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        gameObject.name = $"Player_{playerIndex}_{netId}";
        Debug.Log($"[Player] Client başladı. " +
                  $"NetId:{netId} Index:{playerIndex} Name:{steamName}");

        LobbyUINew.Instance?.RefreshLobby();

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
        LobbyUINew.Instance?.RefreshLobby();
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
            LobbyUINew.Instance?.RefreshLobby();
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