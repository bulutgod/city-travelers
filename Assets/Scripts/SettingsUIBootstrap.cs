using UnityEngine;

/// <summary>
/// SettingsUI yoksa otomatik olusturur. Ilk sahneye eklenebilir veya RuntimeInitializeOnLoadMethod ile.
/// </summary>
public class SettingsUIBootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureSettingsUIExists()
    {
        if (SettingsUI.Instance != null) return;

        var existing = FindFirstObjectByType<SettingsUI>();
        if (existing != null) return;

        var go = new GameObject("SettingsUI");
        go.AddComponent<SettingsUI>();
    }
}
