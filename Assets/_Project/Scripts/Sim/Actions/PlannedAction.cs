namespace Musketeers.Sim
{
    // One tick of a plan. Dir is used by directional actions, Target by Keg (and some signatures).
    public struct PlannedAction
    {
        public ActionType Type;
        public Dir Dir;
        public Int2 Target;

        public PlannedAction(ActionType type, Dir dir = Dir.N, Int2 target = default)
        {
            Type = type;
            Dir = dir;
            Target = target;
        }

        public static PlannedAction CatchBreath() => new PlannedAction(ActionType.CatchBreath);
        public static PlannedAction Move(Dir d) => new PlannedAction(ActionType.Move, d);
        public static PlannedAction Dash(Dir d) => new PlannedAction(ActionType.Dash, d);
        public static PlannedAction Fire(Dir d) => new PlannedAction(ActionType.Fire, d);
        public static PlannedAction Reload() => new PlannedAction(ActionType.Reload);
        public static PlannedAction Parry() => new PlannedAction(ActionType.Parry);
        public static PlannedAction Shove(Dir d) => new PlannedAction(ActionType.Shove, d);
        public static PlannedAction ThrowKeg(Int2 target) => new PlannedAction(ActionType.Keg, Dir.N, target);
        public static PlannedAction Signature(Dir d, Int2 target = default) => new PlannedAction(ActionType.Signature, d, target);

        public override string ToString() => $"{Type} {Dir} {Target}";
    }
}
