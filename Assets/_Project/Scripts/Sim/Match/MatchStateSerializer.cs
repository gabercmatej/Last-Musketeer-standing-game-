using System;
using System.Collections.Generic;

namespace Musketeers.Sim
{
    // Deterministic binary snapshot of a MatchState (TECH_DESIGN §2 safety net: the host sends one to a client whose
    // state hash diverged). Integers only, fixed field order, little endian. What the setup already fixes (Cfg, Map,
    // Abilities, Characters) is NOT stored: Deserialize takes it from a template (the client's own MatchState).
    public static class MatchStateSerializer
    {
        const byte Version = 1;

        public static byte[] Serialize(MatchState m)
        {
            var w = new Writer();
            w.Byte(Version);
            int n = m.PlayerCount;
            w.Int(n);
            w.Int(m.RoundNumber);
            w.Int(m.Crowned);
            w.Bool(m.MatchOver);
            w.Int(m.MatchWinner);
            w.UInt(m.Rng.State);
            w.UInt(m.Seed);
            w.Ints(m.Points);
            w.Ints(m.RoundWins);
            w.Int(m.RoundPoints.Count);
            foreach (var r in m.RoundPoints)
                w.Ints(r);
            w.Bool(m.Places != null);
            if (m.Places != null)
                w.Ints(m.Places);
            w.Int(m.RoundGains.Count);
            foreach (var g in m.RoundGains)
            {
                w.Int(g.Player);
                w.Int(g.Amount);
                w.Int((int)g.Reason);
                w.Int(g.Victim);
            }
            foreach (var arr in m.Stats.All)
                w.Ints(arr);
            w.Bool(m.Round != null);
            if (m.Round != null)
                WriteRound(w, m.Round);
            return w.ToArray();
        }

        public static MatchState Deserialize(byte[] data, MatchState template)
        {
            var r = new Reader(data);
            if (r.Byte() != Version)
                throw new FormatException("Unknown snapshot version.");
            int n = r.Int();
            if (n != template.PlayerCount)
                throw new FormatException($"Snapshot has {n} players, the match has {template.PlayerCount}.");
            var m = new MatchState
            {
                Cfg = template.Cfg,
                Map = template.Map,
                Abilities = (Ability[])template.Abilities.Clone(),
                Characters = (int[])template.Characters.Clone(),
                Stats = new MatchStats(n),
            };
            m.RoundNumber = r.Int();
            m.Crowned = r.Int();
            m.MatchOver = r.Bool();
            m.MatchWinner = r.Int();
            m.Rng = new DeterministicRng(r.UInt());
            m.Seed = r.UInt();
            m.Points = r.Ints();
            m.RoundWins = r.Ints();
            int rounds = r.Int();
            for (int i = 0; i < rounds; i++)
                m.RoundPoints.Add(r.Ints());
            m.Places = r.Bool() ? r.Ints() : null;
            int gains = r.Int();
            for (int i = 0; i < gains; i++)
            {
                m.RoundGains.Add(new PointGain
                {
                    Player = r.Int(),
                    Amount = r.Int(),
                    Reason = (PointReason)r.Int(),
                    Victim = r.Int(),
                });
            }
            foreach (var arr in m.Stats.All)
            {
                var src = r.Ints();
                if (src.Length != arr.Length)
                    throw new FormatException("Stats length mismatch.");
                Array.Copy(src, arr, arr.Length);
            }
            m.Round = r.Bool() ? ReadRound(r, n) : null;
            return m;
        }

