using System.Text;
using UnityEngine;

/// <summary>
/// Oyun istatistiklerini toplar (sunucuda kayıt, client'a GameTurnManager üzerinden gönderilir).
/// Herhangi bir GameObject'e eklenebilir; NetworkIdentity gerekmez.
/// </summary>
public class GameStatsManager : MonoBehaviour
{
    public static GameStatsManager Instance { get; private set; }

    private const int MaxPlayers = 4;
    private const int SpaceCount = 38;

    private readonly int[] _landingsPerSpace = new int[SpaceCount];
    private readonly int[] _earned = new int[MaxPlayers];
    private readonly int[] _spent = new int[MaxPlayers];
    private readonly int[] _rentPaid = new int[MaxPlayers];
    private readonly int[] _rentReceived = new int[MaxPlayers];
    private readonly int[] _timesPassedGo = new int[MaxPlayers];

    private static string _lastStatsText = "";

    public static string GetLastStatsText()
    {
        return _lastStatsText;
    }

    public static void SetLastStatsText(string text)
    {
        _lastStatsText = text ?? "";
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void RecordLanding(int playerIndex, int spaceIndex)
    {
        if (spaceIndex >= 0 && spaceIndex < SpaceCount && playerIndex >= 0 && playerIndex < MaxPlayers)
            _landingsPerSpace[spaceIndex]++;
    }

    public void RecordEarned(int playerIndex, int amount)
    {
        if (amount <= 0 || playerIndex < 0 || playerIndex >= MaxPlayers) return;
        _earned[playerIndex] += amount;
    }

    public void RecordSpent(int playerIndex, int amount)
    {
        if (amount <= 0 || playerIndex < 0 || playerIndex >= MaxPlayers) return;
        _spent[playerIndex] += amount;
    }

    public void RecordRentPaid(int payerIndex, int amount)
    {
        if (payerIndex >= 0 && payerIndex < MaxPlayers && amount > 0)
            _rentPaid[payerIndex] += amount;
    }

    public void RecordRentReceived(int ownerIndex, int amount)
    {
        if (ownerIndex >= 0 && ownerIndex < MaxPlayers && amount > 0)
            _rentReceived[ownerIndex] += amount;
    }

    public void RecordPassedGo(int playerIndex)
    {
        if (playerIndex >= 0 && playerIndex < MaxPlayers)
            _timesPassedGo[playerIndex]++;
    }

    /// <summary>
    /// Sunucuda çağrılır; serileştirilmiş istatistik dizesini döndürür.
    /// </summary>
    public string GetSerializedStats()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < SpaceCount; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(_landingsPerSpace[i]);
        }
        sb.Append('|');
        for (int p = 0; p < MaxPlayers; p++)
        {
            if (p > 0) sb.Append(';');
            sb.Append(_earned[p]).Append(',').Append(_spent[p]).Append(',').Append(_rentPaid[p]).Append(',').Append(_rentReceived[p]).Append(',').Append(_timesPassedGo[p]);
        }
        return sb.ToString();
    }

    public static string FormatStatsForDisplay(string data)
    {
        if (string.IsNullOrEmpty(data)) return "Veri yok.";
        string[] parts = data.Split('|');
        if (parts.Length < 2) return data;
        var sb = new StringBuilder();
        string[] landings = parts[0].Split(',');
        int maxLanding = 0;
        int maxLandingIndex = 0;
        for (int i = 0; i < landings.Length && i < 38; i++)
        {
            int v = 0;
            int.TryParse(landings[i], out v);
            if (v > maxLanding) { maxLanding = v; maxLandingIndex = i; }
        }
        sb.AppendLine("En çok düşülen kare: ").Append(maxLandingIndex).Append(" (").Append(maxLanding).Append(" kez)");
        sb.AppendLine();
        string[] playerParts = parts[1].Split(';');
        for (int p = 0; p < playerParts.Length && p < 4; p++)
        {
            string[] nums = playerParts[p].Split(',');
            int earned = nums.Length > 0 ? int.Parse(nums[0]) : 0;
            int spent = nums.Length > 1 ? int.Parse(nums[1]) : 0;
            int rentPaid = nums.Length > 2 ? int.Parse(nums[2]) : 0;
            int rentReceived = nums.Length > 3 ? int.Parse(nums[3]) : 0;
            int passedGo = nums.Length > 4 ? int.Parse(nums[4]) : 0;
            sb.AppendLine($"Oyuncu {p + 1}:");
            sb.AppendLine($"  Toplam kazanç: {earned} TL");
            sb.AppendLine($"  Toplam harcama: {spent} TL");
            sb.AppendLine($"  Kira ödedi: {rentPaid} TL");
            sb.AppendLine($"  Kira aldı: {rentReceived} TL");
            sb.AppendLine($"  Start'tan geçiş: {passedGo} kez");
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
