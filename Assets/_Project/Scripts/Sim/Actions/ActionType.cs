namespace Musketeers.Sim
{
    // §5. CatchBreath is 0 so a default PlannedAction (an unplanned tick) is Catch Breath.
    public enum ActionType : byte { CatchBreath, Move, Dash, Fire, Reload, Parry, Shove, Keg, Signature }
}
