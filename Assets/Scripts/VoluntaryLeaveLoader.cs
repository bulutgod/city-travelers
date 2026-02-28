using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Gönüllü ayrılışta eski NetworkManager'ı kaldırıp sahneyi temiz yükler.
/// Yeni sahnenin NetworkManager'ı kullanılır - transport taze başlar.
/// </summary>
public class VoluntaryLeaveLoader : MonoBehaviour
{
    public void LoadSceneClean(string sceneName, GameObject oldNetworkManager)
    {
        StartCoroutine(DoLoad(sceneName, oldNetworkManager));
    }

    private System.Collections.IEnumerator DoLoad(string sceneName, GameObject oldNetworkManager)
    {
        yield return null;

        if (oldNetworkManager != null)
        {
            NetworkManager.ResetStatics();
            Destroy(oldNetworkManager);
            yield return null;
        }

        if (!string.IsNullOrWhiteSpace(sceneName))
            SceneManager.LoadScene(sceneName);

        Destroy(gameObject);
    }
}
