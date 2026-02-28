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

    [SyncVar] public int pendingSpaceIndex = -1;
    [SyncVar] public int pendingPlayerIndex = -1;

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
        AdvanceTurnAfterAction(player);
    }

    [Server]
    public void ServerRestoreFromSnapshot(GameStateSnapshot snap)
    {
        if (snap == null || snap.properties == null) return;
        spaceOwners.Clear();
        foreach (var p in snap.properties)
            if (p.ownerPlayerIndex >= 0)
                spaceOwners[p.spaceIndex] = p.ownerPlayerIndex;
        pendingSpaceIndex = -1;
        pendingPlayerIndex = -1;
        Debug.Log($"[Property] Restored {spaceOwners.Count} properties from snapshot.");
    }

    [Server]
    public void ServerSetPending(int spaceIndex, int playerIndex)
    {
        pendingSpaceIndex = spaceIndex;
        pendingPlayerIndex = playerIndex;
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
        AdvanceTurnAfterAction(player);
    }

    [Server]
    private void AdvanceTurnAfterAction(PlayerObject player)
    {
        if (GameTurnManager.Instance != null)
            GameTurnManager.Instance.ServerAdvanceTurnAfterPropertyAction(player);
    }
}
