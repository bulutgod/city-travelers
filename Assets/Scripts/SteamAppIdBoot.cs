using System.IO;
using UnityEngine;

/// <summary>
/// Build'de Steam API'nin calismasi icin steam_appid.txt dosyasinin
/// .exe ile ayni klasorde olmasi gerekir. Editor'da proje klasoru kullanildigi
/// icin senin bilgisayarinda calisiyor; build alip arkadasa attiginda dosya
/// olmadigi icin Steam baglantisi kurulmuyor.
/// Bu script oyun acilir acilmaz (Steam init'ten once) steam_appid.txt'yi
/// StreamingAssets'ten okuyup exe klasorune yazar.
/// </summary>
public static class SteamAppIdBoot
{
    private const uint DefaultAppId = 480; // Spacewar - test icin. Kendi App ID'nizi alinca degistirin.

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureSteamAppIdFile()
    {
        string exeDir = Path.GetDirectoryName(Application.dataPath);
        if (string.IsNullOrEmpty(exeDir)) return;

        string targetPath = Path.Combine(exeDir, "steam_appid.txt");
        if (File.Exists(targetPath)) return;

        uint appId = DefaultAppId;
        string streamingPath = Path.Combine(Application.streamingAssetsPath, "steam_appid.txt");
        if (File.Exists(streamingPath))
        {
            string content = File.ReadAllText(streamingPath).Trim();
            if (uint.TryParse(content, out uint parsed))
                appId = parsed;
        }

        try
        {
            File.WriteAllText(targetPath, appId.ToString());
            Debug.Log($"[Steam] steam_appid.txt yazildi: {targetPath} (AppId: {appId})");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Steam] steam_appid.txt yazilamadi: {e.Message}");
        }
    }
}
