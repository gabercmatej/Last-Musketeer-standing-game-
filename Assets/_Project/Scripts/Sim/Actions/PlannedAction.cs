namespace Musketeers.Sim
{
    // One tick of a plan. Dir is used by directional actions, Target by Keg, Unit (a slot) by Switcheroo.
    public struct PlannedAction
    {
        public ActionType Type;
        public Dir Dir;
        public Int2 Target;
        public byte Unit;      // Switcheroo: the slot of the musketeer to swap with

        public PlannedAction(ActionType type, Dir dir = Dir.N, Int2 target = default, int unit = 0)
        {
            Type = type;
            Dir = dir;
            Target = target;
            Unit = (byte)unit;
        }

        public static PlannedAction Idle() => new PlannedAction(ActionType.Idle);
        public static PlannedAction CatchBreath() => new PlannedAction(ActionType.CatchBreath);
        public static PlannedAction Move(Dir d) => new PlannedAction(ActionType.Move, d);
        public static PlannedAction Dash(Dir d) => new PlannedAction(ActionType.Dash, d);
        public static PlannedAction Fire(Dir d) => new PlannedAction(ActionType.Fire, d);
        public static PlannedAction Reload() => new PlannedAction(ActionType.Reload);
        public static PlannedAction Parry() => new PlannedAction(ActionType.Parry);
        public static PlannedAction Shove(Dir d) => new PlannedAction(ActionType.Shove, d);
        public static PlannedAction ThrowKeg(Int2 target) => new PlannedAction(ActionType.Keg, Dir.N, target);
        public static PlannedAction Signature(Dir d, Int2 target = default) => new PlannedAction(ActionType.Signature, d, target);
        public static PlannedAction Switcheroo(int slot) => new PlannedAction(ActionType.Signature, Dir.N, default, slot);

        public override string ToString() => $"{Type} {Dir} {Target}{(Type == ActionType.Signature && Unit != 0 ? " unit " + Unit : "")}";
    }
}
