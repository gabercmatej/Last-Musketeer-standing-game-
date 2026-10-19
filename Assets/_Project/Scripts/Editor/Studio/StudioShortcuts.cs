using System;
using Musketeers.Data;
using UnityEditor;
using UnityEngine;

namespace Musketeers.EditorTools.Studio
{
    // Keyboard shortcuts for the Wardrobe Studio. Called at the top of OnGUI, so a handled key never
    // reaches IMGUI (Tab would otherwise move keyboard focus). Cmd/Ctrl+Z is left to Unity's Undo.
    public static class StudioShortcuts
    {
        public const string Legend =
            "Arrows nudge (Shift x10)  ·  [ ] rotate  ·  - = scale  ·  Tab / Shift+Tab next item  ·  PgUp / PgDn pose  ·  Cmd/Ctrl+C / V copy / paste fit  ·  Del removes a pen point  ·  Enter / Esc ends mask editing";

        // Returns true when the event was used.
        public static bool Handle(StudioState s, StudioCanvas canvas, Event e)
        {
            if (s == null || canvas == null || EditorGUIUtility.editingTextField)
                return false;
            switch (e.type)
            {
                case EventType.ValidateCommand:
                    if (CommandOk(s, canvas, e.commandName))
                    {
                        e.Use();
                        return true;
                    }
                    return false;
                case EventType.ExecuteCommand:
                    if (!CommandOk(s, canvas, e.commandName))
                        return false;
                    RunCommand(s, canvas, e.commandName);
                    e.Use();
                    return true;
                case EventType.KeyDown:
                    if (EditorGUI.actionKey || e.alt)
                        return false;   // Cmd/Ctrl combos arrive as commands; Alt combos are the browser's / OS's
                    if (Key(s, canvas, e))
                    {
                        e.Use();
                        s.Touch();
                        return true;
                    }
                    return false;
            }
            return false;
        }

        static bool CommandOk(StudioState s, StudioCanvas canvas, string cmd)
        {
            switch (cmd)
            {
                case "Copy": return StudioClipboard.CanCopy(s);
                case "Paste": return StudioClipboard.CanPaste(s);
                case "Delete":
                case "SoftDelete": return PenHasSelection(s);
            }
            return false;
        }

        static void RunCommand(StudioState s, StudioCanvas canvas, string cmd)
        {
            switch (cmd)
            {
                case "Copy": StudioClipboard.Copy(s); break;
                case "Paste": StudioClipboard.PasteThisPose(s); break;
                default: canvas.DeletePenPoint(); break;
            }
            s.Touch();
        }

        static bool PenActive(StudioState s) => s.Mask.Active && s.Mask.Matches(s);
        static bool PenHasSelection(StudioState s) => PenActive(s) && s.Mask.Pen.Selected >= 0;

        static bool Key(StudioState s, StudioCanvas canvas, Event e)
        {
            float mult = e.shift ? 10f : 1f;
            switch (e.keyCode)
            {
                case KeyCode.LeftArrow: return Nudge(s, canvas, new Vector2(-1, 0) * mult);
                case KeyCode.RightArrow: return Nudge(s, canvas, new Vector2(1, 0) * mult);
                case KeyCode.UpArrow: return Nudge(s, canvas, new Vector2(0, 1) * mult);
                case KeyCode.DownArrow: return Nudge(s, canvas, new Vector2(0, -1) * mult);
                case KeyCode.LeftBracket: return Rotate(s, canvas, -1f * mult);
                case KeyCode.RightBracket: return Rotate(s, canvas, 1f * mult);
                case KeyCode.Minus:
                case KeyCode.KeypadMinus: return Scale(s, canvas, e.shift ? 0.9f : 0.99f);
                case KeyCode.Equals:
                case KeyCode.Plus:
                case KeyCode.KeypadPlus:
                case KeyCode.KeypadEquals: return Scale(s, canvas, e.shift ? 1.1f : 1.01f);
                case KeyCode.Tab: return CycleItem(s, e.shift ? -1 : 1);
                case KeyCode.PageUp: return CyclePose(s, -1);
                case KeyCode.PageDown: return CyclePose(s, 1);
                case KeyCode.Delete:
                case KeyCode.Backspace:
                    return PenActive(s) && (canvas.DeletePenPoint() || true);
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.Escape:
                    if (!s.Mask.Active)
                        return false;
                    s.Mask.Stop();
                    return true;
            }
            return false;
        }

        static bool Nudge(StudioState s, StudioCanvas canvas, Vector2 pxDelta)
        {
            var pose = pxDelta * canvas.PosePerPx;
            if (PenActive(s))
            {
                canvas.NudgePen(pose);
                return true;
            }
            if (s.AnchorMode)
                return false;
            return canvas.AdjustSelected((f, a, mirrored) => StudioEdit.Move(f, a, pose, mirrored)) || true;
        }

        static bool Rotate(StudioState s, StudioCanvas canvas, float deg)
        {
            if (PenActive(s) || s.AnchorMode)
                return PenActive(s);
            return canvas.AdjustSelected((f, a, mirrored) => StudioEdit.Rotate(f, deg, mirrored)) || true;
        }

        static bool Scale(StudioState s, StudioCanvas canvas, float factor)
        {
            if (PenActive(s) || s.AnchorMode)
                return PenActive(s);
            return canvas.AdjustSelected((f, a, mirrored) => StudioEdit.ScaleBy(f, factor)) || true;
        }

        // Tab: next item in the selected item's slot (wear it and select it); weapons toggle musket <-> rapier.
        static bool CycleItem(StudioState s, int dir)
        {
            var sel = s.Selection;
            if (sel.Kind == SelectionKind.Cosmetic)
            {
                var list = s.Catalog.BySlot(sel.Slot);
                if (list.Count == 0)
                    return true;
                int at = list.FindIndex(i => i.Id == sel.ItemId);
                var next = list[((at < 0 ? 0 : at) + dir + list.Count) % list.Count];
                s.Wear(sel.Slot, next.Id);
                s.Selection = StudioSelection.Cosmetic(sel.Slot, next.Id);
                return true;
            }
            if (sel.Kind == SelectionKind.Weapon)
            {
                bool musket = !sel.IsMusket;
                if (musket) s.MusketOn = true; else s.RapierOn = true;
                s.Selection = StudioSelection.Weapon(musket);
                return true;
            }
            return false;
        }

        static bool CyclePose(StudioState s, int dir)
        {
            int at = Array.IndexOf(FitEdit.StudioPoses, s.Pose);
            int n = FitEdit.StudioPoses.Length;
            s.Pose = FitEdit.StudioPoses[((at < 0 ? 0 : at) + dir + n) % n];
            return true;
        }
    }
}
