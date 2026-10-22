using System.Runtime.InteropServices;
using UnityEngine;

namespace Musketeers.Net
{
    // Copy text to the player's clipboard: the browser's clipboard on Web (Assets/Plugins/WebGL/Clipboard.jslib),
    // the system copy buffer in the Editor.
    public static class Clipboard
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void Musketeers_CopyToClipboard(string text);
#endif

        public static void Copy(string text)
        {
            text = text ?? "";
#if UNITY_WEBGL && !UNITY_EDITOR
            Musketeers_CopyToClipboard(text);
#else
            GUIUtility.systemCopyBuffer = text;
#endif
        }
    }
}
