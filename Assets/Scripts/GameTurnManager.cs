using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// Kader Anı veya Gündem kartının metni ve para etkisi.
/// </summary>
public struct ChanceCard
{
    public string text;
    public int amount; // pozitif = kazan, negatif = ode
}

/// <summary>
/// Gündem kartı etkisi: bir sonraki Gündem'e kadar geçerli.
/// </summary>
public enum AgendaEffect
{
    None = 0,
    Inflation = 1,       // Tüm kira bedelleri %20 artar
    DoubleMisfortune = 2  // Çift zar atanlar ilerleyemez
}

/// <summary>
/// Gündem kartı: metin + etki tipi.
/// </summary>
public struct GundemCard
{
    public string text;
    public int effect; // AgendaEffect
}

/// <summary>
/// Kader Anı kartı: metin, para etkisi, veya Merkez Bankası'na git.
/// </summary>
public struct KaderAniCard
{
    public string text;
    public int amount;
    public bool goToCentralBank;
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

    [Header("Merkez Bankası")]
    [Tooltip("Kumar ve Küresel Fon'dan gelen paralar burada birikir.")]
    [SyncVar] public int centralBankPool = 0;

    [Header("Gündem")]
    [Tooltip("Aktif gündem etkisi (0=None, 1=Enflasyon, 2=Çifte Şansızlık).")]
    [SyncVar] public int activeAgendaEffect = 0;

    [Header("Tur gecisi")]
    [Tooltip("Bir oyuncu turu bitirdikten sonra diger oyuncunun zar atabilmesi icin beklenecek minimum sure (saniye).")]
    [SerializeField] private float minTurnStartDelay = 2f;

    [Header("Hapishane")]
    [Tooltip("Hapisten isteğe bağlı çıkış ücreti. 0 = ücretsiz.")]
    [SerializeField] private int jailReleaseFee = 50;
    [Tooltip("Çift zar atmadan en fazla kaç kez zar atma denemesi. Bu kadar denemeden sonra sıra tekrar gelince attığın zarla çıkarsın.")]
    [SerializeField] private int maxJailTurns = 3;
    [Tooltip("Üst üste kaç çift zar atılırsa hapishaneye gider. 2 = test için kolay, 3 = klasik monopoli.")]
    [SerializeField] private int doublesCountForJail = 3;

    [SyncVar] public int currentTurnPlayerIndex = -1;
    [SyncVar] public int turnNumber = 1;
    [SyncVar] public int lastRollValue = 0;
    [SyncVar] public int lastRollDice1 = 0;
    [SyncVar] public int lastRollDice2 = 0;
    [SyncVar] public int lastRollPlayerIndex = -1;
    [SyncVar] public bool isRolling = false;
    [SyncVar] public int rollingPlayerIndex = -1;

    /// <summary> Son atılan zar çift (doubles) ise tur geçilmez, aynı oyuncu tekrar atar. </summary>
    private bool _lastRollWasDoubles;
    /// <summary> Aynı turda üst üste kaç kez çift zar atıldı. N olunca hapishaneye gider. </summary>
    private int _consecutiveDoublesCount;
    /// <summary> Hapiste çift zar atmadan yapılan zar atma denemesi sayısı (1, 2, 3). </summary>
    private readonly Dictionary<int, int> _jailRollAttempts = new Dictionary<int, int>();
    /// <summary> 3 denemede çift atamadıysa sıra tekrar gelince attığı zarla çıkacak. </summary>
    private readonly Dictionary<int, bool> _jailExitWithNextRoll = new Dictionary<int, bool>();
    /// <summary> "Hapishaneye Git" karesine zarla gelince: ilk turda çift zar ile çıkış yok (en az 1 tur hapis). </summary>
    private readonly Dictionary<int, bool> _jailSkipFirstDoublesExit = new Dictionary<int, bool>();
    /// <summary> Son tur gecis zamani (Server). minTurnStartDelay dolana kadar zar atilamaz. </summary>
    private float _turnStartTime;

    public int JailReleaseFee => GameEconomy.ScalePrice(jailReleaseFee);
    /// <summary> Tur gecisinden sonra zar atilabilmesi icin gecen sure yeterli mi? (Client UI icin) </summary>
    public bool CanRollNow() => (float)(NetworkTime.time - _turnStartNetworkTime) >= minTurnStartDelay;

    /// <summary> Oyun bittiğinde kazanan oyuncu indexi (-1 = devam ediyor). Bot da sayılır. </summary>
    [SyncVar] public int winnerPlayerIndex = -1;
    [SyncVar] public string winnerName = "";

    /// <summary> 2v2 takımlı mod açık mı? </summary>
    [SyncVar] public bool isTeamGame = false;

    public readonly SyncList<int> bankruptPlayerIndices = new SyncList<int>();

    public static string LastNotification { get; private set; }
    public static float LastNotificationTime { get; private set; }

    /// <summary> Olay popup UI için: son gösterilen kart/olay. Client'ta Rpc ile set edilir. </summary>
    public static string LastCardText { get; private set; }
    public static string LastCardPlayerName { get; private set; }
    public static int LastCardAmount { get; private set; }
    public static bool LastCardIsChance { get; private set; }
    public static float LastCardTime { get; private set; }
    /// <summary> Popup başlığı: KADER ANI, GÜNDEM, KUMAR vb. </summary>
    public static string LastCardTitle { get; private set; }

