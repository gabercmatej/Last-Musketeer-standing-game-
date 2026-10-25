using System;
using System.Collections;
using System.Collections.Generic;
using Musketeers.Data;
using Musketeers.Game;
using Musketeers.Sim;
using PrimeTween;
using UnityEngine;

namespace Musketeers.Presentation
{
    // Plays a resolved turn's EventLog back, grouped by tick then phase (§6 order), ~0.7 s per tick at 1×,
    // with Matej's FX sprites, poses, sounds and voice lines. After every tick the views snap to the sim's
    // state for that tick, so even an imperfect animation never leaves the board wrong. Read-only: it never
    // touches the live GameState.
    public sealed partial class PlaybackDirector : MonoBehaviour
    {
        [SerializeField] private BoardView _board;
        [SerializeField] private Fx _fx;
        [SerializeField] private AudioService _audio;

        public static readonly float[] Speeds = { 0.5f, 1f, 2f };
        public float Speed { get; set; } = 1f;
        public bool IsPlaying { get; private set; }
        public event Action<int> TickStarted;
        public event Action<Callout> Announced;   // the HUD shows the banner
        public event Action<int, int, int> PointsScored;   // scorer, victim, amount: the score box updates and pops (FIXES_4E §8.6)

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
            _replaying = false;
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
                var callout = Callouts.Top(p.TickLogs[t], _crowned);
                if (callout != Callout.None)
                    At(0.45f, () => Announce(callout));
                yield return PlayLog(p.TickLogs[t]);
                yield return Wait(0.08f);
                _board.Sync(p.AfterTick[t], _crowned);
                yield return Wait(0.12f);
            }
            if (p.Outcome.TurnStart != null && p.Outcome.TurnStart.Count > 0)
            {
                var callout = Callouts.Top(p.Outcome.TurnStart, _crowned, turnStart: true);
                if (callout != Callout.None)
                    At(0.6f, () => Announce(callout));
                yield return PlayLog(p.Outcome.TurnStart);
                yield return Wait(0.2f);
            }
            _board.Sync(p.After, _crowned);
            IsPlaying = false;
            _running = null;
            done?.Invoke();
        }

        public void Announce(Callout c)
        {
            if (Callouts.Voiced(c))
                _audio?.Announce(Callouts.Clip(c));
            Announced?.Invoke(c);
        }

        // Splits a log into phase groups (and Phase 2b into one group per ability, since those resolve
        // one after another) and plays each group, waiting for its longest animation.
        IEnumerator PlayLog(EventLog log)
        {
            int indicated = 0;
            int i = 0;
            while (i < log.Count)
            {
                _group.Clear();
                var phase = log[i].Phase;
                do
                {
                    var activation = log[i];
                    // A Triple Shot produces three shot events in this tick: announce it once.
                    if ((activation.Kind == SimEventKind.BullRushed || StartsTripleChargeShot(activation)) &&
                        activation.Unit >= 0 && activation.Unit < 4 && (indicated & (1 << activation.Unit)) == 0)
                    {
                        indicated |= 1 << activation.Unit;
                        var unit = _board.Unit(activation.Unit);
                        if (unit != null) _fx.AbilityIndicator(unit.transform,
                            activation.Kind == SimEventKind.BullRushed ? "FX/bull_rush" : "FX/triple_shot", S(1.15f));
                    }
                    _group.Add(log[i]);
                    i++;
                } while (i < log.Count && log[i].Phase == phase && !(phase == SimPhase.MovementAbilities && StartsAbility(log[i].Kind))
                         && !(phase == SimPhase.Shots && StartsTripleChargeShot(log[i])));
                float d = PlayGroup(_group);
                yield return Wait(d);
            }
        }

        // Triple Charge shots fire one after another on the live state, so each plays as its own group.
        static bool StartsTripleChargeShot(SimEvent e) => e.Kind == SimEventKind.ShotFired && (ShotKind)e.Reason == ShotKind.TripleCharge;

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
                    delay = Mathf.Max(delay, 0.36f);
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

        void Sfx(float delay, string key, float volume = 1f) => At(delay, () => _audio?.Sfx(key, volume));

        void Line(float delay, UnitView u, string line) => At(delay, () => { if (u != null) _audio?.Line(u.CharacterId, line); });

        static float DirX(Dir d) => d == Dir.E ? 1f : d == Dir.W ? -1f : 0f;

        // Returns the animation's length in 1× seconds.
        float PlayEvent(SimEvent e, float delay)
        {
            var u = _board.Unit(e.Unit);
            var o = _board.Unit(e.Other);
            Vector3 to = BoardMath.ToWorld(e.To);
            Vector3 from = BoardMath.ToWorld(e.From);
            Vector3 feet = new Vector3(0f, UnitView.FeetY, 0f);
            switch (e.Kind)
            {
                case SimEventKind.Moved:
                    if (u == null) return 0f;
                    u.SetFacing(e.Dir);
                    Slide(u, e.To, 0.26f, delay, Ease.OutQuad);
                    Hop(u, 0.08f, 0.26f, delay);
                    Sfx(delay, "SFX/actions/step");
                    return 0.26f;
                case SimEventKind.Dashed:
                    if (u == null) return 0f;
                    u.SetFacing(e.Dir);
                    Slide(u, e.To, 0.18f, delay, Ease.OutCubic);
                    At(delay, () => _fx.Dust(from + feet, DirX(e.Dir), 0.7f, S(0.4f)));
                    Sfx(delay, "SFX/actions/dash");
                    return 0.2f;
                case SimEventKind.Leaped:
                    if (u == null) return 0f;
                    Hop(u, 0.45f, 0.2f, delay);
                    Sfx(delay, "SFX/actions/leap");
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
                        _fx.Burst(GameArt.Stars, PlaceholderArt.Star, Color.white, u.transform.localPosition + new Vector3(0f, 0.95f, 0f), 0.3f, 0.75f, S(0.55f));
                        _fx.Shake(0.08f, S(0.2f));
                        if (e.Unit < (e.Other < 0 ? 99 : e.Other))
                            _fx.Popup("BONK!", u.transform.localPosition + Vector3.up * 0.6f, Color.white, S(0.7f), "popup-big");
                    });
                    Sfx(delay + 0.1f, "SFX/impacts/bonk");
                    return 0.28f;
                case SimEventKind.ParryRaised:
                    if (u == null) return 0f;
                    At(delay, () => u.SetParry(true));
                    Sfx(delay, "SFX/actions/parry_ready");
                    return 0.12f;
                case SimEventKind.Shoved:
                {
                    if (u == null) return 0f;
                    u.SetFacing(e.Dir);
                    At(delay, () => u.FlashPose(PoseKind.Lunge, S(0.35f)));
                    Nudge(u.Body, BoardMath.DirVector(e.Dir), 0.3f, 0.2f, delay);
                    Sfx(delay, "SFX/actions/shove");
                    if (e.Other < 0 && e.Keg < 0)
                        At(delay, () => _fx.Popup("whiff", to, new Color(0.8f, 0.8f, 0.8f), S(0.5f)));
                    return 0.2f;
                }
                case SimEventKind.Pushed:
                    if (u == null) return 0f;
                    Slide(u, e.To, 0.2f, delay + 0.06f, Ease.OutQuad);
                    At(delay + 0.06f, () => _fx.Dust(from + feet, (e.To.X - e.From.X), 0.6f, S(0.35f)));
                    return 0.28f;
                case SimEventKind.GuardBreak:
                    if (u == null) return 0f;
                    At(delay, () =>
                    {
                        u.SetParry(false);
                        _fx.Burst(GameArt.Spark, PlaceholderArt.Star, Riposte, u.transform.localPosition + new Vector3(0f, 0.3f, 0f), 0.5f, 1.2f, S(0.4f));
                        _fx.Popup("GUARD BREAK!", u.transform.localPosition + Vector3.up * 0.7f, Riposte, S(0.9f), "popup-big");
                    });
                    Sfx(delay, "SFX/actions/guard_break");
                    return 0.3f;
                case SimEventKind.Squeezed:
                    if (u == null) return 0f;
                    At(delay, () =>
                    {
                        Tween.PunchScale(u.Body, new Vector3(-0.3f, 0.3f, 0), S(0.3f), 2f);
                        _fx.Popup("SQUEEZED", u.transform.localPosition + Vector3.up * 0.7f, Color.white, S(0.7f));
                    });
                    Sfx(delay, "SFX/impacts/squeeze");
                    return 0.3f;
                case SimEventKind.WallSlam:
                    if (u == null) return 0f;
                    At(delay + 0.2f, () =>
                    {
                        _fx.Burst(GameArt.Stars, PlaceholderArt.Star, Color.white, (u.transform.localPosition + to) * 0.5f + new Vector3(0f, 0.4f, 0f), 0.3f, 0.7f, S(0.5f));
                        _fx.Shake(0.1f, S(0.2f));
                    });
                    Nudge(u.Body, to - BoardMath.ToWorld(u.GridPos), 0.25f, 0.18f, delay + 0.2f);
                    Sfx(delay + 0.2f, "SFX/impacts/wall_slam");
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
                        });
                        At(delay + 0.35f, () => _fx.Burst(GameArt.Splash, PlaceholderArt.Ring, new Color(0.4f, 0.6f, 1f, 0.8f), to, 0.5f, 1.1f, S(0.6f)));
                        Sfx(delay, "SFX/impacts/fall_pit");
                        Sfx(delay + 0.35f, "SFX/impacts/splash", 0.7f);
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
                        At(delay + 0.3f, () => _fx.Burst(GameArt.Splash, PlaceholderArt.Ring, new Color(0.4f, 0.6f, 1f, 0.8f), to, 0.4f, 0.9f, S(0.5f)));
                        Sfx(delay + 0.3f, "SFX/impacts/splash", 0.6f);
                        return 0.45f;
                    }
                    return 0f;
                case SimEventKind.ShotFired:
                {
                    if (u == null) return 0f;
                    bool dc = (ShotKind)e.Reason == ShotKind.TripleCharge;
                    u.Aim(e.Dir);
                    Vector3 tip = u.BarrelTip(e.Dir);
                    Vector3 dv = BoardMath.DirVector(e.Dir);
                    Vector3 end = to + new Vector3(0f, UnitView.FeetY + 0.42f, 0f);
                    At(delay, () =>
                    {
                        u.SetLoaded(false);
                        u.Recoil(S(0.3f));
                        _fx.MuzzleFlash(tip, dv, S(dc ? 0.3f : 0.2f));
                        _fx.Smoke(tip + dv * 0.1f, dc ? 0.8f : 0.55f, S(0.8f));
                        _fx.Tracer(tip, end, dc ? Blast : Tracer, dc ? 0.14f : 0.06f, S(0.4f));
                        Nudge(u.Body, -dv, 0.1f, 0.15f, 0f);
                    });
                    Sfx(delay, dc ? "SFX/abilities/double_charge" : "SFX/actions/musket_fire");
                    At(delay + 0.6f, () => { if (u != null) u.Rest(); });
                    return 0.4f;
                }
                case SimEventKind.ShotHit:
                    At(delay, () => _fx.Burst(GameArt.Spark, PlaceholderArt.Star, Color.white, to + new Vector3(0f, 0.15f, 0f), 0.25f, 0.6f, S(0.3f)));
                    Sfx(delay, "SFX/impacts/hit");
                    return 0.2f;
                case SimEventKind.Parried:
                    if (u == null) return 0f;
                    At(delay, () =>
                    {
                        var front = new Vector3(u.Rig != null && !u.Rig.FacingRight ? -0.4f : 0.4f, 0.3f, 0f);
                        _fx.Burst(GameArt.Spark, PlaceholderArt.Star, Riposte, u.transform.localPosition + front, 0.5f, 1.3f, S(0.35f), 20f);
                        _fx.Flash(u.transform.localPosition + front, Color.white, 0.3f, 0.9f, S(0.2f), PlaceholderArt.SoftCircle);
                        u.Flash(Color.white, S(0.15f));
                        _fx.Popup(e.Keg >= 0 ? "BLOCKED" : "PARRY!", u.transform.localPosition + Vector3.up * 0.7f, Riposte, S(0.7f));
                    });
                    Sfx(delay, "SFX/actions/parry_clang");
                    return 0.25f;
                case SimEventKind.Reflected:
                {
                    Vector3 lift = new Vector3(0f, UnitView.FeetY + 0.42f, 0f);
                    At(delay, () =>
                    {
                        _fx.Tracer(from + lift, to + lift, Riposte, 0.08f, S(0.4f));
                        _fx.Popup("RIPOSTE!", from + Vector3.up * 0.8f, Riposte, S(0.9f), "popup-big");
                    });
                    Sfx(delay, "SFX/actions/riposte");
                    return 0.4f;
                }
                case SimEventKind.Damaged:
                    if (u == null) return 0f;
                    At(delay, () =>
                    {
                        u.SetHp(Mathf.Max(0, u.Hp - e.Amount));
                        u.Flash(Red, S(0.3f));
                        u.FlashPose(PoseKind.Hurt, S(0.5f));
                        Tween.PunchScale(u.Body, Vector3.one * 0.12f, S(0.2f), 1f);
                        _fx.Popup("-" + e.Amount, u.transform.localPosition + new Vector3(0.3f, 0.5f, 0f), Red, S(0.8f), "popup-dmg");
                    });
                    Line(delay, u, "hurt");
                    return 0.3f;
                case SimEventKind.Reloaded:
                    if (u == null) return 0f;
                    At(delay, () =>
                    {
                        u.SetLoaded(true);
                        u.HoldMusket(S(0.6f));
                        _fx.Popup("reload", u.transform.localPosition + Vector3.up * 0.3f, new Color(0.9f, 0.9f, 0.8f), S(0.6f));
                    });
                    Sfx(delay, "SFX/actions/reload");
                    return 0.2f;
                case SimEventKind.CaughtBreath:
                    if (u == null) return 0f;
                    At(delay, () => _fx.Popup("+" + e.Amount + " grit", u.transform.localPosition + Vector3.up * 0.3f, Gold, S(0.6f)));
                    Sfx(delay, "SFX/actions/catch_breath", 0.5f);
                    return 0.2f;
                case SimEventKind.Fizzled:
                    if (u == null) return 0f;
                    At(delay, () =>
                    {
                        _fx.Popup("*click*", u.transform.localPosition + Vector3.up * 0.5f, new Color(0.75f, 0.75f, 0.78f), S(0.8f));
                        Tween.ShakeLocalPosition(u.Body, new Vector3(0.05f, 0, 0), S(0.2f), 20f);
                    });
                    Sfx(delay, "SFX/actions/musket_click");
                    return 0.25f;
                case SimEventKind.KegThrown:
                {
                    var kv = _board.Keg(e.Keg) ?? _board.SpawnKeg(e.Keg, e.Unit, e.From);
                    kv.SetGridPos(e.From);
                    kv.SetGridPosLogical(e.To);
                    kv.ShowFuse(false);
                    kv.transform.localScale = Vector3.one * 0.6f;
                    if (u != null) u.SetFacing(e.To.X > e.From.X ? Dir.E : e.To.X < e.From.X ? Dir.W : u.Rig.FacingRight ? Dir.E : Dir.W);
                    At(delay, () =>
                    {
                        // A lob: straight across, plus an arc up and down.
                        Tween.Custom(0f, 1f, S(0.32f), t =>
                        {
                            if (kv == null) return;
                            kv.transform.localPosition = Vector3.Lerp(from, to, t) + new Vector3(0f, Mathf.Sin(t * Mathf.PI) * 0.9f, 0f);
                            kv.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, t);
                        }, Ease.Linear);
                    });
                    Sfx(delay, "SFX/actions/keg_throw");
                    return 0.34f;
                }
                case SimEventKind.KegLanded:
                {
                    var kv = _board.Keg(e.Keg);
                    bool impact = (KegLanding)e.Reason == KegLanding.Impact;
                    At(delay + 0.32f, () =>
                    {
                        if (kv != null)
                        {
                            kv.transform.localScale = Vector3.one;
                            kv.transform.localPosition = to;
                            kv.ShowFuse(true);
                        }
                        _fx.Dust(to + new Vector3(0f, -0.3f, 0f), 0f, 0.6f, S(0.35f));
                        if (impact)
                            _fx.Popup("DIRECT HIT!", to + Vector3.up * 0.7f, Blast, S(0.8f));
                    });
                    Sfx(delay + 0.32f, "SFX/actions/keg_land");
                    if (!impact)
                        Sfx(delay + 0.4f, "SFX/actions/fuse_hiss", 0.3f);
                    return 0.36f;
                }
                case SimEventKind.KegKicked:
                {
                    var kv = _board.Keg(e.Keg);
                    if (kv == null) return 0f;
                    kv.SetGridPosLogical(e.To);
                    At(delay + 0.05f, () => Tween.LocalPosition(kv.transform, to, S(0.25f), Ease.OutQuad));
                    Sfx(delay + 0.05f, "SFX/actions/keg_land", 0.7f);
                    return 0.3f;
                }
                case SimEventKind.KegExploded:
                {
                    var kv = _board.Keg(e.Keg);
                    At(delay, () =>
                    {
                        if (kv != null)
                            Tween.Scale(kv.transform, 1.4f, S(0.12f), Ease.OutQuad);
                        _fx.Shake(0.22f, S(0.35f));
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
                        _fx.Burst(GameArt.Explosion, PlaceholderArt.Star, Gold, to + new Vector3(0f, 0.1f, 0f), 1.2f, 2.9f, S(0.5f), UnityEngine.Random.Range(-20f, 20f), Ease.OutCubic);
                        _fx.Smoke(to + new Vector3(-0.35f, 0.1f, 0f), 0.9f, S(0.9f));
                        _fx.Smoke(to + new Vector3(0.35f, 0.2f, 0f), 0.8f, S(1f));
                        _fx.Popup(e.Amount == 1 ? "CHAIN!" : "BOOM!", to + Vector3.up * 0.6f, Blast, S(0.9f), "popup-big");
                    });
                    Sfx(delay + 0.1f, e.Amount == 1 ? "SFX/impacts/chain_explosion" : "SFX/impacts/explosion");
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
                        _fx.Dust(to, 0f, 0.9f, S(0.45f));
                    });
                    Sfx(delay, "SFX/impacts/crate_break");
                    // §3: the sim already rolled the crate's 0–3 reward (Amount = Grit actually gained).
                    if (e.Amount > 0 && u != null)
                    {
                        At(delay + 0.2f, () => _fx.Popup("+" + e.Amount + " Grit", u.transform.localPosition + Vector3.up * 1.1f, Gold, S(1f), "popup-big"));
                        Sfx(delay + 0.2f, "SFX/ui/plan_add", 0.9f);
                    }
                    return 0.3f;
                case SimEventKind.Grappled:
                {
                    if (u == null) return 0f;
                    u.SetFacing(e.Dir);
                    var result = (GrappleResult)e.Reason;
                    Vector3 hookEnd = result == GrappleResult.Miss || result == GrappleResult.PullSelf
                        ? (result == GrappleResult.PullSelf ? to + BoardMath.DirVector(e.Dir) : to)
                        : from;
                    var self = BoardMath.ToWorld(u.GridPos) + new Vector3(0f, 0.15f, 0f);
                    hookEnd += new Vector3(0f, 0.15f, 0f);
                    At(delay, () => _fx.Hook(self, hookEnd, S(0.18f), S(0.05f), S(0.25f), u.transform));
                    Sfx(delay, "SFX/abilities/grappin_throw");
                    if (result == GrappleResult.YankUnit && o != null)
                    {
                        Slide(o, e.To, 0.25f, delay + 0.2f, Ease.InQuad);
                        Sfx(delay + 0.2f, "SFX/abilities/grappin_yank");
                        return 0.48f;
                    }
                    if (result == GrappleResult.PullKeg)
                    {
                        var kv = _board.Keg(e.Keg);
                        if (kv != null)
                        {
                            kv.SetGridPosLogical(e.To);
                            At(delay + 0.2f, () => Tween.LocalPosition(kv.transform, to, S(0.25f), Ease.InQuad));
                        }
                        Sfx(delay + 0.2f, "SFX/abilities/grappin_yank");
                        return 0.48f;
                    }
                    if (result == GrappleResult.PullSelf)
                    {
                        Slide(u, e.To, 0.25f, delay + 0.2f, Ease.InQuad);
                        Sfx(delay + 0.2f, "SFX/abilities/grappin_yank");
                        return 0.48f;
                    }
                    At(delay + 0.2f, () => _fx.Popup("miss", to + Vector3.up * 0.3f, new Color(0.8f, 0.8f, 0.8f), S(0.6f)));
                    return 0.48f;
                }
                case SimEventKind.Swapped:
                {
                    if (u == null) return 0f;
                    Sfx(delay, "SFX/abilities/switcheroo");
                    if (e.Amount == 0)   // failed: warned tile, dead or invalid target
                    {
                        At(delay, () => _fx.Popup("fails", from + Vector3.up * 0.9f, new Color(0.8f, 0.8f, 0.8f), S(0.6f)));
                        return 0.3f;
                    }
                    At(delay, () =>
                    {
                        _fx.Burst(GameArt.SwapSwirl, PlaceholderArt.Star, new Color(0.7f, 0.5f, 1f, 0.8f), from + new Vector3(0f, 0.3f, 0f), 0.4f, 1.1f, S(0.45f), 0f);
                        _fx.Burst(GameArt.SwapSwirl, PlaceholderArt.Star, new Color(0.7f, 0.5f, 1f, 0.8f), to + new Vector3(0f, 0.3f, 0f), 0.4f, 1.1f, S(0.45f), 180f);
                    });
                    Slide(u, e.To, 0.3f, delay, Ease.InOutQuad);
                    if (o != null)
                        Slide(o, e.From, 0.3f, delay, Ease.InOutQuad);
                    return 0.35f;
                }
                case SimEventKind.BullRushed:
                    if (u == null) return 0f;
                    u.SetFacing(e.Dir);
                    At(delay, () => u.FlashPose(PoseKind.Lunge, S(0.45f)));
                    Slide(u, e.To, 0.22f, delay, Ease.InQuad);
                    At(delay, () =>
                    {
                        _fx.Dust(from + feet, DirX(e.Dir), 0.8f, S(0.5f));
                        _fx.Dust(Vector3.Lerp(from, to, 0.5f) + feet, DirX(e.Dir), 0.7f, S(0.45f));
                    });
                    Sfx(delay, "SFX/abilities/bull_rush");
                    if (e.Other >= 0 || e.Keg >= 0)
                        Sfx(delay + 0.22f, "SFX/abilities/bull_rush_hit");
                    return 0.24f;
                case SimEventKind.TileWarned:
                    At(delay, () => _board.SetWarned(e.To, true));
                    Sfx(delay, "SFX/match/tile_warn", 0.6f);
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
                        _fx.Dust(to, 0f, 0.8f, S(0.45f));
                    });
                    Sfx(delay + 0.3f, "SFX/match/tile_crumble", 0.7f);
                    return 0.75f;
                }
                case SimEventKind.Died:
                    if (u == null) return 0f;
                    At(delay + 0.1f, () =>
                    {
                        bool fell = (DeathCause)e.Reason == DeathCause.Fell;
                        if (!fell)
                            _fx.Burst(GameArt.Stars, PlaceholderArt.Star, Red, u.transform.localPosition + new Vector3(0f, 0.6f, 0f), 0.5f, 1.2f, S(0.5f));
                        _fx.Popup(fell ? "SPLASH!" : "DOWN!", u.transform.localPosition + Vector3.up * 0.8f, Red, S(1.1f), "popup-big");
                        if (!fell)
                            u.SetAlive(false, false, instant: false);
                    });
                    Sfx(delay + 0.1f, "SFX/impacts/death");
                    Line(delay + 0.15f, u, "death");
                    return 0.5f;
                case SimEventKind.PointsScored:
                    if (_replaying) return 0f;
                    // Right after the victim's Died event: the live score changes at the moment of the kill.
                    At(delay + 0.2f, () => PointsScored?.Invoke(e.Unit, e.Other, e.Amount));
                    return 0.25f;
                case SimEventKind.RoundEnded:
                    if (_replaying) return 0f;
                    At(delay + 0.2f, () => _fx.Shake(0.15f, S(0.4f)));
                    if (u != null)
                    {
                        At(delay + 0.3f, () => { if (u.Alive) u.SetPose(PoseKind.Victory); });
                        Line(delay + 0.5f, u, "victory");
                    }
                    Sfx(delay + 0.2f, "SFX/match/round_win");
                    return 0.8f;
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

        // A small hop while walking (or a big one over a pit).
        void Hop(UnitView u, float height, float duration, float delay)
        {
            At(delay, () =>
            {
                var body = u.Body;
                body.localPosition = Vector3.zero;
                Tween.LocalPositionY(body, height, S(duration * 0.5f), Ease.OutQuad, 2, CycleMode.Yoyo);
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
