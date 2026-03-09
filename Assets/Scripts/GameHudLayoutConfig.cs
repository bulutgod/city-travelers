using UnityEngine;
using TMPro;

/// <summary>
/// GameScene HUD'unun tum UI oge konum, boyut ve font ayarlarini Inspector'dan kontrol etmeni saglar.
/// Assets > Create > Brom City > Game HUD Layout Config ile olustur.
/// GameHudUI'yi sahneye ekleyip bu config'i atayin.
/// </summary>
[CreateAssetMenu(fileName = "GameHudLayout", menuName = "Brom City/Game HUD Layout Config")]
public class GameHudLayoutConfig : ScriptableObject
{
    [Header("Font")]
    [Tooltip("Legacy UI Text icin (Turn, Roll, Money vb.). Bos birakilirsa varsayilan Arial kullanilir. .ttf/.otf dosyasini Project'e surukleyip buraya ata.")]
    public Font customFont;
    [Tooltip("TextMeshPro icin Font Asset. Bos birakilirsa TMP varsayilani kullanilir. Font dosyasindan: Sag tik > Create > TextMeshPro > Font Asset.")]
    public TMP_FontAsset customTmpFont;

    [Header("Genel")]
    [Tooltip("Tum font boyutlarina uygulanan carpan. 1 = varsayilan, 1.5 = %50 buyuk.")]
    [Range(0.5f, 3f)]
    public float globalFontScale = 1f;
    [Tooltip("Genel UI olcek carpani (RectTransform sizeDelta vb.).")]
    [Range(0.5f, 2f)]
    public float globalScale = 1f;

    [Header("Sol Panel - Bilgi Metinleri (Turn, Roll, You, Status, Money)")]
    public Vector2 leftPanelAnchorMin = new Vector2(0, 1);
    public Vector2 leftPanelAnchorMax = new Vector2(0, 1);
    public Vector2 leftPanelPivot = new Vector2(0, 1);
    public Vector2 leftPanelFirstTextPos = new Vector2(12, -12);
    public float leftPanelLineSpacing = 32f;
    public Vector2 leftPanelTextSize = new Vector2(500, 28);
    public int leftPanelFontSize = 22;

    [Header("Player Summary (gizli)")]
    public Vector2 playerSummaryPos = new Vector2(12, -172);
    public int playerSummaryFontSize = 16;

    [Header("Turn Timer")]
    public Vector2 turnTimerPos = new Vector2(12, -204);
    public int turnTimerFontSize = 16;

    [Header("Roll Butonu")]
    public Vector2 rollButtonAnchorMin = new Vector2(1, 0);
    public Vector2 rollButtonAnchorMax = new Vector2(1, 0);
    public Vector2 rollButtonPivot = new Vector2(1, 0);
    public Vector2 rollButtonPosition = new Vector2(-16, 16);
    public Vector2 rollButtonSize = new Vector2(180, 52);
    public int rollButtonFontSize = 18;

    [Header("Oyunu Birak Butonu")]
    public Vector2 leaveButtonPosition = new Vector2(16, 16);
    public Vector2 leaveButtonSize = new Vector2(140, 40);
    public int leaveButtonFontSize = 16;

    [Header("Ayarlar Butonu")]
    public Vector2 settingsButtonAnchorMin = new Vector2(1, 1);
    public Vector2 settingsButtonAnchorMax = new Vector2(1, 1);
    public Vector2 settingsButtonPivot = new Vector2(1, 1);
    public Vector2 settingsButtonPosition = new Vector2(-16, -16);
    public Vector2 settingsButtonSize = new Vector2(80, 36);
    public int settingsButtonFontSize = 24;

    [Header("Player Corner HUDs (4 kose)")]
    [Tooltip("Renkli sprite'lar: 0=kirmizi, 1=mavi, 2=yesil, 3=sari (piyon renkleriyle uyumlu). Atanirsa playerIndex'e gore otomatik atanir.")]
    public Sprite[] cornerHudSprites = new Sprite[4];
    public Vector2 cornerHudSize = new Vector2(140, 56);
    public Vector2 cornerHudOffset = new Vector2(16, 16);
    public Vector2 cornerHudAvatarSize = new Vector2(40, 40);
    public float cornerHudAvatarOffset = 8f;
    public int cornerHudPaddingLeft = 54;
    public int cornerHudPaddingRight = 8;
    public int cornerHudPaddingTop = 8;
    public int cornerHudPaddingBottom = 8;
    public int cornerHudNameFontSize = 14;
    public int cornerHudMoneyFontSize = 16;

    [Header("Escape Menu")]
    public Vector2 escapeMenuPanelSize = new Vector2(320, 280);
    public Vector2 escapeMenuTitlePos = new Vector2(0, -24);
    public Vector2 escapeMenuTitleSize = new Vector2(200, 36);
    public int escapeMenuTitleFontSize = 28;
    public float escapeMenuButtonY = -70f;
    public float escapeMenuButtonSpacing = 50f;
    public Vector2 escapeMenuButtonSize = new Vector2(240, 44);

