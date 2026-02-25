using System.Collections;
using System.Collections.Generic;
using Mirror;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ana lobi ekranù controller'ù.
/// Referans tasarùmla birebir eùleùecek ùekilde yapùlandùrùldù.
/// </summary>
public class LobbyUINew : MonoBehaviour
{
    public static LobbyUINew Instance { get; private set; }

    // -------------------------------------------------------
    // Inspector Referanslarù
    // -------------------------------------------------------

    [Header("Paneller")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject lobbyPanel;

    [Header("Ana Menù")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private RawImage menuAvatarImage;
    [SerializeField] private TextMeshProUGUI menuNameText;

    [Header("Lobi - ùst Bilgi")]
    [SerializeField] private TextMeshProUGUI playerCountText;  // "2 / 4 OYUNCU"

    [Header("Lobi - Oyuncu Kartlarù")]
    [SerializeField] private PlayerCardUI[] playerCards;       // 4 adet, Inspector'dan ata

    [Header("Lobi - Alt Bar")]
    [SerializeField] private Button dicePickButton;            // "ZAR SEù" butonu
    [SerializeField] private Image dicePickButtonIcon;         // Butonun iùindeki zar gùrseli
    [SerializeField] private Image dicePickButtonIconDot1;
    [SerializeField] private Image dicePickButtonIconDot2;
    [SerializeField] private Image dicePickButtonIconDot3;
    [SerializeField] private TextMeshProUGUI lobbyIdText;      // "LOBù #10977524"
    [SerializeField] private Button copyIdButton;
    [SerializeField] private TextMeshProUGUI copyButtonText;
    [SerializeField] private Button startGameButton;           // Sadece host gùrùr
    [SerializeField] private Button leaveButton;

    [Header("Zar Picker Modal")]
    [SerializeField] private DicePickerUI dicePicker;

    [Header("Karakter Kameralarù (3D iùin)")]
    [SerializeField] private Camera[] characterCameras;        // Her kart iùin ayrù camera
    [SerializeField] private RenderTexture[] characterRTs;     // Her kamera iùin RenderTexture

    [Header("Ayarlar")]
    [SerializeField] private int maxPlayers = 4;
    [SerializeField] private float refreshRate = 0.5f;

    // -------------------------------------------------------
    // Private State
    // -------------------------------------------------------

    private int _selectedCharIndex = 0;
    private int _selectedDiceIndex = 0;

    // Karakter arka plan renkleri (kartlarla eùleùmeli)
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

    private void OnEnable()
    {
        // Hierarchy'den yeniden aktif edildi?inde duruma gùre do?ru paneli gùster.
        if (NetworkServer.active || NetworkClient.active)
            ShowLobby();
        else
            ShowMainMenu();
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

        // Zar seùim callback
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
    // Steam Profil Yùkleme
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
    // Panel Geùiùleri
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

        // Host kontrolù: start butonu sadece host'ta gùrùnùr
        bool isHost = NetworkServer.active;
        if (startGameButton) startGameButton.gameObject.SetActive(isHost);

        // Karakter ok butonlarù zaten PlayerCardUI iùinde yùnetiliyor

        RefreshLobby();
        StartRefresh();
        StartCoroutine(FitLobbyToScreenNextFrame());
    }

    /// <summary>
    /// Bir frame sonra lobi icerigini ekrana sigdirir (tasmayi onler).
    /// </summary>
    private IEnumerator FitLobbyToScreenNextFrame()
    {
        yield return null;
        if (lobbyPanel == null) yield break;
        Canvas canvas = lobbyPanel.GetComponentInParent<Canvas>();
        if (canvas == null) yield break;
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        if (canvasRect == null) yield break;
        RectTransform content = lobbyPanel.transform as RectTransform;
        if (content == null) yield break;
        // Lobi icerik genisligini kart container uzerinden al (4 kart + spacing)
        RectTransform cardsContainer = (playerCards != null && playerCards.Length > 0 && playerCards[0] != null)
            ? playerCards[0].transform.parent as RectTransform
            : null;
        float contentW = cardsContainer != null ? cardsContainer.rect.width : content.rect.width;
        float contentH = content.rect.height;
        float canvasW = canvasRect.rect.width;
        float canvasH = canvasRect.rect.height;
        if (contentW <= 0 || contentH <= 0) yield break;
        float scale = Mathf.Min(1f, canvasW / contentW, canvasH / contentH);
        lobbyPanel.transform.localScale = new Vector3(scale, scale, 1f);
    }

    public static void NotifyLobbyJoined() => Instance?.ShowLobby();

    // -------------------------------------------------------
    // Lobi Gùncelleme
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
        // Oyuncu sayùsù
        var players = GetCurrentPlayers();

        if (playerCountText)
            playerCountText.text = $"{players.Count} / {maxPlayers} OYUNCU";

        // Lobi ID
        var lobby = SteamLobbyManager.Instance?.CurrentLobby;
        if (lobbyIdText && lobby.HasValue)
        {
            string idStr = lobby.Value.Id.ToString();
            string shortId = idStr.Length > 8 ? idStr.Substring(0, 8) : idStr;
            lobbyIdText.text = $"LOBù #{shortId}";
        }

        // Kartlarù gùncelle
        for (int i = 0; i < playerCards.Length; i++)
        {
            if (playerCards[i] == null) continue;

            if (i < players.Count)
            {
                playerCards[i].SetupWithPlayer(players[i]);

                // Local oyuncunun kartùna zar rengini uygula
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
    // Karakter Seùimi
    // -------------------------------------------------------

    /// <summary>
    /// PlayerCardUI'daki ok butonlarùna bu metodlarù baùla.
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
        // Local oyuncunun kartùnù bul ve gùncelle
        foreach (var card in playerCards)
        {
            if (card != null && card.IsLocalPlayer)
            {
                card.RefreshCharacterColor(_selectedCharIndex);
                // ùleride: 3D modeli de deùiùtir
            }
        }

        // TODO: Server'a seùim gùnder (Command ile) - ileride eklenecek
    }

    // -------------------------------------------------------
    // Zar Seùimi
    // -------------------------------------------------------

    private void OnDicePickClicked()
    {
        dicePicker?.Show(_selectedDiceIndex);
    }

    private void OnDiceSelected(int index)
    {
        _selectedDiceIndex = index;

        // "ZAR SEù" butonundaki mini zarù gùncelle
        var skin = dicePicker?.GetSkin(index);
        if (skin != null)
        {
            if (dicePickButtonIcon) dicePickButtonIcon.color = skin.diceColor;
            if (dicePickButtonIconDot1) dicePickButtonIconDot1.color = skin.dotColor;
            if (dicePickButtonIconDot2) dicePickButtonIconDot2.color = skin.dotColor;
            if (dicePickButtonIconDot3) dicePickButtonIconDot3.color = skin.dotColor;
        }

        // Local oyuncunun kartùndaki zarù gùncelle
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
    // Buton Handler'larù
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
        Debug.Log("[LobbyUI] Oyun baùlatùlùyor...");
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
            copyButtonText.text = "KOPYALANDù!";
            yield return new WaitForSeconds(1.5f);
            copyButtonText.text = orig;
        }
    }

    // -------------------------------------------------------
    // Texture Dùnùùùmù
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