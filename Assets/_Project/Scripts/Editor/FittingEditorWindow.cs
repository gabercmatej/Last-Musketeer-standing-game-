using System;
using System.Collections.Generic;
using Musketeers.Data;
using UnityEditor;
using UnityEngine;

namespace Musketeers.EditorTools
{
    // Musketeers/Fitting Editor (FIXES_v0.2 §1.2), three tabs:
    //  Anchors  pick a musketeer and pose, drag the seven anchors (dot = position, knob = rotation), the
    //           boot-mask and hand-mask polygons; copy anchors from another pose; onion-skin another pose.
    //  Items    one item on all 4 musketeers × every pose at once; offset/rotation/scale for everyone, one
    //           musketeer, one pose, or one musketeer in one pose; EnabledInV1.
    //  Outfit   random full outfits on everyone, to catch combinations that clash.
    // Everything is drawn by RigCompositor with the runtime's CosmeticLayout, saved straight to the assets
    // (undoable), and survives re-imports.
    public sealed class FittingEditorWindow : EditorWindow
    {
        enum Tab { Anchors, Items, Outfit }
        enum Scope { Everyone, Musketeer, Pose, MusketeerInPose }
        enum Grab { None, Anchor, Rotation, BootBack, BootFront, Hand }

        const int Canvas = 640;
        const float CanvasZoom = 1.25f;      // 512 px pose → 640 px canvas
        const int Cell = 200;
        const float CellZoom = 0.39f;
        const float Pick = 9f;

        CharacterRoster _roster;
        CosmeticCatalog _catalog;
        Tab _tab;
        Vector2 _scroll;

        // Anchors tab
        int _char;
        PoseKind _pose = PoseKind.Idle;
        int _onion = -1;                     // index into Poses(), -1 = off
        bool _outfit = true, _weapons = true, _raised;
        bool _showAnchors = true, _showBoots = true, _showHand = true;
        int _copyFrom;
        Grab _grab;
        AnchorId _grabAnchor;
        int _grabIndex = -1;
        Grab _selPoly = Grab.BootBack;
        int _selVertex = -1;

        // Items tab
        CosmeticSlot _slot = CosmeticSlot.Hat;
        int _item;
        Scope _scope;
        int _scopeChar;
        PoseKind _scopePose = PoseKind.Idle;
        bool _itemOutfit;
        Vector2 _dragStart;
        bool _cellDrag;

        // Outfit tab
        readonly Loadout[] _outfits = new Loadout[4];
        bool _outfitDisabled;
        bool _outfitWeapons = true;

        readonly Dictionary<string, Texture2D> _tex = new Dictionary<string, Texture2D>();
        bool _dirty = true;
        double _lastRedraw;

        [MenuItem("Musketeers/Fitting Editor")]
        [MenuItem("Tools/Musketeers/Fitting Editor")]
        public static void Open()
        {
            var w = GetWindow<FittingEditorWindow>("Fitting Editor");
            w.minSize = new Vector2(1100, 760);
        }

        void OnEnable()
        {
            _roster = AssetDatabase.LoadAssetAtPath<CharacterRoster>("Assets/_Project/Data/CharacterRoster.asset");
            _catalog = AssetDatabase.LoadAssetAtPath<CosmeticCatalog>("Assets/_Project/Data/CosmeticCatalog.asset");
            Undo.undoRedoPerformed += MarkDirty;
            RigCompositor.ClearCache();
            Shuffle();
            _dirty = true;
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= MarkDirty;
            foreach (var t in _tex.Values)
            {
                if (t != null)
                    DestroyImmediate(t);
            }
            _tex.Clear();
        }

        void MarkDirty()
        {
            _dirty = true;
            Repaint();
        }

        CharacterDef Char(int i) => _roster != null ? _roster.Get(Mathf.Clamp(i, 0, _roster.Count - 1)) : null;

