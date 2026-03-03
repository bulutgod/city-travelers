using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lobi ekran?ndaki her oyuncu kart?n? y�netir.
/// `LobbyUINew` taraf?ndan kullan?l?r.
/// </summary>
public class PlayerCardUI : MonoBehaviour
{
    [Header("Kart Yap?s?")]
    [SerializeField] private Image cardBackground;           // Kart zemini
    [SerializeField] private Image cardOutline;              // Local oyuncu i�in pembe outline

    [Header("Üst Bar (Avatar + İsim)")]
    [SerializeField] private RawImage steamAvatarImage;      // Steam profil fotoğrafı
    [SerializeField] private TextMeshProUGUI steamNameText;  // Steam kullanıcı adı
    [Tooltip("Avatar daire alanının çapı (px). Tasarımdaki rounded square içindeki daireye uyum için.")]
    [SerializeField] private float avatarSize = 38f;

    [Header("Karakter Alan?")]
    [SerializeField] private Image characterBackground;      // Renkli arka plan
    [SerializeField] private RawImage characterModelImage;   // 3D model render texture
    [SerializeField] private Button prevCharButton;          // Sol ok
    [SerializeField] private Button nextCharButton;          // Sa? ok

    [Header("Zar (Sa? Alt)")]
    [SerializeField] private Image diceImage;                // Zar sprite görseli

    [Header("Durum Katmanları")]
    [SerializeField] private GameObject waitingOverlay;      // "Bekleniyor" paneli
    [SerializeField] private GameObject activeContent;       // Normal kart içeriği
    [SerializeField] private TextMeshProUGUI readyLabel;    // "Hazır" yazısı (opsiyonel)

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
    private TextMeshProUGUI _runtimeReadyLabel;

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

        // Avatar: host icin gecikme olabiliyor, local player icin Steam'den yukle
        if (steamAvatarImage)
        {
            AvatarCircleMask.ApplyTo(steamAvatarImage);
            // Tasarımdaki daire alana tam oturması için boyut ayarla
            var rt = steamAvatarImage.GetComponent<RectTransform>();
            if (rt != null && avatarSize > 0)
                rt.sizeDelta = new Vector2(avatarSize, avatarSize);
            if (player.avatarTexture != null)
            {
                steamAvatarImage.texture = player.avatarTexture;
                steamAvatarImage.color = Color.white;
            }
            else if (player.isLocalPlayer && SteamClient.IsValid)
            {
                steamAvatarImage.texture = null;
                steamAvatarImage.color = new Color(1f, 1f, 1f, 0.1f);
                StartCoroutine(LoadLocalPlayerAvatar());
            }
            else
            {
                steamAvatarImage.texture = null;
                steamAvatarImage.color = new Color(1f, 1f, 1f, 0.1f);
            }
        }

        // Isim: host icin SyncVar gecikmesi olabiliyor, SteamClient'dan fallback
        if (steamNameText)
        {
            var name = player.steamName;
            if (string.IsNullOrWhiteSpace(name) && player.isLocalPlayer && SteamClient.IsValid)
                name = SteamClient.Name ?? "Host";
            if (string.IsNullOrWhiteSpace(name))
                name = "OYUNCU";
            steamNameText.text = name.ToUpperInvariant();
        }

        // Karakter rengi
        RefreshCharacterColor(player.selectedCharacterIndex);

        // Karakter ok butonları: sadece local oyuncu kullanabilir
        bool showCharButtons = IsLocalPlayer;
        if (prevCharButton) prevCharButton.gameObject.SetActive(showCharButtons);
        if (nextCharButton) nextCharButton.gameObject.SetActive(showCharButtons);

        var rl = readyLabel ?? _runtimeReadyLabel;
        if (rl == null)
        {
            var go = new GameObject("ReadyLabel");
            go.transform.SetParent(transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(1, 0);
            rt.anchoredPosition = new Vector2(-8, 8);
            rt.sizeDelta = new Vector2(60, 24);
            _runtimeReadyLabel = go.AddComponent<TextMeshProUGUI>();
            _runtimeReadyLabel.fontSize = 14;
            rl = _runtimeReadyLabel;
        }
        rl.gameObject.SetActive(true);
        rl.text = player.isReady ? "HAZIR" : "";
        rl.color = player.isReady ? new Color(0.2f, 0.9f, 0.3f, 1f) : new Color(0.5f, 0.5f, 0.5f, 0.8f);
    }

    /// <summary>
    /// Kart? bo? slot olarak g�ster.
    /// </summary>
    public void SetEmpty()
    {
        _player = null;

        if (waitingOverlay) waitingOverlay.SetActive(true);
        if (activeContent) activeContent.SetActive(false);

        if (cardOutline) cardOutline.color = _normalOutlineColor;

        if (steamNameText) steamNameText.text = "BOŞ";
        if (readyLabel) readyLabel.gameObject.SetActive(false);
        if (_runtimeReadyLabel) _runtimeReadyLabel.gameObject.SetActive(false);
        if (steamAvatarImage)
        {
            AvatarCircleMask.ApplyTo(steamAvatarImage);
            var rt = steamAvatarImage.GetComponent<RectTransform>();
            if (rt != null && avatarSize > 0)
                rt.sizeDelta = new Vector2(avatarSize, avatarSize);
            steamAvatarImage.texture = null;
            steamAvatarImage.color = new Color(1f, 1f, 1f, 0.05f);
        }

        if (characterBackground && characterBgColors != null && characterBgColors.Length > 0)
            characterBackground.color = characterBgColors[0];
    }

    /// <summary>
    /// Karakter rengine g�re arka plan? g�ncelle.
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

    public void SetDiceSprite(Sprite sprite)
    {
        if (!diceImage) return;
        diceImage.sprite = sprite;
        diceImage.color = Color.white;
        diceImage.preserveAspect = true;
    }

    private System.Collections.IEnumerator LoadLocalPlayerAvatar()
    {
        if (!SteamClient.IsValid || steamAvatarImage == null) yield break;
        var task = SteamFriends.GetLargeAvatarAsync(SteamClient.SteamId);
        yield return new UnityEngine.WaitUntil(() => task.IsCompleted);
        if (task.Result.HasValue && steamAvatarImage != null)
        {
            var img = task.Result.Value;
            var tex = new Texture2D((int)img.Width, (int)img.Height, TextureFormat.RGBA32, false);
            var flipped = new byte[img.Data.Length];
            int rowSize = (int)img.Width * 4;
            for (int y = 0; y < img.Height; y++)
            {
                int src = (int)(img.Height - 1 - y);
                System.Array.Copy(img.Data, src * rowSize, flipped, y * rowSize, rowSize);
            }
            tex.LoadRawTextureData(flipped);
            tex.Apply();
            steamAvatarImage.texture = tex;
            steamAvatarImage.color = Color.white;
        }
    }

    /// <summary>
    /// 3D karakter render texture'?n? ata (iste?e ba?l?).
    /// </summary>
    public void SetCharacterRenderTexture(RenderTexture rt)
    {
        if (characterModelImage) characterModelImage.texture = rt;
    }
}

