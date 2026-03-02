using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Steam avatar RawImage'lara daire veya yuvarlatilmis dikdortgen maske uygular.
/// CornerRadius: 0.5 = daire, 0.01-0.49 = acik yesil alani dolduran rounded rect.
/// </summary>
public static class AvatarCircleMask
{
    private static Shader _shader;
    private static readonly Dictionary<int, Material> _materialCache = new Dictionary<int, Material>();

    private static Shader GetShader()
    {
        if (_shader != null) return _shader;
        _shader = Shader.Find("UI/CircleMask");
        if (_shader == null)
            _shader = Resources.Load<Shader>("Shaders/UICircleMask");
        return _shader;
    }

    private static Material GetOrCreateMaterial(RawImage rawImage, float cornerRadius)
    {
        if (rawImage == null) return null;
        var shader = GetShader();
        if (shader == null)
        {
            Debug.LogWarning("[AvatarCircleMask] UI/CircleMask shader bulunamadi. Avatar kare gorunecek.");
            return null;
        }

        int key = rawImage.GetInstanceID();
        if (!_materialCache.TryGetValue(key, out var mat) || mat == null)
        {
            mat = new Material(shader);
            _materialCache[key] = mat;
        }

        mat.SetFloat("_CornerRadius", Mathf.Clamp(cornerRadius, 0.01f, 0.5f));
        if (rawImage.texture != null)
            mat.SetTexture("_MainTex", rawImage.texture);
        mat.SetColor("_Color", rawImage.color);
        return mat;
    }

    /// <summary>
    /// RawImage'a maske uygular. cornerRadius: 0.5 = daire, kucuk deger = acik yesili dolduran rounded rect.
    /// </summary>
    public static void ApplyTo(RawImage rawImage, float cornerRadius = 0.5f)
    {
        if (rawImage == null) return;
        var mat = GetOrCreateMaterial(rawImage, cornerRadius);
        if (mat != null)
            rawImage.material = mat;
    }

    /// <summary>
    /// Eski API: daire maske (cornerRadius = 0.5).
    /// </summary>
    public static void ApplyTo(RawImage rawImage)
    {
        ApplyTo(rawImage, 0.5f);
    }
}