        // Poses that have art, in display order.
        List<PoseKind> Poses(CharacterDef c)
        {
            var list = new List<PoseKind>();
            foreach (var k in new[] { PoseKind.Idle, PoseKind.IdleB, PoseKind.Hurt, PoseKind.Victory, PoseKind.Lunge })
            {
                if (c != null && c.HasPose(k))
                    list.Add(k);
            }
            return list;
        }

        void OnGUI()
        {
            if (_roster == null || _catalog == null || _roster.Count == 0)
            {
                EditorGUILayout.HelpBox("Run Tools/Musketeers/Build Main Scene and Tools/Musketeers/Import Art first.", MessageType.Info);
                return;
            }
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var t = (Tab)GUILayout.Toolbar((int)_tab, new[] { "Anchors", "Items", "Outfit test" }, EditorStyles.toolbarButton, GUILayout.Width(330));
                if (t != _tab) { _tab = t; _dirty = true; }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Reload sprites", EditorStyles.toolbarButton)) { RigCompositor.ClearCache(); _dirty = true; }
                if (GUILayout.Button("Export sheets (Temp/Fitting)", EditorStyles.toolbarButton)) RigCompositor.ExportSheets();
                if (GUILayout.Button("Save fits as import defaults", EditorStyles.toolbarButton)) ArtImport.CosmeticTuning.SaveDefaults();
            }
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            switch (_tab)
            {
                case Tab.Anchors: AnchorsTab(); break;
                case Tab.Items: ItemsTab(); break;
                default: OutfitTab(); break;
            }
            EditorGUILayout.EndScrollView();
        }

        // ================= Anchors =================

        void AnchorsTab()
        {
            var c = Char(_char);
            var poses = Poses(c);
            if (!poses.Contains(_pose))
                _pose = poses.Count > 0 ? poses[0] : PoseKind.Idle;
            var fitAsset = c.Fit;
            if (fitAsset == null)
            {
                EditorGUILayout.HelpBox($"{c.DisplayName} has no CharacterFit asset yet: run Tools/Musketeers/Import Art.", MessageType.Warning);
                return;
            }
            var fit = fitAsset.GetOrAdd(_pose);

            EditorGUI.BeginChangeCheck();
            using (new EditorGUILayout.HorizontalScope())
            {
                _char = EditorGUILayout.Popup("Musketeer", _char, Names(), GUILayout.Width(300));
                int pi = EditorGUILayout.Popup("Pose", Mathf.Max(0, poses.IndexOf(_pose)), PoseNames(poses), GUILayout.Width(300));
                _pose = poses[Mathf.Clamp(pi, 0, poses.Count - 1)];
                var onionNames = new List<string> { "Off" };
                onionNames.AddRange(PoseNames(poses));
                _onion = EditorGUILayout.Popup("Onion skin", _onion + 1, onionNames.ToArray(), GUILayout.Width(300)) - 1;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                _outfit = GUILayout.Toggle(_outfit, "Default outfit", GUILayout.Width(110));
                _weapons = GUILayout.Toggle(_weapons, "Weapons", GUILayout.Width(80));
                _raised = GUILayout.Toggle(_raised, "Rapier raised (parry)", GUILayout.Width(150));
                GUILayout.Space(20);
                _showAnchors = GUILayout.Toggle(_showAnchors, "Anchor handles", GUILayout.Width(120));
                _showBoots = GUILayout.Toggle(_showBoots, "Boot masks", GUILayout.Width(100));
                _showHand = GUILayout.Toggle(_showHand, "Hand mask", GUILayout.Width(100));
            }
            if (EditorGUI.EndChangeCheck())
                _dirty = true;

            using (new EditorGUILayout.HorizontalScope())
            {
                var rect = GUILayoutUtility.GetRect(Canvas, Canvas, GUILayout.Width(Canvas), GUILayout.Height(Canvas));
                string key = "anchors";
                if (_dirty || !_tex.ContainsKey(key))
                {
                    var items = _outfit ? RigCompositor.Outfit(_catalog, c.DefaultLoadout) : new List<CosmeticDef>();
                    PoseKind? onion = _onion >= 0 && _onion < poses.Count ? poses[_onion] : (PoseKind?)null;
                    var opt = new RigCompositor.Options { Weapons = _weapons, Raised = _raised, Onion = onion, Background = new Color32(66, 66, 74, 255) };
                    Upload(key, RigCompositor.Compose(c, _pose, items, Canvas, CanvasZoom, opt));
                    _dirty = false;
                }
                GUI.DrawTexture(rect, _tex[key]);
                CanvasHandles(rect, c, fitAsset, fit);
                using (new EditorGUILayout.VerticalScope())
                    AnchorPanel(c, poses, fitAsset, fit);
            }
        }

