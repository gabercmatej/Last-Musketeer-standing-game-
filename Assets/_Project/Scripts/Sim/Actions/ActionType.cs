namespace Musketeers.Sim
{
    // §5. Idle is 0 so a default PlannedAction (an unplanned tick) does nothing; Catch Breath only
    // happens when it's explicitly planned.
    public enum ActionType : byte { Idle, CatchBreath, Move, Dash, Fire, Reload, Parry, Shove, Keg, Signature }
}