    [Server]
    public void ServerSetGameDuration(float seconds)
    {
        if (gameDurationSeconds > 0f) return;
        gameDurationSeconds = seconds;
        gameStartNetworkTime = NetworkTime.time;
    }

    [Server]
    public void ServerSetTeamMode(bool teamMode)
    {
        isTeamGame = teamMode;
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
        centralBankPool = snap.centralBankPool;
        activeAgendaEffect = snap.activeAgendaEffect;

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
        centralBankPool = 0;
        activeAgendaEffect = 0;
        _consecutiveDoublesCount = 0;
        _jailRollAttempts.Clear();
        _jailExitWithNextRoll.Clear();
        _jailSkipFirstDoublesExit.Clear();

        foreach (var p in players)
        {
            if (p != null)
            {
                p.currentSpaceIndex = 0;
                p.isInJail = false;
                p.money = GameEconomy.StartingMoney;
            }
        }

        // Host veya gec eklenen oyuncu listede olmayabilir; prefab varsayilani (1500) kalan herkesi de 1.5M yap
        foreach (var p in FindObjectsOfType<PlayerObject>())
        {
            if (p != null && !bankruptPlayerIndices.Contains(p.playerIndex) && p.money == 1500)
            {
                p.money = GameEconomy.StartingMoney;
                p.currentSpaceIndex = 0;
                p.isInJail = false;
            }
        }

        int firstIndex = Random.Range(0, players.Count);
        currentTurnPlayerIndex = players[firstIndex].playerIndex;
        _turnStartNetworkTime = NetworkTime.time - minTurnStartDelay; // Ilk oyuncu hemen atabilsin
        _turnStartTime = Time.time - minTurnStartDelay;
        Debug.Log($"[Turn] Basladi. Ilk oyuncu index: {currentTurnPlayerIndex} (rastgele secildi)");
        if (players[firstIndex] != null && players[firstIndex].isBot)
            StartCoroutine(BotTurnAfterDelay(players[firstIndex], minTurnStartDelay));
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

        if (Time.time - _turnStartTime < minTurnStartDelay)
        {
            Debug.LogWarning($"[Turn] Roll reddedildi: tur gecisi icin en az {minTurnStartDelay}s beklenmeli.");
            return false;
        }

        if (requester.isInJail)
        {
            if (_jailExitWithNextRoll.TryGetValue(requester.playerIndex, out bool exitNext) && exitNext)
            {
                StartCoroutine(ServerJailExitWithRoll(requester));
                return true;
            }
            int jailFee = GameEconomy.ScalePrice(jailReleaseFee);
            if (requester.isBot && jailFee > 0 && requester.money >= jailFee)
            {
                ServerPayToLeaveJail(requester);
                return true;
            }
            StartCoroutine(ServerJailRoll(requester));
            return true;
        }

        int dice1 = Random.Range(1, 7);
        int dice2 = Random.Range(1, 7);
        int roll = dice1 + dice2;
        _lastRollWasDoubles = (dice1 == dice2);
        if (_lastRollWasDoubles)
            _consecutiveDoublesCount++;
        else
            _consecutiveDoublesCount = 0;

        // Üst üste N çift zar -> hapishaneye git
        if (_consecutiveDoublesCount >= doublesCountForJail)
        {
            _consecutiveDoublesCount = 0;
            StartCoroutine(ServerShowDoublesThenSendToJail(requester, dice1, dice2));
            return true;
        }

        // Gündem: Çifte Şansızlık - çift zar atanlar ilerleyemez
        if (_lastRollWasDoubles && activeAgendaEffect == (int)AgendaEffect.DoubleMisfortune)
        {
            StartCoroutine(ServerRollNoMove(requester, dice1, dice2));
            return true;
        }

        StartCoroutine(ServerRollAndMove(requester, roll, dice1, dice2));
        return true;
    }

    /// <summary>Üst üste N. çift zarı göster, sonra hapishaneye gönder.</summary>
    [Server]
    private IEnumerator ServerShowDoublesThenSendToJail(PlayerObject requester, int dice1, int dice2)
    {
        isRolling = true;
        rollingPlayerIndex = requester != null ? requester.playerIndex : -1;
        lastRollValue = dice1 + dice2;
        lastRollDice1 = dice1;
        lastRollDice2 = dice2;
        lastRollPlayerIndex = requester != null ? requester.playerIndex : -1;

        RpcPlayDiceRoll();
        RpcPlayDiceLand(dice1, dice2);
        yield return new WaitForSeconds(Mathf.Max(0f, delayBeforeMoveAfterRoll));

        isRolling = false;
        rollingPlayerIndex = -1;
        SendToJail(requester, fromTripleDoubles: true);
    }

    [Server]
    private int GetJailSpaceIndex()
    {
        if (BoardManager.Instance == null) return -1;
        int n = boardSpaceCount > 0 ? boardSpaceCount : BoardManager.SpaceCount;
        for (int i = 0; i < n; i++)
        {
            var info = BoardManager.Instance.GetSpaceInfo(i);
            if (info != null && info.spaceType == SpaceInfo.SpaceType.Jail)
                return i;
        }
        return -1;
    }

