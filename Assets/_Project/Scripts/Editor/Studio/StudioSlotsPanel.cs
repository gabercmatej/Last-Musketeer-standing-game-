using System;
using Musketeers.Data;
using UnityEditor;
using UnityEngine;

namespace Musketeers.EditorTools.Studio
{
    // The left panel: musketeer picker, pose and weapon-state tabs, and the wardrobe slots.
    public sealed class StudioSlotsPanel
    {
        public const float Width = 310f;
        const int Thumb = 64, PerRow = 4;
        static readonly string[] WeaponTabs = { "Rest", "Musket aimed", "Rapier raised" };
        static readonly string[] PoseTabs = { "Idle", "Lunge", "Hurt", "Victory" };

        readonly StudioState _s;
        Vector2 _scroll;
        GUIStyle _mini;

        public StudioSlotsPanel(StudioState s) => _s = s;

        public void Draw()
        {
            _mini = _mini ?? new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Width(Width));
            Musketeers();
            Tabs();
            Weapons();
            foreach (var slot in Loadout.Slots)
                Slot(slot);
            EditorGUILayout.EndScrollView();
        }

        void Musketeers()
        {
            var names = new string[_s.Roster.Count];
            for (int i = 0; i < names.Length; i++)
                names[i] = _s.Roster.Get(i) != null ? _s.Roster.Get(i).DisplayName : "?";
            int c = GUILayout.Toolbar(_s.Char, names, GUILayout.Height(26));
            if (c != _s.Char)
            {
                _s.Char = c;
                _s.Touch();
            }
            _s.OverrideMusketeer = EditorGUILayout.ToggleLeft("Override for this musketeer", _s.OverrideMusketeer);
            EditorGUILayout.LabelField("Off: edits apply to all four through the anchors.", EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(6);
        }

        void Tabs()
        {
            int p = GUILayout.Toolbar(Array.IndexOf(FitEdit.StudioPoses, _s.Pose), PoseTabs, GUILayout.Height(24));
            if (p >= 0 && FitEdit.StudioPoses[p] != _s.Pose)
            {
                _s.Pose = FitEdit.StudioPoses[p];
                _s.Touch();
            }
            int w = GUILayout.Toolbar((int)_s.Weapon, WeaponTabs, EditorStyles.miniButton);
            if (w != (int)_s.Weapon)
            {
                _s.Weapon = (StudioWeapon)w;
                _s.Touch();
            }
            GUILayout.Space(6);
        }

        void Weapons()
        {
            EditorGUILayout.LabelField("Weapons", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                WeaponCell(_s.Catalog.Musket, true);
                WeaponCell(_s.Catalog.Rapier, false);
            }
            GUILayout.Space(6);
        }

        void WeaponCell(WeaponDef def, bool musket)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(Thumb + 6)))
            {
                bool sel = _s.Selection.Kind == SelectionKind.Weapon && _s.Selection.IsMusket == musket;
                bool on = musket ? _s.MusketOn : _s.RapierOn;
                if (Thumbnail(def != null ? def.Sprite : null, !on, on, sel))
                {
                    if (musket) _s.MusketOn = true; else _s.RapierOn = true;
                    _s.Selection = StudioSelection.Weapon(musket);
                    _s.Touch();
                }
                bool active = StudioTargets.ActiveFor(_s, musket);
                GUILayout.Label(musket ? (active ? "Musket (aimed)" : "Musket") : (active ? "Rapier (raised)" : "Rapier"), _mini, GUILayout.Width(Thumb + 6));
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool now = GUILayout.Toggle(on, "on", EditorStyles.miniButton, GUILayout.Width(30));
                    if (now != on)
                    {
                        if (musket) _s.MusketOn = now; else _s.RapierOn = now;
                        _s.Touch();
                    }
                    if (def != null)
                        CheckToggle(def.For(active), def);
                }
            }
        }

        void Slot(CosmeticSlot slot)
        {
            int si = StudioState.SlotIndex(slot);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(slot.ToString(), EditorStyles.boldLabel, GUILayout.Width(60));
                bool on = EditorGUILayout.ToggleLeft("worn", _s.SlotOn[si]);
                if (on != _s.SlotOn[si])
                {
                    _s.SlotOn[si] = on;
                    _s.Touch();
                }
            }
            var items = _s.Catalog.BySlot(slot);
            for (int i = 0; i < items.Count; i += PerRow)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int j = i; j < Math.Min(items.Count, i + PerRow); j++)
                        ItemCell(slot, si, items[j]);
                }
            }
            GUILayout.Space(8);
        }

        void ItemCell(CosmeticSlot slot, int si, CosmeticDef item)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(Thumb + 6)))
            {
                bool worn = _s.SlotOn[si] && _s.SlotItem[si] == item.Id;
                bool sel = _s.Selection.Kind == SelectionKind.Cosmetic && _s.Selection.ItemId == item.Id;
                if (Thumbnail(item.Sprite, !item.EnabledInV1, worn, sel))
                {
                    _s.Wear(slot, item.Id);
                    _s.Selection = StudioSelection.Cosmetic(slot, item.Id);
                }
                var prev = GUI.color;
                if (!item.EnabledInV1)
                    GUI.color = new Color(1, 1, 1, 0.55f);
                GUILayout.Label(item.DisplayName, _mini, GUILayout.Width(Thumb + 6));
                GUI.color = prev;
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool v1 = Pill(item.EnabledInV1, "v1", new Color(0.2f, 0.45f, 0.9f));
                    if (v1 != item.EnabledInV1)
                    {
                        Undo.RecordObject(item, "Enabled in v1");
                        item.EnabledInV1 = v1;
                        StudioEdit.Touch(item, _s);
                    }
                    CheckToggle(item, item);
                }
            }
        }

        // The tick for the current pose (for weapons: the current weapon state's fit set).
        void CheckToggle(IFitTarget t, UnityEngine.Object owner)
        {
            bool ck = FitEdit.IsChecked(t, _s.Pose);
            bool now = Pill(ck, "✔", new Color(0.18f, 0.62f, 0.3f));
            if (now != ck)
            {
                Undo.RecordObject(owner, "Checked " + t.Id);
                FitEdit.SetChecked(t, _s.Pose, now);
                StudioEdit.Touch(owner, _s);
            }
        }

        // A two-state pill that is unmistakable: on = coloured background and white text, off = dim grey.
        static bool Pill(bool on, string label, Color onColor)
        {
            var r = GUILayoutUtility.GetRect(30, 18, GUILayout.Width(30), GUILayout.Height(18));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(r, on ? onColor : new Color(0.24f, 0.24f, 0.26f));
                var st = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = on ? Color.white : new Color(0.5f, 0.5f, 0.52f) } };
                GUI.Label(r, label, st);
            }
            return GUI.Button(r, GUIContent.none, GUIStyle.none) ? !on : on;
        }

        static bool Thumbnail(Sprite sprite, bool dim, bool worn, bool selected)
        {
            var r = GUILayoutUtility.GetRect(Thumb, Thumb, GUILayout.Width(Thumb), GUILayout.Height(Thumb));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(r, new Color(0.17f, 0.17f, 0.19f));
                if (sprite != null && sprite.texture != null)
                {
                    var tr = sprite.textureRect;
                    var tex = sprite.texture;
                    var uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
                    float aspect = tr.width / Mathf.Max(1f, tr.height);
                    float w = aspect >= 1 ? Thumb - 6 : (Thumb - 6) * aspect;
                    float h = aspect >= 1 ? (Thumb - 6) / aspect : Thumb - 6;
                    var prev = GUI.color;
                    GUI.color = new Color(1, 1, 1, dim ? 0.4f : 1f);
                    GUI.DrawTextureWithTexCoords(new Rect(r.center.x - w / 2, r.center.y - h / 2, w, h), tex, uv);
                    GUI.color = prev;
                }
                if (worn)
                    Frame(r, new Color(0.3f, 0.85f, 0.4f), 2);
                if (selected)
                    Frame(new RectOffset(2, 2, 2, 2).Remove(r), new Color(1f, 0.9f, 0.2f), 2);
            }
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        static void Frame(Rect r, Color c, float t)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, t), c);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - t, r.width, t), c);
            EditorGUI.DrawRect(new Rect(r.x, r.y, t, r.height), c);
            EditorGUI.DrawRect(new Rect(r.xMax - t, r.y, t, r.height), c);
        }
    }
}
