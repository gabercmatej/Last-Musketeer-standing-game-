using System.Collections.Generic;

namespace Musketeers.Sim
{
    public static partial class TurnResolver
    {
        struct Push
        {
            public int Shover;
            public Dir Dir;
            public int Strength;
        }

        // §6 Phase 3: shoves. Targets come from the post-movement snapshot; pushes are combined per
        // target, then applied one target at a time in the targets' initiative order.
        static void Phase3Shoves(TickContext c)
        {
            c.Log.Phase = SimPhase.Shoves;
            var s = c.S;
            var units = s.Units;
            int n = units.Length;
            var pushes = new List<Push>[n];
            var kickedKegs = new List<Keg>();
            var kicks = new List<List<Push>>();
            var wasParrying = new bool[n];
            for (int i = 0; i < n; i++)
                wasParrying[i] = units[i].Parrying;

            // 3.1/3.2 Targets and push vectors. Shovers are walked in initiative order so each list is
            // in initiative order too, which is how kill credit is picked below.
            foreach (int i in c.Order)
            {
                var u = units[i];
                if (!c.Active[i] || !u.Alive || c.Act[i].Type != ActionType.Shove)
                    continue;
                Dir dir = c.Act[i].Dir;
                Int2 at = u.Pos + dir.ToInt2();

                var target = s.UnitAt(at);
                if (target != null)
                {
                    int t = target.Slot;
                    int strength = wasParrying[t] ? c.Cfg.GuardBreakPush : c.Cfg.ShovePush;
                    (pushes[t] ??= new List<Push>()).Add(new Push { Shover = i, Dir = dir, Strength = strength });
                    c.Log.Add(SimEventKind.Shoved, unit: i, other: t, from: u.Pos, to: at, dir: dir, amount: strength);
                    continue;
                }

                var keg = s.KegAt(at);
                if (keg != null)
                {
                    int k = kickedKegs.IndexOf(keg);
                    if (k < 0)
                    {
                        kickedKegs.Add(keg);
                        kicks.Add(new List<Push>());
                        k = kickedKegs.Count - 1;
                    }
                    kicks[k].Add(new Push { Shover = i, Dir = dir, Strength = 1 });
                    c.Log.Add(SimEventKind.Shoved, unit: i, keg: keg.Id, from: u.Pos, to: at, dir: dir);
                    continue;
                }

                // Empty floor, pit, pillar or crate: nothing to push. Crates are not shovable.
                c.Log.Add(SimEventKind.Shoved, unit: i, from: u.Pos, to: at, dir: dir);
            }

            // Guard Break cancels the parry for the rest of the tick, so Phase 4 shots hit.
            for (int t = 0; t < n; t++)
            {
                if (pushes[t] == null || !wasParrying[t])
                    continue;
                units[t].Parrying = false;
                c.Log.Add(SimEventKind.GuardBreak, unit: t, other: pushes[t][0].Shover, to: units[t].Pos);
            }

            // 3.3/3.4 Combine and apply, in the targets' initiative order.
            foreach (int t in c.Order)
            {
                if (pushes[t] != null && units[t].Alive)
                    ApplyUnitPushes(c, t, pushes[t]);
            }

            // 3.5 Keg kicks, after units have settled, in keg id order (s.Kegs is kept in id order;
            // iterate a copy because a keg kicked into a pit is removed).
            if (kickedKegs.Count > 0)
            {
                foreach (var keg in s.Kegs.ToArray())
                {
                    int k = kickedKegs.IndexOf(keg);
                    if (k >= 0)
                        KickKeg(c, keg, kicks[k]);
                }
            }
        }

        static void ApplyUnitPushes(TickContext c, int t, List<Push> list)
        {
            int north = 0, east = 0, south = 0, west = 0;
            foreach (var p in list)
            {
                switch (p.Dir)
                {
                    case Dir.N: north += p.Strength; break;
                    case Dir.E: east += p.Strength; break;
                    case Dir.S: south += p.Strength; break;
                    default: west += p.Strength; break;
                }
            }
            var r = PushMath.Combine(north, east, south, west, c.Cfg.MaxPushStrength);

            // Credit goes to the first shover (initiative order) who pushed the way the unit went;
            // for a squeeze, to the first shover overall.
            int credit = list[0].Shover;
            if (r.Outcome == PushOutcome.Pushed)
            {
                foreach (var p in list)
                {
                    if (p.Dir == r.Dir)
                    {
                        credit = p.Shover;
                        break;
                    }
                }
            }

            switch (r.Outcome)
            {
                case PushOutcome.Squeezed:
                    c.Log.Add(SimEventKind.Squeezed, unit: t, other: credit, to: c.S.Units[t].Pos);
                    Damage(c, t, c.Cfg.SqueezeDamage, credit, DamageKind.Squeeze);
                    break;
                case PushOutcome.Pushed:
                    Credit(c, t, credit);
                    SlideUnit(c, t, r.Dir, r.Strength, credit);
                    break;
                // Cancelled: opposite pushes cancel out and nothing happens to the target.
            }
        }

