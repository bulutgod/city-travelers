using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lobi panelinde sag tarafta Steam arkadas listesi. Gorunum <see cref="LobbyFriendsSidebarConfig"/> ile (LobbyUINew).
/// Sadece arkadas lobisinde gorunur; matchmaking lobisinde gizlenir.
/// </summary>
[DisallowMultipleComponent]
public sealed class LobbyFriendsSidebar : MonoBehaviour
{
    private const float DefaultDebounce = 0.35f;
    private const float InviteAckDurationSeconds = 60f;

    [SerializeField] private float refreshDebounceSeconds = DefaultDebounce;

    private LobbyFriendsSidebarConfig _userConfig;
    private static LobbyFriendsSidebarConfig _runtimeDefaults;

    private RectTransform _root;
    private RectTransform _content;
    private TextMeshProUGUI _hintText;
    private ScrollRect _scroll;
    private bool _built;
    private bool _dirty = true;
    private float _nextRebuildTime;

    /// <summary>Basarili davet sonrasi "Davet edildi" gosterimi (unscaled time).</summary>
    private readonly Dictionary<ulong, float> _inviteSentAtUnscaled = new Dictionary<ulong, float>();

    private LobbyFriendsSidebarConfig C =>
        _userConfig != null
            ? _userConfig
            : (_runtimeDefaults ??= ScriptableObject.CreateInstance<LobbyFriendsSidebarConfig>());

    /// <summary>LobbyUINew, AddComponent sonrasi veya sahne prefabinda Inspector oncesi cagrilir.</summary>
    public void ApplyConfig(LobbyFriendsSidebarConfig config)
    {
        _userConfig = config;
    }

    private void OnEnable()
    {
        if (SteamClient.IsValid)
            SteamFriends.OnPersonaStateChange += OnPersonaStateChanged;
        _dirty = true;
    }

    private void OnDisable()
    {
        if (SteamClient.IsValid)
            SteamFriends.OnPersonaStateChange -= OnPersonaStateChanged;
    }

    private void Start()
    {
        if (!_built)
            BuildChrome();
    }

    private void Update()
    {
        if (_inviteSentAtUnscaled.Count > 0)
        {
            var expired = new List<ulong>();
            foreach (var kv in _inviteSentAtUnscaled)
            {
                if (Time.unscaledTime - kv.Value >= InviteAckDurationSeconds)
                    expired.Add(kv.Key);
            }
            foreach (var id in expired)
                _inviteSentAtUnscaled.Remove(id);
            if (expired.Count > 0)
                _dirty = true;
        }

        if (!_dirty || Time.unscaledTime < _nextRebuildTime)
            return;
        _nextRebuildTime = Time.unscaledTime + refreshDebounceSeconds;
        _dirty = false;
        RebuildList();
    }

    public void MarkDirty() => _dirty = true;

    private void OnPersonaStateChanged(Friend _) => _dirty = true;

    private void BuildChrome()
    {
        if (_built) return;
        _built = true;

        var parent = transform.parent as RectTransform;
        if (parent == null) return;

        var cfg = C;

        _root = gameObject.GetComponent<RectTransform>();
        if (_root == null) _root = gameObject.AddComponent<RectTransform>();
        _root.anchorMin = new Vector2(1f, 0f);
        _root.anchorMax = new Vector2(1f, 1f);
        _root.pivot = new Vector2(1f, 0.5f);
        _root.anchoredPosition = Vector2.zero;
        _root.sizeDelta = new Vector2(cfg.sidebarWidth, 0f);
        _root.offsetMin = new Vector2(_root.offsetMin.x, cfg.marginVertical);
        _root.offsetMax = new Vector2(-cfg.marginRight, -cfg.marginVertical);

        var panelBg = gameObject.GetComponent<Image>();
        if (panelBg == null) panelBg = gameObject.AddComponent<Image>();
        ApplyPanelBackground(panelBg, cfg);
        panelBg.raycastTarget = true;

        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(transform, false);
        var titleRt = titleGo.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -6f);
        titleRt.sizeDelta = new Vector2(0f, 28f);
        var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
        titleTmp.text = cfg.titleText;
        titleTmp.fontSize = cfg.titleFontSize;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.color = cfg.titleColor;

