using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// Oyun tur yonetimi: host zar atar, aktif oyuncuyu ilerletir ve sirayi devreder.
/// Tahta modeli hazir olmasa bile yalnizca indeks uzerinden calisir.
/// </summary>
public class GameTurnManager : NetworkBehaviour
{
    public static GameTurnManager Instance { get; private set; }
    public int MaxDice => maxDice;

    [Header("Zar Ayarlari")]
    [SerializeField] private int minDice = 1;
    [SerializeField] private int maxDice = 6;
    [SerializeField] private float moveStepDelay = 0.16f;

    [Header("Tahta Ayarlari")]
    [SerializeField] private int boardSpaceCount = 38;

    [SyncVar] public int currentTurnPlayerIndex = -1;
    [SyncVar] public int turnNumber = 1;
    [SyncVar] public int lastRollValue = 0;
    [SyncVar] public int lastRollPlayerIndex = -1;
    [SyncVar] public bool isRolling = false;
    [SyncVar] public int rollingPlayerIndex = -1;

    public readonly SyncList<int> bankruptPlayerIndices = new SyncList<int>();

    public static string LastNotification { get; private set; }
    public static float LastNotificationTime { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        StartCoroutine(InitializeAfterPlayersReady());
    }

    [Server]
    private IEnumerator InitializeAfterPlayersReady()
    {
        float timeout = Time.time + 8f;
        while (Time.time < timeout)
        {
            var players = GetOrderedPlayers();
            if (players.Count > 0)
            {
                var snap = GameNetworkManager.Instance != null ? GameNetworkManager.Instance.GetMigrationSnapshot() : null;
                if (snap != null && snap.isValid)
                {
                    ServerRestoreFromSnapshot(snap);
                    if (GameNetworkManager.Instance != null)
                        GameNetworkManager.Instance.ClearMigrationSnapshot();
                    yield break;
                }
                InitializeMatch(players);
                yield break;
            }
            yield return null;
        }

        Debug.LogWarning("[Turn] Oyuncular bulunamadi, tur sistemi baslatilamadi.");
    }

    [Server]
    public void ServerRestoreFromSnapshot(GameStateSnapshot snap)
    {
        if (snap == null || !snap.isValid) return;

        if (PropertyManager.Instance != null)
            PropertyManager.Instance.ServerRestoreFromSnapshot(snap);

        currentTurnPlayerIndex = snap.currentTurnPlayerIndex;
        turnNumber = snap.turnNumber;
        lastRollValue = snap.lastRollValue;
        lastRollPlayerIndex = snap.lastRollPlayerIndex;
        isRolling = false;
        rollingPlayerIndex = -1;

        // Kopan host'un sirasiydiysa, sirayi hala oyundaki ilk oyuncuya ver.
        var players = GetOrderedPlayers();
        bool currentPlayerStillInGame = players.Exists(p => p != null && p.playerIndex == currentTurnPlayerIndex);
        if (!currentPlayerStillInGame && players.Count > 0)
        {
            currentTurnPlayerIndex = players[0].playerIndex;
            Debug.Log($"[Turn] Migration: Eski aktif oyuncu artik yok. Yeni aktif: {currentTurnPlayerIndex}");
        }

        Debug.Log($"[Turn] Migration restore. Aktif oyuncu:{currentTurnPlayerIndex} Tur:{turnNumber}");
    }

    [Server]
    private void InitializeMatch(List<PlayerObject> players)
    {
        if (boardSpaceCount <= 0) boardSpaceCount = 38;
        turnNumber = 1;
        lastRollValue = 0;
        lastRollPlayerIndex = -1;
        isRolling = false;
        rollingPlayerIndex = -1;

        foreach (var p in players)
            if (p != null) p.currentSpaceIndex = 0;

        currentTurnPlayerIndex = players[0].playerIndex;
        Debug.Log($"[Turn] Basladi. Ilk oyuncu index: {currentTurnPlayerIndex}");
    }

    [Server]
    public bool ServerTryRoll(PlayerObject requester)
    {
        if (requester == null)
        {
            Debug.LogWarning("[Turn] Roll reddedildi: requester null.");
            return false;
        }

        if (isRolling)
        {
            Debug.LogWarning("[Turn] Roll reddedildi: baska bir zar animasyonu devam ediyor.");
            return false;
        }

        if (currentTurnPlayerIndex < 0)
        {
            var initPlayers = GetOrderedPlayers();
            if (initPlayers.Count > 0)
                InitializeMatch(initPlayers);
        }

        if (requester.playerIndex != currentTurnPlayerIndex)
        {
            Debug.LogWarning($"[Turn] Roll reddedildi: requester P{requester.playerIndex}, aktif P{currentTurnPlayerIndex}.");
            return false;
        }

        int roll = Random.Range(minDice, maxDice + 1);
        StartCoroutine(ServerRollAndMove(requester, roll));
        return true;
    }

