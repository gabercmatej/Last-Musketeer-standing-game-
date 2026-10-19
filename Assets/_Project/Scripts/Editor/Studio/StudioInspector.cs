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
                FitFields();
            GUILayout.Space(6);
            var anchors = GUILayout.Toggle(_s.AnchorMode, "Anchors (edit the 7 attachment points)", EditorStyles.miniButton, GUILayout.Height(22));
            if (anchors != _s.AnchorMode)
            {
                _s.AnchorMode = anchors;
                _s.Selection = StudioSelection.None;
            }
            _s.ShowMasks = EditorGUILayout.ToggleLeft("Show masks", _s.ShowMasks);
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
                var label = (r.Checked ? "✔  " : "     ") + r.Label;
                bool here = r.Pose == _s.Pose && _s.Selection.ItemId == r.ItemId;
                var style = new GUIStyle(EditorStyles.miniButtonLeft) { alignment = TextAnchor.MiddleLeft, fontStyle = here ? FontStyle.Bold : FontStyle.Normal };
                if (GUILayout.Button(label, style))
                    StudioProgress.Jump(_s, r);
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
