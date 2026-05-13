using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ana menü sol üst (veya herhangi bir) avatar RawImage'a maske uygular.
/// Köşeli = maske yok (düz dikdörtgen). Yuvarlak/yuvarlatılmış istersen Köşeli'yi kapatıp Corner Radius ile ayarla.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class AvatarMaskConfig : MonoBehaviour
{
    [Tooltip("Açık = avatar köşeli (düz dikdörtgen, maske yok). Kapalı = Corner Radius ile daire/yuvarlatılmış.")]
    [SerializeField] private bool koseeli = false;

    [Tooltip("Sadece Köşeli kapalıyken kullanılır. 0.5 = daire, küçük değer = yuvarlatılmış dikdörtgen.")]
    [Range(0.01f, 0.5f)]
    [SerializeField] private float cornerRadius = 0.5f;

    /// <summary>Inspector'daki Köşeli değeri.</summary>
    public bool Koseeli => koseeli;

    /// <summary>Inspector'daki Corner Radius değeri (Köşeli kapalıyken kullanılır).</summary>
    public float CornerRadius => cornerRadius;

    [Tooltip("Boş bırakırsan bu GameObject'teki RawImage kullanılır.")]
    [SerializeField] private RawImage targetImage;

    private RawImage _rawImage;
    private float _lastAppliedRadius = -1f;
    private bool _lastKoseeli = true;

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
        if (Application.isPlaying && _rawImage != null && (Mathf.Abs(_lastAppliedRadius - cornerRadius) > 0.001f || _lastKoseeli != koseeli))
            ApplyMask();
    }

    /// <summary>
    /// Maskeyi güncel ayarlarla uygular. Köşeli = maske yok, değilse cornerRadius kullanılır.
    /// </summary>
    public void ApplyMask()
    {
        if (targetImage == null)
            targetImage = GetComponent<RawImage>();
        if (targetImage == null) return;

        _rawImage = targetImage;
        if (koseeli)
            AvatarCircleMask.ApplyTo(targetImage, 0f);
        else
            AvatarCircleMask.ApplyTo(targetImage, cornerRadius);
        _lastAppliedRadius = cornerRadius;
        _lastKoseeli = koseeli;
    }

    /// <summary>
    /// Inspector'dan denemek için: Corner Radius değerini set et (Köşeli kapalıyken).
    /// </summary>
    public void SetCornerRadius(float value)
    {
        cornerRadius = Mathf.Clamp(value, 0.01f, 0.5f);
        ApplyMask();
    }
}
