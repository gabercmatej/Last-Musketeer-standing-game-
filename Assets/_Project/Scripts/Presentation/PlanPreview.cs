using System.Collections.Generic;
using Musketeers.Game;
using Musketeers.Sim;
using UnityEngine;

namespace Musketeers.Presentation
{
    // Draws the plan being edited on the board (GAME_DESIGN §7): numbered ghosts where you'll be after
    // each tick, shot lines with their range, keg landing marker + 3×3 blast, shove arrows, hook/swap
    // lines and the bull rush path. Uses the client-side projection, so it's a promise, not a prophecy.
    public sealed class PlanPreview : MonoBehaviour
    {
        [SerializeField] private ArtLibrary _art;
        [SerializeField] private WorldLabels _labels;

        readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>();
        readonly List<WorldLabel> _labelPool = new List<WorldLabel>();
        int _used, _labelsUsed;

        static readonly Color Invalid = new Color(0.55f, 0.55f, 0.58f, 0.6f);
        static readonly Color ShotColor = new Color(1f, 0.9f, 0.35f, 0.9f);
        static readonly Color BlastColor = new Color(1f, 0.45f, 0.1f, 0.28f);

        public void Clear()
        {
            _used = 0;
            _labelsUsed = 0;
            Finish();
        }

        // hover: the pending action with the direction/target under the mouse, previewed in the cursor slot.
        public void Draw(PlanBuilder b, Color unitColor, PlannedAction? hover)
        {
            _used = 0;
            _labelsUsed = 0;
            var s = b.State;
            var cfg = b.Config;
            var plan = hover.HasValue ? b.PreviewWith(hover.Value) : b.Plan;
            var proj = hover.HasValue ? PlanProjection.Project(s, b.Slot, plan, cfg) : b.Projection;
            var start = s.Units[b.Slot].Pos;

            // Ghost positions: one ghost per distinct tile, labelled with every tick that ends there.
            var ghostTiles = new List<Int2>();
            var ghostTicks = new List<string>();
            for (int t = 0; t < TurnPlan.Length; t++)
            {
                var a = plan[t];
                bool previewing = hover.HasValue && t == b.EditTick;
                Color c = proj.Issues[t] != PlanIssue.None ? Invalid : previewing ? Color.Lerp(unitColor, Color.white, 0.4f) : unitColor;
                var from = proj.States[t];
                var to = proj.States[t + 1];
                if (a.Type == ActionType.Idle || !from.Alive)
                    continue;
                DrawAction(b, s, cfg, a, from.Pos, to.Pos, c, proj.Issues[t] != PlanIssue.None);
                if (to.Pos != from.Pos || a.Type == ActionType.Parry)
                {
                    int gi = ghostTiles.IndexOf(to.Pos);
                    if (gi < 0)
                    {
                        ghostTiles.Add(to.Pos);
                        ghostTicks.Add((t + 1).ToString());
                    }
                    else
                    {
                        ghostTicks[gi] += "," + (t + 1);
                    }
                }
            }

            for (int i = 0; i < ghostTiles.Count; i++)
            {
                var p = ghostTiles[i];
                var w = BoardMath.ToWorld(p);
                if (p != start)
                {
                    var g = Get(PlaceholderArt.Circle, new Color(unitColor.r, unitColor.g, unitColor.b, 0.38f), Layers.PreviewUnder + 2);
                    g.transform.localPosition = w;
                    g.transform.localScale = Vector3.one * 0.62f;
                    if (!proj.States[TurnPlan.Length].Alive && i == ghostTiles.Count - 1)
                        Label("X", w + new Vector3(0, 0.02f, 0), "ghost-dead");
                }
                Label(ghostTicks[i], w + new Vector3(0.3f, 0.3f, 0), "ghost-num");
            }

            // While choosing: direction arrows around the cursor tick's position, or the keg range.
            if (b.HasPending && !b.Locked)
            {
                var at = proj.States[b.EditTick].Pos;
                if (PlanBuilder.NeedsDirection(b.Pending))
                {
                    foreach (var d in DirExt.All)
                    {
                        var arrow = Get(PlaceholderArt.Triangle, new Color(1f, 1f, 1f, 0.55f), Layers.PreviewOver);
                        arrow.transform.localPosition = BoardMath.ToWorld(at) + BoardMath.DirVector(d) * 0.62f;
                        arrow.transform.localRotation = Quaternion.Euler(0, 0, BoardMath.DirAngle(d));
                        arrow.transform.localScale = Vector3.one * 0.26f;
                    }
                }
                else if (PlanBuilder.NeedsTarget(b.Pending))
                {
                    for (int y = 0; y < s.Board.Height; y++)
                    for (int x = 0; x < s.Board.Width; x++)
                    {
                        var p = new Int2(x, y);
                        if (b.TargetIssue(p) != PlanIssue.None)
                            continue;
                        var sq = Get(PlaceholderArt.Square, new Color(1f, 0.6f, 0.2f, 0.13f), Layers.PreviewUnder);
                        sq.transform.localPosition = BoardMath.ToWorld(p);
                        sq.transform.localScale = Vector3.one * 0.94f;
                    }
                }
            }
            Finish();
        }