        // §6 Phase 3.4: one tile at a time. Wall Slam on solids, Bonk on units (no chain pushing),
        // instant fall on pits and off the board.
        static void SlideUnit(TickContext c, int t, Dir dir, int strength, int attacker)
        {
            var s = c.S;
            var u = s.Units[t];
            Int2 d = dir.ToInt2();
            Int2 start = u.Pos;
            Int2 p = start;
            for (int step = 0; step < strength; step++)
            {
                Int2 next = p + d;
                if (IsPit(s, next))
                {
                    c.Log.Add(SimEventKind.Pushed, unit: t, other: attacker, from: start, to: next, dir: dir, amount: step + 1);
                    Fall(c, t, next);
                    return;
                }
                if (IsSolid(s, next))
                {
                    u.Pos = p;
                    c.Log.Add(SimEventKind.Pushed, unit: t, other: attacker, from: start, to: p, dir: dir, amount: step);
                    c.Log.Add(SimEventKind.WallSlam, unit: t, other: attacker, from: p, to: next, dir: dir);
                    Damage(c, t, c.Cfg.WallSlamDamage, attacker, DamageKind.WallSlam);
                    return;
                }
                var other = s.UnitAt(next);
                if (other != null)
                {
                    u.Pos = p;
                    c.Log.Add(SimEventKind.Pushed, unit: t, other: attacker, from: start, to: p, dir: dir, amount: step);
                    // One Bonk per participant, like Phase 2.
                    c.Log.Add(SimEventKind.Bonk, unit: t, other: other.Slot, from: p, to: next, dir: dir);
                    c.Log.Add(SimEventKind.Bonk, unit: other.Slot, other: t, from: next, to: p, dir: dir.Opposite());
                    Damage(c, t, c.Cfg.BonkDamage, attacker, DamageKind.Bonk);
                    Damage(c, other.Slot, c.Cfg.BonkDamage, attacker, DamageKind.Bonk);
                    return;
                }
                p = next;
            }
            u.Pos = p;
            c.Log.Add(SimEventKind.Pushed, unit: t, other: attacker, from: start, to: p, dir: dir, amount: strength);
        }

        // §6 Phase 3.5: a kicked keg slides up to KegKickDistance until blocked; a pit swallows it.
        // Several kicks on one keg combine like unit pushes; only the resulting direction matters.
        static void KickKeg(TickContext c, Keg keg, List<Push> list)
        {
            var s = c.S;
            int north = 0, east = 0, south = 0, west = 0;
            foreach (var p in list)
            {
                switch (p.Dir)
                {
                    case Dir.N: north++; break;
                    case Dir.E: east++; break;
                    case Dir.S: south++; break;
                    default: west++; break;
                }
            }
            var r = PushMath.Combine(north, east, south, west, c.Cfg.KegKickDistance);
            if (r.Outcome != PushOutcome.Pushed)
                return;
            int kicker = list[0].Shover;
            foreach (var p in list)
            {
                if (p.Dir == r.Dir)
                {
                    kicker = p.Shover;
                    break;
                }
            }

            Int2 d = r.Dir.ToInt2();
            Int2 start = keg.Pos;
            Int2 pos = start;
            for (int step = 0; step < c.Cfg.KegKickDistance; step++)
            {
                Int2 next = pos + d;
                if (IsPit(s, next))
                {
                    c.Log.Add(SimEventKind.KegKicked, unit: kicker, keg: keg.Id, from: start, to: next, dir: r.Dir);
                    c.Log.Add(SimEventKind.Fell, keg: keg.Id, to: next);
                    s.Kegs.Remove(keg);
                    return;
                }
                if (IsSolid(s, next) || s.UnitAt(next) != null)
                    break;
                pos = next;
            }
            keg.Pos = pos;
            c.Log.Add(SimEventKind.KegKicked, unit: kicker, keg: keg.Id, from: start, to: pos, dir: r.Dir);
        }
    }
}
