using System.Collections.Generic;

namespace Musketeers.Sim
{
    public static partial class TurnResolver
    {
        // §6 Phase 2b: Grappin, Switcheroo and Bull Rush, one at a time in initiative order, each
        // using the positions the previous one left. Triple Charge is a shot and resolves in Phase 4.
        static void Phase2bAbilities(TickContext c)
        {
            c.Log.Phase = SimPhase.MovementAbilities;
            var units = c.S.Units;
            foreach (int i in c.Order)
            {
                var u = units[i];
                if (!c.Active[i] || !u.Alive || c.Act[i].Type != ActionType.Signature)
                    continue;
                Dir dir = c.Act[i].Dir;
                switch (u.Ability)
                {
                    case Ability.Grappin: Grapple(c, i, dir); break;
                    case Ability.Switcheroo: Switcheroo(c, i, c.Act[i].Unit); break;
                    case Ability.BullRush: BullRush(c, i, dir); break;
                }
            }
        }

        // What a hook or swap line meets first. Pits and empty floor let it through.
        enum LineHit : byte { Nothing, Obstacle, Unit, Keg }

        // Walks from origin (exclusive) up to range tiles; stops at the board edge.
        // `last` is the last tile reached (the hit tile when something was hit).
        static LineHit TraceLine(GameState s, Int2 origin, Dir dir, int range, out Int2 last, out Unit unit, out Keg keg)
        {
            unit = null;
            keg = null;
            Int2 d = dir.ToInt2();
            last = origin;
            for (int step = 1; step <= range; step++)
            {
                Int2 next = last + d;
                if (s.Board.IsVoid(next))
                {
                    // §4 Grappin: on a walled map the edge is a wall, like a pillar (the hook pulls you next to it).
                    if (s.Board.EdgeWalls)
                    {
                        last = next;
                        return LineHit.Obstacle;
                    }
                    break;
                }
                last = next;
                if (s.Board.Get(next).IsSolid())
                    return LineHit.Obstacle;
                keg = s.KegAt(next);
                if (keg != null)
                    return LineHit.Keg;
                unit = s.UnitAt(next);
                if (unit != null)
                    return LineHit.Unit;
            }
            return LineHit.Nothing;
        }

        // §4 Grappin: range 5. A unit is yanked to the tile next to you (flying over pits); a pillar or
        // crate pulls you to the tile before it; a keg is pulled to the tile next to you. Whatever lands
        // on a pit falls (the landing tile isn't "passed over").
        static void Grapple(TickContext c, int i, Dir dir)
        {
            var s = c.S;
            var u = s.Units[i];
            Int2 next = u.Pos + dir.ToInt2();
            var hit = TraceLine(s, u.Pos, dir, c.Cfg.GrappleRange, out Int2 touched, out Unit target, out Keg keg);
            switch (hit)
            {
                case LineHit.Unit:
                {
                    // Day 6 amendment: a funded Parry blocks the hook before movement or credit.
                    // Signature cost was already paid in Phase 0; reason 1 excludes this from shot awards.
                    if (target.Parrying)
                    {
                        c.Log.Add(SimEventKind.Parried, unit: target.Slot, other: i, to: target.Pos, dir: dir, reason: 1);
                        break;
                    }
                    // FIXES_4E §8.3: dragged tile by tile toward the grappler; the first pit or water tile on the
                    // way (the landing tile included) swallows the target, with the kill credited to the grappler.
                    Int2 from = target.Pos;
                    Int2 back = dir.Opposite().ToInt2();
                    Int2 end = from;
                    bool falls = false;
                    while (end != next)
                    {
                        end = end + back;
                        if (IsPit(s, end))
                        {
                            falls = true;
                            break;
                        }
                    }
                    target.Pos = end;
                    Credit(c, target.Slot, i);
                    c.Log.Add(SimEventKind.Grappled, unit: i, other: target.Slot, from: from, to: end, dir: dir, reason: (byte)GrappleResult.YankUnit);
                    if (falls)
                        Fall(c, target.Slot, end);
                    break;
                }
                case LineHit.Keg:
                {
                    Int2 from = keg.Pos;
                    keg.Pos = next;
                    c.Log.Add(SimEventKind.Grappled, unit: i, keg: keg.Id, from: from, to: next, dir: dir, reason: (byte)GrappleResult.PullKeg);
                    if (IsPit(s, next))
                        DropKegInPit(c, keg, next);
                    break;
                }
                case LineHit.Obstacle:
                {
                    Int2 from = u.Pos;
                    u.Pos = touched - dir.ToInt2();
                    c.Log.Add(SimEventKind.Grappled, unit: i, from: from, to: u.Pos, dir: dir, reason: (byte)GrappleResult.PullSelf);
                    if (IsPit(s, u.Pos))
                        Fall(c, i, u.Pos);
                    break;
                }
                default:
                    c.Log.Add(SimEventKind.Grappled, unit: i, from: u.Pos, to: touched, dir: dir, reason: (byte)GrappleResult.Miss);
                    break;
            }
        }