    [Server]
    private void SendToJail(PlayerObject requester, bool fromTripleDoubles = false, bool fromLandingOnGoToJail = false)
    {
        int jailIndex = GetJailSpaceIndex();
        if (jailIndex < 0)
        {
            Debug.LogWarning("[Turn] Tahtada Jail tipinde kare yok; hapishane kuralı atlandı.");
            AdvanceTurn();
            return;
        }
        if (requester != null)
        {
            requester.currentSpaceIndex = jailIndex;
            requester.isInJail = true;
            _jailRollAttempts[requester.playerIndex] = 0;
            _jailExitWithNextRoll.Remove(requester.playerIndex);
            if (fromLandingOnGoToJail)
                _jailSkipFirstDoublesExit[requester.playerIndex] = true;
            else
                _jailSkipFirstDoublesExit.Remove(requester.playerIndex);
        }
        if (fromTripleDoubles)
            RpcShowNotification(requester != null
                ? $"{requester.steamName} üst üste {doublesCountForJail}. çift zarı attı - hapishaneye gitti!"
                : $"Üst üste {doublesCountForJail} çift zar - hapishaneye!");
        else
            RpcShowNotification(requester != null
                ? $"{requester.steamName} Hapishaneye Gitti karesine indi!"
                : "Hapishaneye gitti!");
        AdvanceTurn();
    }

    [Server]
    private IEnumerator ServerJailRoll(PlayerObject requester)
    {
        isRolling = true;
        rollingPlayerIndex = requester != null ? requester.playerIndex : -1;
        int attempts = 0;
        if (requester != null && _jailRollAttempts.TryGetValue(requester.playerIndex, out var a))
            attempts = a;
        attempts++;
        if (requester != null)
            _jailRollAttempts[requester.playerIndex] = attempts;

        int dice1 = Random.Range(1, 7);
        int dice2 = Random.Range(1, 7);
        int roll = dice1 + dice2;
        lastRollValue = roll;
        lastRollDice1 = dice1;
        lastRollDice2 = dice2;
        lastRollPlayerIndex = requester != null ? requester.playerIndex : -1;
        _lastRollWasDoubles = false;

        RpcPlayDiceRoll();
        RpcPlayDiceLand(dice1, dice2);
        yield return new WaitForSeconds(Mathf.Max(0f, delayBeforeMoveAfterRoll));

        isRolling = false;
        rollingPlayerIndex = -1;

        // Çift zar = iki zarda AYNI sayı (2+2, 4+4 vb.), toplamın çift olması değil
        bool isDoubles = (dice1 == dice2);
        bool skipFirstDoubles = requester != null && attempts == 1 && _jailSkipFirstDoublesExit.TryGetValue(requester.playerIndex, out var skip) && skip;

        if (isDoubles && !skipFirstDoubles)
        {
            if (requester != null)
            {
                requester.isInJail = false;
                _jailRollAttempts.Remove(requester.playerIndex);
                _jailExitWithNextRoll.Remove(requester.playerIndex);
                _jailSkipFirstDoublesExit.Remove(requester.playerIndex);
            }
            RpcShowNotification(requester != null ? $"{requester.steamName} {dice1}+{dice2} çift zar, hapishaneden çıktı!" : $"{dice1}+{dice2} çift zar - hapishaneden çıkıldı!");
            StartCoroutine(ServerRollAndMove(requester, roll, dice1, dice2));
        }
        else if (isDoubles && skipFirstDoubles)
        {
            if (requester != null)
                _jailSkipFirstDoublesExit.Remove(requester.playerIndex);
            RpcShowNotification(requester != null ? $"{requester.steamName} çift zar attı ama Hapishaneye Git karesinden geldiği için bu tur çıkamıyor. ({attempts}/{maxJailTurns})" : "Çift zar - bu tur hapisten çıkılamıyor.");
            AdvanceTurn();
        }
        else if (attempts >= maxJailTurns)
        {
            if (requester != null)
                _jailExitWithNextRoll[requester.playerIndex] = true;
            RpcShowNotification(requester != null
                ? $"{requester.steamName} {maxJailTurns} denemede çift atamadı - sıra tekrar gelince attığın zarla çıkacaksın."
                : $"{maxJailTurns} denemede çift atılamadı - sıra gelince çıkılacak.");
            AdvanceTurn();
        }
        else
        {
            RpcShowNotification(requester != null
                ? $"{requester.steamName} çift zar atamadı - hapiste kalıyor. ({attempts}/{maxJailTurns} deneme)"
                : "Çift zar atılamadı.");
            AdvanceTurn();
        }
    }

