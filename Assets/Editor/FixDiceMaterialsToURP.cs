using UnityEditor;
using UnityEngine;

/// <summary>
/// Dice_6 (ve benzeri Built-in Standard kullanan) materyalleri URP Lit shader'a çevirir.
/// Kullanım: Tools > Fix Dice Materials (URP)
/// </summary>
public static class FixDiceMaterialsToURP
{
    const string MenuPath = "Tools/Fix Dice Materials (URP)";
    const string URP_LitShaderName = "Universal Render Pipeline/Lit";

    [MenuItem(MenuPath)]
    public static void Run()
    {
        Shader urpLit = Shader.Find(URP_LitShaderName);
        if (urpLit == null)
        {
            Debug.LogError("URP Lit shader bulunamadı. Proje Universal Render Pipeline kullanıyor mu kontrol edin.");
            return;
        }

        string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Dice_6" });
        int fixedCount = 0;

        foreach (string guid in materialGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            // Zaten URP Lit ise atla
            if (mat.shader != null && mat.shader.name == URP_LitShaderName)
                continue;

            // Pembe/kırık shader (Unity Built-in yok) veya Built-in Standard -> URP'ye çevir
            bool needsConversion = mat.shader == null
                || mat.shader.name == "Standard"
                || mat.shader.name.Contains("Hidden/")
                || mat.shader.name.Contains("Error");

            if (!needsConversion)
                continue;

            Undo.RecordObject(mat, "Fix Dice Material to URP");
            ConvertMaterialToURPLit(mat, urpLit);
            EditorUtility.SetDirty(mat);
            fixedCount++;
        }

        if (fixedCount > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log($"Dice materyalleri URP'ye çevrildi: {fixedCount} adet. (Tools > Fix Dice Materials (URP))");
        }
        else
        {
            Debug.Log("Dice_6 klasöründe dönüştürülecek Built-in materyal bulunamadı (veya hepsi zaten URP).");
        }
    }

    static void ConvertMaterialToURPLit(Material mat, Shader urpLit)
    {
        // Eski değerleri sakla (Standard property isimleri)
        Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
        Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
        Texture bumpMap = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
        float bumpScale = mat.HasProperty("_BumpScale") ? mat.GetFloat("_BumpScale") : 1f;
        Texture metallicGloss = mat.HasProperty("_MetallicGlossMap") ? mat.GetTexture("_MetallicGlossMap") : null;
        Texture occlusionMap = mat.HasProperty("_OcclusionMap") ? mat.GetTexture("_OcclusionMap") : null;
        float glossiness = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.5f;
        float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
        float occlusionStrength = mat.HasProperty("_OcclusionStrength") ? mat.GetFloat("_OcclusionStrength") : 1f;

        mat.shader = urpLit;

        // URP Lit property isimleri
        if (mainTex != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", mainTex);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (bumpMap != null && mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", bumpMap);
        if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", bumpScale);
        if (metallicGloss != null && mat.HasProperty("_MetallicGlossMap")) mat.SetTexture("_MetallicGlossMap", metallicGloss);
        if (occlusionMap != null && mat.HasProperty("_OcclusionMap")) mat.SetTexture("_OcclusionMap", occlusionMap);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", glossiness);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_OcclusionStrength")) mat.SetFloat("_OcclusionStrength", occlusionStrength);
    }
}