        void DrawAction(PlanBuilder b, GameState s, GameConfig cfg, PlannedAction a, Int2 from, Int2 to, Color c, bool invalid)
        {
            Vector3 wf = BoardMath.ToWorld(from);
            Vector3 wt = BoardMath.ToWorld(to);
            Vector3 dv = BoardMath.DirVector(a.Dir);
            switch (a.Type)
            {
                case ActionType.Move:
                case ActionType.Dash:
                    if (from != to)
                        Line(wf, wt, c, a.Type == ActionType.Dash ? 0.12f : 0.07f, Layers.PreviewUnder + 1);
                    else
                        Arrow(wf + dv * 0.45f, a.Dir, Invalid, 0.25f);   // blocked: a stub where it would go
                    break;
                case ActionType.Fire:
                {
                    PlanProjection.TraceLine(s, b.Slot, from, a.Dir, cfg.FireRange, true, out Int2 end);
                    var sc = invalid ? Invalid : ShotColor;
                    Line(wf + dv * 0.3f, BoardMath.ToWorld(end), sc, 0.05f, Layers.PreviewOver);
                    Marker(BoardMath.ToWorld(end), sc);
                    break;
                }
                case ActionType.Shove:
                    Arrow(wf + dv * 0.75f, a.Dir, c, 0.42f);
                    break;
                case ActionType.Parry:
                {
                    var r = Get(PlaceholderArt.Ring, new Color(0.7f, 0.95f, 1f, invalid ? 0.3f : 0.8f), Layers.PreviewOver);
                    r.transform.localPosition = wf;
                    r.transform.localScale = Vector3.one * 0.95f;
                    break;
                }
                case ActionType.Keg:
                {
                    var target = a.Target;
                    var tw = BoardMath.ToWorld(target);
                    Line(wf, tw, new Color(1f, 0.6f, 0.2f, 0.5f), 0.04f, Layers.PreviewUnder + 1);
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        var p = new Int2(target.X + dx, target.Y + dy);
                        if (!s.Board.InBounds(p))
                            continue;
                        var sq = Get(PlaceholderArt.Square, invalid ? new Color(0.5f, 0.5f, 0.5f, 0.2f) : BlastColor, Layers.PreviewUnder);
                        sq.transform.localPosition = BoardMath.ToWorld(p);
                        sq.transform.localScale = Vector3.one * 0.96f;
                    }
                    var k = Get(PlaceholderArt.Keg, new Color(1f, 1f, 1f, 0.7f), Layers.PreviewOver);
                    k.transform.localPosition = tw;
                    k.transform.localScale = Vector3.one * 0.5f;
                    break;
                }
                case ActionType.Reload:
                    Label("reload", wf + new Vector3(0, -0.15f, 0), "ghost-tag");
                    break;
                case ActionType.CatchBreath:
                    Label("+1 grit", wf + new Vector3(0, -0.15f, 0), "ghost-tag");
                    break;
                case ActionType.Signature:
                    DrawSignature(b, s, cfg, a, from, to, c, invalid);
                    break;
            }
        }

