using System;
using System.Collections;
using System.Collections.Generic;
using Musketeers.Game;
using Musketeers.Sim;
using PrimeTween;
using UnityEngine;

namespace Musketeers.Presentation
{
    // Plays a resolved turn's EventLog back, grouped by tick then phase (§6 order), ~0.7 s per tick at 1×.
    // After every tick the views snap to the sim's state for that tick, so even an imperfect animation
    // never leaves the board wrong. Read-only: it never touches the live GameState.
    public sealed class PlaybackDirector : MonoBehaviour
    {
        [SerializeField] private BoardView _board;
        [SerializeField] private Fx _fx;

        public static readonly float[] Speeds = { 0.5f, 1f, 2f };
        public float Speed { get; set; } = 1f;
        public bool IsPlaying { get; private set; }
        public event Action<int> TickStarted;

        static readonly Color Red = new Color(1f, 0.3f, 0.3f);
        static readonly Color Gold = new Color(1f, 0.85f, 0.3f);
        static readonly Color Tracer = new Color(1f, 0.95f, 0.6f);
        static readonly Color Riposte = new Color(0.55f, 0.95f, 1f);
        static readonly Color Blast = new Color(1f, 0.55f, 0.15f, 0.85f);

        Coroutine _running;
        int _crowned = -1;
        readonly List<SimEvent> _group = new List<SimEvent>();

        public void Play(TurnPlayback p, int crowned, Action done)
        {
            Stop();
            _crowned = crowned;
            _running = StartCoroutine(Run(p, done));
        }

        public void Stop()
        {
            if (_running != null)
                StopCoroutine(_running);
            _running = null;
            Tween.StopAll(this);   // delayed At() callbacks would otherwise hit the next board
            IsPlaying = false;
        }

        // A round's opening (turn 1 TurnStart): nothing to animate except warned tiles.
        public void ShowState(GameState s, int crowned)
        {
            _crowned = crowned;
            _board.Sync(s, crowned);
        }

        float S(float seconds) => seconds / Mathf.Max(0.1f, Speed);

        IEnumerator Wait(float seconds)
        {
            if (seconds > 0f)
                yield return new WaitForSeconds(S(seconds));
        }

        IEnumerator Run(TurnPlayback p, Action done)
        {
            IsPlaying = true;
            for (int t = 0; t < p.TickCount; t++)
            {
                TickStarted?.Invoke(t);
                yield return PlayLog(p.TickLogs[t]);
                yield return Wait(0.08f);
                _board.Sync(p.AfterTick[t], _crowned);
                yield return Wait(0.12f);
            }
            if (p.Outcome.TurnStart != null && p.Outcome.TurnStart.Count > 0)
            {
                yield return PlayLog(p.Outcome.TurnStart);
                yield return Wait(0.2f);
            }
            _board.Sync(p.After, _crowned);
            IsPlaying = false;
            _running = null;
            done?.Invoke();
        }

        // Splits a log into phase groups (and Phase 2b into one group per ability, since those resolve
        // one after another) and plays each group, waiting for its longest animation.
        IEnumerator PlayLog(EventLog log)
        {
            int i = 0;
            while (i < log.Count)
            {
                _group.Clear();
                var phase = log[i].Phase;
                do
                {
                    _group.Add(log[i]);
                    i++;
                } while (i < log.Count && log[i].Phase == phase && !(phase == SimPhase.MovementAbilities && StartsAbility(log[i].Kind)));
                float d = PlayGroup(_group);
                yield return Wait(d);
            }
        }

        static bool StartsAbility(SimEventKind k) => k == SimEventKind.Grappled || k == SimEventKind.Swapped || k == SimEventKind.BullRushed;

        // Starts every animation in the group (some with a small delay) and returns how long they take.
        float PlayGroup(List<SimEvent> group)
        {
            float length = 0f;
            float delay = 0f;
            bool afterThrow = false;
            foreach (var e in group)
            {
                // Within a phase: reflected shots come after the shot they answer; blasts after the lob lands.
                if (e.Kind == SimEventKind.Reflected)
                    delay = Mathf.Max(delay, 0.22f);
                if (e.Kind == SimEventKind.KegThrown)
                    afterThrow = true;
                if (afterThrow && e.Kind == SimEventKind.KegExploded)
                    delay = Mathf.Max(delay, 0.32f);
                if (group[0].Phase == SimPhase.Shots && (e.Kind == SimEventKind.ShotHit || e.Kind == SimEventKind.Damaged ||
                                                         e.Kind == SimEventKind.Parried || e.Kind == SimEventKind.CrateDestroyed))
                    delay = Mathf.Max(delay, 0.12f);
                float d = PlayEvent(e, delay);
                length = Mathf.Max(length, delay + d);
            }
            return length;
        }

