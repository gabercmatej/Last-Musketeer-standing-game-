using System.Runtime.InteropServices;

namespace Musketeers.UI
{
    // Fullscreen for every in-game button (FIXES_4E §7). In the browser it goes through Assets/Plugins/WebGL/Fullscreen.jslib
    // (Fullscreen API on the page container); in the Editor and standalone builds it falls back to Screen.fullScreen.
    public static class WebFullscreen
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int Musketeers_IsFullscreen();
        [DllImport("__Internal")] static extern void Musketeers_ToggleFullscreen();
#endif

        public static bool IsFullscreen
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return Musketeers_IsFullscreen() != 0;
#else
                return UnityEngine.Screen.fullScreen;
#endif
            }
        }

        public static void Toggle()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Musketeers_ToggleFullscreen();
#else
            UnityEngine.Screen.fullScreen = !UnityEngine.Screen.fullScreen;
#endif
        }
    }
}
