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
    /// true = ev dikme teklifi (kendi mülküne indin), false = satın alma teklifi.
    /// </summary>
    [SyncVar] public bool pendingIsBuild = false;

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
    /// Kira ödenmeli mi? (sahibi var, kendimiz değiliz)
    /// </summary>
    public bool MustPayRent(int spaceIndex, int landingPlayerIndex)
    {
        int owner = GetOwner(spaceIndex);
        if (owner < 0) return false;
        return owner != landingPlayerIndex;
    }

    /// <summary>
    /// Mülkteki ev sayısı (0-4).
    /// </summary>
    public int GetHouseCount(int spaceIndex)
    {
        if (spaceHouseCounts.TryGetValue(spaceIndex, out int count))
            return Mathf.Clamp(count, 0, 4);
        return 0;
    }

    /// <summary>
    /// Ev sayısına göre kira: baseRent * 2^houseCount (0 ev=1x, 1 ev=2x, 2 ev=4x, 3 ev=8x, 4 ev=16x).
    /// </summary>
    public int GetRentWithHouses(int spaceIndex, int baseRent)
    {
        int houses = GetHouseCount(spaceIndex);
        int multiplier = 1 << houses; // 2^houses
        return baseRent * multiplier;
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
                    spaceHouseCounts[p.spaceIndex] = Mathf.Clamp(p.houseCount, 0, 4);
            }
        }
        pendingSpaceIndex = -1;
        pendingPlayerIndex = -1;
        pendingIsBuild = false;
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
            int housePrice = info.housePrice > 0 ? info.housePrice : (info.purchasePrice / 2);
            int maxSeviye = 4;
            if (!player.hasPassedStart) maxSeviye = 3;
            count = Mathf.Clamp(count, 1, maxSeviye);
            int evSayisi = count - 1;
            int evCost = evSayisi > 0 ? housePrice * evSayisi : 0;
            if (player.money < info.purchasePrice + evCost) { FinishPendingAndAdvance(player); return; }

            player.money -= info.purchasePrice + evCost;
            spaceOwners[spaceIndex] = player.playerIndex;
            if (evSayisi > 0) spaceHouseCounts[spaceIndex] = evSayisi;
            pendingSpaceIndex = -1;
            pendingPlayerIndex = -1;
            pendingIsBuild = false;
            Debug.Log($"[Property] P{player.playerIndex} bought space {spaceIndex} (seviye {count}: yer + {evSayisi} ev)");
            AdvanceTurnAfterAction(player);
        }
        else if (owner == player.playerIndex)
        {
            int currentHouses = GetHouseCount(spaceIndex);
            int maxCanBuild = 4 - currentHouses;
            if (maxCanBuild <= 0 || count <= 0) { FinishPendingAndAdvance(player); return; }
            if (currentHouses == 3 && !player.hasPassedStart) maxCanBuild = 0;
            else if (!player.hasPassedStart) maxCanBuild = Mathf.Min(maxCanBuild, 3);
            count = Mathf.Clamp(count, 1, maxCanBuild);
            int housePrice = info.housePrice > 0 ? info.housePrice : (info.purchasePrice / 2);
            if (housePrice <= 0 || player.money < housePrice * count) { FinishPendingAndAdvance(player); return; }

            player.money -= housePrice * count;
            spaceHouseCounts[spaceIndex] = currentHouses + count;
            pendingSpaceIndex = -1;
            pendingPlayerIndex = -1;
            pendingIsBuild = false;
            Debug.Log($"[Property] P{player.playerIndex} built {count} house(s) on space {spaceIndex}");
            AdvanceTurnAfterAction(player);
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
        Debug.Log($"[Property] P{payer.playerIndex} paid {amount} rent to P{owner.playerIndex}");
    }

    [Server]
    private void FinishPendingAndAdvance(PlayerObject player)
    {
        pendingSpaceIndex = -1;
        pendingPlayerIndex = -1;
        pendingIsBuild = false;
        AdvanceTurnAfterAction(player);
    }

    [Server]
    private void AdvanceTurnAfterAction(PlayerObject player)
    {
        if (GameTurnManager.Instance != null)
            GameTurnManager.Instance.ServerAdvanceTurnAfterPropertyAction(player);
    }
}
