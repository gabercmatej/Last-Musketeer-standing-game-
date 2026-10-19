using Musketeers.Data;
using UnityEditor;
using UnityEngine;

namespace Musketeers.EditorTools.Studio
{
    // Musketeers/Wardrobe Studio: fit hats, faces, capes, boots and weapons onto the four musketeers like
    // a wardrobe. Layout and wiring only; the panels live in their own files.
    public sealed class WardrobeStudioWindow : EditorWindow
    {
        StudioState _s;
        StudioTextures _tex;
        StudioCanvas _canvas;
        StudioStrip _strip;
        StudioSlotsPanel _slots;
        StudioInspector _inspector;
        int _lastRev = -1;

        [MenuItem("Musketeers/Wardrobe Studio")]
        [MenuItem("Tools/Musketeers/Wardrobe Studio")]
        public static void Open()
        {
            var w = GetWindow<WardrobeStudioWindow>("Wardrobe Studio");
            w.minSize = new Vector2(1380, 940);
        }

        void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
            RigCompositor.ClearCache();
            Build();
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            _tex?.Clear();
        }

        void OnDestroy()
        {
            if (_s != null && _s.HasUnsaved && EditorUtility.DisplayDialog("Wardrobe Studio", "Save your fitting changes?", "Save", "Not now"))
                StudioSave.Save(_s);
        }

        void Build()
        {
            var roster = AssetDatabase.LoadAssetAtPath<CharacterRoster>("Assets/_Project/Data/CharacterRoster.asset");
            var catalog = AssetDatabase.LoadAssetAtPath<CosmeticCatalog>("Assets/_Project/Data/CosmeticCatalog.asset");
            if (roster == null || catalog == null || roster.Count == 0)
            {
                _s = null;
                return;
            }
            _s = new StudioState(roster, catalog);
            _tex = new StudioTextures();
            _canvas = new StudioCanvas(_s, _tex);
            _strip = new StudioStrip(_s, _tex);
            _slots = new StudioSlotsPanel(_s);
            _inspector = new StudioInspector(_s, _canvas);
        }

        void OnUndoRedo()
        {
            if (_s == null)
                return;
            _s.Touch();
            _canvas.Invalidate();
            Repaint();
        }

        void OnGUI()
        {
            if (_s == null)
            {
                EditorGUILayout.HelpBox("Run Tools/Musketeers/Build Main Scene and Tools/Musketeers/Import Art first.", MessageType.Info);
                if (GUILayout.Button("Try again"))
                    Build();
                return;
            }
            string title = StudioSave.Title(_s);
            if (titleContent.text != title)
                titleContent = new GUIContent(title);

            StudioSave.DrawBar(_s);
            using (new EditorGUILayout.HorizontalScope())
            {
                _slots.Draw();
                using (new EditorGUILayout.VerticalScope())
                {
                    CanvasToolbar();
                    float availW = position.width - StudioSlotsPanel.Width - StudioInspector.Width - 40;
                    float availH = position.height - StudioSave.BarHeight - 28 - (StudioStrip.Cell + 24) - 16;
                    int size = Mathf.Clamp((int)Mathf.Min(availW, availH) / 8 * 8, 360, StudioCanvas.MaxSize);
                    _canvas.Draw(size);
                    GUILayout.Space(4);
                    _strip.Draw(_canvas.Dragging);
                }
                _inspector.Draw();
            }
            if (_s.Rev != _lastRev)
            {
                _lastRev = _s.Rev;
                Repaint();
            }
        }

        void CanvasToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var c = _s.Character;
                GUILayout.Label($"{(c != null ? c.DisplayName : "")} · {_s.Pose}", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label("Zoom", EditorStyles.miniLabel);
                float z = GUILayout.HorizontalSlider(_s.Zoom, 0.5f, 2f, GUILayout.Width(140));
                if (!Mathf.Approximately(z, _s.Zoom))
                {
                    _s.Zoom = z;
                    _s.Touch();
                }
                if (GUILayout.Button("Reload sprites", EditorStyles.toolbarButton))
                {
                    RigCompositor.ClearCache();
                    _s.Touch();
                }
            }
        }
    }
}