    [Header("Oyun Suresi Paneli")]
    public Vector2 gameDurationPanelPos = new Vector2(0, -80);
    public Vector2 gameDurationPanelSize = new Vector2(320, 100);
    public Vector2 gameDurationTextPos = new Vector2(0, -12);
    public Vector2 gameDurationTextSize = new Vector2(280, 32);
    public int gameDurationTextFontSize = 24;
    public Vector2 gameDurationButtonRowPos = new Vector2(0, 16);
    public Vector2 gameDurationButtonRowSize = new Vector2(280, 36);
    public Vector2 gameDurationButtonSize = new Vector2(80, 32);
    public float gameDurationButtonSpacing = 95f;
    public int gameDurationButtonFontSize = 14;

    [Header("Quick Chat Bar")]
    public Vector2 quickChatBarPos = new Vector2(0, 56);
    public Vector2 quickChatBarSize = new Vector2(520, 40);
    public int quickChatButtonCount = 6;
    public float quickChatButtonWidth = 72f;
    public float quickChatButtonHeight = 36f;
    public float quickChatButtonSpacing = 8f;
    public int quickChatButtonFontSize = 14;

    [Header("Satın Al Paneli")]
    public Vector2 buyPanelSize = new Vector2(560, 220);
    public Vector2 buyPanelTextPos = new Vector2(0, 30);
    public Vector2 buyPanelTextSize = new Vector2(300, 40);
    public Vector2 buyButtonPos = new Vector2(-70, 20);
    public Vector2 buyButtonSize = new Vector2(100, 36);
    public Vector2 declineButtonPos = new Vector2(70, 20);
    public Vector2 declineButtonSize = new Vector2(100, 36);

    [Header("Ev Dikme Paneli")]
    public Vector2 buildTitlePos = new Vector2(0, 70);
    public Vector2 buildTitleSize = new Vector2(340, 24);
    public Vector2 buildHouseRowPos = new Vector2(0, 20);
    public Vector2 buildHouseRowSize = new Vector2(530, 110);
    public float buildHouseRowSpacing = 12f;
    public Vector2 buildHouseSlotSize = new Vector2(95, 100);
    public Vector2 buildBottomRowPos = new Vector2(0, 8);
    public float buildBottomRowHeight = 44f;
    public float buildBottomSpacing = 16f;

    [Header("Kira/Buy Paneli")]
    public Vector2 rentOrBuyInfoPos = new Vector2(0, 40);
    public Vector2 rentOrBuyInfoSize = new Vector2(500, 60);
    public int rentOrBuyInfoFontSize = 18;
    public Vector2 payRentButtonPos = new Vector2(-90, 20);
    public Vector2 payRentButtonSize = new Vector2(160, 40);
    public Vector2 buyFromOwnerButtonPos = new Vector2(90, 20);
    public Vector2 buyFromOwnerButtonSize = new Vector2(160, 40);

    [Header("Bildirim Toast")]
    public Vector2 notificationAnchorMin = new Vector2(0.5f, 0.85f);
    public Vector2 notificationAnchorMax = new Vector2(0.5f, 0.85f);
    public Vector2 notificationSize = new Vector2(500, 50);
    public int notificationPaddingLeft = 10;
    public int notificationPaddingRight = 10;
    public int notificationPaddingTop = 5;
    public int notificationPaddingBottom = 5;
    public int notificationFontSize = 20;

    [Header("Oyun Bitti Paneli (anchor Y: 0=alt, 1=ust)")]
    public float gameOverTitleAnchorY = 0.6f;
    public Vector2 gameOverTitleSize = new Vector2(600, 50);
    public int gameOverTitleFontSize = 28;
    public float gameOverWinnerAnchorY = 0.48f;
    public Vector2 gameOverWinnerSize = new Vector2(600, 80);
    public int gameOverWinnerFontSize = 42;
    public float gameOverMenuButtonAnchorY = 0.25f;
    public Vector2 gameOverMenuButtonSize = new Vector2(200, 48);
    public int gameOverMenuButtonFontSize = 22;

    [Header("Canvas Scaler (referans cozunurluk)")]
    public Vector2 referenceResolution = new Vector2(1920, 1080);

    /// <summary>globalFontScale uygulanmis font boyutu.</summary>
    public int ScaledFontSize(int baseSize) => Mathf.Max(8, Mathf.RoundToInt(baseSize * globalFontScale));
    /// <summary>globalScale uygulanmis boyut.</summary>
    public float ScaledSize(float baseSize) => baseSize * globalScale;
    public Vector2 ScaledSize(Vector2 baseSize) => baseSize * globalScale;
}