        static void WriteRound(Writer w, GameState g)
        {
            w.Int(g.Turn);
            w.Int(g.NextKegId);
            w.Bool(g.RoundOver);
            w.Int(g.Crowned);
            w.Int(g.RoundWinner);
            w.UInt(g.Rng.State);
            var b = g.Board;
            w.Int(b.Width);
            w.Int(b.Height);
            w.Bool(b.EdgeWalls);
            int cells = b.Width * b.Height;
            for (int i = 0; i < cells; i++)
            {
                w.Byte((byte)b.RawTile(i));
                w.Bool(b.RawRemoved(i));
            }
            w.Int(g.Units.Length);
            foreach (var u in g.Units)
            {
                w.Int(u.Slot);
                w.Int(u.Character);
                w.Int((int)u.Ability);
                w.Int(u.Pos.X);
                w.Int(u.Pos.Y);
                w.Int(u.Hp);
                w.Int(u.Grit);
                w.Bool(u.Loaded);
                w.Bool(u.Alive);
                w.Int(u.LastAttacker);
                w.Int(u.LastAttackTurn);
                w.Bool(u.Parrying);
            }
            w.Int(g.Kegs.Count);
            foreach (var k in g.Kegs)
            {
                w.Int(k.Id);
                w.Int(k.Owner);
                w.Int(k.Pos.X);
                w.Int(k.Pos.Y);
                w.Int(k.Fuse);
                w.Bool(k.Detonating);
                w.Int(k.DetonatedBy);
            }
        }

        static GameState ReadRound(Reader r, int players)
        {
            int turn = r.Int(), nextKeg = r.Int();
            bool over = r.Bool();
            int crowned = r.Int(), winner = r.Int();
            uint rng = r.UInt();
            int bw = r.Int(), bh = r.Int();
            var board = new Board(bw, bh) { EdgeWalls = r.Bool() };
            for (int i = 0; i < bw * bh; i++)
            {
                var tile = (TileType)r.Byte();
                bool removed = r.Bool();
                var p = new Int2(i % bw, i / bw);
                board.Set(p, tile);
                if (removed)
                    board.SetRemoved(p);
            }
            int un = r.Int();
            if (un != players)
                throw new FormatException("Unit count mismatch.");
            var units = new Unit[un];
            for (int i = 0; i < un; i++)
            {
                units[i] = new Unit
                {
                    Slot = r.Int(),
                    Character = r.Int(),
                    Ability = (Ability)r.Int(),
                    Pos = new Int2(r.Int(), r.Int()),
                    Hp = r.Int(),
                    Grit = r.Int(),
                    Loaded = r.Bool(),
                    Alive = r.Bool(),
                    LastAttacker = r.Int(),
                    LastAttackTurn = r.Int(),
                    Parrying = r.Bool(),
                };
            }
            var g = new GameState(board, units, new DeterministicRng(rng))
            {
                Turn = turn,
                NextKegId = nextKeg,
                RoundOver = over,
                Crowned = crowned,
                RoundWinner = winner,
            };
            int kn = r.Int();
            for (int i = 0; i < kn; i++)
            {
                g.Kegs.Add(new Keg
                {
                    Id = r.Int(),
                    Owner = r.Int(),
                    Pos = new Int2(r.Int(), r.Int()),
                    Fuse = r.Int(),
                    Detonating = r.Bool(),
                    DetonatedBy = r.Int(),
                });
            }
            return g;
        }

        sealed class Writer
        {
            readonly List<byte> _b = new List<byte>(512);
            public void Byte(byte v) => _b.Add(v);
            public void Bool(bool v) => _b.Add(v ? (byte)1 : (byte)0);
            public void Int(int v) => UInt(unchecked((uint)v));
            public void UInt(uint v)
            {
                for (int i = 0; i < 4; i++)
                    _b.Add((byte)(v >> (8 * i)));
            }
            public void Ints(int[] a)
            {
                Int(a.Length);
                for (int i = 0; i < a.Length; i++)
                    Int(a[i]);
            }
            public byte[] ToArray() => _b.ToArray();
        }

        sealed class Reader
        {
            readonly byte[] _d;
            int _p;
            public Reader(byte[] d) { _d = d ?? throw new ArgumentNullException(nameof(d)); }
            public byte Byte()
            {
                if (_p >= _d.Length)
                    throw new FormatException("Snapshot truncated.");
                return _d[_p++];
            }
            public bool Bool() => Byte() != 0;
            public uint UInt()
            {
                uint v = 0;
                for (int i = 0; i < 4; i++)
                    v |= (uint)Byte() << (8 * i);
                return v;
            }
            public int Int() => unchecked((int)UInt());
            public int[] Ints()
            {
                int n = Int();
                if (n < 0 || n > 4096)
                    throw new FormatException("Bad array length.");
                var a = new int[n];
                for (int i = 0; i < n; i++)
                    a[i] = Int();
                return a;
            }
        }
    }
}
