using System.Collections;
using System.Collections.Generic;
using Mirror;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ana lobi ekran� controller'�.
/// Referans tasar�mla birebir e�le�ecek �ekilde yap�land�r�ld�.
/// </summary>
public class LobbyUINew : MonoBehaviour
{
    public static LobbyUINew Instance { get; private set; }

    // -------------------------------------------------------
    // Inspector Referanslar�
    // -------------------------------------------------------

    [Header("Paneller")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject lobbyPanel;

    [Header("Ana Menü")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button findMatchButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private RawImage menuAvatarImage;
    [SerializeField] private TextMeshProUGUI menuNameText;
    [Tooltip("Ana menü avatar maske: 0.5=daire, küçük değer=açık yeşili doldurur. AvatarMaskConfig varsa o kullanılır.")]
    [Range(0.01f, 0.5f)]
    [SerializeField] private float menuAvatarCornerRadius = 0.15f;

    [Header("Lobi - �st Bilgi")]
    [SerializeField] private TextMeshProUGUI playerCountText;  // "2 / 4 OYUNCU"

    [Header("Lobi - Oyuncu Kartlar�")]
    [SerializeField] private PlayerCardUI[] playerCards;       // 4 adet, Inspector'dan ata

    [Header("Lobi - Alt Bar")]
    [SerializeField] private Button dicePickButton;            // "ZAR SE�" butonu
    [SerializeField] private Image dicePickButtonIcon;         // Butonun i�indeki zar g�rseli
    [SerializeField] private TextMeshProUGUI lobbyIdText;      // "LOB� #10977524"
    [SerializeField] private Button copyIdButton;
    [SerializeField] private TextMeshProUGUI copyButtonText;
    [SerializeField] private Button startGameButton;           // Sadece host görür
    [SerializeField] private Button addBotButton;             // Sadece host görür
    [SerializeField] private Button readyButton;              // "Hazırım" toggle
    [SerializeField] private Button leaveButton;

    [Header("Zar Picker Modal")]
    [SerializeField] private DicePickerUI dicePicker;

    [Header("Karakter Kameralar� (3D i�in)")]
    [SerializeField] private Camera[] characterCameras;        // Her kart i�in ayr� camera
    [SerializeField] private RenderTexture[] characterRTs;     // Her kamera i�in RenderTexture

    [Header("Ayarlar")]
    [SerializeField] private int maxPlayers = 4;
    [SerializeField] private float refreshRate = 0.5f;

    [Header("Opsiyonel UI (yapildikca Inspector'dan ata)")]
    [Tooltip("Lobi oyun suresi satiri (Oyun suresi: + 20 dk / 1 saat / 2 saat). Atanirsa koddan uretilmez; icinde 'DurationLabel' (TMP) ve 'Dur20', 'Dur60', 'Dur120' isimli butonlar olmali.")]
    [SerializeField] private GameObject overrideDurationRow;

    // -------------------------------------------------------
    // Private State
    // -------------------------------------------------------

    private int _selectedCharIndex = 0;
    private int _selectedDiceIndex = 0;

    // Karakter arka plan renkleri (kartlarla e�le�meli)
    private readonly Color[] _charColors = new Color[]
    {
        new Color(0.96f, 0.90f, 0.82f),
        new Color(0.82f, 0.90f, 0.96f),
        new Color(0.90f, 0.82f, 0.96f),
        new Color(0.82f, 0.96f, 0.90f),
        new Color(0.96f, 0.82f, 0.82f),
    };

    private Coroutine _refreshCoroutine;
    private GameObject _durationRow;
    private Button _dur20Btn, _dur60Btn, _dur120Btn;
    private TextMeshProUGUI _durationLabel;

    // -------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------

    private void Awake()
    {
        if (!ReferenceEquals(Instance, null) && !ReferenceEquals(Instance, this) && Instance != null)
        {
            Debug.LogWarning($"[LobbyUI] Eski CANLI LobbyUINew komponenti siliniyor: {Instance.gameObject.name} (scene={Instance.gameObject.scene.name})");
            Destroy(Instance);
        }
        Instance = this;
    }

    private void OnEnable()
    {
        Debug.Log($"[LobbyUI] OnEnable. ServerActive={NetworkServer.active} ClientActive={NetworkClient.active}");
        if (NetworkServer.active || NetworkClient.active)
            ShowLobby();
        else
        {
            ShowMainMenu();
            LoadSteamProfile();
        }
    }

    private void Start()
    {
        // Buton listeners
        if (hostButton == null) Debug.LogWarning("[LobbyUI] hostButton null - Inspector'da atanmamis olabilir!");
        hostButton?.onClick.AddListener(OnHostClicked);
        findMatchButton?.onClick.AddListener(OnFindMatchClicked);
        leaveButton?.onClick.AddListener(OnLeaveClicked);
        startGameButton?.onClick.AddListener(OnStartGameClicked);
        addBotButton?.onClick.AddListener(OnAddBotClicked);
        readyButton?.onClick.AddListener(OnReadyClicked);
        copyIdButton?.onClick.AddListener(OnCopyIdClicked);
        quitButton?.onClick.AddListener(() => Application.Quit());
        dicePickButton?.onClick.AddListener(OnDicePickClicked);
        settingsButton?.onClick.AddListener(OnSettingsClicked);

        // Zar seçim callback
        if (dicePicker != null)
            dicePicker.OnDiceSelected += OnDiceSelected;

        ShowMainMenu();
        LoadSteamProfile();
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayMenuMusic();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (dicePicker != null)
            dicePicker.OnDiceSelected -= OnDiceSelected;
    }

    // -------------------------------------------------------
    // Steam Profil Y�kleme
    // -------------------------------------------------------

    private void LoadSteamProfile()
    {
        Debug.Log($"[LobbyUI] LoadSteamProfile called. SteamValid={SteamClient.IsValid} menuNameText={menuNameText != null} menuAvatarImage={menuAvatarImage != null}");

        if (!SteamClient.IsValid)
        {
            Debug.LogWarning("[LobbyUI] SteamClient not valid, skipping profile load.");
            return;
        }

        string steamName = SteamClient.Name?.ToUpper() ?? "";
        Debug.Log($"[LobbyUI] Steam name: '{steamName}'");
        if (menuNameText) menuNameText.text = steamName;

        AvatarCircleMask.ApplyTo(menuAvatarImage, GetMenuAvatarCornerRadius());
        StartCoroutine(LoadAvatarCoroutine());
    }

    private System.Collections.IEnumerator LoadAvatarCoroutine()
    {
        var task = SteamFriends.GetLargeAvatarAsync(SteamClient.SteamId);
        while (!task.IsCompleted)
            yield return null;

        if (task.IsFaulted)
        {
            Debug.LogWarning($"[LobbyUI] Avatar yuklenemedi: {task.Exception?.Message}");
            yield break;
        }

        var img = task.Result;
        Debug.Log($"[LobbyUI] Avatar result: hasValue={img.HasValue}");
        if (img.HasValue && menuAvatarImage)
        {
            menuAvatarImage.texture = ConvertToTexture2D(img.Value);
            menuAvatarImage.color = Color.white;
            // Maske + texture material'a yazilir (pembe hatasini onler)
            AvatarCircleMask.ApplyTo(menuAvatarImage, GetMenuAvatarCornerRadius());
        }
    }

    // -------------------------------------------------------
    // Panel Ge�i�leri
    // -------------------------------------------------------

    public void ShowMainMenu()
    {
        if (mainMenuPanel) mainMenuPanel.SetActive(true);
        if (lobbyPanel) lobbyPanel.SetActive(false);
        StopRefresh();
        // Sol üst avatar oval çerçeveye tam otursun diye maske her menü açılışında uygulanır
        if (menuAvatarImage != null)
            AvatarCircleMask.ApplyTo(menuAvatarImage, GetMenuAvatarCornerRadius());
    }

    private float GetMenuAvatarCornerRadius()
    {
        if (menuAvatarImage == null) return 0.15f;
        var config = menuAvatarImage.GetComponent<AvatarMaskConfig>();
        return config != null ? config.CornerRadius : menuAvatarCornerRadius;
    }

    private void EnsureDurationRow()
    {
        if (lobbyPanel == null) return;
        if (_durationRow != null)
        {
            _durationRow.SetActive(NetworkServer.active);
            return;
        }
        if (overrideDurationRow != null)
        {
            _durationRow = overrideDurationRow;
            _durationRow.transform.SetParent(lobbyPanel.transform, false);
            _durationLabel = _durationRow.transform.Find("DurationLabel")?.GetComponent<TextMeshProUGUI>();
            var dur20 = _durationRow.transform.Find("Dur20")?.GetComponent<Button>();
            var dur60 = _durationRow.transform.Find("Dur60")?.GetComponent<Button>();
            var dur120 = _durationRow.transform.Find("Dur120")?.GetComponent<Button>();
            if (dur20 == null || dur60 == null || dur120 == null)
            {
                var btns = _durationRow.GetComponentsInChildren<Button>();
                if (btns.Length >= 3) { dur20 = btns[0]; dur60 = btns[1]; dur120 = btns[2]; }
            }
            if (dur20 != null) dur20.onClick.AddListener(() => { if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick(); SetDurationAndUpdateLabel(1200f, "20 dk"); });
            if (dur60 != null) dur60.onClick.AddListener(() => { if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick(); SetDurationAndUpdateLabel(3600f, "1 saat"); });
            if (dur120 != null) dur120.onClick.AddListener(() => { if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick(); SetDurationAndUpdateLabel(7200f, "2 saat"); });
            _dur20Btn = dur20; _dur60Btn = dur60; _dur120Btn = dur120;
            _durationRow.SetActive(NetworkServer.active);
            return;
        }

        _durationRow = new GameObject("DurationRow");
        _durationRow.transform.SetParent(lobbyPanel.transform, false);
        var rt = _durationRow.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 72f);
        rt.sizeDelta = new Vector2(420f, 40f);

        var hlg = _durationRow.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;
        hlg.padding = new RectOffset(8, 8, 4, 4);

        var labelGo = new GameObject("DurationLabel");
        labelGo.transform.SetParent(_durationRow.transform, false);
        var labelRt = labelGo.AddComponent<RectTransform>();
        labelRt.sizeDelta = new Vector2(120f, 32f);
        _durationLabel = labelGo.AddComponent<TextMeshProUGUI>();
        _durationLabel.text = "Oyun süresi:";
        _durationLabel.fontSize = 18;
        _durationLabel.color = Color.white;

        _dur20Btn = CreateLobbyDurationButton("20 dk", 1200f);
        _dur60Btn = CreateLobbyDurationButton("1 saat", 3600f);
        _dur120Btn = CreateLobbyDurationButton("2 saat", 7200f);

        _dur20Btn.transform.SetParent(_durationRow.transform, false);
        _dur60Btn.transform.SetParent(_durationRow.transform, false);
        _dur120Btn.transform.SetParent(_durationRow.transform, false);

        _durationRow.SetActive(NetworkServer.active);
    }

    private Button CreateLobbyDurationButton(string label, float seconds)
    {
        var go = new GameObject("Dur_" + label);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(88f, 36f);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.25f, 0.4f, 0.6f, 0.95f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var txtGo = new GameObject("Text");
        txtGo.transform.SetParent(go.transform, false);
        var txtRt = txtGo.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;
        var txt = txtGo.AddComponent<TextMeshProUGUI>();
        txt.text = label;
        txt.fontSize = 14;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = Color.white;
        btn.onClick.AddListener(() =>
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
            SetDurationAndUpdateLabel(seconds, label);
        });
        return btn;
    }