    [Server]
    private IEnumerator ServerRollAndMove(PlayerObject requester, int roll)
    {
        isRolling = true;
        rollingPlayerIndex = requester.playerIndex;

        int steps = Mathf.Max(1, roll);
        float stepDelay = Mathf.Max(0.03f, moveStepDelay);

        var startInfo = BoardManager.Instance != null ? BoardManager.Instance.GetSpaceInfo(0) : null;
        int startBonus = startInfo != null ? startInfo.startBonus : 0;

        for (int i = 0; i < steps; i++)
        {
            if (requester == null)
            {
                isRolling = false;
                rollingPlayerIndex = -1;
                yield break;
            }
            requester.ServerMoveBy(1, boardSpaceCount);
            if (requester.currentSpaceIndex == 0 && startBonus > 0)
            {
                requester.money += startBonus;
                RpcShowNotification($"{requester.steamName} Start'tan geçti: +{startBonus} TL");
            }
            yield return new WaitForSeconds(stepDelay);
        }

        lastRollValue = roll;
        lastRollPlayerIndex = requester.playerIndex;

        int landedIndex = requester.currentSpaceIndex;
        Debug.Log($"[Turn] Oyuncu {requester.playerIndex} zar: {roll}, yeni index: {landedIndex}");

        isRolling = false;
        rollingPlayerIndex = -1;

        var info = BoardManager.Instance != null ? BoardManager.Instance.GetSpaceInfo(landedIndex) : null;
        var spaceType = info != null ? info.spaceType : SpaceInfo.SpaceType.Normal;

        // Özel kareler
        switch (spaceType)
        {
            case SpaceInfo.SpaceType.Start:
                break;
            case SpaceInfo.SpaceType.Tax:
                if (info != null && info.taxAmount > 0)
                {
                    requester.money = Mathf.Max(0, requester.money - info.taxAmount);
                    RpcShowNotification($"{requester.steamName} vergi ödedi: -{info.taxAmount} TL");
                    if (requester.money <= 0)
                        ServerHandleBankruptcy(requester);
                }
                break;
            case SpaceInfo.SpaceType.Chance:
            case SpaceInfo.SpaceType.Community:
                // Basit: rastgele -100 ile +150 arası (1500 TL baz alinarak)
                int amount = Random.Range(-100, 151);
                if (amount != 0)
                {
                    requester.money = Mathf.Max(0, requester.money + amount);
                    RpcShowNotification($"{requester.steamName} {(amount > 0 ? "+" : "")}{amount} TL");
                    if (requester.money <= 0)
                        ServerHandleBankruptcy(requester);
                }
                break;
            case SpaceInfo.SpaceType.Jail:
                RpcShowNotification($"{requester.steamName} hapishanede ziyaret");
                break;
            case SpaceInfo.SpaceType.FreeParking:
                RpcShowNotification($"{requester.steamName} park yeri");
                break;
            default:
                // Mülk/kira mantığı
                if (PropertyManager.Instance != null)
                {
                    if (PropertyManager.Instance.MustPayRent(landedIndex, requester.playerIndex))
                    {
                        int rent = info != null ? info.rent : 0;
                        if (rent > 0)
                        {
                            var owner = GetPlayerByIndex(PropertyManager.Instance.GetOwner(landedIndex));
                            if (owner != null)
                            {
                                PropertyManager.Instance.ServerCollectRent(requester, owner, rent);
                                RpcShowRentNotification(requester.steamName, owner.steamName, rent);
                                if (requester.money <= 0)
                                    ServerHandleBankruptcy(requester);
                            }
                        }
                        AdvanceTurn();
                        yield break;
                    }
                    if (PropertyManager.Instance.CanBuy(landedIndex))
                    {
                        PropertyManager.Instance.ServerSetPending(landedIndex, requester.playerIndex);
                        if (requester.isBot)
                            StartCoroutine(BotDecideBuyAfterDelay(requester, landedIndex, 1.5f));
                        yield break;
                    }
                }
                break;
        }

        AdvanceTurn();
    }

    [Server]
    public void ServerAdvanceTurnAfterPropertyAction(PlayerObject player)
    {
        AdvanceTurn();
    }

    [Server]
    private PlayerObject GetPlayerByIndex(int playerIndex)
    {
        if (GameNetworkManager.Instance == null) return null;
        var bot = GameNetworkManager.Instance.GetBotByIndex(playerIndex);
        if (bot != null) return bot;
        foreach (var p in GameNetworkManager.Instance.GetConnectedPlayers())
            if (p != null && p.playerIndex == playerIndex) return p;
        return null;
    }

