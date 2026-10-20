using UnityEngine;

// ffuwp (Oct 20, 2026): UWP / Xbox One S Dev Mode only. Copied into the project by the UWP workflow
// (.github/workflows/unity-four-froggies-uwp.yml) from unity/four-froggies-uwp/, so the WebGL, Windows and Android
// builds never contain it. Everything below compiles to nothing outside a UWP player (UNITY_WSA && !UNITY_EDITOR).
//   * Perf60 starts on the LOW tier (an Xbox One S is far below a desktop GPU); the governor may still step up after a
//     long stretch at >= 57 fps and steps back down the usual way. Applied once the first views exist (lobby camera).
//   * The B button raises the system Back request on Xbox; left unhandled, the shell takes the app back to Home. We mark
//     it handled so B stays a game button (Input System still sees it as gamepad buttonEast).
public static class FFXboxUWP
{
#if UNITY_WSA && !UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        var go = new GameObject("FFXboxUWP");
        Object.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideInHierarchy;
        go.AddComponent<FFXboxUWPRunner>();
        Debug.Log("FFXBOX: UWP player " + SystemInfo.deviceModel + " / " + SystemInfo.operatingSystem + " / " + SystemInfo.graphicsDeviceName
                  + " " + SystemInfo.graphicsDeviceVersion + " mem " + SystemInfo.systemMemorySize + " MB");
#if ENABLE_WINMD_SUPPORT
        UnityEngine.WSA.Application.InvokeOnUIThread(() =>
        {
            try
            {
                Windows.UI.Core.SystemNavigationManager.GetForCurrentView().BackRequested += (sender, args) => { args.Handled = true; };
                Debug.Log("FFXBOX: Back request handled in-game (B stays a game button)");
            }
            catch (System.Exception e) { Debug.LogWarning("FFXBOX: back handler " + e.Message); }
        }, false);
#endif
    }
#endif
}

public class FFXboxUWPRunner : MonoBehaviour
{
    float waited;
    void Update()
    {
        waited += Time.unscaledDeltaTime;
        if (!Perf60.On || Perf60.Forced) { Destroy(this); return; }
        if (Look.Views <= 0 && waited < 60f) return;   // wait for the first ApplyViews (Perf60 then has its cameras)
        if (Perf60.Tier != 0) Perf60.SetTier(0, "UWP / Xbox default low tier");
        Destroy(this);
    }
}
