namespace Musketeers.Sim
{
    // Order matches GAME_DESIGN §6. TurnStart is for events emitted before planning.
    public enum SimPhase : byte { Validate, Self, Movement, MovementAbilities, Shoves, Shots, Kegs, Deaths, TurnStart }

    public enum FizzleReason : byte { None, NotEnoughGrit, NotLoaded, KegLimit, NotYetImplemented }

    public enum DeathCause : byte { Damage, Fell }

    public enum DamageKind : byte { Shot, ReflectedShot, Bonk, WallSlam, Squeeze, Explosion }

    public enum BlockReason : byte { Obstacle, Unit }
}
