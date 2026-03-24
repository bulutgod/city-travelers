using Steamworks;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using TMPro;

/// <summary>
/// Steam oyun içi overlay: Shift+Tab normalde Steam istemcisi tarafından açılır.
/// Unity Input System bazen bu kısayolu yutabildiği için, aynı kombinasyonu yakalayıp
/// Steam arkadaşlar panelini açıyoruz (OpenOverlay). Steam zaten yakaladıysa
/// çoğu kurulumda bu tuş oyunuza düşmez; çift tetikleme nadirdir.
/// </summary>
public sealed class SteamOverlayInput : MonoBehaviour
{
    private const string FriendsOverlayDialog = "Friends";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject(nameof(SteamOverlayInput));
        DontDestroyOnLoad(go);
        go.AddComponent<SteamOverlayInput>();
    }

    private void Awake()
    {
        Application.runInBackground = true;
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (!SteamClient.IsValid)
            return;
        var kb = Keyboard.current;
        if (kb == null)
            return;
        if (IsTypingInSelectableField())
            return;

        bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
        if (shift && kb.tabKey.wasPressedThisFrame)
            SteamFriends.OpenOverlay(FriendsOverlayDialog);
#endif
    }

    private static bool IsTypingInSelectableField()
    {
        var es = EventSystem.current;
        if (es == null)
            return false;
        var go = es.currentSelectedGameObject;
        if (go == null)
            return false;
        if (go.GetComponent<UnityEngine.UI.InputField>() != null)
            return true;
        if (go.GetComponent<TMP_InputField>() != null)
            return true;
        return false;
    }
}
