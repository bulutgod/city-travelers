using UnityEngine;

/// <summary>
/// Tek bir tahta karesinin verisi (isim, renk, tip).
/// ScriptableObject icinde dizi olarak kullanilir.
/// </summary>
[System.Serializable]
public class SpaceInfo
{
    [Tooltip("Kare adi. Alt satir icin \\n kullanin (örn. Nice Patra\\nHavalimani).")]
    [TextArea(2, 4)]
    public string displayName = "";

    [Tooltip("Kare rengi (placeholder kupunun rengi)")]
    public Color color = Color.gray;

    public SpaceType spaceType = SpaceType.Normal;

    [Tooltip("Satın alma fiyatı (Normal tip için). 0 = satın alınamaz.")]
    public int purchasePrice = 0;

    [Tooltip("Kira (Normal tip için, sahibi varsa ödenir).")]
    public int rent = 0;

    [Tooltip("Vergi miktarı (Tax tipi için).")]
    public int taxAmount = 0;

    [Tooltip("Start'tan geçişte verilen bonus.")]
    public int startBonus = 0;

    [Tooltip("Ev fiyatı (0 = purchasePrice/2 kullanılır).")]
    public int housePrice = 0;

    public enum SpaceType
    {
        Normal,         // Satın alınabilir mülk (ev dikilebilir)
        Start,          // Başlangıç / Go
        KaderAni,       // Kart çekme: güvenli yol veya riskli yol (zar)
        MerkezBankasi,  // Kumar/Küresel Fon'dan biriken para burada, gelen oyuncu alır
        Kumar,          // Zar at: 8+ ise 5x nakit, 8- ise 10x merkez bankasına öde
        KureselFon,     // Sahip olunan yerlerin kira değerinin %20'si merkez bankasına
        Havalimani,     // Satın alınabilir, bina dikilmez, her havalimanı kira 2 katına çıkar
        Gundem,         // Kart çek: etkisi bir sonraki Gündem'e kadar geçerli
        Jail,           // Hapishane / Ziyaret
        GoToJail        // Hapishaneye git
    }

    /// <summary>
    /// Satın alınabilir mülk mü? (Normal veya Havalimani + fiyat > 0)
    /// </summary>
    public bool IsPurchasable => (spaceType == SpaceType.Normal || spaceType == SpaceType.Havalimani) && purchasePrice > 0;

    /// <summary>
    /// Bina (ev/otel) dikilebilir mi? Havalimanlarında dikilmez.
    /// </summary>
    public bool CanBuildHouses => spaceType == SpaceType.Normal && purchasePrice > 0;
}

/// <summary>
/// 38 karelik tahta tanimi. Inspector'da duzenleyebilir, BoardManager'a atanir.
/// Create > RichContractor > Board Spaces Data ile yeni asset olustur.
/// </summary>
[CreateAssetMenu(fileName = "BoardSpaces", menuName = "RichContractor/Board Spaces Data", order = 0)]
public class BoardSpaceData : ScriptableObject
{
    public const int Count = 38;

    [Tooltip("Her kare icin isim, renk ve tip. Sira tahta ustunden saat yonunde (0=baslangic).")]
    public SpaceInfo[] spaces = new SpaceInfo[Count];

    private void OnValidate()
    {
        if (spaces == null || spaces.Length != Count)
            System.Array.Resize(ref spaces, Count);
    }

    public SpaceInfo GetSpace(int index)
    {
        if (spaces == null || index < 0 || index >= spaces.Length)
            return null;
        return spaces[index];
    }
}