        _hintText = new GameObject("Hint").AddComponent<TextMeshProUGUI>();
        _hintText.transform.SetParent(transform, false);
        var hintRt = _hintText.rectTransform;
        hintRt.anchorMin = new Vector2(0f, 1f);
        hintRt.anchorMax = new Vector2(1f, 1f);
        hintRt.pivot = new Vector2(0.5f, 1f);
        hintRt.anchoredPosition = new Vector2(0f, -32f);
        hintRt.sizeDelta = new Vector2(-12f, 22f);
        _hintText.fontSize = 12;
        _hintText.alignment = TextAlignmentOptions.Center;
        _hintText.color = new Color(0.75f, 0.8f, 0.85f, 1f);
        _hintText.gameObject.SetActive(false);

        var scrollGo = new GameObject("Scroll");
        scrollGo.transform.SetParent(transform, false);
        var scrollRt = scrollGo.AddComponent<RectTransform>();
        scrollRt.anchorMin = Vector2.zero;
        scrollRt.anchorMax = Vector2.one;
        scrollRt.offsetMin = new Vector2(cfg.scrollHorizontalPadding, cfg.scrollBottomOffset);
        scrollRt.offsetMax = new Vector2(-cfg.scrollHorizontalPadding, -cfg.scrollTopOffset);

        _scroll = scrollGo.AddComponent<ScrollRect>();
        _scroll.horizontal = false;
        _scroll.movementType = ScrollRect.MovementType.Clamped;
        _scroll.scrollSensitivity = 24f;

        var viewportGo = new GameObject("Viewport");
        viewportGo.transform.SetParent(scrollGo.transform, false);
        var viewportRt = viewportGo.AddComponent<RectTransform>();
        viewportRt.anchorMin = Vector2.zero;
        viewportRt.anchorMax = Vector2.one;
        viewportRt.offsetMin = Vector2.zero;
        viewportRt.offsetMax = Vector2.zero;
        var vImg = viewportGo.AddComponent<Image>();
        vImg.color = new Color(0, 0, 0, 0.02f);
        viewportGo.AddComponent<Mask>().showMaskGraphic = false;

