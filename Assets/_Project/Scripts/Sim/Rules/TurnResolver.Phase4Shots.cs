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
            var units = c.S.Units;
            var buf = new ShotBuffer();

            foreach (int i in c.Order)
            {
                var u = units[i];
                if (!c.Active[i] || !u.Alive)
                    continue;
                if (c.Act[i].Type == ActionType.Fire)
                    TraceShot(c, i, u.Pos, c.Act[i].Dir, c.Cfg.FireRange, true, buf);
            }
            ApplyShotBuffer(c, buf);

            // §4 Triple Charge: after the musket shots have landed, each charge fires its shots one at a
            // time on the live state, in initiative order. A unit that fell to an earlier charge doesn't fire.
            foreach (int i in c.Order)
            {
                var u = units[i];
                if (!c.Active[i] || !u.Alive || c.Act[i].Type != ActionType.Signature || u.Ability != Ability.TripleCharge)
                    continue;
                for (int shot = 0; shot < c.Cfg.TripleChargeShots && u.Alive; shot++)
                    FireTripleChargeShot(c, i, c.Act[i].Dir);
            }
        }

        // Applies everything a set of traced shots touched, all at once.
        static void ApplyShotBuffer(TickContext c, ShotBuffer buf)
        {
            var s = c.S;
            var units = s.Units;

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
                if (buf.Kegs[k].Detonating)
                    continue;   // an earlier shot this tick already lit it and keeps the credit
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

        // §4 Triple Charge, one shot (FIXES §3.2 rule 7): traced from the shooter with range FireRange on
        // the live state. A unit hit takes ShotDamage and slides TripleChargePush tiles away (normal push
        // rules: wall slam, bonk, fall) with credit to the shooter, so the next shot meets it further
        // along. A crate is destroyed at once (the next shot can pass), a keg is set to detonate in
        // Phase 5. A parrier takes nothing, isn't pushed, and reflects the shot like a musket shot.
        static void FireTripleChargeShot(TickContext c, int owner, Dir dir)
        {
            var s = c.S;
            Int2 origin = s.Units[owner].Pos;
            Int2 d = dir.ToInt2();
            Int2 p = origin;
            for (int step = 1; step <= c.Cfg.FireRange; step++)
            {
                Int2 next = p + d;
                if (!s.Board.InBounds(next))
                    break;
                p = next;

                var tile = s.Board.Get(p);
                if (tile == TileType.Pillar)
                {
                    c.Log.Add(SimEventKind.ShotFired, unit: owner, from: origin, to: p, dir: dir, reason: (byte)ShotKind.TripleCharge);
                    return;
                }
                if (tile == TileType.Crate)
                {
                    c.Log.Add(SimEventKind.ShotFired, unit: owner, from: origin, to: p, dir: dir, reason: (byte)ShotKind.TripleCharge);
                    var crate = new ShotBuffer();
                    crate.Crates.Add(p);
                    crate.CrateShooters.Add(owner);
                    ApplyShotBuffer(c, crate);
                    return;
                }
                var keg = s.KegAt(p);
                if (keg != null)
                {
                    c.Log.Add(SimEventKind.ShotFired, unit: owner, from: origin, to: p, dir: dir, reason: (byte)ShotKind.TripleCharge);
                    c.Log.Add(SimEventKind.ShotHit, other: owner, keg: keg.Id, to: p, dir: dir);
                    var kegs = new ShotBuffer();
                    kegs.AddKeg(keg, owner);
                    ApplyShotBuffer(c, kegs);
                    return;
                }
                var target = s.UnitAt(p);
                if (target == null)
                    continue;
                c.Log.Add(SimEventKind.ShotFired, unit: owner, from: origin, to: p, dir: dir, reason: (byte)ShotKind.TripleCharge);
                if (target.Parrying)
                {
                    c.Log.Add(SimEventKind.Parried, unit: target.Slot, other: owner, to: p, dir: dir);
                    var back = new ShotBuffer();
                    TraceShot(c, target.Slot, p, dir.Opposite(), c.Cfg.FireRange, false, back);
                    ApplyShotBuffer(c, back);
                    return;
                }
                c.Log.Add(SimEventKind.ShotHit, unit: target.Slot, other: owner, to: p, dir: dir);
                Damage(c, target.Slot, c.Cfg.ShotDamage, owner, DamageKind.TripleCharge);
                SlideUnit(c, target.Slot, dir, c.Cfg.TripleChargePush, owner);
                return;
            }
            // Out of range or off the board: the shot ends on the last tile it reached.
            c.Log.Add(SimEventKind.ShotFired, unit: owner, from: origin, to: p, dir: dir, reason: (byte)ShotKind.TripleCharge);
        }
    }
}
