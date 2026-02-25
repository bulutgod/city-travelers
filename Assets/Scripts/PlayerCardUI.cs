using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lobi ekran?ndaki her oyuncu kart?n? yönetir.
/// `LobbyUINew` taraf?ndan kullan?l?r.
/// </summary>
public class PlayerCardUI : MonoBehaviour
{
    [Header("Kart Yap?s?")]
    [SerializeField] private Image cardBackground;           // Kart zemini
    [SerializeField] private Image cardOutline;              // Local oyuncu için pembe outline

    [Header("Üst Bar (Avatar + ?sim)")]
    [SerializeField] private RawImage steamAvatarImage;      // Steam profil foto?raf?
    [SerializeField] private TextMeshProUGUI steamNameText;  // Steam kullan?c? ad?

    [Header("Karakter Alan?")]
    [SerializeField] private Image characterBackground;      // Renkli arka plan
    [SerializeField] private RawImage characterModelImage;   // 3D model render texture
    [SerializeField] private Button prevCharButton;          // Sol ok
    [SerializeField] private Button nextCharButton;          // Sa? ok

    [Header("Zar (Sa? Alt)")]
    [SerializeField] private Image diceImage;                // Zar rengi
    [SerializeField] private Image diceDot1;                 // Zar noktalar?
    [SerializeField] private Image diceDot2;
    [SerializeField] private Image diceDot3;

    [Header("Durum Katmanlar?")]
    [SerializeField] private GameObject waitingOverlay;      // "Bekleniyor" paneli
    [SerializeField] private GameObject activeContent;       // Normal kart içeri?i

    [Header("Karakter Arka Plan Renkleri")]
    [SerializeField]
    private Color[] characterBgColors =
    {
        new Color(0.96f, 0.90f, 0.82f, 1f),
        new Color(0.82f, 0.90f, 0.96f, 1f),
        new Color(0.90f, 0.82f, 0.96f, 1f),
        new Color(0.82f, 0.96f, 0.90f, 1f),
        new Color(0.96f, 0.82f, 0.82f, 1f),
    };

    private readonly Color _localOutlineColor = new Color(1f, 0.24f, 0.67f, 1f);
    private readonly Color _normalOutlineColor = new Color(0f, 0f, 0f, 0f);

    private PlayerObject _player;

    /// <summary>Bu kart yerel oyuncuya m? ait?</summary>
    public bool IsLocalPlayer => _player != null && _player.isLocalPlayer;

    /// <summary>
    /// Kart? bir PlayerObject ile doldur.
    /// </summary>
    public void SetupWithPlayer(PlayerObject player)
    {
        _player = player;

        if (waitingOverlay) waitingOverlay.SetActive(false);
        if (activeContent) activeContent.SetActive(true);

        // Outline: sadece local oyuncuda pembe
        if (cardOutline)
            cardOutline.color = IsLocalPlayer ? _localOutlineColor : _normalOutlineColor;

        // Avatar
        if (steamAvatarImage)
        {
            if (player.avatarTexture != null)
            {
                steamAvatarImage.texture = player.avatarTexture;
                steamAvatarImage.color = Color.white;
            }
            else
            {
                steamAvatarImage.texture = null;
                steamAvatarImage.color = new Color(1f, 1f, 1f, 0.1f);
            }
        }

        // ?sim
        if (steamNameText)
        {
            var name = string.IsNullOrWhiteSpace(player.steamName)
                ? "OYUNCU"
                : player.steamName;
            steamNameText.text = name.ToUpperInvariant();
        }

        // Karakter rengi
        RefreshCharacterColor(player.selectedCharacterIndex);

        // Karakter ok butonlar?: sadece local oyuncu kullanabilir
        bool showCharButtons = IsLocalPlayer;
        if (prevCharButton) prevCharButton.gameObject.SetActive(showCharButtons);
        if (nextCharButton) nextCharButton.gameObject.SetActive(showCharButtons);
    }

    /// <summary>
    /// Kart? bo? slot olarak göster.
    /// </summary>
    public void SetEmpty()
    {
        _player = null;

        if (waitingOverlay) waitingOverlay.SetActive(true);
        if (activeContent) activeContent.SetActive(false);

        if (cardOutline) cardOutline.color = _normalOutlineColor;

        if (steamNameText) steamNameText.text = "BO?";
        if (steamAvatarImage)
        {
            steamAvatarImage.texture = null;
            steamAvatarImage.color = new Color(1f, 1f, 1f, 0.05f);
        }

        if (characterBackground && characterBgColors != null && characterBgColors.Length > 0)
            characterBackground.color = characterBgColors[0];
    }

    /// <summary>
    /// Karakter rengine göre arka plan? güncelle.
    /// </summary>
    public void RefreshCharacterColor(int index)
    {
        if (!characterBackground || characterBgColors == null || characterBgColors.Length == 0)
            return;

        int count = characterBgColors.Length;
        index %= count;
        if (index < 0) index += count;

        characterBackground.color = characterBgColors[index];
    }

    /// <summary>
    /// Kart üzerindeki zar görselinin rengini günceller.
    /// </summary>
    public void SetDiceColor(Color diceColor, Color dotColor)
    {
        if (diceImage) diceImage.color = diceColor;
        if (diceDot1) diceDot1.color = dotColor;
        if (diceDot2) diceDot2.color = dotColor;
        if (diceDot3) diceDot3.color = dotColor;
    }

    /// <summary>
    /// 3D karakter render texture'?n? ata (iste?e ba?l?).
    /// </summary>
    public void SetCharacterRenderTexture(RenderTexture rt)
    {
        if (characterModelImage) characterModelImage.texture = rt;
    }
}

