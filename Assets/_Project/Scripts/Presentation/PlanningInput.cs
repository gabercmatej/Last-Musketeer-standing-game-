using System;
using Musketeers.Game;
using Musketeers.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Musketeers.Presentation
{
    // Keyboard + mouse planning (BUILD_PLAN Day 3). No browser-reserved keys:
    //   1 Move  2 Dash  3 Fire  4 Reload  5 Parry  6 Shove  7 Keg  8 Catch Breath  Q Signature
    //   WASD / arrows: direction (with nothing chosen, a direction key is a Move)
    //   mouse: click an in-line tile for a direction, a tile for the keg, an adjacent tile to move
    //   Switcheroo: click the musketeer to swap with
    //   Backspace undo, Space lock in / unlock, Esc or right-click cancel, Tab next unit (control-all)
    public sealed class PlanningInput : MonoBehaviour
    {
        [SerializeField] private CameraRig _rig;

        public PlanBuilder Builder { get; set; }
        public bool Active { get; set; }
        public Func<Vector2, bool> IsPointerOverUI { get; set; }
        public Func<Int2, int> UnitAtTile { get; set; }    // slot of a living unit on a tile, or -1

        public event Action LockToggled;
        public event Action<int> UnitClicked;
        public event Action CycleUnit;
        public event Action HoverChanged;

        public PlannedAction? Hover { get; private set; }
        public Int2? HoverTile { get; private set; }

        static readonly (Key key, ActionType type)[] ActionKeys =
        {
            (Key.Digit1, ActionType.Move), (Key.Digit2, ActionType.Dash), (Key.Digit3, ActionType.Fire),
            (Key.Digit4, ActionType.Reload), (Key.Digit5, ActionType.Parry), (Key.Digit6, ActionType.Shove),
            (Key.Digit7, ActionType.Keg), (Key.Digit8, ActionType.CatchBreath), (Key.Q, ActionType.Signature),
            (Key.Numpad1, ActionType.Move), (Key.Numpad2, ActionType.Dash), (Key.Numpad3, ActionType.Fire),
            (Key.Numpad4, ActionType.Reload), (Key.Numpad5, ActionType.Parry), (Key.Numpad6, ActionType.Shove),
            (Key.Numpad7, ActionType.Keg), (Key.Numpad8, ActionType.CatchBreath),
        };

        static readonly (Key key, Dir dir)[] DirKeys =
        {
            (Key.W, Dir.N), (Key.UpArrow, Dir.N), (Key.D, Dir.E), (Key.RightArrow, Dir.E),
            (Key.S, Dir.S), (Key.DownArrow, Dir.S), (Key.A, Dir.W), (Key.LeftArrow, Dir.W),
        };

        void Update()
        {
            if (!Active || Builder == null)
            {
                SetHover(null, null);
                return;
            }
            HandleKeyboard();
            HandleMouse();
        }

        // Called by the action bar buttons too, so mouse and keys share one path.
        public void ChooseAction(ActionType type)
        {
            if (Builder == null || Builder.Locked)
                return;
            if (Builder.HasPending && Builder.Pending == type)
            {
                Builder.CancelPending();
                return;
            }
            Builder.Choose(type);
        }

        void HandleKeyboard()
        {
            var kb = Keyboard.current;
            if (kb == null)
                return;
            if (kb.spaceKey.wasPressedThisFrame)
                LockToggled?.Invoke();
            if (kb.tabKey.wasPressedThisFrame)
                CycleUnit?.Invoke();
            if (Builder.Locked)
                return;
            if (kb.backspaceKey.wasPressedThisFrame)
                Builder.Undo();
            if (kb.escapeKey.wasPressedThisFrame)
                Builder.CancelPending();
            foreach (var (key, type) in ActionKeys)
            {
                if (kb[key].wasPressedThisFrame)
                    ChooseAction(type);
            }
            foreach (var (key, dir) in DirKeys)
            {
                if (!kb[key].wasPressedThisFrame)
                    continue;
                if (Builder.PendingTakesDirection)
                    Builder.CommitDirection(dir);
                else if (!Builder.HasPending && Builder.Choose(ActionType.Move))
                    Builder.CommitDirection(dir);
            }
        }

        void HandleMouse()
        {
            var mouse = Mouse.current;
            var cam = _rig != null ? _rig.Camera : null;
            if (mouse == null || cam == null)
                return;
            Vector2 screen = mouse.position.ReadValue();
            bool overUI = IsPointerOverUI != null && IsPointerOverUI(screen);
            Int2? tile = null;
            if (!overUI)
            {
                var world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
                var t = BoardMath.ToGrid(world);
                if (BoardMath.InBounds(t))
                    tile = t;
            }

            PlannedAction? hover = null;
            if (tile.HasValue && !Builder.Locked && Builder.HasPending)
            {
                var from = Builder.CursorState.Pos;
                if (Builder.PendingNeedsUnit)
                {
                    int over = PickUnit(tile.Value);
                    if (over >= 0 && Builder.IsSwitcherooTarget(over))
                        hover = PlannedAction.Switcheroo(over);
                }
                else if (Builder.PendingTakesDirection && TryDirection(from, tile.Value, out Dir d))
                    hover = Builder.Directional(Builder.Pending, d);
                else if (PlanBuilder.NeedsTarget(Builder.Pending) && Builder.TargetIssue(tile.Value) == PlanIssue.None)
                    hover = new PlannedAction(Builder.Pending, Dir.N, tile.Value);
            }
            SetHover(hover, tile);

            if (mouse.rightButton.wasPressedThisFrame && !overUI)
                Builder.CancelPending();
            if (!mouse.leftButton.wasPressedThisFrame || !tile.HasValue)
                return;

            int unit = UnitAtTile != null ? UnitAtTile(tile.Value) : -1;
            // Switcheroo: clicking a highlighted musketeer (or its tile) picks the target.
            if (!Builder.Locked && Builder.PendingNeedsUnit)
            {
                Builder.CommitUnit(PickUnit(tile.Value));
                return;
            }
            if (Builder.Locked || !Builder.HasPending)
            {
                if (unit >= 0 && unit != Builder.Slot)
                {
                    UnitClicked?.Invoke(unit);
                    return;
                }
                if (Builder.Locked)
                    return;
                // Clicking a neighbouring tile is a quick Move.
                var at = Builder.CursorState.Pos;
                if (at.Manhattan(tile.Value) == 1 && TryDirection(at, tile.Value, out Dir md) && Builder.Choose(ActionType.Move))
                    Builder.CommitDirection(md);
                return;
            }
            if (hover.HasValue)
            {
                if (PlanBuilder.NeedsTarget(Builder.Pending))
                    Builder.CommitTarget(hover.Value.Target);
                else if (hover.Value.Type == ActionType.Signature && Builder.PendingNeedsUnit)
                    Builder.CommitUnit(hover.Value.Unit);
                else
                    Builder.CommitDirection(hover.Value.Dir);
            }
        }

        // The musketeer under a tile; a head poking into the row above counts for the tile below it.
        int PickUnit(Int2 tile)
        {
            if (UnitAtTile == null)
                return -1;
            int u = UnitAtTile(tile);
            if (u < 0 && tile.Y > 0)
                u = UnitAtTile(new Int2(tile.X, tile.Y - 1));
            return u;
        }

        static bool TryDirection(Int2 from, Int2 to, out Dir d)
        {
            d = Dir.N;
            if (from == to || (from.X != to.X && from.Y != to.Y))
                return false;
            var delta = to - from;
            var unit = new Int2(Math.Sign(delta.X), Math.Sign(delta.Y));
            return DirExt.TryFromDelta(unit, out d);
        }

        void SetHover(PlannedAction? hover, Int2? tile)
        {
            bool same = Nullable.Equals(HoverTile, tile) && SameAction(Hover, hover);
            Hover = hover;
            HoverTile = tile;
            if (!same)
                HoverChanged?.Invoke();
        }

        static bool SameAction(PlannedAction? a, PlannedAction? b)
        {
            if (a.HasValue != b.HasValue)
                return false;
            if (!a.HasValue)
                return true;
            return a.Value.Type == b.Value.Type && a.Value.Dir == b.Value.Dir && a.Value.Target == b.Value.Target && a.Value.Unit == b.Value.Unit;
        }
    }
}
