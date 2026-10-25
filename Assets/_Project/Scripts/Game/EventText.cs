using Musketeers.Sim;

namespace Musketeers.Game
{
    // Human-readable lines for the turn log panel ("Matej fires E", "the shot hits Nik").
    public static class EventText
    {
        public static string Describe(SimEvent e, string[] names, EventLog log = null, Board board = null, bool water = false)
        {
            string u = Name(e.Unit, names);
            string o = Name(e.Other, names);
            string keg = e.Keg >= 0 ? $"keg #{e.Keg}" : "keg";
            switch (e.Kind)
            {
                case SimEventKind.Moved: return $"{u} moves {e.Dir} to {e.To}";
                case SimEventKind.Dashed: return $"{u} dashes {e.Dir} to {e.To}";
                case SimEventKind.Leaped: return $"{u} leaps over the pit";
                case SimEventKind.MoveBlocked: return $"{u} is blocked at {e.To}";
                case SimEventKind.Bonk: return e.Other >= 0 ? $"BONK! {u} hits {o}" : $"BONK! {u} bumps into something";
                case SimEventKind.ParryRaised: return $"{u} raises a parry";
                case SimEventKind.Shoved:
                    if (e.Other >= 0) return $"{u} shoves {o}";
                    if (e.Keg >= 0) return $"{u} kicks {keg}";
                    return $"{u} shoves thin air";
                case SimEventKind.Pushed: return $"{u} is pushed {e.Amount} to {e.To}";
                case SimEventKind.GuardBreak: return $"GUARD BREAK! {o} breaks {u}'s parry";
                case SimEventKind.Squeezed: return $"{u} is squeezed";
                case SimEventKind.WallSlam: return $"{u} slams into a wall";
                case SimEventKind.Fell: return e.Unit >= 0 ? $"{u} falls" : $"{keg} falls into the pit";
                case SimEventKind.ShotFired:
                    return (ShotKind)e.Reason == ShotKind.TripleCharge ? $"{u} fires a Triple Charge shot {e.Dir}" : $"{u} fires {e.Dir}";
                case SimEventKind.ShotHit: return e.Unit >= 0 ? $"the shot hits {u}" : $"the shot hits {keg}";
                case SimEventKind.Parried: return e.Keg >= 0 ? $"{u} parries the blast" : $"{u} parries {o}'s shot";
                case SimEventKind.Reflected: return $"RIPOSTE! {u} reflects the shot {e.Dir}";
                case SimEventKind.Damaged: return $"{u} takes {e.Amount} ({(DamageKind)e.Reason})";
                case SimEventKind.Reloaded: return $"{u} reloads";
                case SimEventKind.CaughtBreath: return $"{u} catches breath (+{e.Amount} grit)";
                case SimEventKind.Fizzled:
                    return $"*click* {u}'s {(ActionType)e.Amount} fizzles ({Reason((FizzleReason)e.Reason)})";
                case SimEventKind.KegThrown: return $"{u} lobs {keg} to {e.To}";
                case SimEventKind.KegLanded:
                    return (KegLanding)e.Reason == KegLanding.Impact ? $"{keg} lands on something and blows!" : $"{keg} lands at {e.To}";
                case SimEventKind.KegKicked: return $"{keg} slides to {e.To}";
                case SimEventKind.KegExploded: return e.Amount == 1 ? $"{keg} chain-explodes at {e.To}" : $"{keg} EXPLODES at {e.To}";
                case SimEventKind.CrateDestroyed: return $"a crate breaks at {e.To}";
                case SimEventKind.Grappled:
                    switch ((GrappleResult)e.Reason)
                    {
                        case GrappleResult.YankUnit: return $"{u} hooks {o} and yanks them to {e.To}";
                        case GrappleResult.PullSelf: return $"{u} hooks a wall and flies to {e.To}";
                        case GrappleResult.PullKeg: return $"{u} hooks {keg} and pulls it in";
                        default: return $"{u}'s hook misses";
                    }
                case SimEventKind.Swapped:
                    if (e.Amount > 0 && e.Other >= 0) return $"{u} swaps places with {o}";
                    return $"{u}'s switcheroo fails ({(SwitcherooFail)e.Reason})";
                case SimEventKind.BullRushed: return e.Other >= 0 ? $"{u} bull-rushes into {o}" : $"{u} bull-rushes to {e.To}";
                case SimEventKind.TileWarned: return $"ring {e.Amount} is about to crumble";
                case SimEventKind.TileCrumbled: return $"ring {e.Amount} crumbles";
                case SimEventKind.Died:
                    if ((DeathCause)e.Reason == DeathCause.Fell)
                    {
                        string hazard = board != null && !board.InBounds(e.To) ? "off the map" : water ? "into the water" : "into the pit";
                        bool attacker = e.Other >= 0 && e.Other != e.Unit && names != null && e.Other < names.Length;
                        if (!attacker) return $"{u} fell {hazard}!";
                        string verb = "sent"; // Valid historical credit, without guessing an old action.
                        if (log != null)
                            foreach (var cause in log.Events)
                            {
                                if (cause.Kind == SimEventKind.Died && cause.Unit == e.Unit) break;
                                if (cause.Kind == SimEventKind.Grappled && cause.Unit == e.Other && cause.Other == e.Unit)
                                    verb = "dragged";
                                else if ((cause.Kind == SimEventKind.Shoved || cause.Kind == SimEventKind.BullRushed) && cause.Unit == e.Other && cause.Other == e.Unit)
                                    verb = "pushed";
                                else if (cause.Kind == SimEventKind.ShotHit && cause.Unit == e.Unit && cause.Other == e.Other)
                                    verb = "knocked";
                            }
                        return $"{o} {verb} {u} {hazard}!";
                    }
                    if (e.Other >= 0 && e.Other != e.Unit) return $"{u} is down, killed by {o}";
                    return $"{u} is down";
                case SimEventKind.RoundEnded: return e.Unit >= 0 ? $"{u} is the last musketeer standing!" : "Draw! Nobody is left standing";
                default: return e.ToString();
            }
        }

        public static string Reason(FizzleReason r)
        {
            switch (r)
            {
                case FizzleReason.NotEnoughGrit: return "not enough grit";
                case FizzleReason.NotLoaded: return "musket not loaded";
                case FizzleReason.KegLimit: return "keg already active";
                case FizzleReason.OutOfRange: return "out of range";
                default: return "";
            }
        }

        static string Name(int slot, string[] names)
        {
            if (slot < 0)
                return "?";
            return names != null && slot < names.Length && !string.IsNullOrEmpty(names[slot]) ? names[slot] : $"P{slot + 1}";
        }
    }
}
