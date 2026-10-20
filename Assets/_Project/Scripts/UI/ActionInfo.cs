using Musketeers.Data;
using Musketeers.Game;
using Musketeers.Presentation;
using Musketeers.Sim;
using UnityEngine;

namespace Musketeers.UI
{
    // Labels, hotkeys, costs, descriptions and placeholder icons for the action bar (GAME_DESIGN §5).
    public static class ActionInfo
    {
        public static readonly ActionType[] Bar =
        {
            ActionType.Move, ActionType.Dash, ActionType.CatchBreath,
            ActionType.Fire, ActionType.Reload, ActionType.Shove, ActionType.Parry, ActionType.Keg,
            ActionType.Signature,
        };

        // FIXES §3.3: the bar is grouped (Movement | Combat | Special). Bar order == group order == hotkey order.
        public static readonly string[] GroupNames = { "MOVEMENT", "COMBAT", "SPECIAL" };

        public static int Group(ActionType t)
        {
            switch (t)
            {
                case ActionType.Move:
                case ActionType.Dash:
                case ActionType.CatchBreath: return 0;
                case ActionType.Signature: return 2;
                default: return 1;
            }
        }

        public static string Hotkey(ActionType t)
        {
            switch (t)
            {
                case ActionType.Move: return "1";
                case ActionType.Dash: return "2";
                case ActionType.CatchBreath: return "3";
                case ActionType.Fire: return "4";
                case ActionType.Reload: return "5";
                case ActionType.Shove: return "6";
                case ActionType.Parry: return "7";
                case ActionType.Keg: return "8";
                case ActionType.Signature: return "Q";
                default: return "";
            }
        }

        public static string Name(ActionType t, CharacterDef c)
        {
            switch (t)
            {
                case ActionType.CatchBreath: return "Catch Breath";
                case ActionType.Signature: return c != null ? c.AbilityName : "Signature";
                case ActionType.Idle: return "Idle";
                default: return t.ToString();
            }
        }

        public static string Cost(ActionType t, GameConfig cfg, Ability ability)
        {
            switch (t)
            {
                case ActionType.Dash: return $"{cfg.DashCost} grit";
                case ActionType.Fire: return "needs loaded";
                case ActionType.Parry: return $"{cfg.ParryCost} grit";
                case ActionType.Keg: return $"{cfg.KegCost} grit";
                case ActionType.CatchBreath: return "+1 grit";
                case ActionType.Signature:
                    return ability == Ability.TripleCharge ? $"{cfg.SignatureCost} grit + loaded" : $"{cfg.SignatureCost} grit";
                default: return "free";
            }
        }

        public static string Describe(ActionType t, GameConfig cfg, CharacterDef c)
        {
            switch (t)
            {
                case ActionType.Move: return "Move 1 tile.";
                case ActionType.Dash: return "Move 2 tiles in a line. Flies over crates, kegs, musketeers and 1-tile pits. Only a pillar blocks it; the landing tile must be free.";
                case ActionType.Fire: return $"Instant shot, range {cfg.FireRange}. 1 damage to the first thing hit. Unloads your musket.";
                case ActionType.Reload: return "Load your musket.";
                case ActionType.Parry: return "Block shots and blasts this tick. Shots bounce back at the shooter.";
                case ActionType.Shove: return "Push whatever is next to you 1 tile. A parrying target is pushed 2 (Guard Break). Kicks kegs.";
                case ActionType.Keg: return $"Lob a keg up to {cfg.KegThrowRange} tiles. It explodes in 3 ticks (3x3). One active keg each.";
                case ActionType.CatchBreath: return "+1 grit (usable from the next tick).";
                case ActionType.Signature: return c != null ? c.AbilityDescription : "Your signature ability.";
                default: return "";
            }
        }

        public static string Issue(PlanIssue i)
        {
            switch (i)
            {
                case PlanIssue.NoGrit: return "Not enough grit at this tick";
                case PlanIssue.Unloaded: return "Musket not loaded at this tick (Reload first)";
                case PlanIssue.AlreadyLoaded: return "Musket is already loaded";
                case PlanIssue.KegActive: return "You already have a keg out";
                case PlanIssue.OutOfRange: return "Out of range";
                case PlanIssue.Dead: return "You'd be dead by then";
                case PlanIssue.Warned: return "Can't swap from a cracked tile";
                case PlanIssue.PlanFull: return "Plan is full: click a slot to replace it, or Backspace";
                default: return "";
            }
        }

