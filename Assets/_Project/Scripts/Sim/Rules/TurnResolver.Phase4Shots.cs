using System.Collections.Generic;

namespace Musketeers.Sim
{
    internal struct ShotHitRecord
    {
        public int Target;
        public int Attacker;
        public DamageKind Kind;
    }

    // What every shot of one tick touched. Applied together at the end of Phase 4.
    internal sealed class ShotBuffer
    {
        public readonly List<ShotHitRecord> Hits = new List<ShotHitRecord>();
        public readonly List<Int2> Crates = new List<Int2>();
        public readonly List<int> CrateShooters = new List<int>();
        public readonly List<Keg> Kegs = new List<Keg>();
        public readonly List<int> KegShooters = new List<int>();

        public void AddKeg(Keg keg, int shooter)
        {
            if (Kegs.Contains(keg))
                return;   // first shooter in initiative order keeps the credit
            Kegs.Add(keg);
            KegShooters.Add(shooter);
        }
    }

    public static partial class TurnResolver
    {
        // §6 Phase 4: every shot is traced on the same snapshot, then all effects land at once,
        // so two players who shoot each other both get hit, and a crate destroyed by one shot
        // still blocks another shot in the same tick.
        static void Phase4Shots(TickContext c)
        {
            c.Log.Phase = SimPhase.Shots;
            var s = c.S;
            var units = s.Units;
            var buf = new ShotBuffer();

            foreach (int i in c.Order)
            {
                var u = units[i];
                if (!c.Active[i] || !u.Alive)
                    continue;
                if (c.Act[i].Type == ActionType.Fire)
                    TraceShot(c, i, u.Pos, c.Act[i].Dir, c.Cfg.FireRange, true, buf);
                else if (c.Act[i].Type == ActionType.Signature && u.Ability == Ability.DoubleCharge)
                    TraceDoubleCharge(c, i, u.Pos, c.Act[i].Dir, buf);
            }

            for (int k = 0; k < buf.Crates.Count; k++)
            {
                Int2 p = buf.Crates[k];
                if (s.Board.Get(p) != TileType.Crate)
                    continue;   // already destroyed by an earlier shot this tick
                s.Board.Set(p, TileType.Floor);
                c.Log.Add(SimEventKind.CrateDestroyed, unit: buf.CrateShooters[k], to: p);
            }

            // §6 Phase 4.1: a keg hit by a shot explodes this tick, in Phase 5.
            for (int k = 0; k < buf.Kegs.Count; k++)
            {
                buf.Kegs[k].Detonating = true;
                buf.Kegs[k].DetonatedBy = buf.KegShooters[k];
            }

            // Hits are recorded in shooter initiative order; the first one on each target gets the kill credit.
            var credited = new bool[units.Length];
            foreach (var h in buf.Hits)
            {
                Damage(c, h.Target, c.Cfg.ShotDamage, h.Attacker, h.Kind, credit: !credited[h.Target]);
                credited[h.Target] = true;
            }
        }

        // Walks from origin (exclusive) for up to range tiles. Pillars stop it, crates and kegs are hit
        // and stop it, the first unit is hit. A parrying unit takes no damage; if the shot is
        // reflectable, a new non-reflectable shot is traced back the way it came with full range.
        internal static void TraceShot(TickContext c, int owner, Int2 origin, Dir dir, int range, bool reflectable, ShotBuffer buf)
        {
            var s = c.S;
            var kind = reflectable ? SimEventKind.ShotFired : SimEventKind.Reflected;
            Int2 d = dir.ToInt2();
            Int2 p = origin;
            for (int step = 1; step <= range; step++)
            {
                Int2 next = p + d;
                if (!s.Board.InBounds(next))
                    break;
                p = next;

                var tile = s.Board.Get(p);
                if (tile == TileType.Pillar)
                {
                    c.Log.Add(kind, unit: owner, from: origin, to: p, dir: dir);
                    return;
                }
                if (tile == TileType.Crate)
                {
                    c.Log.Add(kind, unit: owner, from: origin, to: p, dir: dir);
                    buf.Crates.Add(p);
                    buf.CrateShooters.Add(owner);
                    return;
                }
                var keg = s.KegAt(p);
                if (keg != null)
                {
                    c.Log.Add(kind, unit: owner, from: origin, to: p, dir: dir);
                    c.Log.Add(SimEventKind.ShotHit, other: owner, keg: keg.Id, to: p, dir: dir);
                    buf.AddKeg(keg, owner);
                    return;
                }
                var target = s.UnitAt(p);
                if (target != null)
                {
                    c.Log.Add(kind, unit: owner, from: origin, to: p, dir: dir);
                    if (target.Parrying)
                    {
                        // §5 Parry blocks all shot damage. A reflection that meets another parrier is
                        // blocked too, but never reflected a second time.
                        c.Log.Add(SimEventKind.Parried, unit: target.Slot, other: owner, to: p, dir: dir);
                        if (reflectable)
                            TraceShot(c, target.Slot, p, dir.Opposite(), range, false, buf);
                        return;
                    }
                    buf.Hits.Add(new ShotHitRecord
                    {
                        Target = target.Slot,
                        Attacker = owner,
                        Kind = reflectable ? DamageKind.Shot : DamageKind.ReflectedShot,
                    });
                    c.Log.Add(SimEventKind.ShotHit, unit: target.Slot, other: owner, to: p, dir: dir);
                    return;
                }
            }
            // Out of range or off the board: the shot ends on the last tile it reached.
            c.Log.Add(kind, unit: owner, from: origin, to: p, dir: dir);
        }

        // §4 Double Charge: range 7, hits up to DoubleChargePierce units (stopping at the last one),
        // ignores Parry (no block, no reflection). Pillars stop it; a crate is destroyed and stops it;
        // a keg is detonated and stops it, like a musket shot.
        static void TraceDoubleCharge(TickContext c, int owner, Int2 origin, Dir dir, ShotBuffer buf)
        {
            var s = c.S;
            Int2 d = dir.ToInt2();
            Int2 p = origin;
            Keg hitKeg = null;
            var victims = new List<Unit>(c.Cfg.DoubleChargePierce);
            for (int step = 1; step <= c.Cfg.DoubleChargeRange; step++)
            {
                Int2 next = p + d;
                if (!s.Board.InBounds(next))
                    break;
                p = next;

                var tile = s.Board.Get(p);
                if (tile == TileType.Pillar)
                    break;
                if (tile == TileType.Crate)
                {
                    buf.Crates.Add(p);
                    buf.CrateShooters.Add(owner);
                    break;
                }
                hitKeg = s.KegAt(p);
                if (hitKeg != null)
                    break;
                var target = s.UnitAt(p);
                if (target != null)
                {
                    victims.Add(target);
                    if (victims.Count >= c.Cfg.DoubleChargePierce)
                        break;
                }
            }

            // Same event order as a musket shot: the tracer first, then what it hit.
            c.Log.Add(SimEventKind.ShotFired, unit: owner, from: origin, to: p, dir: dir, reason: (byte)ShotKind.DoubleCharge);
            foreach (var v in victims)
            {
                buf.Hits.Add(new ShotHitRecord { Target = v.Slot, Attacker = owner, Kind = DamageKind.DoubleCharge });
                c.Log.Add(SimEventKind.ShotHit, unit: v.Slot, other: owner, to: v.Pos, dir: dir);
            }
            if (hitKeg != null)
            {
                c.Log.Add(SimEventKind.ShotHit, other: owner, keg: hitKeg.Id, to: p, dir: dir);
                buf.AddKeg(hitKeg, owner);
            }
        }
    }
}