        Vector2 ToGui(Rect r, Vector2 pose, int size, float zoom)
        {
            var o = RigCompositor.Origin(size);
            float k = RigCompositor.Ppu * zoom;
            float s = r.width / size;
            return new Vector2(r.x + (o.x + pose.x * k) * s, r.y + (size - (o.y + pose.y * k)) * s);
        }

        Vector2 ToPose(Rect r, Vector2 gui, int size, float zoom)
        {
            var o = RigCompositor.Origin(size);
            float k = RigCompositor.Ppu * zoom;
            float s = r.width / size;
            return new Vector2(((gui.x - r.x) / s - o.x) / k, (size - (gui.y - r.y) / s - o.y) / k);
        }

        Vector2 G(Rect r, Vector2 p) => ToGui(r, p, Canvas, CanvasZoom);

        static Vector2 RotKnob(Anchor a) => a.Position + CosmeticLayout.Rotate(new Vector2(0.16f, 0f), a.Rotation);

        void CanvasHandles(Rect r, CharacterDef c, CharacterFit asset, PoseFit fit)
        {
            var e = Event.current;
            // ---- draw ----
            Handles.BeginGUI();
            if (_showBoots)
            {
                Poly(r, fit.BootMaskBack, new Color(0.2f, 1f, 1f), _selPoly == Grab.BootBack);
                Poly(r, fit.BootMaskFront, new Color(1f, 0.3f, 1f), _selPoly == Grab.BootFront);
            }
            if (_showHand)
                Poly(r, fit.HandMask, new Color(1f, 0.85f, 0.2f), _selPoly == Grab.Hand);
            if (_showAnchors)
            {
                foreach (AnchorId id in Enum.GetValues(typeof(AnchorId)))
                {
                    var a = fit.Get(id);
                    Color col = RigCompositor.AnchorColor(id);
                    Handles.color = col;
                    var p = G(r, a.Position);
                    var k = G(r, RotKnob(a));
                    Handles.DrawAAPolyLine(2f, p, k);
                    Handles.DrawSolidDisc(k, Vector3.forward, 3.5f);
                    Handles.color = Color.black;
                    Handles.DrawSolidDisc(p, Vector3.forward, 7f);
                    Handles.color = col;
                    Handles.DrawSolidDisc(p, Vector3.forward, 5f);
                    GUI.Label(new Rect(p.x + 8, p.y - 9, 90, 18), id.ToString(), EditorStyles.whiteMiniLabel);
                }
            }
            Handles.EndGUI();

            // ---- interact ----
            if (!r.Contains(e.mousePosition) && _grab == Grab.None)
                return;
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                Pickup(r, fit, e.mousePosition);
                if (_grab != Grab.None)
                {
                    Undo.RecordObject(asset, "Fit " + c.DisplayName);
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseDrag && _grab != Grab.None)
            {
                var pose = ToPose(r, e.mousePosition, Canvas, CanvasZoom);
                switch (_grab)
                {
                    case Grab.Anchor:
                    {
                        var a = fit.Get(_grabAnchor);
                        a.Position = Round(pose);
                        fit.Set(_grabAnchor, a);
                        break;
                    }
                    case Grab.Rotation:
                    {
                        var a = fit.Get(_grabAnchor);
                        var d = pose - a.Position;
                        a.Rotation = Mathf.Round(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                        fit.Set(_grabAnchor, a);
                        break;
                    }
                    default:
                        PolyOf(fit, _grab)[_grabIndex] = Round(pose);
                        break;
                }
                EditorUtility.SetDirty(asset);
                if (EditorApplication.timeSinceStartup - _lastRedraw > 0.08)
                {
                    _lastRedraw = EditorApplication.timeSinceStartup;
                    _dirty = true;
                }
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.MouseUp && _grab != Grab.None)
            {
                _grab = Grab.None;
                Save(asset);
                e.Use();
            }
        }

        void Pickup(Rect r, PoseFit fit, Vector2 mouse)
        {
            _grab = Grab.None;
            float best = Pick;
            void Try(Grab g, Vector2 posePoint, AnchorId id, int index)
            {
                float d = Vector2.Distance(G(r, posePoint), mouse);
                if (d < best)
                {
                    best = d;
                    _grab = g;
                    _grabAnchor = id;
                    _grabIndex = index;
                }
            }
            if (_showAnchors)
            {
                foreach (AnchorId id in Enum.GetValues(typeof(AnchorId)))
                {
                    var a = fit.Get(id);
                    Try(Grab.Anchor, a.Position, id, -1);
                    Try(Grab.Rotation, RotKnob(a), id, -1);
                }
            }
            foreach (var g in new[] { Grab.BootBack, Grab.BootFront, Grab.Hand })
            {
                if ((g == Grab.Hand ? !_showHand : !_showBoots))
                    continue;
                var poly = PolyOf(fit, g);
                for (int i = 0; i < poly.Length; i++)
                    Try(g, poly[i], AnchorId.HeadTop, i);
            }
            if (_grab == Grab.BootBack || _grab == Grab.BootFront || _grab == Grab.Hand)
            {
                _selPoly = _grab;
                _selVertex = _grabIndex;
            }
        }

        static Vector2 Round(Vector2 v) => new Vector2(Mathf.Round(v.x * 1000f) / 1000f, Mathf.Round(v.y * 1000f) / 1000f);

        static Vector2[] PolyOf(PoseFit fit, Grab g) => g == Grab.BootBack ? fit.BootMaskBack : g == Grab.BootFront ? fit.BootMaskFront : fit.HandMask;

        static void SetPoly(PoseFit fit, Grab g, Vector2[] poly)
        {
            if (g == Grab.BootBack) fit.BootMaskBack = poly;
            else if (g == Grab.BootFront) fit.BootMaskFront = poly;
            else fit.HandMask = poly;
        }

        void Poly(Rect r, Vector2[] poly, Color col, bool selected)
        {
            if (poly == null || poly.Length == 0)
                return;
            var pts = new Vector3[poly.Length + 1];
            for (int i = 0; i <= poly.Length; i++)
                pts[i] = G(r, poly[i % poly.Length]);
            Handles.color = col;
            Handles.DrawAAPolyLine(selected ? 3f : 1.5f, pts);
            for (int i = 0; i < poly.Length; i++)
            {
                bool sel = selected && i == _selVertex;
                var p = (Vector2)pts[i];
                EditorGUI.DrawRect(new Rect(p.x - (sel ? 4 : 3), p.y - (sel ? 4 : 3), sel ? 8 : 6, sel ? 8 : 6), sel ? Color.white : col);
            }
        }

        void AnchorPanel(CharacterDef c, List<PoseKind> poses, CharacterFit asset, PoseFit fit)
        {
            EditorGUILayout.LabelField($"{c.DisplayName}: {_pose}", EditorStyles.boldLabel);
            var ids = (AnchorId[])Enum.GetValues(typeof(AnchorId));
            var edited = new Anchor[ids.Length];
            EditorGUI.BeginChangeCheck();
            for (int n = 0; n < ids.Length; n++)
            {
                var a = fit.Get(ids[n]);
                using (new EditorGUILayout.HorizontalScope())
                {
                    var col = (Color)RigCompositor.AnchorColor(ids[n]);
                    EditorGUI.DrawRect(GUILayoutUtility.GetRect(10, 16, GUILayout.Width(10)), col);
                    GUILayout.Label(ids[n].ToString(), GUILayout.Width(70));
                    a.Position = EditorGUILayout.Vector2Field("", a.Position, GUILayout.Width(150));
                    GUILayout.Label("rot", GUILayout.Width(22));
                    a.Rotation = EditorGUILayout.FloatField(a.Rotation, GUILayout.Width(44));
                    GUILayout.Label("scale", GUILayout.Width(34));
                    a.Scale = EditorGUILayout.FloatField(a.S, GUILayout.Width(44));
                }
                edited[n] = a;
            }
            EditorGUILayout.HelpBox("HeadTop's scale is the head size: hats and face items scale with it, so an item fitted on one musketeer fits the others. " +
                                    "HandBack holds the musket (behind the body), HandFront the rapier.", MessageType.None);
            bool noMusket = EditorGUILayout.ToggleLeft("No musket in this pose", fit.NoMusket);
            bool noRapier = EditorGUILayout.ToggleLeft("No rapier in this pose", fit.NoRapier);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(asset, "Fit " + c.DisplayName);
                for (int n = 0; n < ids.Length; n++)
                    fit.Set(ids[n], edited[n]);
                fit.NoMusket = noMusket;
                fit.NoRapier = noRapier;
                Save(asset);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Masks", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Toggle(_selPoly == Grab.BootBack, "Back boot", "Button")) _selPoly = Grab.BootBack;
                if (GUILayout.Toggle(_selPoly == Grab.BootFront, "Front boot", "Button")) _selPoly = Grab.BootFront;
                if (GUILayout.Toggle(_selPoly == Grab.Hand, "Front hand", "Button")) _selPoly = Grab.Hand;
            }
            var poly = PolyOf(fit, _selPoly);
            EditorGUILayout.LabelField($"{poly.Length} points, selected #{_selVertex}", EditorStyles.miniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add point after selected"))
                {
                    Undo.RecordObject(asset, "Add mask point");
                    var list = new List<Vector2>(poly);
                    if (list.Count == 0)
                        list.AddRange(new[] { new Vector2(-0.1f, 0f), new Vector2(0.1f, 0f), new Vector2(0f, 0.15f) });
                    else
                    {
                        int i = Mathf.Clamp(_selVertex, 0, list.Count - 1);
                        list.Insert(i + 1, Round((list[i] + list[(i + 1) % list.Count]) * 0.5f));
                        _selVertex = i + 1;
                    }
                    SetPoly(fit, _selPoly, list.ToArray());
                    Save(asset);
                }
                if (GUILayout.Button("Delete selected") && _selVertex >= 0 && _selVertex < poly.Length)
                {
                    Undo.RecordObject(asset, "Delete mask point");
                    var list = new List<Vector2>(poly);
                    list.RemoveAt(_selVertex);
                    _selVertex = Mathf.Min(_selVertex, list.Count - 1);
                    SetPoly(fit, _selPoly, list.ToArray());
                    Save(asset);
                }
                if (GUILayout.Button("Clear"))
                {
                    Undo.RecordObject(asset, "Clear mask");
                    SetPoly(fit, _selPoly, new Vector2[0]);
                    Save(asset);
                }
            }
            EditorGUILayout.HelpBox("Boot masks hide the body's own boots when footwear is equipped. The hand mask is the part of the body redrawn over the rapier's grip. " +
                                    "Drag the squares; click one to select it.", MessageType.None);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Copy", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                var others = poses.FindAll(p => p != _pose);
                if (others.Count > 0)
                {
                    _copyFrom = Mathf.Clamp(_copyFrom, 0, others.Count - 1);
                    _copyFrom = EditorGUILayout.Popup(_copyFrom, PoseNames(others), GUILayout.Width(120));
                    if (GUILayout.Button($"Copy anchors from {others[_copyFrom]}"))
                    {
                        Undo.RecordObject(asset, "Copy anchors");
                        fit.CopyFrom(asset.GetOrAdd(others[_copyFrom]));
                        Save(asset);
                    }
                }
            }
        }

        // ================= Items =================

        void ItemsTab()
        {
            EditorGUI.BeginChangeCheck();
            List<CosmeticDef> items;
            using (new EditorGUILayout.HorizontalScope())
            {
                _slot = (CosmeticSlot)EditorGUILayout.EnumPopup("Slot", _slot, GUILayout.Width(260));
                items = _catalog.BySlot(_slot);
                var names = new string[items.Count];
                for (int i = 0; i < items.Count; i++)
                    names[i] = items[i].DisplayName + (items[i].EnabledInV1 ? "" : "  (off in v1)") + (items[i].Tuned ? "  ✓" : "");
                _item = Mathf.Clamp(EditorGUILayout.Popup("Item", _item, names, GUILayout.Width(380)), 0, Mathf.Max(0, items.Count - 1));
                _itemOutfit = GUILayout.Toggle(_itemOutfit, "Default outfit too", GUILayout.Width(130));
            }
            if (EditorGUI.EndChangeCheck())
                _dirty = true;
            if (items.Count == 0)
                return;
            var item = items[_item];

            ItemControls(item);
            EditorGUILayout.Space();

            var grid = new List<CharacterDef>();
            for (int i = 0; i < _roster.Count; i++)
                grid.Add(_roster.Get(i));
            var poses = Poses(grid[0]);
            bool redraw = _dirty;
            _dirty = false;
            // Header row: pose names.
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(70);
                foreach (var p in poses)
                    GUILayout.Label(p.ToString(), EditorStyles.centeredGreyMiniLabel, GUILayout.Width(Cell));
            }
            for (int ci = 0; ci < grid.Count; ci++)
            {
                var c = grid[ci];
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(c.DisplayName, EditorStyles.boldLabel, GUILayout.Width(66));
                    foreach (var p in poses)
                    {
                        var r = GUILayoutUtility.GetRect(Cell, Cell, GUILayout.Width(Cell), GUILayout.Height(Cell));
                        string key = $"item/{ci}/{p}";
                        if (redraw || !_tex.ContainsKey(key))
                        {
                            var list = new List<CosmeticDef>();
                            if (_itemOutfit)
                            {
                                foreach (var it in RigCompositor.Outfit(_catalog, c.DefaultLoadout))
                                    if (it.Slot != _slot) list.Add(it);
                            }
                            list.Add(item);
                            bool picked = InScope(c, p);
                            var bg = picked ? new Color32(78, 86, 104, 255) : new Color32(62, 62, 70, 255);
                            Upload(key, RigCompositor.Compose(c, p, list, Cell, CellZoom, new RigCompositor.Options { Weapons = false, Background = bg }));
                        }
                        GUI.DrawTexture(r, _tex[key]);
                        GUI.Label(new Rect(r.x + 4, r.y + 2, r.width, 16), WhichFit(item, c.Id, p), EditorStyles.whiteMiniLabel);
                        CellInput(r, c, ci, p, item);
                    }
                }
            }
            EditorGUILayout.HelpBox("Click a cell to pick the musketeer/pose the scope refers to (highlighted cells are affected). Drag inside a cell to move the item. " +
                                    "The label in each cell says which fit it uses.", MessageType.None);
        }

        bool InScope(CharacterDef c, PoseKind p)
        {
            bool charOk = _scope == Scope.Everyone || _scope == Scope.Pose || Char(_scopeChar) == c;
            bool poseOk = _scope == Scope.Everyone || _scope == Scope.Musketeer || _scopePose == p;
            return charOk && poseOk;
        }

        static string WhichFit(CosmeticDef item, string character, PoseKind pose)
        {
            if (item.FindOverride(character, false, pose) != null) return "musketeer+pose";
            if (item.FindOverride("", false, pose) != null) return "pose";
            if (item.FindOverride(character, true, pose) != null) return "musketeer";
            return "everyone";
        }

        string ScopeCharacter => _scope == Scope.Musketeer || _scope == Scope.MusketeerInPose ? Char(_scopeChar).Id : "";
        bool ScopeAnyPose => _scope == Scope.Everyone || _scope == Scope.Musketeer;

        void ItemControls(CosmeticDef item)
        {
            EditorGUILayout.LabelField($"{item.DisplayName} ({item.Id})", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            bool enabled = EditorGUILayout.ToggleLeft("Enabled in v1 (shown in the wardrobe)", item.EnabledInV1);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(item, "EnabledInV1");
                item.EnabledInV1 = enabled;
                item.Tuned = true;
                Save(item);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                _scope = (Scope)EditorGUILayout.EnumPopup("Apply to", _scope, GUILayout.Width(300));
                if (_scope == Scope.Musketeer || _scope == Scope.MusketeerInPose)
                    _scopeChar = EditorGUILayout.Popup(_scopeChar, Names(), GUILayout.Width(100));
                if (_scope == Scope.Pose || _scope == Scope.MusketeerInPose)
                    _scopePose = (PoseKind)EditorGUILayout.EnumPopup(_scopePose, GUILayout.Width(100));
            }
            var o = _scope == Scope.Everyone ? null : item.FindOverride(ScopeCharacter, ScopeAnyPose, _scopePose);
            var shownChar = _scope == Scope.Everyone || _scope == Scope.Pose ? Char(0).Id : ScopeCharacter;
            var fit = _scope == Scope.Everyone ? item.Global : o != null ? o.Fit : item.FitFor(shownChar, ScopeAnyPose ? PoseKind.Idle : _scopePose);
            if (_scope != Scope.Everyone)
                EditorGUILayout.LabelField(o != null ? "Editing this override." : "No override yet: editing creates one, starting from what's shown.", EditorStyles.miniLabel);
            EditorGUI.BeginChangeCheck();
            fit.Offset.x = EditorGUILayout.Slider("Offset x", fit.Offset.x, -0.6f, 0.6f);
            fit.Offset.y = EditorGUILayout.Slider("Offset y", fit.Offset.y, -0.6f, 0.6f);
            fit.Rotation = EditorGUILayout.Slider("Rotation", fit.Rotation, -90f, 90f);
            fit.Scale = EditorGUILayout.Slider("Scale", fit.S, 0.1f, 3f);
            if (EditorGUI.EndChangeCheck())
                WriteFit(item, fit);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = o != null;
                if (GUILayout.Button("Remove this override", GUILayout.Width(180)))
                {
                    Undo.RecordObject(item, "Remove override");
                    item.Overrides.Remove(o);
                    Save(item);
                }
                GUI.enabled = true;
                GUILayout.Label($"{item.Overrides.Count} override(s)", EditorStyles.miniLabel);
            }
        }

        void WriteFit(CosmeticDef item, ItemFit fit)
        {
            Undo.RecordObject(item, "Fit " + item.DisplayName);
            if (_scope == Scope.Everyone)
                item.Global = fit;
            else
            {
                var o = item.FindOverride(ScopeCharacter, ScopeAnyPose, _scopePose);
                if (o == null)
                {
                    o = new FitOverride { Character = ScopeCharacter, AnyPose = ScopeAnyPose, Pose = _scopePose };
                    item.Overrides.Add(o);
                }
                o.Fit = fit;
            }
            item.Tuned = true;
            Save(item);
        }

        void CellInput(Rect r, CharacterDef c, int ci, PoseKind p, CosmeticDef item)
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
            {
                _scopeChar = ci;
                _scopePose = p;
                _dragStart = e.mousePosition;
                _cellDrag = true;
                _dirty = true;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && _cellDrag && r.Contains(e.mousePosition) && _scopeChar == ci && _scopePose == p)
            {
                // Screen delta → pose units → the anchor's frame (the offset turns and scales with it).
                var d = e.mousePosition - _dragStart;
                _dragStart = e.mousePosition;
                float k = RigCompositor.Ppu * CellZoom * (r.width / Cell);
                var world = new Vector2(d.x / k, -d.y / k);
                var a = c.FitFor(p).Get(CosmeticLayout.AnchorFor(item.Slot, false));
                var local = CosmeticLayout.Rotate(world, -a.Rotation) / a.S;
                var o = _scope == Scope.Everyone ? null : item.FindOverride(ScopeCharacter, ScopeAnyPose, _scopePose);
                var fit = _scope == Scope.Everyone ? item.Global : o != null ? o.Fit : item.FitFor(c.Id, p);
                fit.Offset = Round(fit.Offset + local);
                WriteFit(item, fit);
                e.Use();
            }
            else if (e.type == EventType.MouseUp)
                _cellDrag = false;
        }

        // ================= Outfit test =================

        void Shuffle()
        {
            if (_catalog == null)
                return;
            for (int i = 0; i < _outfits.Length; i++)
                _outfits[i] = _catalog.RandomLoadout(!_outfitDisabled);
            _dirty = true;
        }

        void OutfitTab()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Shuffle outfits", GUILayout.Width(140)))
                    Shuffle();
                EditorGUI.BeginChangeCheck();
                _outfitDisabled = GUILayout.Toggle(_outfitDisabled, "Include items that are off in v1", GUILayout.Width(220));
                _outfitWeapons = GUILayout.Toggle(_outfitWeapons, "Weapons", GUILayout.Width(80));
                if (EditorGUI.EndChangeCheck())
                    _dirty = true;
            }
            bool redraw = _dirty;
            _dirty = false;
            for (int ci = 0; ci < _roster.Count && ci < _outfits.Length; ci++)
            {
                var c = _roster.Get(ci);
                var items = RigCompositor.Outfit(_catalog, _outfits[ci]);
                var names = new List<string>();
                foreach (var it in items)
                    names.Add(it.DisplayName);
                EditorGUILayout.LabelField($"{c.DisplayName}: {string.Join(", ", names)}", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    foreach (var p in Poses(c))
                    {
                        var r = GUILayoutUtility.GetRect(Cell, Cell, GUILayout.Width(Cell), GUILayout.Height(Cell));
                        string key = $"outfit/{ci}/{p}";
                        if (redraw || !_tex.ContainsKey(key))
                            Upload(key, RigCompositor.Compose(c, p, items, Cell, CellZoom, new RigCompositor.Options { Weapons = _outfitWeapons, Background = new Color32(62, 62, 70, 255) }));
                        GUI.DrawTexture(r, _tex[key]);
                    }
                }
            }
        }

        // ================= helpers =================

        string[] Names()
        {
            var n = new string[_roster.Count];
            for (int i = 0; i < n.Length; i++)
                n[i] = _roster.Get(i).DisplayName;
            return n;
        }

        static string[] PoseNames(List<PoseKind> poses) => poses.ConvertAll(p => p.ToString()).ToArray();

        void Upload(string key, ArtImport.Img img)
        {
            if (!_tex.TryGetValue(key, out var t) || t == null || t.width != img.W || t.height != img.H)
            {
                if (t != null)
                    DestroyImmediate(t);
                t = new Texture2D(img.W, img.H, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, filterMode = FilterMode.Bilinear };
                _tex[key] = t;
            }
            t.SetPixels32(img.Px);
            t.Apply();
        }

        void Save(UnityEngine.Object o)
        {
            EditorUtility.SetDirty(o);
            AssetDatabase.SaveAssetIfDirty(o);
            _dirty = true;
            Repaint();
        }
    }
}
