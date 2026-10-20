namespace Musketeers.Sim
{
    // Order matches GAME_DESIGN §6. TurnStart is for events emitted before planning.
    public enum SimPhase : byte { Validate, Self, Movement, MovementAbilities, Shoves, Shots, Kegs, Deaths, TurnStart }

    // OutOfRange: a keg target further than KegThrowRange (Manhattan) or off the board.
    public enum FizzleReason : byte { None, NotEnoughGrit, NotLoaded, KegLimit, OutOfRange }

    public enum DeathCause : byte { Damage, Fell }

    public enum DamageKind : byte { Shot, ReflectedShot, Bonk, WallSlam, Squeeze, Explosion, TripleCharge }

    // What a Grappin hook touched (§4).
    public enum GrappleResult : byte { Miss, YankUnit, PullSelf, PullKeg }

    // ShotFired.Reason: a plain musket shot or one of a Triple Charge's shots.
    public enum ShotKind : byte { Musket, TripleCharge }

    // KegLanded.Reason.
    public enum KegLanding : byte { Floor, Impact }

    public enum BlockReason : byte { Obstacle, Unit }

    // Swapped.Reason when Switcheroo fails (Amount 0): Matic stands on a warned tile, the target is dead, or the
    // target slot is invalid (Matic himself or out of range).
    public enum SwitcherooFail : byte { None, Warned, TargetDead, BadTarget }
}
