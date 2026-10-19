using Musketeers.EditorTools.ArtImport;
using UnityEditor;
using UnityEngine;

namespace Musketeers.EditorTools.Studio
{
    // The top bar: one big SAVE button and an obvious saved / unsaved state.
    public static class StudioSave
    {
        public const float BarHeight = 38f;
        static readonly Color Orange = new Color(0.86f, 0.45f, 0.08f);
        static readonly Color Green = new Color(0.16f, 0.46f, 0.26f);

        public static string Title(StudioState s) => s != null && s.HasUnsaved ? "Wardrobe Studio *" : "Wardrobe Studio";

        public static void DrawBar(StudioState s)
        {
            var r = GUILayoutUtility.GetRect(0, BarHeight, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, s.HasUnsaved ? Orange : Green);
            var label = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15, alignment = TextAnchor.MiddleLeft, normal = { textColor = Color.white } };
            GUI.Label(new Rect(r.x + 12, r.y, r.width - 180, r.height), s.HasUnsaved ? "● Unsaved changes" : "All saved", label);
            var btn = new GUIStyle(GUI.skin.button) { fontSize = 15, fontStyle = FontStyle.Bold };
            if (GUI.Button(new Rect(r.xMax - 150, r.y + 4, 140, r.height - 8), "SAVE", btn))
                Save(s);
        }

        // Assets to disk, then FitDefaults.json so a re-import never undoes the work.
        public static void Save(StudioState s)
        {
            int n = s.Unsaved.Count;
            foreach (var o in s.Unsaved)
            {
                if (o != null)
                    EditorUtility.SetDirty(o);
            }
            AssetDatabase.SaveAssets();
            CosmeticTuning.SaveDefaults();
            s.Unsaved.Clear();
            Debug.Log($"[Studio] Saved {n} asset(s) and FitDefaults.json.");
        }
    }
}
