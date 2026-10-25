using System.Collections.Generic;
using Musketeers.Sim;

namespace Musketeers.Game
{
    public sealed class ReplayClip
    {
        public GameState Before { get; internal set; }
        public GameState After { get; internal set; }
        public EventLog Log { get; internal set; }
        public int Crowned { get; internal set; }
        public Int2 Focus { get; internal set; }
        public int DeathCount { get; internal set; }
        public Int2 FrameMin { get; internal set; }
        public Int2 FrameMax { get; internal set; }
    }

    // Retains only elimination ticks, in their authoritative event order. Never resolves a turn.
    public sealed class RoundReplay
    {
        readonly List<ReplayClip> _clips = new List<ReplayClip>();
        int _round = -1;
        public IReadOnlyList<ReplayClip> Clips => _clips;
        public void Clear() { _clips.Clear(); _round = -1; }
        public void Add(TurnPlayback p)
        {
            if (_round != p.RoundNumber) { Clear(); _round = p.RoundNumber; }
            for (int t = 0; t < p.TickCount; t++)
                Capture(t == 0 ? p.Before : p.AfterTick[t - 1], p.AfterTick[t], p.TickLogs[t], p.CrownedBefore);
            if (p.Outcome.TurnStart != null)
                Capture(p.TickCount == 0 ? p.Before : p.AfterTick[p.TickCount - 1], p.After, p.Outcome.TurnStart, p.CrownedBefore);
        }
        void Capture(GameState before, GameState after, EventLog log, int crowned)
        {
            int deaths = log.CountOf(SimEventKind.Died);
            if (deaths == 0) return;
            var copy = new EventLog();
            Int2 focus = default; bool found = false;
            foreach (var e in log.Events)
            {
                copy.Tick = e.Tick; copy.Phase = e.Phase; copy.Add(e);
                if (!found && e.Kind == SimEventKind.Died) { focus = e.To; found = true; }
            }
            Int2 min = focus, max = focus;
            void Include(Int2 p)
            {
                min = new Int2(System.Math.Min(min.X, p.X), System.Math.Min(min.Y, p.Y));
                max = new Int2(System.Math.Max(max.X, p.X), System.Math.Max(max.Y, p.Y));
            }
            foreach (var death in log.Events)
            {
                if (death.Kind != SimEventKind.Died || death.Unit < 0 || death.Unit >= before.Units.Length) continue;
                Include(before.Units[death.Unit].Pos); Include(death.To);
                int attacker = death.Other;
                if (attacker < 0 || attacker == death.Unit || attacker >= before.Units.Length) continue;
                // Kill credit may come from an older tick. Only frame an attacker with a direct cause here.
                bool direct = false;
                foreach (var cause in log.Events)
                {
                    if (cause.Kind == SimEventKind.Died && cause.Unit == death.Unit) break;
                    if ((cause.Kind == SimEventKind.Damaged || cause.Kind == SimEventKind.ShotHit || cause.Kind == SimEventKind.Pushed) &&
                        cause.Unit == death.Unit && cause.Other == attacker) direct = true;
                    if ((cause.Kind == SimEventKind.Grappled || cause.Kind == SimEventKind.BullRushed) &&
                        cause.Unit == attacker && cause.Other == death.Unit) direct = true;
                }
                if (!direct) continue;
                Include(before.Units[attacker].Pos); Include(after.Units[attacker].Pos);
                foreach (var shot in log.Events)
                    if (shot.Unit == attacker && (shot.Kind == SimEventKind.ShotFired || shot.Kind == SimEventKind.Reflected))
                    { Include(shot.From); Include(shot.To); }
            }
            _clips.Add(new ReplayClip { Before = before.Clone(), After = after.Clone(), Log = copy,
                Crowned = crowned, Focus = focus, DeathCount = deaths, FrameMin = min, FrameMax = max });
        }
    }
}
