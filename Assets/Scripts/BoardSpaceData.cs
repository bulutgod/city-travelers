using UnityEngine;

/// <summary>
/// Tek bir tahta karesinin verisi (isim, renk, tip).
/// ScriptableObject icinde dizi olarak kullanilir.
/// </summary>
[System.Serializable]
public class SpaceInfo
{
    [Tooltip("Kare adi (örn. Baslangic, Vergi, Sokak adi)")]
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

    public enum SpaceType
    {
        Normal,
        Start,      // Baslangic / Go
        Chance,     // Sans karti
        Community,  // Kasa
        Tax,        // Vergi
        Jail,       // Hapishane / Ziyaret
        FreeParking,
        GoToJail,
        Railway,
        Utility
    }

    /// <summary>
    /// Satın alınabilir mülk mü? (Normal tip + fiyat > 0)
    /// </summary>
    public bool IsPurchasable => spaceType == SpaceType.Normal && purchasePrice > 0;
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