    /// <summary>3 denemede çift atamayan oyuncu: sıra gelince attığı zarla hapisten çıkar, ilerler.</summary>
    [Server]
    private IEnumerator ServerJailExitWithRoll(PlayerObject requester)
    {
        isRolling = true;
        rollingPlayerIndex = requester != null ? requester.playerIndex : -1;
        int dice1 = Random.Range(1, 7);
        int dice2 = Random.Range(1, 7);
        int roll = dice1 + dice2;
        lastRollValue = roll;
        lastRollDice1 = dice1;
        lastRollDice2 = dice2;
        lastRollPlayerIndex = requester != null ? requester.playerIndex : -1;
        _lastRollWasDoubles = (dice1 == dice2);

        RpcPlayDiceRoll();
        RpcPlayDiceLand(dice1, dice2);
        yield return new WaitForSeconds(Mathf.Max(0f, delayBeforeMoveAfterRoll));

        isRolling = false;
        rollingPlayerIndex = -1;

        if (requester != null)
        {
            requester.isInJail = false;
            _jailRollAttempts.Remove(requester.playerIndex);
            _jailExitWithNextRoll.Remove(requester.playerIndex);
        }
        RpcShowNotification(requester != null ? $"{requester.steamName} hapisten çıktı, attığı zarla ilerliyor." : "Hapisten çıkıldı.");
        StartCoroutine(ServerRollAndMove(requester, roll, dice1, dice2));
    }

