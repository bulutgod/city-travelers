using UnityEngine;

/// <summary>
/// AudioManager yoksa otomatik olusturur. LoadingScene veya ilk yuklenen sahneye bos GameObject ekle, bu scripti ata.
/// </summary>
public class AudioManagerBootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureAudioManagerExists()
    {
        if (AudioManager.Instance != null) return;

        var existing = FindFirstObjectByType<AudioManager>();
        if (existing != null) return;

        var go = new GameObject("AudioManager");
        go.AddComponent<AudioManager>();
    }
}
