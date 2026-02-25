using System.Collections;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lobi ekranýndaki her oyuncu kartýný yönetir.
/// Referans tasarýmla birebir eþleþecek þekilde yapýlandýrýldý.
/// </summary>
public class PlayerCardUI : MonoBehaviour
{
    [Header("Kart Yapýsý")]
    [SerializeField] private Image cardBackground;           // Beyaz kart zemini
    [SerializeField] private Image cardOutline;              // Local oyuncu için pembe outline

    [Header("Üst Bar (Avatar + Ýsim)")]
    [SerializeField] private RawImage steamAvatarImage;      // Steam profil fotoðrafý
    [SerializeField] private TextMeshProUGUI steamNameText;  // Steam kullanýcý adý

    [Header("Karakter Alaný")]
    [SerializeField] private Image characterBackground;      // Renkli arka plan
    [SerializeField] private RawImage characterModelImage;   // 3D model render texture buraya
    [SerializeField] private Button prevCharButton;          // Sol ok
    [SerializeField] private Button nextCharButton;          // Sað ok

    [Header("Zar (Sað Alt)")]
    [SerializeField] private Image diceImage;                // Zar rengi
    [SerializeField] private Image diceDot1;                 // Zar noktalarý
    [SerializeField] private Image diceDot2;
    [SerializeField] private Image diceDot3;

    [Header("Bekleniyor Durumu")]
    [SerializeField] private GameObject waitingOverlay;      // "BEKLENÝYOR" paneli
    [SerializeField] private GameObject activeContent;       // Normal kart içeriði

    [Header("Renkler - Karakter Arka Planlarý")]
    [SerializeField]
    private Color[] characterBgColors = new Color[]
    {
        new Color(0.96f, 0.90f, 0.82f, 1f),  // Þeftali
        new Color(0.82f, 0.90f, 0.96f, 1f),  // Mavi
        new Color(0.90f, 0.82f, 0.96f, 1f),  // Mor
        new Color(0.82f, 0.96f, 0.90f, 1f),  // Yeþil
        new Color(0.96f, 0.82f, 0.82f, 1f),  // Kýrmýzý
    };

    // Pembe outline rengi (local oyuncu)
    private readonly Color _localOutlineColor = new Color(1f, 0.24f, 0.67f, 1f);
    private readonly Color _normalOutlineColor = new Color(0f, 0f, 0f, 0f);

    private PlayerObject _playerData;
    private bool _isLocalPlayer;

    // -------------------------------------------------------
    // Public API
    // -------------------------------------------------------

    /// <summary>
    /// Kartý bir PlayerObject ile doldur.
    /// </summary>
    public void SetupWithPlayer(PlayerObject player)
    {
        _playerData = player;
        _isLocalPlayer = player.isLocalPlayer;

        if (waitingOverlay) waitingOverlay.SetActive(false);
        if (activeContent) activeContent.SetActive(true);

        // Outline: sadece local oyuncuda pembe
        if (cardOutline)
            cardOutline.color = _isLocalPlayer ? _localOutlineColor : _normalOutlineColor;

        // Avatar
        if (steamAvatarImage && player.avatarTexture != null)
            steamAvatarImage.texture = player.avatarTexture;

        // Ýsim
        if (steamNameText)
            steamNameText.text = string.IsNullOrEmpty(player.steamName)
                ? "..." : player.steamName.ToUpper();

        // Karakter rengi
        RefreshCharacterColor(player.selectedCharacterIndex);

        // Ok butonlarý: sadece local oyuncu kullanabilir
        if (prevCharButton) prevCharButton.gameObject.SetActive(_isLocalPlayer);
        if (nextCharButton) nextCharButton.gameObject.SetActive(_isLocalPlayer);
    }

    /// <summary>
    /// Kartý boþ slot olarak göster.
    /// </summary>
    public void SetEmpty()
    {
        _playerData = null;
        if (waitingOverlay) waitingOverlay.SetActive(true);
        if (activeContent) activeContent.SetActive(false);
        if (cardOutline) cardOutline.color = _normalOutlineColor;
    }

    /// <summary>
    /// Seçili zarýn rengini güncelle.
    /// </summary>
    public void SetDiceColor(Color diceColor, Color dotColor)
    {
        if (diceImage) diceImage.color = diceColor;
        if (diceDot1) diceDot1.color = dotColor;
        if (diceDot2) diceDot2.color = dotColor;
        if (diceDot3) diceDot3.color = dotColor;
    }

    /// <summary>
    /// Karakter deðiþince rengi güncelle.
    /// </summary>
    public void RefreshCharacterColor(int charIndex)
    {
        if (characterBackground && charIndex >= 0 && charIndex < characterBgColors.Length)
            characterBackground.color = characterBgColors[charIndex];
    }

    /// <summary>
    /// 3D karakter render texture'ýný ata.
    /// </summary>
    public void SetCharacterRenderTexture(RenderTexture rt)
    {
        if (characterModelImage) characterModelImage.texture = rt;
    }

    public bool IsLocalPlayer => _isLocalPlayer;
    public PlayerObject PlayerData => _playerData;
}