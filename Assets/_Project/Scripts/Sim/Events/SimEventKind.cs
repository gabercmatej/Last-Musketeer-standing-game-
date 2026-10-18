namespace Musketeers.Sim
{
    // TECH_DESIGN §4 event kinds, plus Pushed (the unit a shove moved) and ParryRaised. Field use per kind:
    //   Moved / Dashed      Unit, From, To, Dir
    //   Leaped              Unit, From, To (the dash crossed a pit)
    //   MoveBlocked         Unit, From, To (the tile it wanted), Reason = BlockReason
    //   Bonk                one per participant: Unit, Other (partner, -1 if none), From, To (tile it ran into)
    //   ParryRaised         Unit (paid and is parrying this tick)
    //   Shoved              Unit = shover, Other = target unit or -1, Keg = kicked keg or -1, To = target tile, Amount = strength
    //   Pushed              Unit = pushed unit, Other = credited shover, From, To, Amount = tiles moved
    //   GuardBreak          Unit = parrier whose guard broke, Other = shover
    //   Squeezed            Unit, Other = credited shover
    //   WallSlam            Unit, Other = credited shover, From, To = the solid tile
    //   Fell                Unit (or Keg when a keg falls), To = pit tile
    //   ShotFired           Unit = shooter, From, To = where the shot stopped, Dir
    //   ShotHit             Unit = victim (or Keg), Other = shooter, To
    //   Parried             Unit = parrier, Other = shooter, To
    //   Reflected           Unit = parrier, From, To = where the reflection stopped, Dir
    //   Damaged             Unit, Other = attacker or -1, Amount, Reason = DamageKind
    //   Reloaded            Unit
    //   CaughtBreath        Unit, Amount = Grit actually gained
    //   Fizzled             Unit, Amount = (int)ActionType, Reason = FizzleReason
    //   KegKicked           Keg, Unit = credited kicker, From, To, Dir
    //   CrateDestroyed      Unit = shooter, To
    //   Died                Unit, Other = killer or -1 (self-kill), To, Reason = DeathCause
    //   RoundEnded          Unit = winner slot or -1 for a draw
    // Day 2: KegThrown, KegLanded, KegExploded, Grappled, Swapped, BullRushed, TileWarned, TileCrumbled.
    public enum SimEventKind : byte
    {
        Moved,
        MoveBlocked,
        Bonk,
        Dashed,
        Leaped,
        Shoved,
        Pushed,
        GuardBreak,
        Squeezed,
        WallSlam,
        Fell,
        ShotFired,
        ShotHit,
        ParryRaised,
        Parried,
        Reflected,
        Damaged,
        Reloaded,
        CaughtBreath,
        Fizzled,
        KegThrown,
        KegLanded,
        KegKicked,
        KegExploded,
        CrateDestroyed,
        Grappled,
        Swapped,
        BullRushed,
        TileWarned,
        TileCrumbled,
        Died,
        RoundEnded,
    }
}