        // §4 Switcheroo (FIXES §3.2 rule 5): swap places with the chosen musketeer, wherever they are
        // when this resolves (abilities go one at a time in initiative order, so the target's position
        // already includes earlier abilities this phase). No range, no line of sight. It fails (Grit
        // stays spent) if Matic stands on a warned tile, the target is dead, or the target is Matic.
        static void Switcheroo(TickContext c, int i, int targetSlot)
        {
            var s = c.S;
            var u = s.Units[i];
            Int2 from = u.Pos;
            if (targetSlot < 0 || targetSlot >= s.Units.Length || targetSlot == i)
            {
                c.Log.Add(SimEventKind.Swapped, unit: i, from: from, to: from, amount: 0, reason: (byte)SwitcherooFail.BadTarget);
                return;
            }
            var target = s.Units[targetSlot];
            if (!target.Alive)
            {
                c.Log.Add(SimEventKind.Swapped, unit: i, other: targetSlot, from: from, to: from, amount: 0, reason: (byte)SwitcherooFail.TargetDead);
                return;
            }
            if (Crumble.IsWarned(s, from, c.Cfg))
            {
                c.Log.Add(SimEventKind.Swapped, unit: i, other: targetSlot, from: from, to: from, amount: 0, reason: (byte)SwitcherooFail.Warned);
                return;
            }
            u.Pos = target.Pos;
            target.Pos = from;
            Credit(c, targetSlot, i);
            c.Log.Add(SimEventKind.Swapped, unit: i, other: targetSlot, from: from, to: u.Pos, amount: 1);
        }

        // §4 Bull Rush: charge up to 3 tiles, stopping before any pillar, crate, keg, unit or pit. A unit
        // that stopped the charge is pushed BullRushPush (4) tiles (Phase 3 push rules); a keg that stopped it is kicked.
        // Running out of distance next to something doesn't count as being stopped by it.
        static void BullRush(TickContext c, int i, Dir dir)
        {
            var s = c.S;
            var u = s.Units[i];
            Int2 d = dir.ToInt2();
            Int2 start = u.Pos;
            Int2 p = start;
            Unit hitUnit = null;
            Keg hitKeg = null;
            for (int step = 0; step < c.Cfg.BullRushDistance; step++)
            {
                Int2 next = p + d;
                if (s.Board.IsVoid(next) || s.Board.Get(next).IsSolid() || IsPit(s, next))
                    break;
                hitKeg = s.KegAt(next);
                if (hitKeg != null)
                    break;
                hitUnit = s.UnitAt(next);
                if (hitUnit != null)
                    break;
                p = next;
            }
            u.Pos = p;
            c.Log.Add(SimEventKind.BullRushed, unit: i, other: hitUnit?.Slot ?? SimEvent.None,
                keg: hitKeg?.Id ?? SimEvent.None, from: start, to: p, dir: dir);

            if (hitUnit != null)
            {
                // FIXES_4E §8.4: 1 damage, then flung BullRushPush tiles with the normal push rules.
                Credit(c, hitUnit.Slot, i);
                Damage(c, hitUnit.Slot, c.Cfg.BullRushDamage, i, DamageKind.BullRush);
                SlideUnit(c, hitUnit.Slot, dir, c.Cfg.BullRushPush, i);
            }
            else if (hitKeg != null)
            {
                KickKeg(c, hitKeg, new List<Push> { new Push { Shover = i, Dir = dir, Strength = 1 } });
            }
        }

        static void DropKegInPit(TickContext c, Keg keg, Int2 pit)
        {
            c.Log.Add(SimEventKind.Fell, keg: keg.Id, to: pit);
            c.S.Kegs.Remove(keg);
        }
    }
}
