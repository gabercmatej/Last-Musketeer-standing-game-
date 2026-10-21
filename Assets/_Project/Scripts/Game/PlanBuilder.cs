using System;
using Musketeers.Sim;

namespace Musketeers.Game
{
    // Client-side plan editing (TECH_DESIGN §3): a cursor over the 3 slots, a pending action waiting
    // for its direction or keg target, undo, lock, and a projection that says what would fizzle.
    // Pick an action → (direction / target) → it goes into the cursor slot and the cursor moves on.
    public sealed class PlanBuilder
    {
        public event Action Changed;

        public int Slot { get; private set; }
        public TurnPlan Plan => _plan;
        public int Cursor { get; private set; }           // 0..2, or 3 when every slot is filled
        public bool Locked { get; private set; }
        public bool HasPending { get; private set; }
        public ActionType Pending { get; private set; }
        public PlanProjection Projection { get; private set; }
        public GameState State => _s;
        public GameConfig Config => _cfg;
        public Ability Ability => _s.Units[Slot].Ability;
        public bool IsFull => Cursor >= TurnPlan.Length;

        TurnPlan _plan;
        GameState _s;
        GameConfig _cfg;

        public void Begin(GameState s, int slot, GameConfig cfg) => Load(s, slot, cfg, new TurnPlan(slot), false);

        // Resume editing a plan (F2 control-all switches between units).
        public void Load(GameState s, int slot, GameConfig cfg, TurnPlan plan, bool locked)
        {
            _s = s;
            _cfg = cfg;
            Slot = slot;
            _plan = plan;
            _plan.Slot = (byte)slot;
            Locked = locked;
            HasPending = false;
            Cursor = FirstIdleFrom(0);
            Refresh();
        }

        public static bool NeedsDirection(ActionType t) =>
            t == ActionType.Move || t == ActionType.Dash || t == ActionType.Fire || t == ActionType.Shove || t == ActionType.Signature;

        public static bool NeedsTarget(ActionType t) => t == ActionType.Keg;

        // The tick the next action goes into (clamped so a full plan still has a "current" tick).
        public int EditTick => Math.Min(Cursor, TurnPlan.Length - 1);

        // What the cursor tick starts from.
        public TickProjection CursorState => Projection.States[EditTick];

        // Greys out the action bar: could this action type go into the cursor slot?
        public PlanIssue IssueFor(ActionType type)
        {
            if (IsFull)
                return PlanIssue.PlanFull;
            return PlanProjection.Check(_s.Board, _cfg, CursorState, new PlannedAction(type), Ability, false, Crumble.IsWarned(_s, CursorState.Pos, _cfg));
        }

        // Starts an action. Directional and keg actions wait for CommitDirection / CommitTarget.
        public bool Choose(ActionType type)
        {
            if (IssueFor(type) != PlanIssue.None)
                return false;
            if (TakesDirection(type) || NeedsUnit(type) || NeedsTarget(type))
            {
                HasPending = true;
                Pending = type;
                Changed?.Invoke();
                return true;
            }
            Place(new PlannedAction(type));
            return true;
        }

        // Switcheroo picks a musketeer, not a direction (FIXES §3.2 rule 5).
        public bool NeedsUnit(ActionType t) => t == ActionType.Signature && Ability == Ability.Switcheroo;

        // Directional for this musketeer: the static rule minus a Switcheroo signature.
        public bool TakesDirection(ActionType t) => NeedsDirection(t) && !NeedsUnit(t);

        public bool PendingTakesDirection => HasPending && TakesDirection(Pending);
        public bool PendingNeedsUnit => HasPending && NeedsUnit(Pending);

        public bool CommitDirection(Dir dir)
        {
            if (!PendingTakesDirection || IsDirectionBlocked(Pending, dir))
                return false;
            Place(new PlannedAction(Pending, dir));
            return true;
        }

        // FIXES_4E §8.1: Move and Dash never offer a tile off the map or an 'x' tile.
        public bool IsDirectionBlocked(ActionType type, Dir dir) =>
            PlanProjection.WalksIntoVoid(_s.Board, CursorState.Pos, type, dir);

        // The action for a direction pick (hover preview and commit share it).
        public PlannedAction Directional(ActionType type, Dir dir) => new PlannedAction(type, dir);

        // Slots that can be a Switcheroo target: every living musketeer except you.
        public bool IsSwitcherooTarget(int slot) => slot != Slot && slot >= 0 && slot < _s.Units.Length && _s.Units[slot].Alive;

        public bool CommitUnit(int slot)
        {
            if (!PendingNeedsUnit || !IsSwitcherooTarget(slot))
                return false;
            Place(PlannedAction.Switcheroo(slot));
            return true;
        }

        public PlanIssue TargetIssue(Int2 target) =>
            PlanProjection.KegTargetInRange(_s.Board, _cfg, CursorState.Pos, target) ? PlanIssue.None : PlanIssue.OutOfRange;

        public bool CommitTarget(Int2 target)
        {
            if (!HasPending || !NeedsTarget(Pending) || TargetIssue(target) != PlanIssue.None)
                return false;
            Place(new PlannedAction(Pending, Dir.N, target));
            return true;
        }

        public void CancelPending()
        {
            if (!HasPending)
                return;
            HasPending = false;
            Changed?.Invoke();
        }

        // Backspace: cancel the pending action, else clear the last planned slot and put the cursor there.
        public void Undo()
        {
            if (HasPending)
            {
                CancelPending();
                return;
            }
            for (int t = TurnPlan.Length - 1; t >= 0; t--)
            {
                if (_plan[t].Type == ActionType.Idle)
                    continue;
                _plan[t] = PlannedAction.Idle();
                Cursor = t;
                Edited();
                return;
            }
        }

        // Click a slot to (re)place its action.
        public void SelectSlot(int tick)
        {
            if (tick < 0 || tick >= TurnPlan.Length)
                return;
            Cursor = tick;
            HasPending = false;
            Changed?.Invoke();
        }

        public void SetLocked(bool locked)
        {
            if (Locked == locked)
                return;
            Locked = locked;
            HasPending = false;
            Changed?.Invoke();
        }

        // The plan as it would be if `a` went into the cursor slot (hover preview).
        public TurnPlan PreviewWith(PlannedAction a)
        {
            var p = _plan;
            if (!IsFull)
                p[Cursor] = a;
            return p;
        }

        void Place(PlannedAction a)
        {
            if (IsFull)
                return;
            int at = Cursor;
            _plan[at] = a;
            HasPending = false;
            Cursor = FirstIdleFrom(at + 1);
            if (IsFull)
                Cursor = FirstIdleFrom(0);   // an earlier slot may still be empty
            Edited();
        }

        // The next empty slot after `from`; a replaced slot in a full plan leaves the cursor at "full".
        int FirstIdleFrom(int from)
        {
            for (int t = from; t < TurnPlan.Length; t++)
            {
                if (_plan[t].Type == ActionType.Idle)
                    return t;
            }
            return TurnPlan.Length;
        }

        // Any edit unlocks: a locked plan must be the one you looked at.
        void Edited()
        {
            Locked = false;
            Refresh();
        }

        void Refresh()
        {
            Projection = PlanProjection.Project(_s, Slot, _plan, _cfg);
            Changed?.Invoke();
        }
    }
}