    private void SetDurationAndUpdateLabel(float seconds, string label)
    {
        var players = GetCurrentPlayers();
        foreach (var p in players)
        {
            if (p != null && p.isLocalPlayer)
            {
                p.CmdSetPendingGameDuration(seconds);
                if (_durationLabel != null) _durationLabel.text = "Süre: " + label;
                break;
            }
        }
    }

    public void ShowLobby()
    {
        if (lobbyPanel) lobbyPanel.SetActive(true);
        if (mainMenuPanel) mainMenuPanel.SetActive(false);

        EnsureDurationRow();

        if (playerCards != null)
        {
            for (int i = 0; i < playerCards.Length; i++)
            {
                if (playerCards[i] != null && playerCards[i].gameObject != null)
                    playerCards[i].gameObject.SetActive(true);
            }
        }

        // Host kontrolü: start ve bot ekle butonları sadece host'ta görünür
        bool isHost = NetworkServer.active;
        if (startGameButton) startGameButton.gameObject.SetActive(isHost);
        if (addBotButton) addBotButton.gameObject.SetActive(isHost);
        if (readyButton) readyButton.gameObject.SetActive(true);

        // Karakter ok butonlar� zaten PlayerCardUI i�inde y�netiliyor

        RefreshLobby();
        StartRefresh();
        StartCoroutine(FitLobbyToScreenNextFrame());
        StartCoroutine(RefreshLobbyAfterSpawnDelay());
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
        if (float.IsNaN(scale) || scale <= 0f) scale = 1f;
        scale = Mathf.Max(0.5f, scale);
        lobbyPanel.transform.localScale = new Vector3(scale, scale, 1f);
    }

