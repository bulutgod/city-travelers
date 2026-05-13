using UnityEngine;

/// <summary>
/// Sahnedeki SettingsUI instance'ini runtime basinda bulur.
/// Settings paneli artik koddan olusturulmaz; hazir GameObject uzerinden calisir.
/// </summary>
public class SettingsUIBootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CacheSceneSettingsUI()
    {
        if (SettingsUI.Instance != null) return;

        var existing = FindFirstObjectByType<SettingsUI>();
        if (existing != null) return;
    }
}
