namespace Musketeers.Sim
{
    // Order matches GAME_DESIGN §6. TurnStart is for events emitted before planning.
    public enum SimPhase : byte { Validate, Self, Movement, MovementAbilities, Shoves, Shots, Kegs, Deaths, TurnStart }

    // OutOfRange: a keg target further than KegThrowRange (Manhattan) or off the board.
    public enum FizzleReason : byte { None, NotEnoughGrit, NotLoaded, KegLimit, OutOfRange }

    public enum DeathCause : byte { Damage, Fell }

    public enum DamageKind : byte { Shot, ReflectedShot, Bonk, WallSlam, Squeeze, Explosion, DoubleCharge }

    // What a Grappin hook touched (§4).
    public enum GrappleResult : byte { Miss, YankUnit, PullSelf, PullKeg }

    // ShotFired.Reason: a plain musket shot or a Double Charge.
    public enum ShotKind : byte { Musket, DoubleCharge }

    // KegLanded.Reason.
    public enum KegLanding : byte { Floor, Impact }

    public enum BlockReason : byte { Obstacle, Unit }
}