    [Server]
    public void ServerNotifyBotSpawned(PlayerObject bot)
    {
        if (bot == null) return;
        if (currentTurnPlayerIndex == bot.playerIndex && !isRolling)
            StartCoroutine(BotTurnAfterDelay(bot, 1.5f));
    }

    [ClientRpc]
    private void RpcShowNotification(string message)
    {
        LastNotification = message;
        LastNotificationTime = Time.time;
    }

    [ClientRpc]
    private void RpcShowRentNotification(string payerName, string ownerName, int amount)
    {
        LastNotification = $"{payerName} {amount} TL kira ödedi ({ownerName})";
        LastNotificationTime = Time.time;
    }

    [Server]
    private void ServerHandleBankruptcy(PlayerObject player)
    {
        if (player == null) return;
        if (bankruptPlayerIndices.Contains(player.playerIndex)) return;
        bankruptPlayerIndices.Add(player.playerIndex);
        RpcShowNotification($"{player.steamName} iflas etti!");
        Debug.Log($"[Turn] P{player.playerIndex} iflas etti.");
        if (currentTurnPlayerIndex == player.playerIndex)
        {
            AdvanceTurn();
        }
    }

    [Server]
    private void AdvanceTurn()
    {
        var players = GetOrderedPlayers();
        if (players.Count <= 1) return;

        int currentPos = players.FindIndex(p => p != null && p.playerIndex == currentTurnPlayerIndex);
        if (currentPos < 0) currentPos = 0;

        int nextPos = (currentPos + 1) % players.Count;
        currentTurnPlayerIndex = players[nextPos].playerIndex;
        turnNumber++;

        var nextPlayer = GetPlayerByIndex(currentTurnPlayerIndex);
        if (nextPlayer != null && nextPlayer.isBot && !isRolling)
            StartCoroutine(BotTurnAfterDelay(nextPlayer, 1.5f));
    }

    [Server]
    public void ServerHandlePlayerDisconnected(int disconnectedPlayerIndex)
    {
        if (disconnectedPlayerIndex < 0) return;

        if (rollingPlayerIndex == disconnectedPlayerIndex)
        {
            isRolling = false;
            rollingPlayerIndex = -1;
        }

        if (currentTurnPlayerIndex != disconnectedPlayerIndex) return;

        var players = GetOrderedPlayers();
        if (players.Count == 0)
        {
            currentTurnPlayerIndex = -1;
            return;
        }

        int nextPos = players.FindIndex(p => p != null && p.playerIndex > disconnectedPlayerIndex);
        if (nextPos < 0) nextPos = 0;

        currentTurnPlayerIndex = players[nextPos].playerIndex;
        turnNumber++;
        Debug.Log($"[Turn] Aktif oyuncu koptu. Yeni aktif oyuncu: {currentTurnPlayerIndex}");
    }

    [Server]
    private List<PlayerObject> GetOrderedPlayers()
    {
        var list = new List<PlayerObject>();
        if (GameNetworkManager.Instance != null)
        {
            foreach (var p in GameNetworkManager.Instance.GetAllActivePlayers())
                if (p != null && !list.Contains(p)) list.Add(p);
        }
        if (list.Count == 0)
        {
            foreach (var p in FindObjectsOfType<PlayerObject>())
                if (p != null && !list.Contains(p)) list.Add(p);
        }
        list.RemoveAll(p => p == null || bankruptPlayerIndices.Contains(p.playerIndex));
        list.Sort((a, b) => a.playerIndex.CompareTo(b.playerIndex));
        return list;
    }

    [Server]
    private IEnumerator BotTurnAfterDelay(PlayerObject bot, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (bot == null || !bot.isBot) yield break;
        if (currentTurnPlayerIndex != bot.playerIndex || isRolling) yield break;
        ServerTryRoll(bot);
    }

    [Server]
    private IEnumerator BotDecideBuyAfterDelay(PlayerObject bot, int spaceIndex, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (bot == null || PropertyManager.Instance == null) yield break;
        if (PropertyManager.Instance.pendingSpaceIndex != spaceIndex || PropertyManager.Instance.pendingPlayerIndex != bot.playerIndex)
            yield break;

        var info = BoardManager.Instance != null ? BoardManager.Instance.GetSpaceInfo(spaceIndex) : null;
        bool buy = info != null && bot.money >= info.purchasePrice && Random.value > 0.3f;
        if (buy)
            PropertyManager.Instance.ServerTryBuy(bot, spaceIndex);
        else
            PropertyManager.Instance.ServerDeclineBuy(bot, spaceIndex);
    }
}
