using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerCard : MonoBehaviour
{
    [Header("UI Referanslarý")]
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

    private PlayerObject _playerObject;

    public void Setup(PlayerObject player)
    {
        _playerObject = player;
        Refresh();
    }

    public void SetEmpty(int slotIndex)
    {
        _playerObject = null;

        if (avatarImage) avatarImage.texture = null;
        if (nameText) nameText.text = "Bekleniyor...";
        if (statusText) statusText.text = "";
        if (statusDot) statusDot.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        if (cardBackground) cardBackground.color = emptyCardColor;
        if (borderGlow) borderGlow.color = new Color(0.2f, 0.2f, 0.25f, 0.3f);
        if (hostCrownIcon) hostCrownIcon.SetActive(false);
        if (localPlayerIndicator) localPlayerIndicator.SetActive(false);
    }

    public void Refresh()
    {
        if (_playerObject == null) return;

        bool isHost = _playerObject.playerIndex == 0;
        bool isLocal = _playerObject.isLocalPlayer;


        if (nameText)
        {
            nameText.text = string.IsNullOrEmpty(_playerObject.steamName)
                ? "Yükleniyor..."
                : _playerObject.steamName;
        }

        if (avatarImage && _playerObject.avatarTexture != null)
            avatarImage.texture = _playerObject.avatarTexture;


        if (hostCrownIcon) hostCrownIcon.SetActive(isHost);

        if (localPlayerIndicator) localPlayerIndicator.SetActive(isLocal);

        if (borderGlow)
            borderGlow.color = isHost ? hostBorderColor : clientBorderColor;

        if (statusDot) statusDot.color = readyColor;
        if (statusText) statusText.text = isHost ? "HOST" : "Hazýr";
    }
}