        // Matej's icon for an action (the signature icon depends on the ability), or the placeholder shape.
        public static Sprite Icon(ActionType t, Ability ability = Ability.Grappin)
        {
            var real = UiArt.Get(GameArt.ActionIcon(IconName(t, ability)));
            return real != null ? real : PlaceholderIcon(t);
        }

        public static bool HasRealIcon(ActionType t, Ability ability = Ability.Grappin) =>
            t != ActionType.Idle && UiArt.Get(GameArt.ActionIcon(IconName(t, ability))) != null;

        static string IconName(ActionType t, Ability ability)
        {
            switch (t)
            {
                case ActionType.Move: return "move";
                case ActionType.Dash: return "dash";
                case ActionType.Fire: return "fire";
                case ActionType.Reload: return "reload";
                case ActionType.Parry: return "parry";
                case ActionType.Shove: return "shove";
                case ActionType.Keg: return "keg";
                case ActionType.CatchBreath: return "catch_breath";
                case ActionType.Signature:
                    switch (ability)
                    {
                        case Ability.Grappin: return "grappin";
                        case Ability.Switcheroo: return "switcheroo";
                        case Ability.TripleCharge: return "double_charge";   // asset name predates the rename
                        default: return "bull_rush";
                    }
                default: return "";
            }
        }

        static Sprite PlaceholderIcon(ActionType t)
        {
            switch (t)
            {
                case ActionType.Move:
                case ActionType.Dash:
                case ActionType.Shove: return PlaceholderArt.Arrow;
                case ActionType.Fire: return PlaceholderArt.Musket;
                case ActionType.Reload: return PlaceholderArt.Musket;
                case ActionType.Parry: return PlaceholderArt.Ring;
                case ActionType.Keg: return PlaceholderArt.Keg;
                case ActionType.CatchBreath: return PlaceholderArt.Circle;
                case ActionType.Signature: return PlaceholderArt.Star;
                default: return null;
            }
        }

        public static Color IconTint(ActionType t, Ability ability = Ability.Grappin)
        {
            if (HasRealIcon(t, ability))
                return Color.white;
            switch (t)
            {
                case ActionType.Dash: return new Color(0.6f, 0.85f, 1f);
                case ActionType.Fire: return new Color(1f, 0.85f, 0.4f);
                case ActionType.Reload: return new Color(0.75f, 0.75f, 0.75f);
                case ActionType.Parry: return new Color(0.6f, 0.95f, 1f);
                case ActionType.Shove: return new Color(1f, 0.6f, 0.45f);
                case ActionType.Keg: return new Color(0.85f, 0.55f, 0.3f);
                case ActionType.CatchBreath: return new Color(1f, 0.85f, 0.3f);
                case ActionType.Signature: return new Color(1f, 0.75f, 0.2f);
                default: return Color.white;
            }
        }

        // Rotation for a directional icon: arrows and the musket point their way.
        public static float IconAngle(PlannedAction a, Ability ability = Ability.Grappin)
        {
            bool directional = PlanBuilder.NeedsDirection(a.Type) && !HasRealIcon(a.Type, ability);
            if (!directional)
                return 0f;
            // UI rotation is clockwise; arrows point north, the musket sprite points east.
            return -BoardMath.DirAngle(a.Dir) - (a.Type == ActionType.Fire ? 90f : 0f);
        }

        public static string Short(PlannedAction a, CharacterDef c, string[] playerNames = null)
        {
            if (a.Type == ActionType.Idle)
                return "";
            string n = Name(a.Type, c);
            if (a.Type == ActionType.Signature && c != null && c.Ability == Ability.Switcheroo)
            {
                // picks a musketeer, not a direction
                return playerNames != null && a.Unit < playerNames.Length ? $"Swap with {playerNames[a.Unit]}" : n;
            }
            if (PlanBuilder.NeedsDirection(a.Type))
                return $"{n} {a.Dir}";
            if (a.Type == ActionType.Keg)
                return $"Keg {a.Target}";
            return n;
        }
    }
}