    public static void NotifyLobbyJoined() => Instance?.ShowLobby();

    // -------------------------------------------------------
    // Lobi G�ncelleme
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

    /// <summary>
    /// Spawn gecikmesi icin aralikli refresh (FizzySteam host baglantisi gecikebiliyor).
    /// </summary>
    private IEnumerator RefreshLobbyAfterSpawnDelay()
    {
        for (int i = 0; i < 8; i++)
        {
            yield return new WaitForSeconds(0.25f);
            RefreshLobby();
        }
    }

    public void RefreshLobby()
    {
        // Oyuncu say�s�
        var players = GetCurrentPlayers();

        if (playerCountText)
            playerCountText.text = $"{players.Count} / {maxPlayers} OYUNCU";

        // Lobi ID
        var lobby = SteamLobbyManager.Instance?.CurrentLobby;
        if (lobbyIdText && lobby.HasValue)
        {
            string idStr = lobby.Value.Id.ToString();
            string shortId = idStr.Length > 8 ? idStr.Substring(0, 8) : idStr;
            lobbyIdText.text = "LOBI #" + shortId;
        }

        // Kartları güncelle
        for (int i = 0; i < playerCards.Length; i++)
        {
            if (playerCards[i] == null) continue;

            if (i < players.Count)
            {
                playerCards[i].SetupWithPlayer(players[i]);

                // Local oyuncunun kartına zar rengini uygula
                ApplyDiceToCard(playerCards[i], players[i]);
                if (players[i].isLocalPlayer)
                {
                    _selectedDiceIndex = players[i].selectedDiceIndex;
                }
            }
            else
            {
                playerCards[i].SetEmpty();
            }
        }

        // Hazır butonu metni
        if (readyButton != null)
        {
            PlayerObject localPlayer = null;
            foreach (var p in players) { if (p != null && p.isLocalPlayer) { localPlayer = p; break; } }
            var txt = readyButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (txt != null)
                txt.text = localPlayer != null && localPlayer.isReady ? "HAZIR DEĞİL" : "HAZIRIM";
        }

        // Start butonu: en az 2 oyuncu ve hepsi hazır (sadece host)
        bool isHost = NetworkServer.active;
        if (startGameButton != null && isHost)
        {
            bool allReady = players.Count >= 2;
            if (allReady)
            {
                foreach (var p in players)
                {
                    if (p == null || !p.isReady) { allReady = false; break; }
                }
            }
            startGameButton.interactable = allReady;
        }
    }

