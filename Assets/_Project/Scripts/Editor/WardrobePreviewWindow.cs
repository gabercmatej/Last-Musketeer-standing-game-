using System.Collections.Generic;
using Musketeers.Data;
using UnityEditor;
using UnityEngine;

namespace Musketeers.EditorTools
{
    // Musketeers/Wardrobe Preview: all four musketeers wearing one item, side by side, in any pose.
    // Nudge the item (offset/scale, shared by everyone) or each character's anchor for that slot, and the
    // preview redraws with the same placement the game uses. Edits are saved on the assets (undoable),
    // and a nudged item is marked Tuned so Import Art never overwrites it.
    public sealed class WardrobePreviewWindow : EditorWindow
    {
        const float Step = 0.01f;
        const int Size = 320;

        CharacterRoster _roster;
        CosmeticCatalog _catalog;
        PoseKind _pose = PoseKind.Idle;
        CosmeticSlot _slot = CosmeticSlot.Hat;
        int _itemIndex;
        bool _outfit;
        bool _anchors = true;
        readonly Texture2D[] _tex = new Texture2D[4];
        bool _dirty = true;
        Vector2 _scroll;

        [MenuItem("Musketeers/Wardrobe Preview")]
        [MenuItem("Tools/Musketeers/Wardrobe Preview")]
        public static void Open()
        {
            var w = GetWindow<WardrobePreviewWindow>("Wardrobe Preview");
            w.minSize = new Vector2(Size * 4 + 40, 640);
        }

        void OnEnable()
        {
            _roster = AssetDatabase.LoadAssetAtPath<CharacterRoster>("Assets/_Project/Data/CharacterRoster.asset");
            _catalog = AssetDatabase.LoadAssetAtPath<CosmeticCatalog>("Assets/_Project/Data/CosmeticCatalog.asset");
            Undo.undoRedoPerformed += MarkDirty;
            WardrobeCompositor.ClearCache();
            _dirty = true;
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= MarkDirty;
            foreach (var t in _tex)
            {
                if (t != null)
                    DestroyImmediate(t);
            }
        }

        void MarkDirty()
        {
            _dirty = true;
            Repaint();
        }

