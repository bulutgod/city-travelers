using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// Sans veya Kasa kartinin metni ve para etkisi.
/// </summary>
public struct ChanceCard
{
    public string text;
    public int amount; // pozitif = kazan, negatif = ode
}

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
    [Tooltip("Zar yere dustukten sonra piyonlarin hareket etmeden once beklenen sure (saniye). DiceRollAnimator fallDuration + showResultDuration ile uyumlu (ornegin 2.5).")]
    [SerializeField] private float delayBeforeMoveAfterRoll = 2.5f;

    [Header("Tur Zamani")]
    [Tooltip("Her sira icin sure (saniye). 0 = sinirsiz.")]
    [SerializeField] private float turnTimeLimit = 60f;
    [SyncVar] private double _turnStartNetworkTime;

    [Header("Oyun Suresi (board merkez)")]
    [SyncVar] public float gameDurationSeconds;  // 0=secilmedi, 1200=20dk, 3600=1sa, 7200=2sa
    [SyncVar] public double gameStartNetworkTime;

    [Header("Tahta Ayarlari")]
    [SerializeField] private int boardSpaceCount = 38;

    [SyncVar] public int currentTurnPlayerIndex = -1;
    [SyncVar] public int turnNumber = 1;
    [SyncVar] public int lastRollValue = 0;
    [SyncVar] public int lastRollDice1 = 0;
    [SyncVar] public int lastRollDice2 = 0;
    [SyncVar] public int lastRollPlayerIndex = -1;
    [SyncVar] public bool isRolling = false;
    [SyncVar] public int rollingPlayerIndex = -1;

    /// <summary> Oyun bittiğinde kazanan oyuncu indexi (-1 = devam ediyor). Bot da sayılır. </summary>
    [SyncVar] public int winnerPlayerIndex = -1;
    [SyncVar] public string winnerName = "";

    public readonly SyncList<int> bankruptPlayerIndices = new SyncList<int>();

    public static string LastNotification { get; private set; }
    public static float LastNotificationTime { get; private set; }

    [Server]
    public void ServerSetGameDuration(float seconds)
    {
        if (gameDurationSeconds > 0f) return;
        gameDurationSeconds = seconds;
        gameStartNetworkTime = NetworkTime.time;
    }

    public float GetRemainingGameTime()
    {
        if (gameDurationSeconds <= 0f) return -1f;
        double elapsed = NetworkTime.time - gameStartNetworkTime;
        return Mathf.Max(0f, (float)(gameDurationSeconds - elapsed));
    }

    /// <summary>Kalan tur suresi (saniye). turnTimeLimit 0 ise -1 doner.</summary>
    public float GetRemainingTurnTime()
    {
        if (turnTimeLimit <= 0f) return -1f;
        if (isRolling) return turnTimeLimit;
        double elapsed = NetworkTime.time - _turnStartNetworkTime;
        return Mathf.Max(0f, (float)(turnTimeLimit - elapsed));
    }

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
        lastRollDice1 = snap.lastRollDice1;
        lastRollDice2 = snap.lastRollDice2;
        lastRollPlayerIndex = snap.lastRollPlayerIndex;
        isRolling = false;
        rollingPlayerIndex = -1;
        winnerPlayerIndex = snap.winnerPlayerIndex;
        winnerName = snap.winnerName ?? "";

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
        RpcPlayGameMusic();
        if (boardSpaceCount <= 0) boardSpaceCount = 38;
        turnNumber = 1;
        lastRollValue = 0;
        lastRollDice1 = 0;
        lastRollDice2 = 0;
        lastRollPlayerIndex = -1;
        isRolling = false;
        rollingPlayerIndex = -1;

        foreach (var p in players)
            if (p != null) p.currentSpaceIndex = 0;

        int firstIndex = Random.Range(0, players.Count);
        currentTurnPlayerIndex = players[firstIndex].playerIndex;
        _turnStartNetworkTime = NetworkTime.time;
        Debug.Log($"[Turn] Basladi. Ilk oyuncu index: {currentTurnPlayerIndex} (rastgele secildi)");
        if (players[firstIndex] != null && players[firstIndex].isBot)
            StartCoroutine(BotTurnAfterDelay(players[firstIndex], 1.5f));
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

        int dice1 = Random.Range(1, 7);
        int dice2 = Random.Range(1, 7);
        int roll = dice1 + dice2;
        StartCoroutine(ServerRollAndMove(requester, roll, dice1, dice2));
        return true;
    }

    [Server]
    private IEnumerator ServerRollAndMove(PlayerObject requester, int roll, int dice1, int dice2)
    {
        isRolling = true;
        rollingPlayerIndex = requester.playerIndex;
        lastRollValue = roll;
        lastRollDice1 = dice1;
        lastRollDice2 = dice2;
        lastRollPlayerIndex = requester.playerIndex;

        RpcPlayDiceRoll();
        RpcPlayDiceLand(dice1, dice2);

        yield return new WaitForSeconds(Mathf.Max(0f, delayBeforeMoveAfterRoll));

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
                requester.hasPassedStart = true;
                requester.money += startBonus;
                RpcPlayCoinGain();
                RpcShowNotification($"{requester.steamName} Start'tan geçti: +{startBonus} TL");
            }
            yield return new WaitForSeconds(stepDelay);
        }

        int landedIndex = requester.currentSpaceIndex;
        Debug.Log($"[Turn] Oyuncu {requester.playerIndex} zar: {dice1}+{dice2}={roll}, yeni index: {landedIndex}");

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
                var card = GetRandomCard(spaceType == SpaceInfo.SpaceType.Chance);
                if (card.amount != 0)
                {
                    requester.money = Mathf.Max(0, requester.money + card.amount);
                    RpcShowCardNotification(card.text, requester.steamName, card.amount);
                    if (requester.money <= 0)
                        ServerHandleBankruptcy(requester);
                }
                else
                {
                    RpcShowCardNotification(card.text, requester.steamName, 0);
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
                        int baseRent = info != null ? info.rent : 0;
                        int rent = PropertyManager.Instance.GetRentWithHouses(landedIndex, baseRent);
                        if (rent > 0)
                        {
                            var owner = GetPlayerByIndex(PropertyManager.Instance.GetOwner(landedIndex));
                            if (owner != null)
                            {
                                PropertyManager.Instance.ServerCollectRent(requester, owner, rent);
                                RpcShowRentNotification(requester.steamName, owner.steamName, rent);
                                if (requester.money <= 0)
                                {
                                    ServerHandleBankruptcy(requester);
                                    AdvanceTurn();
                                    yield break;
                                }
                            }
                        }

                        if (!PropertyManager.Instance.HasHotel(landedIndex) && requester.money > 0)
                        {
                            PropertyManager.Instance.ServerSetPendingRentOrBuy(landedIndex, requester.playerIndex);
                            if (requester.isBot)
                                StartCoroutine(BotDecideRentOrBuyAfterDelay(requester, landedIndex, 1.5f));
                        }
                        else
                        {
                            AdvanceTurn();
                        }
                        yield break;
                    }
                    if (PropertyManager.Instance.CanBuy(landedIndex))
                    {
                        PropertyManager.Instance.ServerSetPendingBuild(landedIndex, requester.playerIndex);
                        if (requester.isBot)
                            StartCoroutine(BotDecideBuyAfterDelay(requester, landedIndex, 1.5f));
                        yield break;
                    }
                    // Kendi mülküne indin: sadece üzerinde bulunduğun mülkte ev dikme teklifi
                    if (PropertyManager.Instance.GetOwner(landedIndex) == requester.playerIndex)
                    {
                        int houses = PropertyManager.Instance.GetHouseCount(landedIndex);
                        if (houses < 5 && (houses == 4 || houses < 3 || requester.hasPassedStart))
                        {
                            var spaceInfo = BoardManager.Instance != null ? BoardManager.Instance.GetSpaceInfo(landedIndex) : null;
                            if (spaceInfo != null && spaceInfo.IsPurchasable)
                            {
                                int housePrice = spaceInfo.housePrice > 0 ? spaceInfo.housePrice : (spaceInfo.purchasePrice / 2);
                                if (housePrice > 0 && requester.money >= housePrice)
                                {
                                    PropertyManager.Instance.ServerSetPendingBuild(landedIndex, requester.playerIndex);
                                    if (requester.isBot)
                                        StartCoroutine(BotDecideBuildAfterDelay(requester, landedIndex, 1.5f));
                                    yield break;
                                }
                            }
                        }
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
    public PlayerObject GetPlayerByIndexPublic(int playerIndex)
    {
        return GetPlayerByIndex(playerIndex);
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

    [Server]
    public void ServerSendNotification(string message)
    {
        RpcShowNotification(message);
    }

    private static readonly string[] QuickChatMessages =
    {
        "Zar at!", "Bekle", "Hadi", "GG", "Şanslı ol!", "Hızlı oyna"
    };

    [Server]
    public void ServerBroadcastQuickChat(string playerName, int messageIndex)
    {
        if (messageIndex < 0 || messageIndex >= QuickChatMessages.Length) return;
        RpcShowQuickChat(playerName, messageIndex);
    }

    [ClientRpc]
    private void RpcShowQuickChat(string playerName, int messageIndex)
    {
        if (messageIndex < 0 || messageIndex >= QuickChatMessages.Length) return;
        string msg = $"{playerName}: {QuickChatMessages[messageIndex]}";
        LastNotification = msg;
        LastNotificationTime = Time.time;
        if (AudioManager.Instance != null) AudioManager.Instance.PlayNotification();
    }

    [ClientRpc]
    private void RpcPlayGameMusic()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayGameMusic();
    }

    [ClientRpc]
    private void RpcPlayDiceRoll()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayDiceRoll();
        OnDiceRollStarted?.Invoke();
    }

    [ClientRpc]
    private void RpcPlayDiceLand(int dice1, int dice2)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayDiceLand();
        OnDiceRollLanded?.Invoke(dice1, dice2);
    }

    /// <summary>Client tarafinda zar atma animasyonu icin. Zar donmeye basladiginda tetiklenir.</summary>
    public static event System.Action OnDiceRollStarted;
    /// <summary>Client tarafinda zar atma animasyonu icin. Iki zarin degeri (1-6, 1-6) belli oldugunda tetiklenir.</summary>
    public static event System.Action<int, int> OnDiceRollLanded;

    [ClientRpc]
    private void RpcPlayCoinGain()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayCoinGain();
    }

    [ClientRpc]
    private void RpcPlayBankrupt()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayBankrupt();
    }

    [ClientRpc]
    private void RpcShowNotification(string message)
    {
        LastNotification = message;
        LastNotificationTime = Time.time;
        if (AudioManager.Instance != null) AudioManager.Instance.PlayNotification();
    }

    private static readonly ChanceCard[] ChanceCards =
    {
        new ChanceCard { text = "Banka size faiz odedi.", amount = 50 },
        new ChanceCard { text = "Dogum gunu hediyesi! Her oyuncudan 10 TL al.", amount = 30 },
        new ChanceCard { text = "Vergi iadesi.", amount = 100 },
        new ChanceCard { text = "Yarisma kazandiniz!", amount = 150 },
        new ChanceCard { text = "Hastane faturasi.", amount = -50 },
        new ChanceCard { text = "Okul ucreti.", amount = -80 },
        new ChanceCard { text = "Yol tamiri.", amount = -40 },
        new ChanceCard { text = "Sans size guldu!", amount = 75 },
        new ChanceCard { text = "Kayip cuzdan buldunuz.", amount = 60 },
        new ChanceCard { text = "Kira geliri.", amount = 90 },
    };

    private static readonly ChanceCard[] CommunityCards =
    {
        new ChanceCard { text = "Doktor ucreti.", amount = -50 },
        new ChanceCard { text = "Miras!", amount = 200 },
        new ChanceCard { text = "Yillik sigorta primi.", amount = -60 },
        new ChanceCard { text = "Hazine avi kazandiniz!", amount = 100 },
        new ChanceCard { text = "Dugun hediyesi.", amount = 80 },
        new ChanceCard { text = "Kutu acma sansi!", amount = 120 },
        new ChanceCard { text = "Yol vergisi.", amount = -70 },
        new ChanceCard { text = "Emeklilik fonu.", amount = 150 },
        new ChanceCard { text = "Hastane ziyareti.", amount = -100 },
        new ChanceCard { text = "Piyango!", amount = 50 },
    };

    [Server]
    private ChanceCard GetRandomCard(bool isChance)
    {
        var deck = isChance ? ChanceCards : CommunityCards;
        return deck[Random.Range(0, deck.Length)];
    }

    [ClientRpc]
    private void RpcShowCardNotification(string cardText, string playerName, int amount)
    {
        string msg = amount != 0
            ? $"{playerName}: {cardText} ({(amount > 0 ? "+" : "")}{amount} TL)"
            : $"{playerName}: {cardText}";
        LastNotification = msg;
        LastNotificationTime = Time.time;
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayNotification();
    }

    [ClientRpc]
    private void RpcShowRentNotification(string payerName, string ownerName, int amount)
    {
        LastNotification = $"{payerName} {amount} TL kira ödedi ({ownerName})";
        LastNotificationTime = Time.time;
        if (AudioManager.Instance != null) AudioManager.Instance.PlayRentPay();
    }

    [Server]
    public void ServerHandleBankruptcyPublic(PlayerObject player)
    {
        ServerHandleBankruptcy(player);
    }

    [Server]
    private void ServerHandleBankruptcy(PlayerObject player)
    {
        if (player == null) return;
        if (bankruptPlayerIndices.Contains(player.playerIndex)) return;
        bankruptPlayerIndices.Add(player.playerIndex);
        RpcPlayBankrupt();
        RpcShowNotification($"{player.steamName} iflas etti!");
        Debug.Log($"[Turn] P{player.playerIndex} iflas etti.");

        var remaining = GetOrderedPlayers();
        if (remaining.Count == 1)
        {
            winnerPlayerIndex = remaining[0].playerIndex;
            winnerName = remaining[0].steamName ?? $"Oyuncu {remaining[0].playerIndex}";
            Debug.Log($"[Turn] Oyun bitti! Kazanan: {winnerName}");
        }

        if (currentTurnPlayerIndex == player.playerIndex)
        {
            AdvanceTurn();
        }
    }

    [Server]
    private void CheckGameTimeUp()
    {
        if (gameDurationSeconds <= 0f || winnerPlayerIndex >= 0) return;
        float remaining = GetRemainingGameTime();
        if (remaining > 0f) return;
        var players = GetOrderedPlayers();
        if (players.Count == 0) return;
        var richest = players[0];
        foreach (var p in players)
        {
            if (p != null && p.money > richest.money) richest = p;
        }
        winnerPlayerIndex = richest.playerIndex;
        winnerName = richest.steamName ?? $"Oyuncu {richest.playerIndex}";
        RpcShowNotification($"Süre doldu! Kazanan: {winnerName}");
    }

    [Server]
    private void AdvanceTurn()
    {
        var players = GetOrderedPlayers();
        if (players.Count <= 1) return;

        int currentPos = players.FindIndex(p => p != null && p.playerIndex == currentTurnPlayerIndex);
        if (currentPos < 0) currentPos = 0;

        CheckGameTimeUp();
        if (winnerPlayerIndex >= 0) return;

        int nextPos = (currentPos + 1) % players.Count;
        currentTurnPlayerIndex = players[nextPos].playerIndex;
        turnNumber++;
        _turnStartNetworkTime = NetworkTime.time;

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

        // Once onceki zar + hareket + panel islemleri bitene kadar bekle (max ~8 sn)
        float timeout = Time.time + 8f;
        while (isRolling && Time.time < timeout)
            yield return null;
        if (Time.time >= timeout) yield break;

        yield return new WaitForSeconds(0.2f);
        if (bot == null || !bot.isBot) yield break;
        if (currentTurnPlayerIndex != bot.playerIndex || isRolling) yield break;
        ServerTryRoll(bot);
    }

    [Server]
    private IEnumerator BotDecideBuildAfterDelay(PlayerObject bot, int spaceIndex, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (bot == null || PropertyManager.Instance == null) yield break;
        if (PropertyManager.Instance.pendingSpaceIndex != spaceIndex || PropertyManager.Instance.pendingPlayerIndex != bot.playerIndex || !PropertyManager.Instance.pendingIsBuild)
            yield break;

        var info = BoardManager.Instance != null ? BoardManager.Instance.GetSpaceInfo(spaceIndex) : null;
        int housePrice = info != null ? (info.housePrice > 0 ? info.housePrice : (info.purchasePrice / 2)) : 0;
        int currentHouses = PropertyManager.Instance.GetHouseCount(spaceIndex);
        int maxAdd;
        if (currentHouses == 4)
            maxAdd = 1;
        else
        {
            maxAdd = 4 - currentHouses;
            if (!bot.hasPassedStart) maxAdd = Mathf.Min(maxAdd, Mathf.Max(0, 3 - currentHouses));
        }
        int count = 0;
        if (maxAdd > 0 && housePrice > 0 && bot.money >= housePrice && Random.value > 0.3f)
        {
            int maxAfford = Mathf.Min(maxAdd, bot.money / housePrice);
            count = maxAfford > 0 ? Random.Range(1, maxAfford + 1) : 0;
        }
        if (count > 0)
            PropertyManager.Instance.ServerTryBuyOrBuild(bot, spaceIndex, count);
        else
            PropertyManager.Instance.ServerDeclineBuy(bot, spaceIndex);
    }

    [Server]
    private IEnumerator BotDecideBuyAfterDelay(PlayerObject bot, int spaceIndex, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (bot == null || PropertyManager.Instance == null) yield break;
        if (PropertyManager.Instance.pendingSpaceIndex != spaceIndex || PropertyManager.Instance.pendingPlayerIndex != bot.playerIndex || !PropertyManager.Instance.pendingIsBuild)
            yield break;

        var info = BoardManager.Instance != null ? BoardManager.Instance.GetSpaceInfo(spaceIndex) : null;
        if (info == null || bot.money < info.purchasePrice) { PropertyManager.Instance.ServerDeclineBuy(bot, spaceIndex); yield break; }
        if (Random.value <= 0.3f) { PropertyManager.Instance.ServerDeclineBuy(bot, spaceIndex); yield break; }
        int count = 1;
        int housePrice = info.housePrice > 0 ? info.housePrice : (info.purchasePrice / 2);
        if (housePrice > 0 && bot.money >= info.purchasePrice + housePrice && Random.value > 0.5f)
            count = Mathf.Min(4, 1 + (bot.money - info.purchasePrice) / housePrice);
        if (!bot.hasPassedStart) count = Mathf.Min(count, 3);
        PropertyManager.Instance.ServerTryBuyOrBuild(bot, spaceIndex, count);
    }

    [Server]
    private IEnumerator BotDecideRentOrBuyAfterDelay(PlayerObject bot, int spaceIndex, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (bot == null || PropertyManager.Instance == null) yield break;
        if (PropertyManager.Instance.pendingSpaceIndex != spaceIndex || PropertyManager.Instance.pendingPlayerIndex != bot.playerIndex || !PropertyManager.Instance.pendingIsRentOrBuy)
            yield break;

        var info = BoardManager.Instance != null ? BoardManager.Instance.GetSpaceInfo(spaceIndex) : null;
        int baseRent = info != null ? info.rent : 0;
        int rent = PropertyManager.Instance.GetRentWithHouses(spaceIndex, baseRent);
        int buyPrice = rent * 2;

        if (bot.money >= buyPrice && Random.value > 0.5f)
            PropertyManager.Instance.ServerBuyFromOwner(bot, spaceIndex);
        else
            PropertyManager.Instance.ServerDeclineBuy(bot, spaceIndex);
    }
}
