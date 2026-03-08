using UnityEngine;

/// <summary>
/// Oyun ekonomisi: başlangıç parası, para birimi sembolü (teliften kaçınmak için TL yok), ölçek.
/// Tahta verileri (BoardSpaces) "tasarım değerleri" (60, 40, 200...) olarak kalır; oyunda kullanırken Scale ile çarpılır.
/// Kira artışı formülü değişmez: ev sayısına göre 2^ev (1x, 2x, 4x, 8x, 16x, otel 32x).
/// </summary>
public static class GameEconomy
{
    /// <summary>Oyuna başlarken her oyuncunun parası (milyonlarla başlamak için örn. 1_500_000).</summary>
    public static int StartingMoney = 1_500_000;

    /// <summary>Para birimi sembolü (TL yerine telif riski olmayan sembol, örn. ¤).</summary>
    public static string CurrencySymbol = "¤";

    /// <summary>Tasarım değerleri bu katsayı ile çarpılarak oyun parasına dönüşür. 1000 = 60→60.000, 200→200.000.</summary>
    public static int Scale = 1000;

    /// <summary>Tahta/karttaki tasarım fiyatını oyun parasına çevirir (satın alma, kira, ev fiyatı, vergi, start bonus).</summary>
    public static int ScalePrice(int designValue)
    {
        if (designValue <= 0) return 0;
        return designValue * Scale;
    }

    /// <summary>Parayı gösterim için formatlar (örn. "1.500.000 ¤" veya "1,500,000 ¤").</summary>
    public static string FormatMoney(int amount)
    {
        return $"{amount:N0} {CurrencySymbol}";
    }
}
