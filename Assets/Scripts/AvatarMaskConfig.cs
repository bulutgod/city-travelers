using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ana menü sol üst (veya herhangi bir) avatar RawImage'a maske uygular.
/// Inspector'dan Corner Radius'u deneyerek açık yeşil alanı tam doldurana getirebilirsin.
/// - 0.5 = daire (köşelerde yeşil bant kalır)
/// - 0.01'e yakın = yuvarlatılmış dikdörtgen, açık yeşili tamamen doldurur
/// </summary>
[RequireComponent(typeof(RawImage))]
public class AvatarMaskConfig : MonoBehaviour
{
    [Tooltip("0.5 = daire, küçültünce görsel açık yeşil alanı tam doldurur. Deneyerek ayarla.")]
    [Range(0.01f, 0.5f)]
    [SerializeField] private float cornerRadius = 0.15f;

    /// <summary>Inspector'daki Corner Radius değeri (LobbyUINew bunu kullanır).</summary>
    public float CornerRadius => cornerRadius;

    [Tooltip("Boş bırakırsan bu GameObject'teki RawImage kullanılır.")]
    [SerializeField] private RawImage targetImage;

    private RawImage _rawImage;
    private float _lastAppliedRadius = -1f;

    private void Awake()
    {
        if (targetImage == null)
            targetImage = GetComponent<RawImage>();
        _rawImage = targetImage;
    }

    private void OnEnable()
    {
        ApplyMask();
    }

    private void OnValidate()
    {
        if (Application.isPlaying && _rawImage != null && Mathf.Abs(_lastAppliedRadius - cornerRadius) > 0.001f)
            ApplyMask();
    }

    /// <summary>
    /// Maskeyi güncel cornerRadius ile uygular.
    /// </summary>
    public void ApplyMask()
    {
        if (targetImage == null)
            targetImage = GetComponent<RawImage>();
        if (targetImage == null) return;

        _rawImage = targetImage;
        AvatarCircleMask.ApplyTo(targetImage, cornerRadius);
        _lastAppliedRadius = cornerRadius;
    }

    /// <summary>
    /// Inspector'dan denemek için: Corner Radius değerini set et.
    /// </summary>
    public void SetCornerRadius(float value)
    {
        cornerRadius = Mathf.Clamp(value, 0.01f, 0.5f);
        ApplyMask();
    }
}
