namespace Musketeers.Sim
{
    // TECH_DESIGN §4 event kinds, plus Pushed (the unit a shove moved) and ParryRaised. Field use per kind:
    //   Moved / Dashed      Unit, From, To, Dir
    //   Leaped              Unit, From, To (the dash crossed a pit)
    //   MoveBlocked         Unit, From, To (the tile it wanted), Reason = BlockReason
    //   Bonk                one per participant: Unit, Other (partner, -1 if none), From, To (tile it ran into).
    //                       Phase 2 also bonks movers whose paths cross (shared tile or opposite traversal).
    //   ParryRaised         Unit (paid and is parrying this tick)
    //   Shoved              Unit = shover, Other = target unit or -1, Keg = kicked keg or -1, To = target tile, Amount = strength
    //   Pushed              Unit = pushed unit, Other = credited shover, From, To, Amount = tiles moved
    //   GuardBreak          Unit = parrier whose guard broke, Other = shover
    //   Squeezed            Unit, Other = credited shover
    //   WallSlam            Unit, Other = credited shover, From, To = the solid tile
    //   Fell                Unit (or Keg when a keg falls), To = pit tile
    //   ShotFired           Unit = shooter, From, To = where the shot stopped, Dir, Reason = ShotKind
    //   ShotHit             Unit = victim (or Keg), Other = shooter, To
    //   Parried             Unit = parrier, Other = shooter (or keg credit), Keg = exploding keg or -1, To
    //   Reflected           Unit = parrier, From, To = where the reflection stopped, Dir
    //   Damaged             Unit, Other = attacker or -1, Amount, Reason = DamageKind
    //   Reloaded            Unit
    //   CaughtBreath        Unit, Amount = Grit actually gained
    //   Fizzled             Unit, Amount = (int)ActionType, Reason = FizzleReason
    //   KegKicked           Keg, Unit = credited kicker, From, To, Dir
    //   KegThrown           Unit = thrower, Keg, From = thrower tile, To = target tile
    //   KegLanded           Keg, Unit = thrower, To, Reason = KegLanding (Impact = explodes now)
    //   KegExploded         Keg, Unit = credited player or -1, Other = owner, To = center, Amount = 1 if chained
    //   Grappled            Unit = grappler, Other = yanked unit, Keg = pulled keg, From/To = start/end of
    //                       whatever moved (the hook's last tile on a miss), Dir, Reason = GrappleResult
    //   Swapped             Unit = user, Other = unit or -1, Keg = keg or -1, From = user start, To = user end, Dir
    //   BullRushed          Unit = rusher, Other = unit it hit or -1, Keg = keg it hit or -1, From, To, Dir
    //   TileWarned          To = tile, Amount = ring (TurnStart)
    //   TileCrumbled        To = tile, Amount = ring (TurnStart)
    //   CrateDestroyed      Unit = shooter or keg credit, To
    //   Died                Unit, Other = killer or -1 (self-kill), To, Reason = DeathCause
    //   RoundEnded          Unit = winner slot or -1 for a draw
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