    /// <summary>Gündem Çifte Şansızlık: zar atılır ama hareket edilmez.</summary>
    [Server]
    private IEnumerator ServerRollNoMove(PlayerObject requester, int dice1, int dice2)
    {
        isRolling = true;
        rollingPlayerIndex = requester != null ? requester.playerIndex : -1;
        lastRollValue = dice1 + dice2;
        lastRollDice1 = dice1;
        lastRollDice2 = dice2;
        lastRollPlayerIndex = requester != null ? requester.playerIndex : -1;

        RpcPlayDiceRoll();
        RpcPlayDiceLand(dice1, dice2);
        yield return new WaitForSeconds(Mathf.Max(0f, delayBeforeMoveAfterRoll));

        isRolling = false;
        rollingPlayerIndex = -1;
        RpcShowNotification(requester != null
            ? $"{requester.steamName} çift zar attı ama Gündem: Çifte Şansızlık - ilerleyemez!"
            : "Çifte Şansızlık: ilerleme yok.");
        AdvanceTurn();
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
        int startBonus = startInfo != null ? GameEconomy.ScalePrice(startInfo.startBonus) : 0;

        for (int i = 0; i < steps; i++)
        {
            if (requester == null)
            {
                isRolling = false;
                rollingPlayerIndex = -1;
                yield break;
            }
            requester.ServerMoveBy(1, boardSpaceCount);
            RpcPlaySpaceLanding(requester.currentSpaceIndex);
            if (requester.currentSpaceIndex == 0 && startBonus > 0)
            {
                requester.hasPassedStart = true;
                requester.money += startBonus;
                if (GameStatsManager.Instance != null)
                {
                    GameStatsManager.Instance.RecordEarned(requester.playerIndex, startBonus);
                    GameStatsManager.Instance.RecordPassedGo(requester.playerIndex);
                }
                RpcPlayCoinGain();
                RpcShowNotification($"{requester.steamName} Start'tan geçti: +{GameEconomy.FormatMoney(startBonus)}");
            }
            yield return new WaitForSeconds(stepDelay);
        }

        int landedIndex = requester.currentSpaceIndex;
        Debug.Log($"[Turn] Oyuncu {requester.playerIndex} zar: {dice1}+{dice2}={roll}, yeni index: {landedIndex}");

        if (GameStatsManager.Instance != null)
            GameStatsManager.Instance.RecordLanding(requester.playerIndex, landedIndex);

        isRolling = false;
        rollingPlayerIndex = -1;

        var info = BoardManager.Instance != null ? BoardManager.Instance.GetSpaceInfo(landedIndex) : null;
        var spaceType = info != null ? info.spaceType : SpaceInfo.SpaceType.Normal;

        // Özel kareler
        switch (spaceType)
        {
            case SpaceInfo.SpaceType.Start:
                break;
            case SpaceInfo.SpaceType.KaderAni:
                // Kader Anı: Kart çek - güvenli/riskli yol veya "Merkez Bankası'na git"
                var kaderCard = GetRandomKaderAniCard();
                if (kaderCard.goToCentralBank)
                {
                    RpcShowEventPopup("KADER ANI", kaderCard.text, requester.steamName, 0);
                    int merkezIdx = GetMerkezBankasiSpaceIndex();
                    if (merkezIdx >= 0)
                    {
                        requester.currentSpaceIndex = merkezIdx;
                        int pool = centralBankPool;
                        centralBankPool = 0;
                        if (pool > 0)
                        {
                            requester.money += pool;
                            if (GameStatsManager.Instance != null)
                                GameStatsManager.Instance.RecordEarned(requester.playerIndex, pool);
                            RpcShowNotification($"{requester.steamName} Merkez Bankası'na gitti - {GameEconomy.FormatMoney(pool)} aldı!");
                            RpcPlayCoinGain();
                        }
                    }
                }
                else if (kaderCard.amount != 0)
                {
                    int scaledAmount = GameEconomy.ScalePrice(Mathf.Abs(kaderCard.amount)) * (kaderCard.amount >= 0 ? 1 : -1);
                    requester.money = Mathf.Max(0, requester.money + scaledAmount);
                    if (GameStatsManager.Instance != null)
                    {
                        if (scaledAmount > 0) GameStatsManager.Instance.RecordEarned(requester.playerIndex, scaledAmount);
                        else GameStatsManager.Instance.RecordSpent(requester.playerIndex, -scaledAmount);
                    }
                    RpcShowEventPopup("KADER ANI", kaderCard.text, requester.steamName, scaledAmount);
                    if (requester.money <= 0)
                        ServerHandleBankruptcy(requester, "Kader Anı kartından ödemeden sonra");
                }
                else
                {
                    RpcShowEventPopup("KADER ANI", kaderCard.text, requester.steamName, 0);
                }
                break;
            case SpaceInfo.SpaceType.MerkezBankasi:
                // Merkez Bankası: Biriken parayı al
                int bankPool = centralBankPool;
                centralBankPool = 0;
                if (bankPool > 0)
                {
                    requester.money += bankPool;
                    if (GameStatsManager.Instance != null)
                        GameStatsManager.Instance.RecordEarned(requester.playerIndex, bankPool);
                    RpcShowEventPopup("MERKEZ BANKASI", $"{requester.steamName} biriken parayı aldı!", requester.steamName, bankPool);
                    RpcPlayCoinGain();
                }
                else
                {
                    RpcShowEventPopup("MERKEZ BANKASI", "Kasa boş - ödül yok.", requester.steamName, 0);
                }
                break;
            case SpaceInfo.SpaceType.Kumar:
                // Kumar: Zar at - 8+ ise 5x nakit, 8- ise 10x merkez bankasına öde
                int kumarD1 = Random.Range(1, 7);
                int kumarD2 = Random.Range(1, 7);
                int kumarSum = kumarD1 + kumarD2;
                lastRollValue = kumarSum;
                lastRollDice1 = kumarD1;
                lastRollDice2 = kumarD2;
                lastRollPlayerIndex = requester.playerIndex;
                isRolling = true;
                rollingPlayerIndex = requester.playerIndex;
                RpcPlayDiceRoll();
                RpcPlayDiceLand(kumarD1, kumarD2);
                yield return new WaitForSeconds(2.5f);
                isRolling = false;
                rollingPlayerIndex = -1;
                if (kumarSum >= 8)
                {
                    int win = GameEconomy.ScalePrice(kumarSum * 5);
                    requester.money += win;
                    if (GameStatsManager.Instance != null)
                        GameStatsManager.Instance.RecordEarned(requester.playerIndex, win);
                    RpcShowEventPopup("KUMAR", $"{requester.steamName} zar attı: {kumarSum} (8+)", requester.steamName, win);
                    RpcPlayCoinGain();
                }
                else
                {
                    int pay = GameEconomy.ScalePrice(kumarSum * 10);
                    requester.money = Mathf.Max(0, requester.money - pay);
                    centralBankPool += pay;
                    if (GameStatsManager.Instance != null)
                        GameStatsManager.Instance.RecordSpent(requester.playerIndex, pay);
                    RpcShowEventPopup("KUMAR", $"{requester.steamName} zar attı: {kumarSum} (8-)", requester.steamName, -pay);
                    if (requester.money <= 0)
                        ServerHandleBankruptcy(requester, "kumar masasından ödemeden sonra");
                }
                break;
            case SpaceInfo.SpaceType.KureselFon:
                // Küresel Fon: Sahip olunan yerlerin kira değerinin %20'si merkez bankasına
                int totalRentValue = GetTotalRentValueForPlayer(requester.playerIndex);
                int kureselAmount = Mathf.Max(0, totalRentValue * 20 / 100);
                if (kureselAmount > 0)
                {
                    requester.money = Mathf.Max(0, requester.money - kureselAmount);
                    centralBankPool += kureselAmount;
                    if (GameStatsManager.Instance != null)
                        GameStatsManager.Instance.RecordSpent(requester.playerIndex, kureselAmount);
                    RpcShowEventPopup("KÜRESEL FON", $"Sahip olunan yerlerin kira değerinin %20'si Merkez Bankası'na ödendi.", requester.steamName, -kureselAmount);
                    if (requester.money <= 0)
                        ServerHandleBankruptcy(requester, "Küresel Fon ödemesinden sonra");
                }
                else
                {
                    RpcShowEventPopup("KÜRESEL FON", "Mülk yok - ödeme yok.", requester.steamName, 0);
                }
                break;
            case SpaceInfo.SpaceType.Gundem:
                // Gündem: Kart çek, etkisi bir sonraki Gündem'e kadar geçerli
                var gundemCard = GetRandomGundemCard();
                activeAgendaEffect = gundemCard.effect;
                RpcShowEventPopup("GÜNDEM", gundemCard.text, requester.steamName, 0);
                RpcShowNotification($"Gündem: {gundemCard.text}");
                break;
            case SpaceInfo.SpaceType.Jail:
                RpcShowNotification($"{requester.steamName} hapishanede ziyaret");
                break;
            case SpaceInfo.SpaceType.GoToJail:
                SendToJail(requester, fromTripleDoubles: false, fromLandingOnGoToJail: true);
                yield break;
            default:
                // Mülk/kira mantığı
                if (PropertyManager.Instance != null)
                {
                    if (PropertyManager.Instance.MustPayRent(landedIndex, requester.playerIndex))
                    {
                        int baseRent = info != null ? GameEconomy.ScalePrice(info.rent) : 0;
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
                                    ServerHandleBankruptcy(requester, "kirayı ödeyemedi");
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
                            if (_lastRollWasDoubles)
                            {
                                _lastRollWasDoubles = false;
                                if (_consecutiveDoublesCount >= doublesCountForJail)
                                {
                                    _consecutiveDoublesCount = 0;
                                    SendToJail(requester, fromTripleDoubles: true);
                                }
                                else
                                {
                                    RpcShowNotification("Çift zar! Tekrar at.");
                                    if (requester != null && requester.isBot)
                                    {
                                        RpcShowNotification($"{requester.steamName} tekrar zar atıyor (çift zar).");
                                        StartCoroutine(BotTurnAfterDelay(requester, 2.5f));
                                    }
                                }
                            }
                            else
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
                    // Kendi mülküne veya takım arkadaşının mülküne indin: ev dikme teklifi (2v2'de takım yeri senin yerin gibi)
                    int ownerIdx = PropertyManager.Instance.GetOwner(landedIndex);
                    bool isOwnOrTeammate = (ownerIdx == requester.playerIndex) ||
                        (isTeamGame && ownerIdx >= 0 && AreSameTeam(ownerIdx, requester.playerIndex));
                    if (isOwnOrTeammate)
                    {
                        int houses = PropertyManager.Instance.GetHouseCount(landedIndex);
                        if (houses < 5 && (houses == 4 || houses < 3 || requester.hasPassedStart))
                        {
                            var spaceInfo = BoardManager.Instance != null ? BoardManager.Instance.GetSpaceInfo(landedIndex) : null;
                            if (spaceInfo != null && spaceInfo.IsPurchasable && spaceInfo.CanBuildHouses)
                            {
                                int housePriceDesign = spaceInfo.housePrice > 0 ? spaceInfo.housePrice : (spaceInfo.purchasePrice / 2);
                                int housePrice = GameEconomy.ScalePrice(housePriceDesign);
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

        if (_lastRollWasDoubles)
        {
            _lastRollWasDoubles = false;
            if (_consecutiveDoublesCount >= doublesCountForJail)
            {
                _consecutiveDoublesCount = 0;
                SendToJail(requester, fromTripleDoubles: true);
            }
            else
            {
                RpcShowNotification("Çift zar! Tekrar at.");
                if (requester != null && requester.isBot)
                {
                    RpcShowNotification($"{requester.steamName} tekrar zar atıyor (çift zar).");
                    StartCoroutine(BotTurnAfterDelay(requester, 2.5f));
                }
            }
        }
        else
        {
            AdvanceTurn();
        }
    }

    [Server]
    public void ServerAdvanceTurnAfterPropertyAction(PlayerObject player)
    {
        if (_lastRollWasDoubles)
        {
            _lastRollWasDoubles = false;
            if (_consecutiveDoublesCount >= doublesCountForJail)
            {
                _consecutiveDoublesCount = 0;
                var p = GetPlayerByIndex(currentTurnPlayerIndex);
                SendToJail(p, fromTripleDoubles: true);
            }
            else
            {
                RpcShowNotification("Çift zar! Tekrar at.");
                var p = GetPlayerByIndex(currentTurnPlayerIndex);
                if (p != null && p.isBot)
                {
                    RpcShowNotification($"{p.steamName} tekrar zar atıyor (çift zar).");
                    StartCoroutine(BotTurnAfterDelay(p, 2.5f));
                }
            }
        }
        else
        {
            AdvanceTurn();
        }
    }

    [Server]
    public PlayerObject GetPlayerByIndexPublic(int playerIndex)
    {
        return GetPlayerByIndex(playerIndex);
    }

    /// <summary> 2v2: Aynı takımda mı? </summary>
    [Server]
    public bool AreSameTeam(int playerIndexA, int playerIndexB)
    {
        if (!isTeamGame || playerIndexA < 0 || playerIndexB < 0) return false;
        var a = GetPlayerByIndex(playerIndexA);
        var b = GetPlayerByIndex(playerIndexB);
        return a != null && b != null && a.teamIndex == b.teamIndex;
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

    [Server]
    public void ServerPayToLeaveJail(PlayerObject requester)
    {
        if (requester == null) return;
        if (!requester.isInJail) return;
        int fee = GameEconomy.ScalePrice(Mathf.Max(0, jailReleaseFee));
        if (fee > 0 && requester.money < fee) return;

        if (fee > 0)
        {
            requester.money -= fee;
            if (GameStatsManager.Instance != null)
                GameStatsManager.Instance.RecordSpent(requester.playerIndex, fee);
        }

        requester.isInJail = false;
        _jailRollAttempts.Remove(requester.playerIndex);
        _jailExitWithNextRoll.Remove(requester.playerIndex);

        string name = string.IsNullOrWhiteSpace(requester.steamName) ? $"Oyuncu {requester.playerIndex}" : requester.steamName;
        if (fee > 0)
            RpcShowNotification($"{name} hapisten çıkmak için {GameEconomy.FormatMoney(fee)} ödedi. Zar atıyor...");
        else
            RpcShowNotification($"{name} hapisten çıktı. Zar atıyor...");

        StartCoroutine(PayToLeaveJailThenRoll(requester, 1.5f));
    }

    [Server]
    private IEnumerator PayToLeaveJailThenRoll(PlayerObject requester, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (requester != null && !isRolling)
            ServerTryRoll(requester);
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
    private void RpcPlaySpaceLanding(int spaceIndex)
    {
        if (BoardManager.Instance != null)
            BoardManager.Instance.PlaySpaceLandingAnimation(spaceIndex);
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
        new ChanceCard { text = "Doğum günü hediyesi! Her oyuncudan 10 birim al.", amount = 30 },
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

    private static readonly KaderAniCard[] KaderAniCards =
    {
        new KaderAniCard { text = "Güvenli yol: Banka faizi.", amount = 40, goToCentralBank = false },
        new KaderAniCard { text = "Güvenli yol: Vergi iadesi.", amount = 80, goToCentralBank = false },
        new KaderAniCard { text = "Güvenli yol: Küçük harcama.", amount = -30, goToCentralBank = false },
        new KaderAniCard { text = "Güvenli yol: Kira geliri.", amount = 50, goToCentralBank = false },
        new KaderAniCard { text = "Riskli yol: Büyük kazanç!", amount = 120, goToCentralBank = false },
        new KaderAniCard { text = "Riskli yol: Ağır ceza.", amount = -100, goToCentralBank = false },
        new KaderAniCard { text = "Merkez Bankası'na git!", amount = 0, goToCentralBank = true },
    };

    [Server]
    private KaderAniCard GetRandomKaderAniCard()
    {
        return KaderAniCards[Random.Range(0, KaderAniCards.Length)];
    }

    [Server]
    private int GetMerkezBankasiSpaceIndex()
    {
        if (BoardManager.Instance == null) return -1;
        int n = boardSpaceCount > 0 ? boardSpaceCount : BoardManager.SpaceCount;
        for (int i = 0; i < n; i++)
        {
            var info = BoardManager.Instance.GetSpaceInfo(i);
            if (info != null && info.spaceType == SpaceInfo.SpaceType.MerkezBankasi)
                return i;
        }
        return -1;
    }

    private static readonly GundemCard[] GundemCards =
    {
        new GundemCard { text = "Enflasyon: Tüm kira bedelleri %20 artar.", effect = (int)AgendaEffect.Inflation },
        new GundemCard { text = "Çifte Şansızlık: Çift zar atanlar ilerleyemez.", effect = (int)AgendaEffect.DoubleMisfortune },
        new GundemCard { text = "İstikrar: Özel etki yok.", effect = (int)AgendaEffect.None },
    };

    [Server]
    private GundemCard GetRandomGundemCard()
    {
        return GundemCards[Random.Range(0, GundemCards.Length)];
    }

    [Server]
    private int GetTotalRentValueForPlayer(int playerIndex)
    {
        if (PropertyManager.Instance == null || BoardManager.Instance == null) return 0;
        int total = 0;
        int n = boardSpaceCount > 0 ? boardSpaceCount : BoardManager.SpaceCount;
        for (int i = 0; i < n; i++)
        {
            if (PropertyManager.Instance.GetOwner(i) != playerIndex) continue;
            var info = BoardManager.Instance.GetSpaceInfo(i);
            if (info == null) continue;
            int baseRent = GameEconomy.ScalePrice(info.rent);
            int rent = PropertyManager.Instance.GetRentWithHouses(i, baseRent);
            total += rent;
        }
        return total;
    }

    [ClientRpc]
    private void RpcShowEventPopup(string title, string cardText, string playerName, int amount)
    {
        LastCardTitle = title ?? "";
        LastCardText = cardText ?? "";
        LastCardPlayerName = playerName ?? "";
        LastCardAmount = amount;
        LastCardIsChance = false;
        LastCardTime = Time.time;
        string msg = amount != 0
            ? $"{playerName}: {cardText} ({(amount > 0 ? "+" : "")}{GameEconomy.FormatMoney(amount)})"
            : $"{playerName}: {cardText}";
        LastNotification = msg;
        LastNotificationTime = Time.time;
        Debug.Log($"[OLAY] {title} | {playerName} | {cardText} | {(amount != 0 ? $"{(amount > 0 ? "+" : "")}{GameEconomy.FormatMoney(amount)}" : "-")}");
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayNotification();
    }

    [ClientRpc]
    private void RpcShowCardNotification(bool isChance, string cardText, string playerName, int amount)
    {
        LastCardTitle = isChance ? "ŞANS" : "KASA";
        LastCardText = cardText ?? "";
        LastCardPlayerName = playerName ?? "";
        LastCardAmount = amount;
        LastCardIsChance = isChance;
        LastCardTime = Time.time;
        string msg = amount != 0
            ? $"{playerName}: {cardText} ({(amount > 0 ? "+" : "")}{GameEconomy.FormatMoney(amount)})"
            : $"{playerName}: {cardText}";
        LastNotification = msg;
        LastNotificationTime = Time.time;
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayNotification();
    }

    [ClientRpc]
    private void RpcShowRentNotification(string payerName, string ownerName, int amount)
    {
        LastNotification = $"{payerName} {GameEconomy.FormatMoney(amount)} kira ödedi ({ownerName})";
        LastNotificationTime = Time.time;
        if (AudioManager.Instance != null) AudioManager.Instance.PlayRentPay();
    }

    [Server]
    public void ServerSendStatsTo(NetworkConnectionToClient conn)
    {
        if (conn == null || !conn.isReady) return;
        string data = GameStatsManager.Instance != null ? GameStatsManager.Instance.GetSerializedStats() : "";
        TargetRpcReceiveStats(conn, data);
    }

    [TargetRpc]
    private void TargetRpcReceiveStats(NetworkConnection conn, string data)
    {
        string formatted = GameStatsManager.FormatStatsForDisplay(data);
        GameStatsManager.SetLastStatsText(formatted);
    }

    [Server]
    public void ServerHandleBankruptcyPublic(PlayerObject player)
    {
        ServerHandleBankruptcy(player);
    }

    [Server]
    public void ServerHandleBankruptcyWithReason(PlayerObject player, string reason)
    {
        ServerHandleBankruptcy(player, reason);
    }

    [Server]
    private void ServerHandleBankruptcy(PlayerObject player, string reason = null)
    {
        if (player == null) return;
        if (bankruptPlayerIndices.Contains(player.playerIndex)) return;
        _jailRollAttempts.Remove(player.playerIndex);
        _jailExitWithNextRoll.Remove(player.playerIndex);
        bankruptPlayerIndices.Add(player.playerIndex);
        RpcPlayBankrupt();
        string msg = !string.IsNullOrEmpty(reason)
            ? $"{player.steamName} {reason} - iflas etti!"
            : $"{player.steamName} iflas etti!";
        RpcShowNotification(msg);
        RpcShowEventPopup("İFLAS", msg, player.steamName, 0);
        Debug.Log($"[Turn] P{player.playerIndex} iflas etti: " + msg);

        var remaining = GetOrderedPlayers();
        if (isTeamGame && remaining.Count > 0)
        {
            int team0 = 0, team1 = 0;
            foreach (var p in remaining)
            {
                if (p.teamIndex == 0) team0++; else team1++;
            }
            if (team0 == 0 || team1 == 0)
            {
                int winningTeam = team0 > 0 ? 0 : 1;
                var first = remaining[0];
                winnerPlayerIndex = first.playerIndex;
                winnerName = $"Takım {winningTeam + 1}";
                Debug.Log($"[Turn] Oyun bitti! Kazanan: {winnerName}");
            }
        }
        else if (remaining.Count == 1)
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

        _consecutiveDoublesCount = 0;

        int nextPos = (currentPos + 1) % players.Count;
        currentTurnPlayerIndex = players[nextPos].playerIndex;
        turnNumber++;
        _turnStartNetworkTime = NetworkTime.time;
        _turnStartTime = Time.time;

        var nextPlayer = GetPlayerByIndex(currentTurnPlayerIndex);
        if (nextPlayer != null && nextPlayer.isBot && !isRolling)
            StartCoroutine(BotTurnAfterDelay(nextPlayer, minTurnStartDelay));
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
        if (winnerPlayerIndex >= 0) yield break;

        // Once onceki zar + hareket + panel islemleri bitene kadar bekle (max ~8 sn)
        float timeout = Time.time + 8f;
        while (isRolling && Time.time < timeout)
            yield return null;
        if (Time.time >= timeout) yield break;

        yield return new WaitForSeconds(0.2f);
        if (bot == null || !bot.isBot) yield break;
        if (winnerPlayerIndex >= 0 || currentTurnPlayerIndex != bot.playerIndex || isRolling) yield break;
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
        int housePriceDesign = info != null ? (info.housePrice > 0 ? info.housePrice : (info.purchasePrice / 2)) : 0;
        int housePrice = GameEconomy.ScalePrice(housePriceDesign);
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
        int purchasePrice = info != null ? GameEconomy.ScalePrice(info.purchasePrice) : 0;
        if (info == null || bot.money < purchasePrice) { PropertyManager.Instance.ServerDeclineBuy(bot, spaceIndex); yield break; }
        if (Random.value <= 0.3f) { PropertyManager.Instance.ServerDeclineBuy(bot, spaceIndex); yield break; }
        int count = 1;
        int housePriceDesign = info.housePrice > 0 ? info.housePrice : (info.purchasePrice / 2);
        int housePrice = GameEconomy.ScalePrice(housePriceDesign);
        if (housePrice > 0 && bot.money >= purchasePrice + housePrice && Random.value > 0.5f)
            count = Mathf.Min(4, 1 + (bot.money - purchasePrice) / housePrice);
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
        int baseRent = info != null ? GameEconomy.ScalePrice(info.rent) : 0;
        int rent = PropertyManager.Instance.GetRentWithHouses(spaceIndex, baseRent);
        int buyPrice = rent * 2;

        if (bot.money >= buyPrice && Random.value > 0.5f)
            PropertyManager.Instance.ServerBuyFromOwner(bot, spaceIndex);
        else
            PropertyManager.Instance.ServerDeclineBuy(bot, spaceIndex);
    }
}
