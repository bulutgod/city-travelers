using System.Collections;
using System.Collections.Generic;
using Mirror;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyUI : MonoBehaviour
{
    public static LobbyUI Instance { get; private set; }

    [Header("Panel Referanslarý")]
    [SerializeField] private GameObject lobbyPanel;    
    [SerializeField] private GameObject mainMenuPanel;   

    [Header("Ana Menü")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private TextMeshProUGUI playerNameText; 
    [SerializeField] private RawImage playerAvatarImage;    

    [Header("Lobi")]
    [SerializeField] private TextMeshProUGUI lobbyTitleText;
    [SerializeField] private TextMeshProUGUI lobbyIdText;
    [SerializeField] private TextMeshProUGUI playerCountText;
    [SerializeField] private Button startGameButton;   
    [SerializeField] private Button leaveButton;
    [SerializeField] private Button copyIdButton;      
    [SerializeField] private Transform playerCardsContainer; 
    [SerializeField] private GameObject playerCardPrefab; 
    
    [Header("Ayarlar")]
    [SerializeField] private int maxPlayers = 4;
    [SerializeField] private float refreshInterval = 0.5f; 


    private readonly List<PlayerCard> _playerCards = new List<PlayerCard>();
    private Coroutine _refreshCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
   
        hostButton?.onClick.AddListener(OnHostClicked);
        leaveButton?.onClick.AddListener(OnLeaveClicked);
        startGameButton?.onClick.AddListener(OnStartGameClicked);
        copyIdButton?.onClick.AddListener(OnCopyIdClicked);
        quitButton?.onClick.AddListener(() => Application.Quit());

        ShowMainMenu();

        LoadLocalSteamInfo();

        CreatePlayerCardSlots();
    }

    private void OnDestroy()
    {
        hostButton?.onClick.RemoveAllListeners();
        leaveButton?.onClick.RemoveAllListeners();
        startGameButton?.onClick.RemoveAllListeners();
        copyIdButton?.onClick.RemoveAllListeners();
        quitButton?.onClick.RemoveAllListeners();
    }
    private async void LoadLocalSteamInfo()
    {
        if (!SteamClient.IsValid) return;

        if (playerNameText)
            playerNameText.text = SteamClient.Name;

        var steamId = SteamClient.SteamId;
        var image = await SteamFriends.GetLargeAvatarAsync(steamId);
        if (image.HasValue && playerAvatarImage)
        {
            playerAvatarImage.texture = ConvertToTexture2D(image.Value);
        }
    }
    public void ShowMainMenu()
    {
        if (mainMenuPanel) mainMenuPanel.SetActive(true);
        if (lobbyPanel) lobbyPanel.SetActive(false);

        if (_refreshCoroutine != null)
        {
            StopCoroutine(_refreshCoroutine);
            _refreshCoroutine = null;
        }
    }

    public void ShowLobby()
    {
        if (mainMenuPanel) mainMenuPanel.SetActive(false);
        if (lobbyPanel) lobbyPanel.SetActive(true);

        UpdateLobbyInfo();

        bool isHost = NetworkServer.active;
        if (startGameButton) startGameButton.gameObject.SetActive(isHost);

        if (_refreshCoroutine != null) StopCoroutine(_refreshCoroutine);
        _refreshCoroutine = StartCoroutine(RefreshLoop());
    }

    private void UpdateLobbyInfo()
    {
        var lobby = SteamLobbyManager.Instance?.CurrentLobby;

        if (lobbyTitleText)
            lobbyTitleText.text = NetworkServer.active ? "LOBÝNÝZ" : "LOBÝ";

        if (lobbyIdText && lobby.HasValue)
            lobbyIdText.text = $"#{lobby.Value.Id.ToString().Substring(0, 8)}...";

        int count = NetworkServer.active
            ? GameNetworkManager.Instance?.GetConnectedPlayers().Count ?? 0
            : NetworkClient.spawned.Count;

        if (playerCountText)
            playerCountText.text = $"{count} / {maxPlayers} OYUNCU";

        RefreshPlayerCards();
    }

    private void CreatePlayerCardSlots()
    {
        if (playerCardsContainer == null || playerCardPrefab == null) return;


        foreach (var card in _playerCards)
            if (card) Destroy(card.gameObject);
        _playerCards.Clear();


        for (int i = 0; i < maxPlayers; i++)
        {
            var go = Instantiate(playerCardPrefab, playerCardsContainer);
            var card = go.GetComponent<PlayerCard>();
            if (card != null)
            {
                card.SetEmpty(i);
                _playerCards.Add(card);
            }
        }
    }

    private void RefreshPlayerCards()
    {
        if (_playerCards.Count == 0) return;


        var players = new List<PlayerObject>();
        foreach (var spawned in NetworkClient.spawned.Values)
        {
            var p = spawned.GetComponent<PlayerObject>();
            if (p != null) players.Add(p);
        }

        for (int i = 0; i < _playerCards.Count; i++)
        {
            if (i < players.Count)
                _playerCards[i].Setup(players[i]);
            else
                _playerCards[i].SetEmpty(i);
        }
    }

    private IEnumerator RefreshLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(refreshInterval);

            if (lobbyPanel && lobbyPanel.activeSelf)
            {
            
                int count = 0;
                if (NetworkServer.active)
                    count = GameNetworkManager.Instance?.GetConnectedPlayers().Count ?? 0;

             
                if (count == 0)
                    count = NetworkClient.spawned.Count;

                if (playerCountText)
                    playerCountText.text = $"{count} / {maxPlayers} OYUNCU";

                RefreshPlayerCards();
            }
        }
    }

    private void OnHostClicked()
    {
        if (NetworkServer.active || NetworkClient.active) return;
        SteamLobbyManager.Instance?.CreateLobby();
      
    }

    private void OnLeaveClicked()
    {
        SteamLobbyManager.Instance?.LeaveLobby();
        if (NetworkServer.active) GameNetworkManager.Instance?.StopHost();
        else if (NetworkClient.active) GameNetworkManager.Instance?.StopClient();
        ShowMainMenu();
    }

    private void OnStartGameClicked()
    {
        if (!NetworkServer.active) return;
        Debug.Log("[LobbyUI] Oyun baþlatýlýyor...");
        
    }

    private void OnCopyIdClicked()
    {
        var lobby = SteamLobbyManager.Instance?.CurrentLobby;
        if (lobby.HasValue)
        {
            GUIUtility.systemCopyBuffer = lobby.Value.Id.ToString();
            StartCoroutine(ShowCopiedFeedback());
        }
    }

    private IEnumerator ShowCopiedFeedback()
    {
        if (copyIdButton)
        {
            var txt = copyIdButton.GetComponentInChildren<TextMeshProUGUI>();
            if (txt)
            {
                string original = txt.text;
                txt.text = "KOPYALANDÝ!";
                yield return new WaitForSeconds(1.5f);
                txt.text = original;
            }
        }
    }

    public static void NotifyLobbyJoined()
    {
        Instance?.ShowLobby();
    }

    private Texture2D ConvertToTexture2D(Steamworks.Data.Image image)
    {
        var texture = new Texture2D((int)image.Width, (int)image.Height,
            TextureFormat.RGBA32, false);
        var flipped = new byte[image.Data.Length];
        int rowSize = (int)image.Width * 4;
        for (int y = 0; y < image.Height; y++)
        {
            int srcRow = (int)(image.Height - 1 - y);
            System.Array.Copy(image.Data, srcRow * rowSize,
                              flipped, y * rowSize, rowSize);
        }
        texture.LoadRawTextureData(flipped);
        texture.Apply();
        return texture;
    }

}