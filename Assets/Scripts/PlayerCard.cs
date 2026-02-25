using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Eski lobi arayüzü için oyuncu kart? bile?eni.
/// `LobbyUI` taraf?ndan kullan?l?r.
/// </summary>
public class PlayerCard : MonoBehaviour
{
    [Header("UI Referanslar?")]
    [SerializeField] private RawImage avatarImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Image statusDot;
    [SerializeField] private Image cardBackground;
    [SerializeField] private Image borderGlow;
    [SerializeField] private GameObject hostCrownIcon;
    [SerializeField] private GameObject localPlayerIndicator;

    [Header("Renkler")]
    [SerializeField] private Color hostBorderColor = new Color(1f, 0.84f, 0f, 1f);
    [SerializeField] private Color clientBorderColor = new Color(0.4f, 0.4f, 0.5f, 1f);
    [SerializeField] private Color readyColor = new Color(0.2f, 0.9f, 0.4f, 1f);
    [SerializeField] private Color waitingColor = new Color(0.9f, 0.6f, 0.2f, 1f);
    [SerializeField] private Color emptyCardColor = new Color(0.08f, 0.08f, 0.12f, 0.6f);

    private PlayerObject _player;

    /// <summary>
    /// Kart? dolu bir oyuncu ile yap?land?r?r.
    /// </summary>
    public void Setup(PlayerObject player)
    {
        _player = player;

        if (nameText != null)
        {
            var name = string.IsNullOrWhiteSpace(player.steamName)
                ? "OYUNCU"
                : player.steamName;
            nameText.text = name.ToUpperInvariant();
        }

        if (avatarImage != null)
        {
            if (player.avatarTexture != null)
            {
                avatarImage.texture = player.avatarTexture;
                avatarImage.color = Color.white;
            }
            else
            {
                avatarImage.texture = null;
                avatarImage.color = new Color(1f, 1f, 1f, 0.1f);
            }
        }

        bool isHost = player.isServer;

        if (borderGlow != null)
            borderGlow.color = isHost ? hostBorderColor : clientBorderColor;

        if (hostCrownIcon != null)
            hostCrownIcon.SetActive(isHost);

        if (localPlayerIndicator != null)
            localPlayerIndicator.SetActive(player.isLocalPlayer);

        if (cardBackground != null)
            cardBackground.color = Color.white;

        if (statusText != null)
            statusText.text = "HAZIR";

        if (statusDot != null)
            statusDot.color = readyColor;
    }

    /// <summary>
    /// Kart? bo? slot görünümüne getirir.
    /// </summary>
    public void SetEmpty(int index)
    {
        _player = null;

        if (nameText != null)
            nameText.text = $"BO? SLOT {index + 1}";

        if (statusText != null)
            statusText.text = "BEKL?YOR";

        if (statusDot != null)
            statusDot.color = waitingColor;

        if (cardBackground != null)
            cardBackground.color = emptyCardColor;

        if (hostCrownIcon != null)
            hostCrownIcon.SetActive(false);

        if (localPlayerIndicator != null)
            localPlayerIndicator.SetActive(false);

        if (avatarImage != null)
        {
            avatarImage.texture = null;
            avatarImage.color = new Color(1f, 1f, 1f, 0.15f);
        }
    }
}

