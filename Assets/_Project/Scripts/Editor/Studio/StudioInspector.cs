using Musketeers.Data;
using UnityEditor;
using UnityEngine;

namespace Musketeers.EditorTools.Studio
{
    // The right panel: the selected item's fit, badges, layer list, anchors and the progress checklist.
    public sealed class StudioInspector
    {
        public const float Width = 320f;
        readonly StudioState _s;
        readonly StudioCanvas _canvas;
        Vector2 _scroll, _progressScroll;

        public StudioInspector(StudioState s, StudioCanvas canvas)
        {
            _s = s;
            _canvas = canvas;
        }

        public void Draw()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Width(Width));
            if (_s.AnchorMode)
                AnchorFields();
            else
            {
                FitFields();
                if (StudioTargets.Resolve(_s).Valid)
                {
                    GUILayout.Space(6);
                    MaskFields();
                    GUILayout.Space(6);
                    CopyPasteFields();
                }
            }
            GUILayout.Space(6);
            var anchors = GUILayout.Toggle(_s.AnchorMode, "Anchors (edit the 7 attachment points)", EditorStyles.miniButton, GUILayout.Height(22));
            if (anchors != _s.AnchorMode)
            {
                _s.AnchorMode = anchors;
                _s.Selection = StudioSelection.None;
            }
            var show = EditorGUILayout.ToggleLeft("Show masks", _s.ShowMasks);
            if (show != _s.ShowMasks)
            {
                _s.ShowMasks = show;
                _s.Touch();
            }
            GUILayout.Space(6);
            Layers();
            GUILayout.Space(6);
            Progress();
            EditorGUILayout.EndScrollView();
        }

        void FitFields()
        {
            var t = StudioTargets.Resolve(_s);
            if (!t.Valid)
            {
                EditorGUILayout.LabelField("Nothing selected", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Click an item on the canvas, or in the lists.", EditorStyles.wordWrappedMiniLabel);
                return;
            }
            string id = _s.CharId;
            EditorGUILayout.LabelField(t.Name, EditorStyles.boldLabel);
            bool custom = FitEdit.IsCustom(t.Fit, _s.Pose);
            string badge = _s.Pose == PoseKind.Idle ? "master" : custom ? "custom" : "inherits Idle";
            EditorGUILayout.LabelField($"{_s.Pose}: {badge}", EditorStyles.miniBoldLabel);
            var f = FitEdit.Resolve(t.Fit, id, _s.Pose);
            EditorGUI.BeginChangeCheck();
            var ox = EditorGUILayout.FloatField("Offset X", f.Offset.x);
            var oy = EditorGUILayout.FloatField("Offset Y", f.Offset.y);
            var rot = EditorGUILayout.FloatField("Rotation", f.Rotation);
            var sc = EditorGUILayout.FloatField("Scale", f.S);
            if (EditorGUI.EndChangeCheck())
            {
                StudioEdit.Write(t.Fit, t.Owner, _s.Pose, _s.OverrideMusketeer ? id : null, new ItemFit(ox, oy, Mathf.Clamp(sc, 0.05f, 10f), rot), _s.LinkAllPoses);
                _s.MarkDirty(t.Owner);
                _s.Touch();
            }
            if (custom && GUILayout.Button("Reset to Idle"))
            {
                Undo.RecordObject(t.Owner, "Reset to Idle");
                FitEdit.ResetToIdle(t.Fit, _s.Pose);
                _s.MarkDirty(t.Owner);
                _s.Touch();
            }
            _s.LinkAllPoses = EditorGUILayout.ToggleLeft("Link all poses", _s.LinkAllPoses);
            if (FitEdit.HasMusketeerOverride(t.Fit, id, _s.Pose))
            {
                EditorGUILayout.LabelField("override: " + _s.Character.DisplayName, EditorStyles.miniBoldLabel);
                if (!_s.OverrideMusketeer)
                    EditorGUILayout.LabelField("Turn on \"Override for this musketeer\" to edit it.", EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button("Remove override"))
                {
                    Undo.RecordObject(t.Owner, "Remove override");
                    FitEdit.RemoveMusketeerOverride(t.Fit, id, _s.Pose);
                    _s.MarkDirty(t.Owner);
                    _s.Touch();
                }
            }
        }


        // ---------- masks ----------

        void Edited(UnityEngine.Object owner, string undoName, System.Action change)
        {
            Undo.RecordObject(owner, undoName);
            change();
            if (owner is CosmeticDef cd)
                cd.Tuned = true;
            _s.MarkDirty(owner);
            _s.Touch();
        }

        void MaskFields()
        {
            var t = StudioTargets.Resolve(_s);
            var m = _s.Mask;
            EditorGUILayout.LabelField("Masks", EditorStyles.boldLabel);
            bool worn = _canvas.SelectedLayer >= 0;
            bool front = StudioMasks.DrawsFront(_s);
            bool custom = t.Fit.Masks.IsCustom(_s.Pose);
            bool locked = front || custom;
            bool per = locked || _s.MaskThisPose;
            using (new EditorGUI.DisabledScope(locked || (m.Active && !m.Body)))
            {
                var now = EditorGUILayout.ToggleLeft("This pose only", per);
                if (!locked)
                    _s.MaskThisPose = now;
                per = locked || _s.MaskThisPose;
            }
            if (front)
                EditorGUILayout.LabelField("This pose draws the front sprite: its mask is always per pose.", EditorStyles.wordWrappedMiniLabel);
            var path = StudioMasks.ItemPath(t.Fit, _s.Pose, per, false);
            bool has = StudioMasks.HasPoints(path);
            bool editingItem = m.Active && !m.Body && m.Matches(_s);
            if (editingItem)
            {
                EditorGUILayout.HelpBox("Click to add points, click-drag for curves, click the first point to close. Alt-drag a handle to break it. Delete removes a point.", MessageType.None);
                if (GUILayout.Button("Finish editing (Enter)"))
                    m.Stop();
            }
            else if (!has)
            {
                using (new EditorGUI.DisabledScope(!worn))
                {
                    if (GUILayout.Button("Add mask"))
                    {
                        Edited(t.Owner, "Add mask", () => StudioMasks.ItemPath(t.Fit, _s.Pose, per, true));
                        m.StartItem(_s, per);
                    }
                }
                if (!worn)
                    EditorGUILayout.LabelField("Wear the item first.", EditorStyles.miniLabel);
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!worn))
                    {
                        if (GUILayout.Button("Edit mask"))
                            m.StartItem(_s, per);
                    }
                    if (GUILayout.Button("Delete mask"))
                    {
                        m.Stop();
                        Edited(t.Owner, "Delete mask", () =>
                        {
                            if (per)
                                t.Fit.Masks.ResetToBase(_s.Pose);
                            else
                                t.Fit.Masks.Base = new BezierPath();
                        });
                    }
                }
            }
            if (has)
            {
                bool inv = EditorGUILayout.ToggleLeft("Invert (keep the inside instead of cutting it away)", path.Invert);
                if (inv != path.Invert)
                    Edited(t.Owner, "Mask invert", () => path.Invert = inv);
            }
            if (t.Fit.Masks.IsCustom(_s.Pose))
            {
                EditorGUILayout.LabelField("custom mask (this pose)", EditorStyles.miniBoldLabel);
                if (GUILayout.Button("Reset mask to base"))
                {
                    m.Stop();
                    Edited(t.Owner, "Reset mask to base", () => t.Fit.Masks.ResetToBase(_s.Pose));
                }
            }
            EditorGUILayout.LabelField("Masks live on the item: they work on all four musketeers.", EditorStyles.wordWrappedMiniLabel);
            BodyMaskFields();
        }

        void BodyMaskFields()
        {
            var ids = StudioClipboard.BodyMaskIds(_s);
            if (ids.Count == 0)
                return;
            GUILayout.Space(4);
            EditorGUILayout.LabelField($"Body masks · {_s.Character.DisplayName} · {_s.Pose}", EditorStyles.miniBoldLabel);
            var c = _s.Character;
            var pf = c != null && c.Fit != null ? c.Fit.Get(c.Resolve(_s.Pose)) : null;
            foreach (var id in ids)
            {
                string label = id == BodyMaskId.BootBack ? "Body boot mask (back foot)" : id == BodyMaskId.BootFront ? "Body boot mask (front foot)" : "Hand mask";
                bool on = _s.Mask.Active && _s.Mask.Body && _s.Mask.BodyId == id && _s.Mask.Matches(_s);
                bool set = pf != null && pf.BodyMask(id) != null && pf.BodyMask(id).Valid;
                var now = GUILayout.Toggle(on, (set ? "✔ " : "· ") + label, EditorStyles.miniButton);
                if (now != on)
                {
                    if (now)
                        _s.Mask.StartBody(_s, id);
                    else
                        _s.Mask.Stop();
                    _s.Touch();
                }
            }
            CopyMaskFields();
        }

        void CopyMaskFields()
        {
            GUILayout.Space(4);
            EditorGUILayout.LabelField("Copy mask from", EditorStyles.miniBoldLabel);
            var names = new string[_s.Roster.Count];
            for (int i = 0; i < names.Length; i++)
                names[i] = _s.Roster.Get(i) != null ? _s.Roster.Get(i).DisplayName : "?";
            _s.CopySource = EditorGUILayout.Popup(_s.CopySource, names);
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < names.Length && i < _s.CopyTargets.Length; i++)
                {
                    using (new EditorGUI.DisabledScope(i == _s.CopySource))
                        _s.CopyTargets[i] = GUILayout.Toggle(_s.CopyTargets[i] && i != _s.CopySource, names[i], EditorStyles.miniButton);
                }
            }
            int scope = GUILayout.Toolbar(_s.CopyAllPoses ? 1 : 0, new[] { "This pose", "All poses" }, EditorStyles.miniButton);
            _s.CopyAllPoses = scope == 1;
            if (GUILayout.Button("Copy mask"))
                StudioClipboard.CopyBodyMasks(_s);
        }

        // ---------- copy / paste ----------

        void CopyPasteFields()
        {
            EditorGUILayout.LabelField("Copy / paste fit", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Copy fit"))
                    StudioClipboard.Copy(_s);
                _s.CopyIncludeMask = EditorGUILayout.ToggleLeft("include mask", _s.CopyIncludeMask, GUILayout.Width(100));
            }
            EditorGUILayout.LabelField(StudioClipboard.Describe(_s), EditorStyles.miniLabel);
            using (new EditorGUI.DisabledScope(!_s.HasClipboard))
            {
                if (GUILayout.Button(_s.PasteOpen ? "Paste to…  ▾" : "Paste to…  ▸"))
                    _s.PasteOpen = !_s.PasteOpen;
            }
            if (!_s.PasteOpen || !_s.HasClipboard)
                return;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (GUILayout.Button("This pose"))
                    StudioClipboard.PasteThisPose(_s);
                if (GUILayout.Button("All poses of this item"))
                    StudioClipboard.PasteAllPoses(_s);
                EditorGUILayout.LabelField("Other items in this slot", EditorStyles.miniBoldLabel);
                var others = StudioClipboard.Others(_s);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("All", EditorStyles.miniButtonLeft))
                    {
                        foreach (var o in others)
                            _s.PasteChecked.Add(o.Key);
                    }
                    if (GUILayout.Button("None", EditorStyles.miniButtonRight))
                        _s.PasteChecked.Clear();
                }
                foreach (var o in others)
                {
                    bool on = _s.PasteChecked.Contains(o.Key);
                    bool now = EditorGUILayout.ToggleLeft(o.Label, on);
                    if (now && !on) _s.PasteChecked.Add(o.Key);
                    else if (!now && on) _s.PasteChecked.Remove(o.Key);
                }
                _s.PasteOthersAllPoses = EditorGUILayout.ToggleLeft("all poses", _s.PasteOthersAllPoses);
                if (GUILayout.Button("Paste to checked items"))
                    StudioClipboard.PasteOthers(_s);
            }
        }

        void AnchorFields()
        {
            EditorGUILayout.LabelField("Anchors", EditorStyles.boldLabel);
            var c = _s.Character;
            var k = c != null ? c.Resolve(_s.Pose) : PoseKind.Idle;
            if (_s.Selection.Kind != SelectionKind.Anchor || c == null || c.Fit == null || c.Fit.Get(k) == null)
            {
                EditorGUILayout.LabelField("Click an anchor dot on the canvas.", EditorStyles.wordWrappedMiniLabel);
                return;
            }
            var pf = c.Fit.Get(k);
            var id = _s.Selection.Anchor;
            var a = pf.Get(id);
            EditorGUILayout.LabelField($"{id} · {c.DisplayName} · {k}", EditorStyles.miniBoldLabel);
            EditorGUI.BeginChangeCheck();
            var x = EditorGUILayout.FloatField("X", a.Position.x);
            var y = EditorGUILayout.FloatField("Y", a.Position.y);
            var r = EditorGUILayout.FloatField("Rotation", a.Rotation);
            var sc = EditorGUILayout.FloatField("Scale", a.S);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(c.Fit, "Edit anchor");
                pf.Set(id, new Anchor(x, y, r, Mathf.Max(0.01f, sc)));
                _s.MarkDirty(c.Fit);
                _s.Touch();
            }
        }

        void Layers()
        {
            EditorGUILayout.LabelField("Layers (top to bottom)", EditorStyles.boldLabel);
            var layers = _canvas.Layers;
            for (int i = layers.Count - 1; i >= 0; i--)
            {
                var l = layers[i];
                string name = LayerName(l);
                bool sel = _s.Selection.ItemId == l.Key && l.Key != "body";
                using (new EditorGUI.DisabledScope(l.Key == "body"))
                {
                    if (GUILayout.Toggle(sel, name, EditorStyles.miniButton) && !sel)
                    {
                        _s.AnchorMode = false;
                        _canvas.SelectLayer(i);
                    }
                }
            }
        }

        string LayerName(RigCompositor.ComposedLayer l)
        {
            switch (l.Key)
            {
                case "body": return "Body";
                case "musket": return "Musket";
                case "rapier": return "Rapier";
            }
            var item = _s.Catalog.Get(l.Key);
            string n = item != null ? item.DisplayName : l.Key;
            if (item != null && item.Slot == CosmeticSlot.Boots)
                return (l.BackFoot ? "Back boot: " : "Front boot: ") + n;
            return item != null ? item.Slot + ": " + n : n;
        }

        void Progress()
        {
            var rows = StudioProgress.Build(_s.Catalog);
            EditorGUILayout.LabelField($"{StudioProgress.CountChecked(rows)} / {rows.Count} checked", EditorStyles.boldLabel);
            _progressScroll = EditorGUILayout.BeginScrollView(_progressScroll, GUILayout.Height(240));
            foreach (var r in rows)
            {
                var label = (r.Checked ? "<color=#4ade80><b>✔</b></color>  " : "<color=#8a8a8a>·</color>  ") + r.Label;
                bool here = r.Pose == _s.Pose && _s.Selection.ItemId == r.ItemId;
                var style = new GUIStyle(EditorStyles.miniButtonLeft) { richText = true, alignment = TextAnchor.MiddleLeft, fontStyle = here ? FontStyle.Bold : FontStyle.Normal };
                if (GUILayout.Button(label, style))
                    StudioProgress.Jump(_s, r);
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