        void OnGUI()
        {
            if (_roster == null || _catalog == null)
            {
                EditorGUILayout.HelpBox("Run Tools/Musketeers/Build Main Scene and Tools/Musketeers/Import Art first.", MessageType.Info);
                return;
            }
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUI.BeginChangeCheck();
            using (new EditorGUILayout.HorizontalScope())
            {
                _pose = (PoseKind)EditorGUILayout.EnumPopup("Pose", _pose, GUILayout.Width(260));
                _slot = (CosmeticSlot)EditorGUILayout.EnumPopup("Slot", _slot, GUILayout.Width(260));
                var items = _catalog.BySlot(_slot);
                var names = new string[items.Count];
                for (int i = 0; i < items.Count; i++)
                    names[i] = items[i].DisplayName + (items[i].Tuned ? "  ✓" : "");
                _itemIndex = Mathf.Clamp(EditorGUILayout.Popup("Item", _itemIndex, names, GUILayout.Width(360)), 0, Mathf.Max(0, items.Count - 1));
                _outfit = GUILayout.Toggle(_outfit, "Default outfit too", GUILayout.Width(130));
                _anchors = GUILayout.Toggle(_anchors, "Anchors", GUILayout.Width(80));
            }
            if (EditorGUI.EndChangeCheck())
                _dirty = true;

            var list = _catalog.BySlot(_slot);
            var item = list.Count > 0 ? list[Mathf.Clamp(_itemIndex, 0, list.Count - 1)] : null;
            if (_dirty)
                Redraw(item);

            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < 4 && i < _roster.Count; i++)
                {
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(Size)))
                    {
                        var r = GUILayoutUtility.GetRect(Size, Size);
                        if (_tex[i] != null)
                            GUI.DrawTexture(r, _tex[i], ScaleMode.ScaleToFit);
                        AnchorControls(_roster.Get(i));
                    }
                }
            }

            EditorGUILayout.Space();
            if (item != null)
                ItemControls(item);

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reload sprites", GUILayout.Width(140)))
                {
                    WardrobeCompositor.ClearCache();
                    _dirty = true;
                }
                if (GUILayout.Button("Export check sheets (Temp/Wardrobe)", GUILayout.Width(260)))
                    WardrobeCompositor.ExportSheets();
            }
            EditorGUILayout.HelpBox("Dots: yellow = HeadTop (hat), cyan = Face, pink = Back (cape), green = Feet (boots), orange = Hand (musket). " +
                                    "Item values are shared by all four; anchors are per character and pose. Front poses (victory) use the item's Front values.", MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        void Redraw(CosmeticDef item)
        {
            _dirty = false;
            for (int i = 0; i < 4 && i < _roster.Count; i++)
            {
                var c = _roster.Get(i);
                var items = new List<CosmeticDef>();
                if (_outfit)
                {
                    foreach (var s in Loadout.Slots)
                    {
                        var it = s == _slot ? null : _catalog.Get(c.DefaultLoadout.Get(s));
                        if (it != null) items.Add(it);
                    }
                }
                if (item != null)
                    items.Add(item);
                var img = WardrobeCompositor.Compose(c, _pose, items, Size * 2, 1.25f, _anchors);
                if (_tex[i] == null)
                    _tex[i] = new Texture2D(img.W, img.H, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
                _tex[i].SetPixels32(img.Px);
                _tex[i].Apply();
            }
        }

        void ItemControls(CosmeticDef item)
        {
            bool front = _roster.Get(0)?.Pose(_pose)?.Front == true && item.FrontSprite != null;
            EditorGUILayout.LabelField($"{item.DisplayName} ({item.Id}): {(front ? "front values (victory pose)" : "side values")}", EditorStyles.boldLabel);
            Vector2 offset = front ? item.FrontOffset : item.Offset;
            float scale = front ? item.FrontScale : item.Scale;
            EditorGUI.BeginChangeCheck();
            offset = NudgeVector("Offset", offset);
            scale = NudgeFloat("Scale", scale, 0.02f);
            int order = EditorGUILayout.IntField("Sort order (cape < 0 = behind body)", item.SortOrder, GUILayout.Width(360));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(item, "Nudge cosmetic");
                if (front) { item.FrontOffset = offset; item.FrontScale = scale; }
                else { item.Offset = offset; item.Scale = scale; }
                item.SortOrder = order;
                item.Tuned = true;
                EditorUtility.SetDirty(item);
                _dirty = true;
            }
        }

        void AnchorControls(CharacterDef c)
        {
            if (c == null)
                return;
            var pose = c.Pose(_pose);
            EditorGUILayout.LabelField($"{c.DisplayName}: {_slot} anchor", EditorStyles.miniBoldLabel);
            var a = pose.Anchors;
            EditorGUI.BeginChangeCheck();
            Vector2 v = a.For(_slot);
            v = NudgeVector("", v);
            if (_slot == CosmeticSlot.Hat || _slot == CosmeticSlot.Face)
                a.HeadAngle = NudgeFloat("Head angle", a.HeadAngle, 1f);
            if (_slot == CosmeticSlot.Boots)
            {
                a.FeetStretch = NudgeFloat("Stretch", a.FeetStretch <= 0 ? 1f : a.FeetStretch, 0.05f);
                a.HideBoots = EditorGUILayout.Toggle("Hide boots", a.HideBoots);
            }
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(c, "Nudge anchor");
                switch (_slot)
                {
                    case CosmeticSlot.Hat: a.HeadTop = v; break;
                    case CosmeticSlot.Face: a.Face = v; break;
                    case CosmeticSlot.Cape: a.Back = v; break;
                    default: a.Feet = v; break;
                }
                pose.Anchors = a;
                pose.AnchorsSet = true;
                EditorUtility.SetDirty(c);
                _dirty = true;
            }
        }

        static Vector2 NudgeVector(string label, Vector2 v)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (!string.IsNullOrEmpty(label))
                    GUILayout.Label(label, GUILayout.Width(50));
                if (GUILayout.Button("◀", GUILayout.Width(24))) v.x -= Step;
                if (GUILayout.Button("▶", GUILayout.Width(24))) v.x += Step;
                if (GUILayout.Button("▲", GUILayout.Width(24))) v.y += Step;
                if (GUILayout.Button("▼", GUILayout.Width(24))) v.y -= Step;
                v = EditorGUILayout.Vector2Field("", v, GUILayout.Width(150));
            }
            return v;
        }

        static float NudgeFloat(string label, float f, float step)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(label, GUILayout.Width(70));
                if (GUILayout.Button("−", GUILayout.Width(24))) f -= step;
                if (GUILayout.Button("+", GUILayout.Width(24))) f += step;
                f = EditorGUILayout.FloatField(f, GUILayout.Width(70));
            }
            return f;
        }
    }
}
