using Musketeers.Sim;

namespace Musketeers.Game
{
    // The announcer's named moments (GAME_DESIGN §8.2). Round- and match-level ones (EnGarde, Draw,
    // LastMusketeer) come from the flow; the rest are detected from one tick's events.
    public enum Callout { None, Bonk, Signature, Riposte, GuardBreak, SwanDive, Touche, DoubleKo, CrownFalls, EnGarde, Draw, LastMusketeer }

    public static class Callouts
    {
        // Bonk keeps its banner but plays no voice line (FIXES_v0.2 §2.6). "Signature move!" is back with its voice line (FIXES_4E §8.5).
        public static bool Voiced(Callout c) => c != Callout.Bonk;

        // The most important moment in a tick (later enum values win), so one tick gets one line.
        // turnStart: the log is a turn start, where falls are the crumbling arena, not a swan dive.
        public static Callout Top(EventLog tick, int crowned, bool turnStart = false)
        {
            var best = Callout.None;
            int deaths = 0;
            for (int i = 0; i < tick.Count; i++)
            {
                var e = tick[i];
                switch (e.Kind)
                {
                    case SimEventKind.Bonk: best = Max(best, Callout.Bonk); break;
                    case SimEventKind.Reflected: best = Max(best, Callout.Riposte); break;
                    case SimEventKind.GuardBreak: best = Max(best, Callout.GuardBreak); break;
                    case SimEventKind.SignatureUsed:   // emitted in Phase 0 once the Grit is paid, so even a missed or failed one is announced
                    case SimEventKind.Grappled:
                    case SimEventKind.Swapped:
                    case SimEventKind.BullRushed:
                        best = Max(best, Callout.Signature);
                        break;
                    case SimEventKind.ShotFired:
                        if ((ShotKind)e.Reason == ShotKind.TripleCharge)
                            best = Max(best, Callout.Signature);
                        break;
                    case SimEventKind.Died:
                        deaths++;
                        if (e.Unit == crowned && crowned >= 0)
                            best = Max(best, Callout.CrownFalls);
                        else if ((DeathCause)e.Reason == DeathCause.Fell && e.Other < 0 && !turnStart)
                            best = Max(best, Callout.SwanDive);
                        else if (KilledByRiposte(tick, e.Unit))
                            best = Max(best, Callout.Touche);
                        break;
                }
            }
            if (deaths >= 2)
                best = Max(best, Callout.DoubleKo);
            return best;
        }

        static bool KilledByRiposte(EventLog tick, int unit)
        {
            for (int i = 0; i < tick.Count; i++)
            {
                var e = tick[i];
                if (e.Kind == SimEventKind.Damaged && e.Unit == unit && (DamageKind)e.Reason == DamageKind.ReflectedShot)
                    return true;
            }
            return false;
        }

        static Callout Max(Callout a, Callout b) => b > a ? b : a;

        // Banner text and the announcer clip name (Audio/Voice/announcer/<clip>).
        public static string Text(Callout c)
        {
            switch (c)
            {
                case Callout.Bonk: return "BONK!";
                case Callout.Signature: return "SIGNATURE MOVE!";
                case Callout.Riposte: return "RIPOSTE!";
                case Callout.GuardBreak: return "GUARD BREAK!";
                case Callout.SwanDive: return "SWAN DIVE!";
                case Callout.Touche: return "TOUCHÉ!";
                case Callout.DoubleKo: return "DOUBLE KO!";
                case Callout.CrownFalls: return "THE CROWN FALLS!";
                case Callout.EnGarde: return "EN GARDE!";
                case Callout.Draw: return "DRAW!";
                case Callout.LastMusketeer: return "THE LAST MUSKETEER STANDING";
                default: return "";
            }
        }

        public static string Clip(Callout c)
        {
            switch (c)
            {
                case Callout.Bonk: return "bonk";
                case Callout.Signature: return "signature";
                case Callout.Riposte: return "riposte";
                case Callout.GuardBreak: return "guard_break";
                case Callout.SwanDive: return "swan_dive";
                case Callout.Touche: return "touche";
                case Callout.DoubleKo: return "double_ko";
                case Callout.CrownFalls: return "the_crown_falls";
                case Callout.EnGarde: return "en_garde";
                case Callout.Draw: return "draw";
                case Callout.LastMusketeer: return "last_musketeer_standing";
                default: return null;
            }
        }
    }
}
