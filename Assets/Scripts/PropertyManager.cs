using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// Mülk sahipliği yönetimi. spaceIndex -> ownerPlayerIndex (-1 = bank/sahipsiz).
/// Satın alma ve kira sunucuda işlenir.
/// </summary>
public class PropertyManager : NetworkBehaviour
{
    public static PropertyManager Instance { get; private set; }

    [Header("Referanslar")]
    [SerializeField] private BoardManager boardManager;

    /// <summary>
    /// spaceIndex -> ownerPlayerIndex. -1 = sahipsiz.
    /// </summary>
    public readonly SyncDictionary<int, int> spaceOwners = new SyncDictionary<int, int>();

    /// <summary>
    /// spaceIndex -> ev sayısı (0-4). Sadece satın alınabilir mülklerde kullanılır.
    /// </summary>
    public readonly SyncDictionary<int, int> spaceHouseCounts = new SyncDictionary<int, int>();

    [SyncVar] public int pendingSpaceIndex = -1;
    [SyncVar] public int pendingPlayerIndex = -1;
    /// <summary>
    /// true = ev/otel dikme teklifi, false = satin alma teklifi.
    /// </summary>
    [SyncVar] public bool pendingIsBuild = false;
    /// <summary>
    /// true = kira ode veya mulku satin al secimi (otel yoksa).
    /// </summary>
    [SyncVar] public bool pendingIsRentOrBuy = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (boardManager == null) boardManager = FindObjectOfType<BoardManager>();
    }

    /// <summary>
    /// spaceIndex'in sahibi. -1 = sahipsiz.
    /// </summary>
    public int GetOwner(int spaceIndex)
    {
        if (spaceOwners.TryGetValue(spaceIndex, out int owner))
            return owner;
        return -1;
    }

    /// <summary>
    /// Satın alınabilir ve sahipsiz mi?
    /// </summary>
    public bool CanBuy(int spaceIndex)
    {
        var info = GetSpaceInfo(spaceIndex);
        if (info == null || !info.IsPurchasable) return false;
        return GetOwner(spaceIndex) < 0;
    }

    /// <summary>
    /// Kira ödenmeli mi? (sahibi var, kendimiz değiliz). 2v2'de takım arkadaşının yerine kira ödenmez.
    /// </summary>
    public bool MustPayRent(int spaceIndex, int landingPlayerIndex)
    {
        int owner = GetOwner(spaceIndex);
        if (owner < 0) return false;
        if (owner == landingPlayerIndex) return false;
        if (GameTurnManager.Instance != null && GameTurnManager.Instance.isTeamGame &&
            GameTurnManager.Instance.AreSameTeam(owner, landingPlayerIndex))
            return false;
        return true;
    }

    /// <summary>
    /// Mülkteki ev sayısı (0-4).
    /// </summary>
    public int GetHouseCount(int spaceIndex)
    {
        if (spaceHouseCounts.TryGetValue(spaceIndex, out int count))
            return Mathf.Clamp(count, 0, 5);
        return 0;
    }

    /// <summary>
    /// 5 = otel.
    /// </summary>
    public bool HasHotel(int spaceIndex) => GetHouseCount(spaceIndex) >= 5;

    /// <summary>
    /// Kira hesapla: Normal için ev sayısına göre, Havalimanı için sahibin havalimanı sayısına göre (2^n).
    /// Normal: 0=1x, 1=2x, 2=4x, 3=8x, 4=16x, 5(otel)=32x.
    /// Havalimanı: Her havalimanı kira 2 katına çıkar (sahibin toplam havalimanı sayısı).
    /// </summary>
    public int GetRentWithHouses(int spaceIndex, int baseRent)
    {
        int rent;
        var info = GetSpaceInfo(spaceIndex);
        if (info != null && info.spaceType == SpaceInfo.SpaceType.Havalimani)
        {
            int owner = GetOwner(spaceIndex);
            if (owner < 0) rent = baseRent;
            else
            {
                int airportCount = GetAirportCountForPlayer(owner);
                int multiplier = 1 << airportCount; // 1 havalimanı=2x, 2=4x, 3=8x, 4=16x
                rent = baseRent * multiplier;
            }
        }
        else
        {
            int houses = GetHouseCount(spaceIndex);
            int mult = 1 << houses;
            rent = baseRent * mult;
        }

        // Gündem: Enflasyon - tüm kira bedelleri %20 artar
        if (GameTurnManager.Instance != null && GameTurnManager.Instance.activeAgendaEffect == (int)AgendaEffect.Inflation)
            rent = Mathf.RoundToInt(rent * 1.2f);

        return rent;
    }

    /// <summary>
    /// Oyuncunun sahip olduğu havalimanı sayısı.
    /// </summary>
    public int GetAirportCountForPlayer(int playerIndex)
    {
        if (boardManager == null) return 0;
        int count = 0;
        int n = BoardManager.SpaceCount;
        for (int i = 0; i < n; i++)
        {
            if (GetOwner(i) != playerIndex) continue;
            var info = boardManager.GetSpaceInfo(i);
            if (info != null && info.spaceType == SpaceInfo.SpaceType.Havalimani)
                count++;
        }
        return count;
    }

    private SpaceInfo GetSpaceInfo(int spaceIndex)
    {
        return boardManager != null ? boardManager.GetSpaceInfo(spaceIndex) : null;
    }

    [Server]
    public void ServerTryBuy(PlayerObject buyer, int spaceIndex)
    {
        if (buyer == null) return;
        if (pendingSpaceIndex != spaceIndex || pendingPlayerIndex != buyer.playerIndex)
            return;

        var info = GetSpaceInfo(spaceIndex);
        if (info == null || !info.IsPurchasable)
        {
            FinishPendingAndAdvance(buyer);
            return;
        }
        if (GetOwner(spaceIndex) >= 0)
        {
            FinishPendingAndAdvance(buyer);
            return;
        }
        if (buyer.money < info.purchasePrice)
        {
            FinishPendingAndAdvance(buyer);
            return;
        }

        buyer.money -= info.purchasePrice;
        spaceOwners[spaceIndex] = buyer.playerIndex;
        pendingSpaceIndex = -1;
        pendingPlayerIndex = -1;
        Debug.Log($"[Property] P{buyer.playerIndex} bought space {spaceIndex} for {info.purchasePrice}");
        string spaceName = info.displayName ?? $"Alan {spaceIndex}";
        GameTurnManager.Instance?.ServerSendNotification($"{buyer.steamName} {spaceName} aldı: -{info.purchasePrice} TL");
        AdvanceTurnAfterAction(buyer);
    }

    [Server]
    public void ServerDeclineBuy(PlayerObject player, int spaceIndex)
    {
        if (player == null) return;
        if (pendingSpaceIndex != spaceIndex || pendingPlayerIndex != player.playerIndex)
            return;

        pendingSpaceIndex = -1;
        pendingPlayerIndex = -1;
        pendingIsBuild = false;
        pendingIsRentOrBuy = false;
        AdvanceTurnAfterAction(player);
    }

    [Server]
    public void ServerRestoreFromSnapshot(GameStateSnapshot snap)
    {
        if (snap == null || snap.properties == null) return;
        spaceOwners.Clear();
        spaceHouseCounts.Clear();
        foreach (var p in snap.properties)
        {
            if (p.ownerPlayerIndex >= 0)
            {
                spaceOwners[p.spaceIndex] = p.ownerPlayerIndex;
                if (p.houseCount > 0)
                    spaceHouseCounts[p.spaceIndex] = Mathf.Clamp(p.houseCount, 0, 5);
            }
        }
        pendingSpaceIndex = -1;
        pendingPlayerIndex = -1;
        pendingIsBuild = false;
        pendingIsRentOrBuy = false;
        Debug.Log($"[Property] Restored {spaceOwners.Count} properties (with houses) from snapshot.");
    }

    [Server]
    public void ServerSetPending(int spaceIndex, int playerIndex)
    {
        pendingSpaceIndex = spaceIndex;
        pendingPlayerIndex = playerIndex;
        pendingIsBuild = false;
    }

    [Server]
    public void ServerSetPendingBuild(int spaceIndex, int playerIndex)
    {
        pendingSpaceIndex = spaceIndex;
        pendingPlayerIndex = playerIndex;
        pendingIsBuild = true;
        pendingIsRentOrBuy = false;
    }

    [Server]
    public void ServerSetPendingRentOrBuy(int spaceIndex, int playerIndex)
    {
        pendingSpaceIndex = spaceIndex;
        pendingPlayerIndex = playerIndex;
        pendingIsBuild = false;
        pendingIsRentOrBuy = true;
    }

    [Server]
    public void ServerPayRentOnly(PlayerObject payer, int spaceIndex)
    {
        if (payer == null) return;
        if (pendingSpaceIndex != spaceIndex || pendingPlayerIndex != payer.playerIndex || !pendingIsRentOrBuy)
            return;

        var info = GetSpaceInfo(spaceIndex);
        int baseRent = info != null ? info.rent : 0;
        int rent = GetRentWithHouses(spaceIndex, baseRent);
        var owner = GetOwnerPlayer(spaceIndex);
        if (owner != null && rent > 0)
        {
            ServerCollectRent(payer, owner, rent);
            GameTurnManager.Instance?.ServerSendNotification($"{payer.steamName} {rent} TL kira ödedi ({owner.steamName})");
        }
        if (payer.money <= 0)
            GameTurnManager.Instance?.ServerHandleBankruptcyWithReason(payer, "kirayı ödeyemedi");
        FinishPendingAndAdvance(payer);
    }

    [Server]
    public void ServerBuyFromOwner(PlayerObject buyer, int spaceIndex)
    {
        if (buyer == null) return;
        if (pendingSpaceIndex != spaceIndex || pendingPlayerIndex != buyer.playerIndex || !pendingIsRentOrBuy)
            return;

        int ownerIdx = GetOwner(spaceIndex);
        if (ownerIdx < 0 || ownerIdx == buyer.playerIndex) { FinishPendingAndAdvance(buyer); return; }
        if (HasHotel(spaceIndex)) { FinishPendingAndAdvance(buyer); return; }

        var info = GetSpaceInfo(spaceIndex);
        int baseRent = info != null ? info.rent : 0;
        int rent = GetRentWithHouses(spaceIndex, baseRent);
        int buyPrice = rent * 2;

        var owner = GetOwnerPlayer(spaceIndex);
        if (buyer.money < buyPrice) { FinishPendingAndAdvance(buyer); return; }

        buyer.money -= buyPrice;
        if (owner != null) owner.money += buyPrice;

        spaceOwners[spaceIndex] = buyer.playerIndex;

        string spaceName = info != null ? info.displayName : $"Alan {spaceIndex}";
        string ownerName = owner != null ? owner.steamName : "?";
        GameTurnManager.Instance?.ServerSendNotification($"{buyer.steamName} {spaceName} mülkünü {ownerName}'den {buyPrice} TL'ye aldı!");
        FinishPendingAndAdvance(buyer);
    }

    [Server]
    private PlayerObject GetOwnerPlayer(int spaceIndex)
    {
        int ownerIdx = GetOwner(spaceIndex);
        if (ownerIdx < 0 || GameTurnManager.Instance == null) return null;
        return GameTurnManager.Instance.GetPlayerByIndexPublic(ownerIdx);
    }

    /// <summary>
    /// Ev dik: Boş mülkte count 1=yer satın al, 2-4=yer+(count-1) ev. Sahibiyse count ev dik.
    /// count 0 = geç (sadece ServerDeclineBuy ile).
    /// </summary>
    [Server]
    public void ServerTryBuyOrBuild(PlayerObject player, int spaceIndex, int count)
    {
        if (player == null) return;
        if (pendingSpaceIndex != spaceIndex || pendingPlayerIndex != player.playerIndex || !pendingIsBuild)
            return;

        var info = GetSpaceInfo(spaceIndex);
        if (info == null || !info.IsPurchasable) return;

        int owner = GetOwner(spaceIndex);
        if (owner < 0)
        {
            if (count < 1) { FinishPendingAndAdvance(player); return; }
            // Havalimanında sadece satın alma, bina dikilmez
            if (info.spaceType == SpaceInfo.SpaceType.Havalimani)
                count = 1;
            int housePrice = info.housePrice > 0 ? info.housePrice : (info.purchasePrice / 2);
            int maxSeviye = player.hasPassedStart ? 4 : 3;
            count = Mathf.Clamp(count, 1, maxSeviye);
            int evSayisi = info.CanBuildHouses ? (count - 1) : 0;
            int evCost = evSayisi > 0 ? housePrice * evSayisi : 0;
            if (player.money < info.purchasePrice + evCost) { FinishPendingAndAdvance(player); return; }

            player.money -= info.purchasePrice + evCost;
            spaceOwners[spaceIndex] = player.playerIndex;
            if (evSayisi > 0) spaceHouseCounts[spaceIndex] = evSayisi;
            pendingSpaceIndex = -1;
            pendingPlayerIndex = -1;
            pendingIsBuild = false;
            Debug.Log($"[Property] P{player.playerIndex} bought space {spaceIndex} (seviye {count}: yer + {evSayisi} ev)");
            string name1 = info.displayName ?? $"Alan {spaceIndex}";
            int totalCost = info.purchasePrice + evCost;
            string msg = evSayisi > 0
                ? $"{player.steamName} {name1} aldı + {evSayisi} ev dikti: -{totalCost} TL"
                : $"{player.steamName} {name1} aldı: -{totalCost} TL";
            GameTurnManager.Instance?.ServerSendNotification(msg);
            AdvanceTurnAfterAction(player);
        }
        else if ((owner == player.playerIndex ||
                  (GameTurnManager.Instance != null && GameTurnManager.Instance.isTeamGame &&
                   GameTurnManager.Instance.AreSameTeam(owner, player.playerIndex))) &&
                 info.CanBuildHouses)
        {
            int currentHouses = GetHouseCount(spaceIndex);
            int housePrice = info.housePrice > 0 ? info.housePrice : (info.purchasePrice / 2);

            if (currentHouses == 4)
            {
                if (count < 1 || housePrice <= 0 || player.money < housePrice) { FinishPendingAndAdvance(player); return; }
                player.money -= housePrice;
                spaceHouseCounts[spaceIndex] = 5;
                pendingSpaceIndex = -1;
                pendingPlayerIndex = -1;
                pendingIsBuild = false;
                string name2 = info.displayName ?? $"Alan {spaceIndex}";
                Debug.Log($"[Property] P{player.playerIndex} built hotel on space {spaceIndex}");
                GameTurnManager.Instance?.ServerSendNotification($"{player.steamName} {name2} üzerine otel dikti: -{housePrice} TL");
                AdvanceTurnAfterAction(player);
            }
            else
            {
                int maxCanBuild = 4 - currentHouses;
                if (maxCanBuild <= 0 || count <= 0) { FinishPendingAndAdvance(player); return; }
                if (!player.hasPassedStart)
                {
                    int maxWithoutStart = Mathf.Max(0, 3 - currentHouses);
                    maxCanBuild = Mathf.Min(maxCanBuild, maxWithoutStart);
                }
                if (maxCanBuild <= 0) { FinishPendingAndAdvance(player); return; }
                count = Mathf.Clamp(count, 1, maxCanBuild);
                if (housePrice <= 0 || player.money < housePrice * count) { FinishPendingAndAdvance(player); return; }

                player.money -= housePrice * count;
                spaceHouseCounts[spaceIndex] = currentHouses + count;
                pendingSpaceIndex = -1;
                pendingPlayerIndex = -1;
                pendingIsBuild = false;
                string name2 = info.displayName ?? $"Alan {spaceIndex}";
                Debug.Log($"[Property] P{player.playerIndex} built {count} house(s) on space {spaceIndex}");
                GameTurnManager.Instance?.ServerSendNotification($"{player.steamName} {name2} üzerine {count} ev dikti: -{housePrice * count} TL");
                AdvanceTurnAfterAction(player);
            }
        }
        else
        {
            FinishPendingAndAdvance(player);
        }
    }

    [Server]
    public void ServerCollectRent(PlayerObject payer, PlayerObject owner, int amount)
    {
        if (payer == null || owner == null || amount <= 0) return;
        payer.money = Mathf.Max(0, payer.money - amount);
        owner.money += amount;
        if (GameStatsManager.Instance != null)
        {
            GameStatsManager.Instance.RecordRentPaid(payer.playerIndex, amount);
            GameStatsManager.Instance.RecordRentReceived(owner.playerIndex, amount);
        }
        Debug.Log($"[Property] P{payer.playerIndex} paid {amount} rent to P{owner.playerIndex}");
    }

    [Server]
    private void FinishPendingAndAdvance(PlayerObject player)
    {
        pendingSpaceIndex = -1;
        pendingPlayerIndex = -1;
        pendingIsBuild = false;
        pendingIsRentOrBuy = false;
        AdvanceTurnAfterAction(player);
    }

    [Server]
    private void AdvanceTurnAfterAction(PlayerObject player)
    {
        if (GameTurnManager.Instance != null)
            GameTurnManager.Instance.ServerAdvanceTurnAfterPropertyAction(player);
    }
}