        var contentGo = new GameObject("Content");
        contentGo.transform.SetParent(viewportGo.transform, false);
        _content = contentGo.AddComponent<RectTransform>();
        _content.anchorMin = new Vector2(0f, 1f);
        _content.anchorMax = new Vector2(1f, 1f);
        _content.pivot = new Vector2(0.5f, 1f);
        _content.anchoredPosition = Vector2.zero;
        var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 4f;
        vlg.padding = new RectOffset(4, 4, 4, 4);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;
        var fitter = contentGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _scroll.viewport = viewportRt;
        _scroll.content = _content;
    }

    private static void ApplyPanelBackground(Image img, LobbyFriendsSidebarConfig cfg)
    {
        if (cfg.panelBackgroundSprite != null)
        {
            img.sprite = cfg.panelBackgroundSprite;
            img.type = cfg.panelSliced ? Image.Type.Sliced : Image.Type.Simple;
            img.color = cfg.panelColor;
        }
        else
        {
            img.sprite = null;
            img.type = Image.Type.Simple;
            img.color = cfg.panelColor;
        }
    }

    private void RebuildList()
    {
        if (_content == null) return;

        for (int i = _content.childCount - 1; i >= 0; i--)
            Destroy(_content.GetChild(i).gameObject);

        if (!SteamClient.IsValid)
        {
            _hintText.gameObject.SetActive(true);
            _hintText.text = "Steam bağlı değil";
            return;
        }

        _hintText.gameObject.SetActive(false);

        var friends = SteamFriends.GetFriends()
            .Where(f => f.IsFriend && !f.IsMe)
            .OrderByDescending(f => f.IsOnline)
            .ThenBy(f => f.Name ?? "")
            .ToList();

        if (friends.Count == 0)
        {
            _hintText.gameObject.SetActive(true);
            _hintText.text = "Arkadaş bulunamadı";
            return;
        }

        Steamworks.Data.Lobby? lobby = null;
        var mgr = SteamLobbyManager.Instance;
        if (mgr != null && mgr.InLobby)
            lobby = mgr.CurrentLobby;

        var memberIds = new HashSet<ulong>();
        if (lobby.HasValue)
        {
            foreach (var m in lobby.Value.Members)
                memberIds.Add(m.Id.Value);
        }

        var cfg = C;
        foreach (var f in friends)
            CreateRow(f, lobby, memberIds, cfg);
    }

    private void CreateRow(Friend friend, Steamworks.Data.Lobby? lobby, HashSet<ulong> memberIds, LobbyFriendsSidebarConfig cfg)
    {
        var row = new GameObject("Friend_" + friend.Id.Value);
        row.transform.SetParent(_content, false);
        var rowRt = row.AddComponent<RectTransform>();
        rowRt.sizeDelta = new Vector2(0f, cfg.rowHeight);

        var rowBg = row.AddComponent<Image>();
        if (cfg.rowBackgroundSprite != null)
        {
            rowBg.sprite = cfg.rowBackgroundSprite;
            rowBg.type = cfg.rowSliced ? Image.Type.Sliced : Image.Type.Simple;
        }
        else
        {
            rowBg.sprite = null;
            rowBg.type = Image.Type.Simple;
        }
        rowBg.color = cfg.rowBackgroundColor;

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;
        hlg.padding = new RectOffset(4, 4, 2, 2);

        var avatarGo = new GameObject("Avatar");
        avatarGo.transform.SetParent(row.transform, false);
        var avatarRt = avatarGo.AddComponent<RectTransform>();
        avatarRt.sizeDelta = new Vector2(cfg.avatarSize, cfg.avatarSize);
        var avatarLe = avatarGo.AddComponent<LayoutElement>();
        avatarLe.preferredWidth = cfg.avatarSize;
        avatarLe.preferredHeight = cfg.avatarSize;
        var raw = avatarGo.AddComponent<RawImage>();
        raw.color = friend.IsOnline ? cfg.avatarPlaceholderOnline : cfg.avatarPlaceholderOffline;
        AvatarCircleMask.ApplyTo(raw, cfg.avatarCornerRadius);
        StartCoroutine(LoadAvatarCoroutine(raw, friend, cfg.avatarCornerRadius));

        var textCol = new GameObject("Texts");
        textCol.transform.SetParent(row.transform, false);
        textCol.AddComponent<RectTransform>();
        var textLe = textCol.AddComponent<LayoutElement>();
        textLe.flexibleWidth = 1f;
        textLe.minWidth = 80f;
        var textVlg = textCol.AddComponent<VerticalLayoutGroup>();
        textVlg.spacing = 0f;
        textVlg.childAlignment = TextAnchor.MiddleLeft;
        textVlg.childControlHeight = true;
        textVlg.childControlWidth = true;
        textVlg.childForceExpandHeight = false;
        textVlg.childForceExpandWidth = true;

        var nameGo = new GameObject("Name");
        nameGo.transform.SetParent(textCol.transform, false);
        var nameTmp = nameGo.AddComponent<TextMeshProUGUI>();
        nameTmp.text = string.IsNullOrEmpty(friend.Name) ? "…" : friend.Name;
        nameTmp.fontSize = cfg.nameFontSize;
        nameTmp.fontStyle = FontStyles.Bold;
        nameTmp.color = cfg.nameColor;
        nameTmp.enableWordWrapping = false;
        nameTmp.overflowMode = TextOverflowModes.Ellipsis;

        var statusGo = new GameObject("Status");
        statusGo.transform.SetParent(textCol.transform, false);
        var statusTmp = statusGo.AddComponent<TextMeshProUGUI>();
        ulong idVal = friend.Id.Value;
        bool inLobby = memberIds.Contains(idVal);
        bool showInviteAck = !inLobby &&
                             _inviteSentAtUnscaled.TryGetValue(idVal, out float sentAt) &&
                             Time.unscaledTime - sentAt < InviteAckDurationSeconds;

        if (inLobby)
            statusTmp.text = "Lobide";
        else if (friend.IsOnline)
            statusTmp.text = "Çevrimiçi";
        else
            statusTmp.text = "Çevrimdışı";
        statusTmp.fontSize = cfg.statusFontSize;
        statusTmp.color = inLobby ? cfg.statusInLobby : (friend.IsOnline ? cfg.statusOnline : cfg.statusOffline);

        var inviteGo = new GameObject("Invite");
        inviteGo.transform.SetParent(row.transform, false);
        var inviteLe = inviteGo.AddComponent<LayoutElement>();
        inviteLe.preferredWidth = cfg.inviteButtonSize.x;
        inviteLe.minWidth = cfg.inviteButtonSize.x;
        inviteLe.preferredHeight = cfg.inviteButtonSize.y;
        var inviteImg = inviteGo.AddComponent<Image>();
        var inviteBtn = inviteGo.AddComponent<Button>();
        inviteBtn.targetGraphic = inviteImg;

        bool canInvite = lobby.HasValue && !inLobby && !showInviteAck;
        ApplyInviteVisual(inviteImg, cfg, canInvite);

        var inviteLabelGo = new GameObject("Label");
        inviteLabelGo.transform.SetParent(inviteGo.transform, false);
        var inviteLabelRt = inviteLabelGo.AddComponent<RectTransform>();
        inviteLabelRt.anchorMin = Vector2.zero;
        inviteLabelRt.anchorMax = Vector2.one;
        inviteLabelRt.offsetMin = Vector2.zero;
        inviteLabelRt.offsetMax = Vector2.zero;
        var inviteTxt = inviteLabelGo.AddComponent<TextMeshProUGUI>();
        inviteTxt.text = showInviteAck ? "Davet edildi" : cfg.inviteLabelText;
        inviteTxt.fontSize = cfg.inviteLabelFontSize;
        inviteTxt.alignment = TextAlignmentOptions.Center;
        inviteTxt.color = showInviteAck ? cfg.statusOnline : cfg.inviteLabelColor;

        inviteBtn.interactable = canInvite;

        inviteBtn.onClick.AddListener(() =>
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
            var m = SteamLobbyManager.Instance;
            if (m == null || !m.InLobby) return;
            bool ok = m.CurrentLobby.InviteFriend(new SteamId { Value = idVal });
            if (ok)
            {
                _inviteSentAtUnscaled[idVal] = Time.unscaledTime;
                _nextRebuildTime = 0f;
                _dirty = true;
            }
        });

        row.AddComponent<FriendRowTextureCleanup>().Target = raw;
    }

    private static void ApplyInviteVisual(Image inviteImg, LobbyFriendsSidebarConfig cfg, bool enabled)
    {
        if (enabled)
        {
            if (cfg.inviteButtonSprite != null)
            {
                inviteImg.sprite = cfg.inviteButtonSprite;
                inviteImg.type = cfg.inviteSliced ? Image.Type.Sliced : Image.Type.Simple;
                inviteImg.color = Color.white;
            }
            else
            {
                inviteImg.sprite = null;
                inviteImg.type = Image.Type.Simple;
                inviteImg.color = cfg.inviteColorNormal;
            }
        }
        else
        {
            if (cfg.inviteButtonDisabledSprite != null)
            {
                inviteImg.sprite = cfg.inviteButtonDisabledSprite;
                inviteImg.type = cfg.inviteSliced ? Image.Type.Sliced : Image.Type.Simple;
                inviteImg.color = Color.white;
            }
            else if (cfg.inviteButtonSprite != null)
            {
                inviteImg.sprite = cfg.inviteButtonSprite;
                inviteImg.type = cfg.inviteSliced ? Image.Type.Sliced : Image.Type.Simple;
                inviteImg.color = cfg.inviteColorDisabled;
            }
            else
            {
                inviteImg.sprite = null;
                inviteImg.type = Image.Type.Simple;
                inviteImg.color = cfg.inviteColorDisabled;
            }
        }
    }

    private static IEnumerator LoadAvatarCoroutine(RawImage target, Friend friend, float cornerRadius)
    {
        var req = friend.RequestInfoAsync();
        while (!req.IsCompleted)
            yield return null;

        var task = friend.GetMediumAvatarAsync();
        while (!task.IsCompleted)
            yield return null;

        if (target == null) yield break;
        if (task.IsFaulted || !task.Result.HasValue) yield break;
        var tex = ConvertToTexture2D(task.Result.Value);
        if (tex != null)
        {
            target.texture = tex;
            target.color = Color.white;
            AvatarCircleMask.ApplyTo(target, cornerRadius);
        }
    }

    private static Texture2D ConvertToTexture2D(Steamworks.Data.Image image)
    {
        var texture = new Texture2D((int)image.Width, (int)image.Height, TextureFormat.RGBA32, false);
        var flipped = new byte[image.Data.Length];
        int rowSize = (int)image.Width * 4;
        for (int y = 0; y < image.Height; y++)
        {
            int srcRow = (int)(image.Height - 1 - y);
            System.Array.Copy(image.Data, srcRow * rowSize, flipped, y * rowSize, rowSize);
        }
        texture.LoadRawTextureData(flipped);
        texture.Apply();
        return texture;
    }

    private sealed class FriendRowTextureCleanup : MonoBehaviour
    {
        public RawImage Target;

        private void OnDestroy()
        {
            if (Target == null) return;
            var t = Target.texture;
            if (t != null) Destroy(t);
        }
    }
}