    /// <summary>
    /// Baglantiyla spawn edilmis oyunculari dondurur.
    /// Host: GameNetworkManager._connectedPlayers (en guvenilir kaynak).
    /// Client: NetworkClient.spawned, sahne UI slotlari haric.
    /// </summary>
    private List<PlayerObject> GetCurrentPlayers()
    {
        var list = new List<PlayerObject>();

        if (NetworkServer.active && GameNetworkManager.Instance != null)
        {
            foreach (var p in GameNetworkManager.Instance.GetAllActivePlayers())
                if (p != null && !list.Contains(p)) list.Add(p);
        }

        if (list.Count == 0)
        {
            Transform slotContainer = (playerCards != null && playerCards.Length > 0 && playerCards[0] != null)
                ? playerCards[0].transform.parent
                : null;

            foreach (var spawned in NetworkClient.spawned.Values)
            {
                var p = spawned.GetComponent<PlayerObject>();
                if (p == null || list.Contains(p)) continue;
                if (slotContainer != null && p.transform.IsChildOf(slotContainer)) continue;
                list.Add(p);
            }

            if (NetworkServer.active)
            {
                foreach (var spawned in NetworkServer.spawned.Values)
                {
                    var p = spawned.GetComponent<PlayerObject>();
                    if (p == null || list.Contains(p)) continue;
                    if (slotContainer != null && p.transform.IsChildOf(slotContainer)) continue;
                    list.Add(p);
                }
            }
        }

        list.Sort((a, b) => a.playerIndex.CompareTo(b.playerIndex));
        return list;
    }

    // -------------------------------------------------------
    // Karakter Se�imi
    // -------------------------------------------------------

