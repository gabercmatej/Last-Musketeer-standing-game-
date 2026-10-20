using UnityEditor;

namespace Musketeers.EditorTools
{
    // Dev-only: choose a map for a bot match that starts by itself when you enter Play mode (AppFlow reads the
    // pref). Used for MCP screenshots. "Off" restores the normal menu.
    public static class DevAutoplayMenu
    {
        const string Key = "Musketeers.DevAutoplayMap";

        [MenuItem("Tools/Musketeers/Dev/Autoplay on Courtyard")] static void Courtyard() => EditorPrefs.SetString(Key, "courtyard");
        [MenuItem("Tools/Musketeers/Dev/Autoplay on Bridge")] static void Bridge() => EditorPrefs.SetString(Key, "bridge");
        [MenuItem("Tools/Musketeers/Dev/Autoplay off")] static void Off() { EditorPrefs.DeleteKey(Key); EditorPrefs.DeleteKey("Musketeers.DevHumanMap"); }
        [MenuItem("Tools/Musketeers/Dev/Human match on Courtyard (control all)")] static void HumanCourtyard() { EditorPrefs.DeleteKey(Key); EditorPrefs.SetString("Musketeers.DevHumanMap", "courtyard"); }
    }
}
