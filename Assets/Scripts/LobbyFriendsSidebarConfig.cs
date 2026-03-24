using UnityEngine;

/// <summary>
/// Lobi arkadas listesi paneli gorunumu. Assets > Create > Brom City > Lobby Friends Sidebar Config ile olustur,
/// LobbyUINew uzerindeki alana surukle. Sprite atanmazsa renklerle devam eder.
/// </summary>
[CreateAssetMenu(fileName = "LobbyFriendsSidebarConfig", menuName = "Brom City/Lobby Friends Sidebar Config")]
public class LobbyFriendsSidebarConfig : ScriptableObject
{
    [Header("Panel boyutu ve konum")]
    [Tooltip("Sag sutunun genisligi (pixel).")]
    public float sidebarWidth = 280f;
    [Tooltip("Sag kenardan iceri bosluk.")]
    public float marginRight = 16f;
    [Tooltip("Ust / alt ic bosluk.")]
    public float marginVertical = 24f;

    [Header("Panel arka plan")]
    public Sprite panelBackgroundSprite;
    [Tooltip("Sprite ile carpilan renk (sprite yoksa duz renk).")]
    public Color panelColor = new Color(0.08f, 0.12f, 0.18f, 0.92f);
    public bool panelSliced = true;

    [Header("Satir")]
    public float rowHeight = 56f;
    [Tooltip("Profil resmi kutu boyutu (genislik = yukseklik).")]
    public float avatarSize = 48f;
    [Range(0.01f, 0.5f)]
    public float avatarCornerRadius = 0.5f;
    public Sprite rowBackgroundSprite;
    public Color rowBackgroundColor = new Color(0f, 0f, 0f, 0.15f);
    public bool rowSliced = true;
    public Color avatarPlaceholderOnline = new Color(0.2f, 0.55f, 0.35f, 1f);
    public Color avatarPlaceholderOffline = new Color(0.35f, 0.35f, 0.4f, 1f);

    [Header("Davet butonu")]
    public Vector2 inviteButtonSize = new Vector2(88f, 36f);
    public Sprite inviteButtonSprite;
    public Sprite inviteButtonDisabledSprite;
    [Tooltip("Sprite yoksa kullanilan renkler.")]
    public Color inviteColorNormal = new Color(0.25f, 0.45f, 0.65f, 0.95f);
    public Color inviteColorDisabled = new Color(0.25f, 0.25f, 0.28f, 0.7f);
    public bool inviteSliced = true;

    [Header("Baslik")]
    public string titleText = "ARKADAŞLAR";
    public float titleFontSize = 16f;
    public Color titleColor = Color.white;

    [Header("Metinler")]
    public float nameFontSize = 13f;
    public float statusFontSize = 11f;
    public Color nameColor = Color.white;
    public Color statusInLobby = new Color(0.5f, 0.85f, 1f);
    public Color statusOnline = new Color(0.65f, 0.9f, 0.7f);
    public Color statusOffline = new Color(0.65f, 0.65f, 0.7f);
    public Color inviteLabelColor = Color.white;
    public float inviteLabelFontSize = 12f;
    public string inviteLabelText = "Davet";

    [Header("Scroll")]
    public float scrollTopOffset = 58f;
    public float scrollBottomOffset = 8f;
    public float scrollHorizontalPadding = 6f;
}