        void At(float delay, Action a)
        {
            if (delay <= 0f)
                a();
            else
                Tween.Delay(this, S(delay), _ => a());
        }

        // Returns the animation's length in 1× seconds.
        float PlayEvent(SimEvent e, float delay)
        {
            var u = _board.Unit(e.Unit);
            var o = _board.Unit(e.Other);
            Vector3 to = BoardMath.ToWorld(e.To);
            Vector3 from = BoardMath.ToWorld(e.From);
            switch (e.Kind)
            {
                case SimEventKind.Moved:
                    if (u == null) return 0f;
                    u.SetFacing(e.Dir);
                    Slide(u, e.To, 0.26f, delay, Ease.OutQuad);
                    return 0.26f;
                case SimEventKind.Dashed:
                    if (u == null) return 0f;
                    u.SetFacing(e.Dir);
                    Slide(u, e.To, 0.18f, delay, Ease.OutCubic);
                    At(delay, () => _fx.Flash(from, new Color(1, 1, 1, 0.5f), 0.4f, 0.8f, S(0.2f), PlaceholderArt.SoftCircle));
                    return 0.2f;
                case SimEventKind.Leaped:
                    if (u == null) return 0f;
                    At(delay, () => Tween.PunchScale(u.Body, Vector3.one * 0.35f, S(0.2f), 1f));
                    return 0.2f;
                case SimEventKind.MoveBlocked:
                    if (u == null) return 0f;
                    Nudge(u.Body, to - BoardMath.ToWorld(u.GridPos), 0.15f, 0.2f, delay);
                    return 0.2f;
                case SimEventKind.Bonk:
                    if (u == null) return 0f;
                    Nudge(u.Body, to - u.transform.localPosition, 0.3f, 0.24f, delay);
                    At(delay + 0.1f, () =>
                    {
                        _fx.Flash(u.transform.localPosition, Color.white, 0.4f, 1.0f, S(0.2f), PlaceholderArt.Star);
                        _fx.Shake(0.08f, S(0.2f));
                        if (e.Unit < (e.Other < 0 ? 99 : e.Other))
                            _fx.Popup("BONK!", u.transform.localPosition + Vector3.up * 0.3f, Color.white, S(0.7f), "popup-big");
                    });
                    return 0.28f;
                case SimEventKind.ParryRaised:
                    if (u == null) return 0f;
                    At(delay, () => u.SetParry(true));
                    return 0.12f;
                case SimEventKind.Shoved:
                {
                    if (u == null) return 0f;
                    u.SetFacing(e.Dir);
                    Nudge(u.Body, BoardMath.DirVector(e.Dir), 0.3f, 0.2f, delay);
                    if (e.Other < 0 && e.Keg < 0)
                        At(delay, () => _fx.Popup("whiff", to, new Color(0.8f, 0.8f, 0.8f), S(0.5f)));
                    return 0.2f;
                }
                case SimEventKind.Pushed:
                    if (u == null) return 0f;
                    Slide(u, e.To, 0.2f, delay + 0.06f, Ease.OutQuad);
                    return 0.28f;
                case SimEventKind.GuardBreak:
                    if (u == null) return 0f;
                    At(delay, () =>
                    {
                        u.SetParry(false);
                        _fx.Flash(u.transform.localPosition, Riposte, 1f, 1.5f, S(0.3f));
                        _fx.Popup("GUARD BREAK!", u.transform.localPosition + Vector3.up * 0.4f, Riposte, S(0.9f), "popup-big");
                    });
                    return 0.3f;
                case SimEventKind.Squeezed:
                    if (u == null) return 0f;
                    At(delay, () =>
                    {
                        Tween.PunchScale(u.Body, new Vector3(-0.3f, 0.3f, 0), S(0.3f), 2f);
                        _fx.Popup("SQUEEZED", u.transform.localPosition + Vector3.up * 0.4f, Color.white, S(0.7f));
                    });
                    return 0.3f;
                case SimEventKind.WallSlam:
                    if (u == null) return 0f;
                    At(delay + 0.2f, () =>
                    {
                        _fx.Flash((u.transform.localPosition + to) * 0.5f, Color.white, 0.3f, 0.8f, S(0.2f), PlaceholderArt.Star);
                        _fx.Shake(0.1f, S(0.2f));
                    });
                    Nudge(u.Body, to - BoardMath.ToWorld(u.GridPos), 0.25f, 0.18f, delay + 0.2f);
                    return 0.4f;
                case SimEventKind.Fell:
                    if (u != null)
                    {
                        u.SetGridPosLogical(e.To);
                        At(delay, () =>
                        {
                            Tween.LocalPosition(u.transform, to, S(0.15f));
                            Tween.Scale(u.Body, 0.2f, S(0.45f), Ease.InBack, startDelay: S(0.12f));
                            Tween.LocalRotation(u.Body, Quaternion.Euler(0, 0, 170f), S(0.45f), startDelay: S(0.12f));
                            _fx.Flash(to, new Color(0.4f, 0.6f, 1f, 0.8f), 0.3f, 0.9f, S(0.45f));
                        });
                        return 0.6f;
                    }
                    if (e.Keg >= 0)
                    {
                        var kv = _board.Keg(e.Keg);
                        if (kv == null) return 0f;
                        At(delay, () =>
                        {
                            Tween.LocalPosition(kv.transform, to, S(0.12f));
                            Tween.Scale(kv.transform, 0.1f, S(0.35f), Ease.InBack, startDelay: S(0.1f));
                        });
                        return 0.45f;
                    }
                    return 0f;
                case SimEventKind.ShotFired:
                {
                    if (u == null) return 0f;
                    u.SetFacing(e.Dir);
                    bool dc = (ShotKind)e.Reason == ShotKind.DoubleCharge;
                    Vector3 start = from + BoardMath.DirVector(e.Dir) * 0.35f;
                    At(delay, () =>
                    {
                        u.SetLoaded(false);
                        _fx.Flash(start, Gold, 0.2f, 0.6f, S(0.15f), PlaceholderArt.Star);
                        _fx.Tracer(start, to, dc ? Blast : Tracer, dc ? 0.14f : 0.07f, S(0.4f));
                        Nudge(u.Body, -BoardMath.DirVector(e.Dir), 0.1f, 0.15f, 0f);
                    });
                    return 0.4f;
                }
                case SimEventKind.ShotHit:
                    At(delay, () => _fx.Flash(to, Color.white, 0.2f, 0.7f, S(0.2f), PlaceholderArt.Star));
                    return 0.2f;
                case SimEventKind.Parried:
                    if (u == null) return 0f;
                    At(delay, () =>
                    {
                        _fx.Flash(u.transform.localPosition, Riposte, 0.8f, 1.4f, S(0.25f));
                        _fx.Popup(e.Keg >= 0 ? "BLOCKED" : "PARRY!", u.transform.localPosition + Vector3.up * 0.4f, Riposte, S(0.7f));
                    });
                    return 0.25f;
                case SimEventKind.Reflected:
                    At(delay, () =>
                    {
                        _fx.Tracer(from, to, Riposte, 0.08f, S(0.4f));
                        _fx.Popup("RIPOSTE!", from + Vector3.up * 0.55f, Riposte, S(0.9f), "popup-big");
                    });
                    return 0.4f;
                case SimEventKind.Damaged:
                    if (u == null) return 0f;
                    At(delay, () =>
                    {
                        u.SetHp(Mathf.Max(0, u.Hp - e.Amount));
                        u.Flash(Red, S(0.3f));
                        Tween.PunchScale(u.Body, Vector3.one * 0.15f, S(0.2f), 1f);
                        _fx.Popup("-" + e.Amount, u.transform.localPosition + Vector3.right * 0.25f, Red, S(0.8f), "popup-dmg");
                    });
                    return 0.3f;
                case SimEventKind.Reloaded:
                    if (u == null) return 0f;
                    At(delay, () =>
                    {
                        u.SetLoaded(true);
                        _fx.Popup("reload", u.transform.localPosition, new Color(0.9f, 0.9f, 0.8f), S(0.6f));
                    });
                    return 0.2f;
                case SimEventKind.CaughtBreath:
                    if (u == null) return 0f;
                    At(delay, () => _fx.Popup("+" + e.Amount + " grit", u.transform.localPosition, Gold, S(0.6f)));
                    return 0.2f;
                case SimEventKind.Fizzled:
                    if (u == null) return 0f;
                    At(delay, () =>
                    {
                        _fx.Popup("*click*", u.transform.localPosition + Vector3.up * 0.2f, new Color(0.75f, 0.75f, 0.78f), S(0.8f));
                        Tween.ShakeLocalPosition(u.Body, new Vector3(0.05f, 0, 0), S(0.2f), 20f);
                    });
                    return 0.25f;
                case SimEventKind.KegThrown:
                {
                    var kv = _board.Keg(e.Keg) ?? _board.SpawnKeg(e.Keg, e.Unit, e.From);
                    kv.SetGridPos(e.From);
                    kv.SetGridPosLogical(e.To);
                    kv.ShowFuse(false);
                    kv.transform.localScale = Vector3.one * 0.5f;
                    At(delay, () =>
                    {
                        Tween.LocalPosition(kv.transform, to, S(0.3f), Ease.InOutQuad);
                        Tween.Scale(kv.transform, 1.3f, S(0.15f), Ease.OutQuad, 2, CycleMode.Yoyo);
                    });
                    return 0.32f;
                }
                case SimEventKind.KegLanded:
                {
                    var kv = _board.Keg(e.Keg);
                    At(delay + 0.3f, () =>
                    {
                        if (kv != null)
                        {
                            kv.transform.localScale = Vector3.one;
                            kv.ShowFuse(true);
                        }
                        _fx.Flash(to, new Color(1, 1, 1, 0.6f), 0.4f, 0.9f, S(0.2f), PlaceholderArt.SoftCircle);
                        if ((KegLanding)e.Reason == KegLanding.Impact)
                            _fx.Popup("DIRECT HIT!", to + Vector3.up * 0.4f, Blast, S(0.8f));
                    });
                    return 0.35f;
                }
                case SimEventKind.KegKicked:
                {
                    var kv = _board.Keg(e.Keg);
                    if (kv == null) return 0f;
                    kv.SetGridPosLogical(e.To);
                    At(delay + 0.05f, () => Tween.LocalPosition(kv.transform, to, S(0.25f), Ease.OutQuad));
                    return 0.3f;
                }
                case SimEventKind.KegExploded:
                {
                    var kv = _board.Keg(e.Keg);
                    At(delay, () =>
                    {
                        if (kv != null)
                            Tween.Scale(kv.transform, 1.5f, S(0.12f), Ease.OutQuad);
                        _fx.Flash(to, Color.white, 0.6f, 2.2f, S(0.25f), PlaceholderArt.SoftCircle);
                        _fx.Shake(0.2f, S(0.3f));
                    });
                    At(delay + 0.1f, () =>
                    {
                        _board.RemoveKeg(e.Keg);
                        for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            var p = new Int2(e.To.X + dx, e.To.Y + dy);
                            if (BoardMath.InBounds(p))
                                _fx.TileBurst(BoardMath.ToWorld(p), Blast, S(0.5f));
                        }
                        _fx.Flash(to, Gold, 0.5f, 1.6f, S(0.35f), PlaceholderArt.Star);
                        _fx.Popup(e.Amount == 1 ? "CHAIN!" : "BOOM!", to + Vector3.up * 0.3f, Blast, S(0.9f), "popup-big");
                    });
                    return 0.6f;
                }
                case SimEventKind.CrateDestroyed:
                    At(delay, () =>
                    {
                        var prop = _board.Prop(e.To);
                        if (prop != null)
                        {
                            Tween.Scale(prop.transform, 0.2f, S(0.25f), Ease.InBack)
                                .OnComplete(() => _board.SetTile(e.To, TileType.Floor));
                        }
                        _fx.Flash(to, new Color(0.8f, 0.55f, 0.3f), 0.5f, 1.2f, S(0.3f), PlaceholderArt.Star);
                    });
                    return 0.3f;
                case SimEventKind.Grappled:
                {
                    if (u == null) return 0f;
                    u.SetFacing(e.Dir);
                    var result = (GrappleResult)e.Reason;
                    Vector3 hookEnd = result == GrappleResult.Miss || result == GrappleResult.PullSelf
                        ? (result == GrappleResult.PullSelf ? to + BoardMath.DirVector(e.Dir) : to)
                        : from;
                    var self = BoardMath.ToWorld(u.GridPos);
                    At(delay, () => _fx.Tracer(self, hookEnd, new Color(0.85f, 0.85f, 0.9f), 0.05f, S(0.45f)));
                    if (result == GrappleResult.YankUnit && o != null)
                    {
                        Slide(o, e.To, 0.25f, delay + 0.18f, Ease.InQuad);
                        return 0.45f;
                    }
                    if (result == GrappleResult.PullKeg)
                    {
                        var kv = _board.Keg(e.Keg);
                        if (kv != null)
                        {
                            kv.SetGridPosLogical(e.To);
                            At(delay + 0.18f, () => Tween.LocalPosition(kv.transform, to, S(0.25f), Ease.InQuad));
                        }
                        return 0.45f;
                    }
                    if (result == GrappleResult.PullSelf)
                    {
                        Slide(u, e.To, 0.25f, delay + 0.18f, Ease.InQuad);
                        return 0.45f;
                    }
                    At(delay + 0.2f, () => _fx.Popup("miss", to, new Color(0.8f, 0.8f, 0.8f), S(0.6f)));
                    return 0.4f;
                }
                case SimEventKind.Swapped:
                {
                    if (u == null) return 0f;
                    u.SetFacing(e.Dir);
                    if (e.Other < 0 && e.Keg < 0)
                    {
                        At(delay, () => _fx.Popup("miss", from + BoardMath.DirVector(e.Dir), new Color(0.8f, 0.8f, 0.8f), S(0.6f)));
                        return 0.3f;
                    }
                    At(delay, () =>
                    {
                        _fx.Flash(from, new Color(0.7f, 0.5f, 1f, 0.8f), 0.4f, 1.2f, S(0.35f), PlaceholderArt.Star);
                        _fx.Flash(to, new Color(0.7f, 0.5f, 1f, 0.8f), 0.4f, 1.2f, S(0.35f), PlaceholderArt.Star);
                    });
                    Slide(u, e.To, 0.3f, delay, Ease.InOutQuad);
                    if (o != null)
                    {
                        Slide(o, e.From, 0.3f, delay, Ease.InOutQuad);
                    }
                    else if (e.Keg >= 0)
                    {
                        var kv = _board.Keg(e.Keg);
                        if (kv != null)
                        {
                            kv.SetGridPosLogical(e.From);
                            At(delay, () => Tween.LocalPosition(kv.transform, from, S(0.3f), Ease.InOutQuad));
                        }
                    }
                    return 0.35f;
                }
                case SimEventKind.BullRushed:
                    if (u == null) return 0f;
                    u.SetFacing(e.Dir);
                    Slide(u, e.To, 0.22f, delay, Ease.InQuad);
                    At(delay, () => _fx.Tracer(from, to, new Color(0.85f, 0.7f, 0.5f, 0.6f), 0.35f, S(0.5f)));
                    return 0.24f;
                case SimEventKind.TileWarned:
                    At(delay, () => _board.SetWarned(e.To, true));
                    return 0.2f;
                case SimEventKind.TileCrumbled:
                {
                    var g = _board.Ground(e.To);
                    At(delay, () =>
                    {
                        if (g != null)
                            Tween.ShakeLocalPosition(g.transform, new Vector3(0.06f, 0.06f, 0), S(0.35f), 25f);
                    });
                    At(delay + 0.35f, () =>
                    {
                        _board.SetWarned(e.To, false);
                        _board.SetTile(e.To, TileType.Pit);
                        _fx.TileBurst(to, new Color(0.4f, 0.35f, 0.3f, 0.8f), S(0.4f));
                    });
                    return 0.75f;
                }
                case SimEventKind.Died:
                    if (u == null) return 0f;
                    At(delay + 0.1f, () =>
                    {
                        bool fell = (DeathCause)e.Reason == DeathCause.Fell;
                        if (!fell)
                            _fx.Flash(u.transform.localPosition, Red, 0.6f, 1.6f, S(0.4f), PlaceholderArt.Star);
                        _fx.Popup(fell ? "SPLASH!" : "DOWN!", u.transform.localPosition + Vector3.up * 0.45f, Red, S(1.1f), "popup-big");
                        if (!fell)
                            u.SetAlive(false, false);
                    });
                    return 0.5f;
                case SimEventKind.RoundEnded:
                    At(delay + 0.2f, () => _fx.Shake(0.15f, S(0.4f)));
                    return 0.6f;
                default:
                    return 0f;
            }
        }

        void Slide(UnitView u, Int2 to, float duration, float delay, Ease ease)
        {
            u.SetGridPosLogical(to);
            var target = BoardMath.ToWorld(to);
            At(delay, () =>
            {
                if ((u.transform.localPosition - target).sqrMagnitude > 0.0001f)
                    Tween.LocalPosition(u.transform, target, S(duration), ease);
            });
        }

        // Bump toward a point and back (bonks, lunges, recoil).
        void Nudge(Transform body, Vector3 toward, float distance, float duration, float delay)
        {
            Vector3 dir = toward.sqrMagnitude > 0.0001f ? toward.normalized : Vector3.zero;
            At(delay, () =>
            {
                body.localPosition = Vector3.zero;
                Tween.LocalPosition(body, dir * distance, S(duration * 0.5f), Ease.OutQuad, 2, CycleMode.Yoyo);
            });
        }
    }
}
