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
        currentTurnPlayerIndex = snap.currentTurnPlayerIndex;
        turnNumber = snap.turnNumber;
        lastRollValue = snap.lastRollValue;
        lastRollPlayerIndex = snap.lastRollPlayerIndex;
        isRolling = false;
        rollingPlayerIndex = -1;
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

        for (int i = 0; i < steps; i++)
        {
            if (requester == null)
            {
                isRolling = false;
                rollingPlayerIndex = -1;
                yield break;
            }
            requester.ServerMoveBy(1, boardSpaceCount);
            yield return new WaitForSeconds(stepDelay);
        }

        lastRollValue = roll;
        lastRollPlayerIndex = requester.playerIndex;

        Debug.Log($"[Turn] Oyuncu {requester.playerIndex} zar: {roll}, yeni index: {requester.currentSpaceIndex}");

        isRolling = false;
        rollingPlayerIndex = -1;
        AdvanceTurn();
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
            var connected = GameNetworkManager.Instance.GetConnectedPlayers();
            foreach (var p in connected)
                if (p != null && !list.Contains(p)) list.Add(p);
        }

        if (list.Count == 0)
        {
            foreach (var p in FindObjectsOfType<PlayerObject>())
                if (p != null && !list.Contains(p)) list.Add(p);
        }

        list.Sort((a, b) => a.playerIndex.CompareTo(b.playerIndex));
        return list;
    }
}
