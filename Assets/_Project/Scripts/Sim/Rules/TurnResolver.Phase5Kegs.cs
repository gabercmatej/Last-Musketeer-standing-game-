using System;
using System.Collections.Generic;

namespace Musketeers.Sim
{
    public static partial class TurnResolver
    {
        // §6 Phase 5: landing, fuses, then every explosion of the tick resolved together.
        static void Phase5Kegs(TickContext c)
        {
            c.Log.Phase = SimPhase.Kegs;
            var s = c.S;

            // 5.1 Landing, in thrower initiative order (that order only matters when two kegs target
            // one tile: the second lands on the first and blows both). A thrower who fell earlier this
            // tick never threw, like a fallen shooter never fires.
            List<Keg> landed = null;
            foreach (int i in c.Order)
            {
                var u = s.Units[i];
                if (!c.Active[i] || !u.Alive || c.Act[i].Type != ActionType.Keg)
                    continue;
                Int2 t = c.Act[i].Target;
                bool impact = s.UnitAt(t) != null || s.KegAt(t) != null || s.Board.Get(t) != TileType.Floor;
                var keg = new Keg { Id = s.NextKegId++, Owner = i, Pos = t, Fuse = c.Cfg.KegFuseTicks, Detonating = impact };
                s.Kegs.Add(keg);   // ids only grow, so the list stays in id order
                (landed ??= new List<Keg>()).Add(keg);
                c.Log.Add(SimEventKind.KegThrown, unit: i, keg: keg.Id, from: u.Pos, to: t);
                c.Log.Add(SimEventKind.KegLanded, unit: i, keg: keg.Id, to: t, reason: (byte)(impact ? KegLanding.Impact : KegLanding.Floor));
            }

            // 5.2 Fuses. A keg that landed this tick starts counting next tick, so it explodes
            // KegFuseTicks ticks later: the same tick slot next turn (§5).
            var exploding = new List<Keg>();
            var credit = new List<int>();
            foreach (var keg in s.Kegs)
            {
                if (landed == null || !landed.Contains(keg))
                    keg.Fuse--;
                if (keg.Fuse <= 0 || keg.Detonating)
                {
                    exploding.Add(keg);
                    // Kill credit: the shooter who set it off, otherwise its owner (-1 for map kegs).
                    credit.Add(keg.DetonatedBy >= 0 ? keg.DetonatedBy : keg.Owner);
                }
            }
            if (exploding.Count == 0)
                return;

            // 5.3 Chain reactions: any keg in the 3×3 of an exploding keg explodes too, credited to
            // whoever set off the keg that reached it first (walked in id order, breadth first).
            int triggered = exploding.Count;
            for (int e = 0; e < exploding.Count; e++)
            {
                foreach (var keg in s.Kegs)
                {
                    if (!exploding.Contains(keg) && InBlast(exploding[e].Pos, keg.Pos))
                    {
                        exploding.Add(keg);
                        credit.Add(credit[e]);
                    }
                }
            }
            Explode(c, exploding, credit, triggered);
        }

        // All explosions of a tick at once: damage (max one keg damage per unit, parry blocks it),
        // crates destroyed, kegs removed, then outward pushes combined and applied like Phase 3 shoves.
        static void Explode(TickContext c, List<Keg> exploding, List<int> credit, int triggered)
        {
            var s = c.S;
            var units = s.Units;
            int n = units.Length;
            for (int e = 0; e < exploding.Count; e++)
            {
                var k = exploding[e];
                c.Log.Add(SimEventKind.KegExploded, unit: credit[e], other: k.Owner, keg: k.Id, to: k.Pos, amount: e >= triggered ? 1 : 0);
            }

            // Damage. The first explosion (in the order above) that covers a unit owns the credit.
            for (int t = 0; t < n; t++)
            {
                var u = units[t];
                if (!u.Alive)
                    continue;
                for (int e = 0; e < exploding.Count; e++)
                {
                    if (!InBlast(exploding[e].Pos, u.Pos))
                        continue;
                    if (u.Parrying)
                        c.Log.Add(SimEventKind.Parried, unit: t, other: credit[e], keg: exploding[e].Id, to: u.Pos);
                    else
                        Damage(c, t, c.Cfg.KegDamage, credit[e], DamageKind.Explosion);
                    break;
                }
            }

            // Crates in any blast area.
            for (int e = 0; e < exploding.Count; e++)
            {
                Int2 center = exploding[e].Pos;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        Int2 p = new Int2(center.X + dx, center.Y + dy);
                        if (s.Board.Get(p) != TileType.Crate)
                            continue;
                        s.Board.Set(p, TileType.Floor);
                        c.Log.Add(SimEventKind.CrateDestroyed, unit: credit[e], keg: exploding[e].Id, to: p);
                    }
                }
            }

            foreach (var k in exploding)
                s.Kegs.Remove(k);

            // Orthogonal neighbours are pushed 1 outward, parry or not. Several blasts on one unit
            // combine like several shoves (§6 Phase 3.3); applied in the targets' initiative order.
            var pushes = new List<Push>[n];
            for (int e = 0; e < exploding.Count; e++)
            {
                foreach (Dir d in DirExt.All)
                {
                    var target = s.UnitAt(exploding[e].Pos + d.ToInt2());
                    if (target != null)
                        (pushes[target.Slot] ??= new List<Push>()).Add(new Push { Shover = credit[e], Dir = d, Strength = c.Cfg.KegPush });
                }
            }
            foreach (int t in c.Order)
            {
                if (pushes[t] != null && units[t].Alive)
                    ApplyUnitPushes(c, t, pushes[t]);
            }
        }

        // §6 Phase 5.3: the 3×3 area around a keg.
        static bool InBlast(Int2 center, Int2 p) => Math.Abs(center.X - p.X) <= 1 && Math.Abs(center.Y - p.Y) <= 1;
    }
}