    /// <summary>
    /// PlayerCardUI'daki ok butonlar�na bu metodlar� ba�la.
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
        // Local oyuncunun kart�n� bul ve g�ncelle
        foreach (var card in playerCards)
        {
            if (card != null && card.IsLocalPlayer)
            {
                card.RefreshCharacterColor(_selectedCharIndex);
                // �leride: 3D modeli de de�i�tir
            }
        }

        // TODO: Server'a se�im g�nder (Command ile) - ileride eklenecek
    }

    // -------------------------------------------------------
    // Zar Se�imi
    // -------------------------------------------------------

    private void OnDicePickClicked()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
        dicePicker?.Show(_selectedDiceIndex);
    }

    private void OnDiceSelected(int index)
    {
        _selectedDiceIndex = index;

        // Local oyuncunun kart�ndaki zar� g�ncelle
        var players = GetCurrentPlayers();
        foreach (var p in players)
        {
            if (p != null && p.isLocalPlayer)
            {
                p.CmdSetDiceIndex(index);
                break;
            }
        }
    }

    private void ApplyDiceToCard(PlayerCardUI card, PlayerObject player)
    {
        if (player == null || dicePicker == null) return;
        var skin = dicePicker.GetSkin(player.selectedDiceIndex);
        if (skin != null)
            card.SetDiceSprite(skin.diceSprite);
    }

    // -------------------------------------------------------
    // Buton Handler'lar�
    // -------------------------------------------------------

    private void OnHostClicked()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
        Debug.Log("[LobbyUI] OnHostClicked");
        if (NetworkServer.active || NetworkClient.active)
        {
            Debug.LogWarning($"[LobbyUI] Host atlandi: Server={NetworkServer.active} Client={NetworkClient.active}");
            return;
        }
        if (SteamLobbyManager.Instance == null)
        {
            Debug.LogWarning("[LobbyUI] SteamLobbyManager.Instance null!");
            return;
        }
        SteamLobbyManager.Instance.CreateLobby();
    }

    private void OnFindMatchClicked()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
        Debug.Log("[LobbyUI] OnFindMatchClicked");
        if (NetworkServer.active || NetworkClient.active)
        {
            Debug.LogWarning("[LobbyUI] FindMatch atlandi: aktif baglanti var.");
            return;
        }
        if (SteamLobbyManager.Instance == null)
        {
            Debug.LogWarning("[LobbyUI] SteamLobbyManager.Instance null!");
            return;
        }
        SteamLobbyManager.Instance.FindMatch();
    }

    private void OnLeaveClicked()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
        if (SteamLobbyManager.Instance != null && SteamLobbyManager.Instance.IsMatchmaking)
        {
            SteamLobbyManager.Instance.CancelMatchmaking();
            ShowMainMenu();
            return;
        }
        SteamLobbyManager.Instance?.LeaveLobby();
        if (NetworkServer.active) GameNetworkManager.Instance?.StopHost();
        else GameNetworkManager.Instance?.StopClient();
        ShowMainMenu();
    }

    private void OnReadyClicked()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
        var players = GetCurrentPlayers();
        foreach (var p in players)
        {
            if (p != null && p.isLocalPlayer)
            {
                p.CmdSetReady(!p.isReady);
                break;
            }
        }
    }

    private void OnAddBotClicked()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
        var players = GetCurrentPlayers();
        foreach (var p in players)
        {
            if (p != null && p.isLocalPlayer)
            {
                p.CmdRequestAddBot();
                break;
            }
        }
    }

    private void OnStartGameClicked()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
        if (!NetworkServer.active) return;
        Debug.Log("[LobbyUI] Oyun ba�lat�l�yor...");
        string gameScene = GameNetworkManager.Instance != null ? GameNetworkManager.Instance.GameSceneName : "GameScene";
        if (string.IsNullOrEmpty(gameScene)) gameScene = "GameScene";
        Debug.Log($"[LobbyUI] Oyun baslatiliyor: {gameScene}");
        NetworkManager.singleton.ServerChangeScene(gameScene);
    }

    private void OnSettingsClicked()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
        if (SettingsUI.Instance != null) SettingsUI.Instance.Show();
    }

    private void OnCopyIdClicked()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
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
            copyButtonText.text = "KOPYALAND�!";
            yield return new WaitForSeconds(1.5f);
            copyButtonText.text = orig;
        }
    }

    // -------------------------------------------------------
    // Texture D�n���m�
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