using System.Collections;
using System.Collections.Generic;
using Mirror;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ana lobi ekraný controller'ý.
/// Referans tasarýmla birebir eþleþecek þekilde yapýlandýrýldý.
/// </summary>
public class LobbyUINew : MonoBehaviour
{
    public static LobbyUINew Instance { get; private set; }

    // -------------------------------------------------------
    // Inspector Referanslarý
    // -------------------------------------------------------

    [Header("Paneller")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject lobbyPanel;

    [Header("Ana Menü")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private RawImage menuAvatarImage;
    [SerializeField] private TextMeshProUGUI menuNameText;

    [Header("Lobi - Üst Bilgi")]
    [SerializeField] private TextMeshProUGUI playerCountText;  // "2 / 4 OYUNCU"

    [Header("Lobi - Oyuncu Kartlarý")]
    [SerializeField] private PlayerCardUI[] playerCards;       // 4 adet, Inspector'dan ata

    [Header("Lobi - Alt Bar")]
    [SerializeField] private Button dicePickButton;            // "ZAR SEÇ" butonu
    [SerializeField] private Image dicePickButtonIcon;         // Butonun içindeki zar görseli
    [SerializeField] private Image dicePickButtonIconDot1;
    [SerializeField] private Image dicePickButtonIconDot2;
    [SerializeField] private Image dicePickButtonIconDot3;
    [SerializeField] private TextMeshProUGUI lobbyIdText;      // "LOBÝ #10977524"
    [SerializeField] private Button copyIdButton;
    [SerializeField] private TextMeshProUGUI copyButtonText;
    [SerializeField] private Button startGameButton;           // Sadece host görür
    [SerializeField] private Button leaveButton;

    [Header("Zar Picker Modal")]
    [SerializeField] private DicePickerUI dicePicker;

    [Header("Karakter Kameralarý (3D için)")]
    [SerializeField] private Camera[] characterCameras;        // Her kart için ayrý camera
    [SerializeField] private RenderTexture[] characterRTs;     // Her kamera için RenderTexture

    [Header("Ayarlar")]
    [SerializeField] private int maxPlayers = 4;
    [SerializeField] private float refreshRate = 0.5f;

    // -------------------------------------------------------
    // Private State
    // -------------------------------------------------------

    private int _selectedCharIndex = 0;
    private int _selectedDiceIndex = 0;

    // Karakter arka plan renkleri (kartlarla eþleþmeli)
    private readonly Color[] _charColors = new Color[]
    {
        new Color(0.96f, 0.90f, 0.82f),
        new Color(0.82f, 0.90f, 0.96f),
        new Color(0.90f, 0.82f, 0.96f),
        new Color(0.82f, 0.96f, 0.90f),
        new Color(0.96f, 0.82f, 0.82f),
    };

    private Coroutine _refreshCoroutine;

    // -------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // Buton listeners
        hostButton?.onClick.AddListener(OnHostClicked);
        leaveButton?.onClick.AddListener(OnLeaveClicked);
        startGameButton?.onClick.AddListener(OnStartGameClicked);
        copyIdButton?.onClick.AddListener(OnCopyIdClicked);
        quitButton?.onClick.AddListener(() => Application.Quit());
        dicePickButton?.onClick.AddListener(OnDicePickClicked);

        // Zar seçim callback
        if (dicePicker != null)
            dicePicker.OnDiceSelected += OnDiceSelected;

        ShowMainMenu();
        LoadSteamProfile();
    }

    private void OnDestroy()
    {
        if (dicePicker != null)
            dicePicker.OnDiceSelected -= OnDiceSelected;
    }

    // -------------------------------------------------------
    // Steam Profil Yükleme
    // -------------------------------------------------------

    private async void LoadSteamProfile()
    {
        if (!SteamClient.IsValid) return;

        if (menuNameText) menuNameText.text = SteamClient.Name?.ToUpper() ?? "";

        var img = await SteamFriends.GetLargeAvatarAsync(SteamClient.SteamId);
        if (img.HasValue && menuAvatarImage)
            menuAvatarImage.texture = ConvertToTexture2D(img.Value);
    }

    // -------------------------------------------------------
    // Panel Geçiþleri
    // -------------------------------------------------------

    public void ShowMainMenu()
    {
        if (mainMenuPanel) mainMenuPanel.SetActive(true);
        if (lobbyPanel) lobbyPanel.SetActive(false);
        StopRefresh();
    }

    public void ShowLobby()
    {
        if (mainMenuPanel) mainMenuPanel.SetActive(false);
        if (lobbyPanel) lobbyPanel.SetActive(true);

        // Host kontrolü: start butonu sadece host'ta görünür
        bool isHost = NetworkServer.active;
        if (startGameButton) startGameButton.gameObject.SetActive(isHost);

        // Karakter ok butonlarý zaten PlayerCardUI içinde yönetiliyor

        RefreshLobby();
        StartRefresh();
    }

    public static void NotifyLobbyJoined() => Instance?.ShowLobby();

    // -------------------------------------------------------
    // Lobi Güncelleme
    // -------------------------------------------------------

    private void StartRefresh()
    {
        StopRefresh();
        _refreshCoroutine = StartCoroutine(RefreshLoop());
    }

    private void StopRefresh()
    {
        if (_refreshCoroutine != null)
        {
            StopCoroutine(_refreshCoroutine);
            _refreshCoroutine = null;
        }
    }

    private IEnumerator RefreshLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(refreshRate);
            RefreshLobby();
        }
    }

    public void RefreshLobby()
    {
        // Oyuncu sayýsý
        var players = GetCurrentPlayers();

        if (playerCountText)
            playerCountText.text = $"{players.Count} / {maxPlayers} OYUNCU";

        // Lobi ID
        var lobby = SteamLobbyManager.Instance?.CurrentLobby;
        if (lobbyIdText && lobby.HasValue)
        {
            string idStr = lobby.Value.Id.ToString();
            string shortId = idStr.Length > 8 ? idStr.Substring(0, 8) : idStr;
            lobbyIdText.text = $"LOBÝ #{shortId}";
        }

        // Kartlarý güncelle
        for (int i = 0; i < playerCards.Length; i++)
        {
            if (playerCards[i] == null) continue;

            if (i < players.Count)
            {
                playerCards[i].SetupWithPlayer(players[i]);

                // Local oyuncunun kartýna zar rengini uygula
                if (players[i].isLocalPlayer)
                {
                    ApplyDiceToCard(playerCards[i]);
                }
            }
            else
            {
                playerCards[i].SetEmpty();
            }
        }
    }

    private List<PlayerObject> GetCurrentPlayers()
    {
        var list = new List<PlayerObject>();

        foreach (var spawned in NetworkClient.spawned.Values)
        {
            var p = spawned.GetComponent<PlayerObject>();
            if (p != null) list.Add(p);
        }

        if (list.Count == 0)
        {
            var found = FindObjectsByType<PlayerObject>(FindObjectsSortMode.None);
            list.AddRange(found);
        }

        list.Sort((a, b) => a.playerIndex.CompareTo(b.playerIndex));
        return list;
    }

    // -------------------------------------------------------
    // Karakter Seçimi
    // -------------------------------------------------------

    /// <summary>
    /// PlayerCardUI'daki ok butonlarýna bu metodlarý baðla.
    /// </summary>
    public void OnPrevCharacter()
    {
        _selectedCharIndex = (_selectedCharIndex - 1 + _charColors.Length) % _charColors.Length;
        UpdateLocalCharacter();
    }

    public void OnNextCharacter()
    {
        _selectedCharIndex = (_selectedCharIndex + 1) % _charColors.Length;
        UpdateLocalCharacter();
    }

    private void UpdateLocalCharacter()
    {
        // Local oyuncunun kartýný bul ve güncelle
        foreach (var card in playerCards)
        {
            if (card != null && card.IsLocalPlayer)
            {
                card.RefreshCharacterColor(_selectedCharIndex);
                // Ýleride: 3D modeli de deðiþtir
            }
        }

        // TODO: Server'a seçim gönder (Command ile) - ileride eklenecek
    }

    // -------------------------------------------------------
    // Zar Seçimi
    // -------------------------------------------------------

    private void OnDicePickClicked()
    {
        dicePicker?.Show(_selectedDiceIndex);
    }

    private void OnDiceSelected(int index)
    {
        _selectedDiceIndex = index;

        // "ZAR SEÇ" butonundaki mini zarý güncelle
        var skin = dicePicker?.GetSkin(index);
        if (skin != null)
        {
            if (dicePickButtonIcon) dicePickButtonIcon.color = skin.diceColor;
            if (dicePickButtonIconDot1) dicePickButtonIconDot1.color = skin.dotColor;
            if (dicePickButtonIconDot2) dicePickButtonIconDot2.color = skin.dotColor;
            if (dicePickButtonIconDot3) dicePickButtonIconDot3.color = skin.dotColor;
        }

        // Local oyuncunun kartýndaki zarý güncelle
        foreach (var card in playerCards)
        {
            if (card != null && card.IsLocalPlayer && skin != null)
                ApplyDiceToCard(card);
        }
    }

    private void ApplyDiceToCard(PlayerCardUI card)
    {
        var skin = dicePicker?.GetSkin(_selectedDiceIndex);
        if (skin != null)
            card.SetDiceColor(skin.diceColor, skin.dotColor);
    }

    // -------------------------------------------------------
    // Buton Handler'larý
    // -------------------------------------------------------

    private void OnHostClicked()
    {
        if (NetworkServer.active || NetworkClient.active) return;
        SteamLobbyManager.Instance?.CreateLobby();
    }

    private void OnLeaveClicked()
    {
        SteamLobbyManager.Instance?.LeaveLobby();
        if (NetworkServer.active) GameNetworkManager.Instance?.StopHost();
        else GameNetworkManager.Instance?.StopClient();
        ShowMainMenu();
    }

    private void OnStartGameClicked()
    {
        if (!NetworkServer.active) return;
        Debug.Log("[LobbyUI] Oyun baþlatýlýyor...");
        // NetworkManager.singleton.ServerChangeScene("GameScene");
    }

    private void OnCopyIdClicked()
    {
        var lobby = SteamLobbyManager.Instance?.CurrentLobby;
        if (lobby.HasValue)
        {
            GUIUtility.systemCopyBuffer = lobby.Value.Id.ToString();
            StartCoroutine(CopyFeedback());
        }
    }

    private IEnumerator CopyFeedback()
    {
        if (copyButtonText)
        {
            string orig = copyButtonText.text;
            copyButtonText.text = "KOPYALANDÝ!";
            yield return new WaitForSeconds(1.5f);
            copyButtonText.text = orig;
        }
    }

    // -------------------------------------------------------
    // Texture Dönüþümü
    // -------------------------------------------------------

    private Texture2D ConvertToTexture2D(Steamworks.Data.Image image)
    {
        var tex = new Texture2D((int)image.Width, (int)image.Height, TextureFormat.RGBA32, false);
        var flipped = new byte[image.Data.Length];
        int rowSize = (int)image.Width * 4;
        for (int y = 0; y < image.Height; y++)
        {
            int src = (int)(image.Height - 1 - y);
            System.Array.Copy(image.Data, src * rowSize, flipped, y * rowSize, rowSize);
        }
        tex.LoadRawTextureData(flipped);
        tex.Apply();
        return tex;
    }
}