        void DrawSignature(PlanBuilder b, GameState s, GameConfig cfg, PlannedAction a, Int2 from, Int2 to, Color c, bool invalid)
        {
            Vector3 wf = BoardMath.ToWorld(from);
            Vector3 dv = BoardMath.DirVector(a.Dir);
            var lineColor = invalid ? Invalid : Color.Lerp(c, Color.white, 0.3f);
            switch (b.Ability)
            {
                case Ability.Grappin:
                {
                    var hit = PlanProjection.TraceLine(s, b.Slot, from, a.Dir, cfg.GrappleRange, true, out Int2 end);
                    Line(wf, BoardMath.ToWorld(end), lineColor, 0.04f, Layers.PreviewOver);
                    Marker(BoardMath.ToWorld(end), lineColor);
                    if (hit == LineHit.Unit || hit == LineHit.Keg)
                        Arrow(BoardMath.ToWorld(from + a.Dir.ToInt2()) + dv * 0.3f, a.Dir.Opposite(), lineColor, 0.35f);
                    else if (hit == LineHit.Obstacle && to != from)
                        Line(wf, BoardMath.ToWorld(to), c, 0.12f, Layers.PreviewUnder + 1);
                    break;
                }
                case Ability.Switcheroo:
                {
                    // Chunk 3 replaces this with click-to-target; for now the target is the unit picked by direction.
                    bool valid = a.Unit != b.Slot && a.Unit < s.Units.Length && s.Units[a.Unit].Alive;
                    Int2 end = valid ? s.Units[a.Unit].Pos : from;
                    Line(wf, BoardMath.ToWorld(end), lineColor, 0.04f, Layers.PreviewOver);
                    if (valid)
                    {
                        var g = Get(PlaceholderArt.Circle, new Color(1f, 1f, 1f, 0.3f), Layers.PreviewUnder + 2);
                        g.transform.localPosition = wf;
                        g.transform.localScale = Vector3.one * 0.5f;
                        Label("swap", wf + new Vector3(0, -0.15f, 0), "ghost-tag");
                    }
                    else
                    {
                        Marker(BoardMath.ToWorld(end), Invalid);
                    }
                    break;
                }
                case Ability.BullRush:
                {
                    Line(wf, BoardMath.ToWorld(to), c, 0.16f, Layers.PreviewUnder + 1);
                    var next = to + a.Dir.ToInt2();
                    var victim = s.UnitAt(next);
                    if (victim != null && victim.Slot != b.Slot && from.Manhattan(next) <= cfg.BullRushDistance)
                    {
                        var vw = BoardMath.ToWorld(next);
                        Line(vw, vw + dv * cfg.BullRushPush, new Color(1f, 0.4f, 0.3f, 0.8f), 0.06f, Layers.PreviewOver);
                        Arrow(vw + dv * cfg.BullRushPush, a.Dir, new Color(1f, 0.4f, 0.3f, 0.9f), 0.3f);
                    }
                    break;
                }
                case Ability.TripleCharge:
                {
                    PlanProjection.TraceLine(s, b.Slot, from, a.Dir, cfg.FireRange, false, out Int2 end);
                    var sc = invalid ? Invalid : new Color(1f, 0.55f, 0.15f, 0.95f);
                    Line(wf + dv * 0.3f, BoardMath.ToWorld(end), sc, 0.1f, Layers.PreviewOver);
                    Marker(BoardMath.ToWorld(end), sc);
                    break;
                }
            }
        }

        void Line(Vector3 a, Vector3 b, Color c, float width, int order)
        {
            var sr = Get(PlaceholderArt.Pixel, c, order);
            Fx.PlaceLine(sr.transform, a, b, width);
        }

        void Arrow(Vector3 pos, Dir d, Color c, float scale)
        {
            var sr = Get(PlaceholderArt.Arrow, c, Layers.PreviewOver);
            sr.transform.localPosition = pos;
            sr.transform.localRotation = Quaternion.Euler(0, 0, BoardMath.DirAngle(d));
            sr.transform.localScale = Vector3.one * scale;
        }

        void Marker(Vector3 pos, Color c)
        {
            var sr = Get(PlaceholderArt.Ring, c, Layers.PreviewOver);
            sr.transform.localPosition = pos;
            sr.transform.localScale = Vector3.one * 0.35f;
        }

        SpriteRenderer Get(Sprite sprite, Color c, int order)
        {
            SpriteRenderer sr;
            if (_used < _pool.Count)
            {
                sr = _pool[_used];
            }
            else
            {
                sr = _art.MakeRenderer("preview", transform, sprite, c, order);
                _pool.Add(sr);
            }
            _used++;
            sr.enabled = true;
            sr.sprite = sprite;
            sr.color = c;
            sr.sortingOrder = order;
            sr.transform.localRotation = Quaternion.identity;
            sr.transform.localScale = Vector3.one;
            return sr;
        }

        void Label(string text, Vector3 world, string cssClass)
        {
            WorldLabel wl;
            if (_labelsUsed < _labelPool.Count)
            {
                wl = _labelPool[_labelsUsed];
            }
            else
            {
                wl = _labels.Create("", null);
                _labelPool.Add(wl);
            }
            _labelsUsed++;
            wl.Element.ClearClassList();
            wl.Element.AddToClassList("world-label");
            wl.Element.AddToClassList(cssClass);
            wl.Text = text;
            wl.World = world;
            wl.SetVisible(true);
        }

        void Finish()
        {
            for (int i = _used; i < _pool.Count; i++)
                _pool[i].enabled = false;
            for (int i = _labelsUsed; i < _labelPool.Count; i++)
                _labelPool[i].SetVisible(false);
        }
    }
}
