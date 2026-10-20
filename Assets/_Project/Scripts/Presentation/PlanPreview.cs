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
                if (proj.Contested[t])
                    ContestedMarker(to.Pos);
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
                if (b.PendingTakesDirection)
                {
                    foreach (var d in DirExt.All)
                    {
                        var arrow = Get(PlaceholderArt.Triangle, new Color(1f, 1f, 1f, 0.55f), Layers.PreviewOver);
                        arrow.transform.localPosition = BoardMath.ToWorld(at) + BoardMath.DirVector(d) * 0.62f;
                        arrow.transform.localRotation = Quaternion.Euler(0, 0, BoardMath.DirAngle(d));
                        arrow.transform.localScale = Vector3.one * 0.26f;
                    }
                }
                else if (b.PendingNeedsUnit)
                {
                    // Switcheroo: every musketeer you could swap with glows; click one.
                    for (int i = 0; i < s.Units.Length; i++)
                    {
                        if (!b.IsSwitcherooTarget(i))
                            continue;
                        var w = BoardMath.ToWorld(s.Units[i].Pos);
                        var glow = Get(PlaceholderArt.SoftCircle, new Color(0.75f, 0.55f, 1f, 0.75f), Layers.PreviewUnder + 2);
                        glow.transform.localPosition = w + new Vector3(0f, 0.2f, 0f);
                        glow.transform.localScale = new Vector3(1.5f, 2.0f, 1f);
                        var ring = Get(PlaceholderArt.Ring, new Color(0.9f, 0.8f, 1f, 0.95f), Layers.PreviewOver);
                        ring.transform.localPosition = w;
                        ring.transform.localScale = Vector3.one * 1.05f;
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
                    if (from != to && a.Type == ActionType.Dash && PlanProjection.DashHops(s, b.Slot, from, a.Dir))
                        HopArc(wf, wt, c);
                    else if (from != to)
                        Line(wf, wt, c, a.Type == ActionType.Dash ? 0.12f : 0.07f, Layers.PreviewUnder + 1);
                    else
                        Arrow(wf + dv * 0.45f, a.Dir, Invalid, 0.25f);   // blocked: a stub where it would go
                    break;
                case ActionType.Fire:
                {
                    var sc = invalid ? Invalid : ShotColor;
                    ShotRange(s, b.Slot, from, a.Dir, cfg.FireRange, true, sc, 0.05f);
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
                    // Two-headed swap arrow between where Matic stands then and the target's current tile.
                    bool valid = a.Unit != b.Slot && a.Unit < s.Units.Length && s.Units[a.Unit].Alive;
                    if (valid)
                    {
                        var tw = BoardMath.ToWorld(s.Units[a.Unit].Pos);
                        var sw = new Color(0.8f, 0.6f, 1f, 0.95f);
                        TwoWayArrow(wf, tw, invalid ? Invalid : sw);
                        Label("swap", (wf + tw) * 0.5f + new Vector3(0, 0.3f, 0), "ghost-tag");
                    }
                    break;
                }
                case Ability.BullRush:
                {
                    var rush = PlanProjection.BullRush(s, b.Slot, from, a.Dir, cfg.BullRushDistance, cfg.BullRushPush);
                    var rc = invalid ? Invalid : c;
                    if (rush.End != from)
                        Line(wf, BoardMath.ToWorld(rush.End), rc, 0.16f, Layers.PreviewUnder + 1);
                    if (rush.HasImpact)
                    {
                        var iw = BoardMath.ToWorld(rush.ImpactTile);
                        var hot = invalid ? Invalid : new Color(1f, 0.4f, 0.3f, 0.95f);
                        var x = Get(PlaceholderArt.Cross, hot, Layers.PreviewOver);
                        x.transform.localPosition = iw;
                        x.transform.localScale = Vector3.one * 0.55f;
                        if (rush.HitUnit && rush.Landing != rush.ImpactTile)
                        {
                            var lw = BoardMath.ToWorld(rush.Landing);
                            Line(iw, lw, hot, 0.07f, Layers.PreviewOver);
                            Arrow(lw - dv * 0.3f, a.Dir, hot, 0.38f);
                            Marker(lw, hot);
                            if (rush.Falls)
                                Label("falls!", lw + new Vector3(0, 0.35f, 0), "ghost-dead");
                            else if (rush.Slam)
                                Label("slam", lw + new Vector3(0, 0.35f, 0), "ghost-tag");
                        }
                    }
                    break;
                }
                case Ability.TripleCharge:
                {
                    var sc = invalid ? Invalid : new Color(1f, 0.55f, 0.15f, 0.95f);
                    var tiles = ShotRange(s, b.Slot, from, a.Dir, cfg.FireRange, true, sc, 0.1f);
                    if (tiles.Count > 0)
                    {
                        var last = tiles[tiles.Count - 1];
                        var lw = BoardMath.ToWorld(last);
                        // Three bullets, one after another, bunched at the end of the line.
                        for (int k = 0; k < cfg.TripleChargeShots; k++)
                        {
                            var dot = Get(PlaceholderArt.Circle, sc, Layers.PreviewOver + 1);
                            dot.transform.localPosition = lw - dv * (0.12f + 0.2f * k);
                            dot.transform.localScale = Vector3.one * 0.2f;
                        }
                        if (s.UnitAt(last) != null && s.UnitAt(last).Slot != b.Slot)
                        {
                            var pw = lw + dv * cfg.TripleChargePush;
                            Arrow(lw + dv * 0.5f, a.Dir, sc, 0.34f);
                            Label("push " + cfg.TripleChargePush, pw + new Vector3(0, 0.3f, 0), "ghost-tag");
                        }
                    }
                    break;
                }
            }
        }

        // Fire / Triple Charge: tint exactly the tiles the shot reaches (PlanProjection.ShotTiles) and draw
        // the line to the last of them, so nothing is drawn past range or past the thing that stops it.
        List<Int2> ShotRange(GameState s, int self, Int2 from, Dir dir, int range, bool stopAtUnits, Color c, float width)
        {
            var tiles = PlanProjection.ShotTiles(s, self, from, dir, range, stopAtUnits);
            var fill = new Color(c.r, c.g, c.b, 0.3f);
            foreach (var t in tiles)
            {
                var sq = Get(PlaceholderArt.Square, fill, Layers.PreviewUnder);
                sq.transform.localPosition = BoardMath.ToWorld(t);
                sq.transform.localScale = Vector3.one * 0.94f;
            }
            Vector3 wf = BoardMath.ToWorld(from);
            Vector3 dv = BoardMath.DirVector(dir);
            if (tiles.Count > 0)
            {
                Line(wf + dv * 0.3f, BoardMath.ToWorld(tiles[tiles.Count - 1]), c, width, Layers.PreviewOver);
                Marker(BoardMath.ToWorld(tiles[tiles.Count - 1]), c);
            }
            return tiles;
        }

        // Orange ring with crossed blades: another musketeer may be standing on this tile when you arrive.
        void ContestedMarker(Int2 tile)
        {
            var w = BoardMath.ToWorld(tile);
            var orange = new Color(1f, 0.55f, 0.1f, 0.95f);
            var ring = Get(PlaceholderArt.Ring, orange, Layers.PreviewOver);
            ring.transform.localPosition = w;
            ring.transform.localScale = Vector3.one * 1.0f;
            var x = Get(PlaceholderArt.Cross, orange, Layers.PreviewOver + 1);
            x.transform.localPosition = w + new Vector3(0.28f, 0.28f, 0f);
            x.transform.localScale = Vector3.one * 0.3f;
            Label("contested", w + new Vector3(0, -0.45f, 0), "ghost-tag");
        }

        // A hop over something: dotted parabola from a to b.
        void HopArc(Vector3 a, Vector3 b, Color c)
        {
            const int Dots = 9;
            for (int i = 0; i <= Dots; i++)
            {
                float t = i / (float)Dots;
                float arc = Mathf.Sin(t * Mathf.PI);
                // Vertical hops bulge sideways (up would run along the line); horizontal ones bulge up.
                Vector3 bulge = Mathf.Abs(b.y - a.y) > Mathf.Abs(b.x - a.x) ? Vector3.right * 0.4f : Vector3.up * 0.5f;
                var p = Vector3.Lerp(a, b, t) + bulge * arc;
                var dot = Get(PlaceholderArt.Circle, c, Layers.PreviewOver);
                dot.transform.localPosition = p;
                dot.transform.localScale = Vector3.one * (0.09f + 0.1f * arc);
            }
        }

        void TwoWayArrow(Vector3 a, Vector3 b, Color c)
        {
            Vector3 v = b - a;
            if (v.sqrMagnitude < 0.0001f)
                return;
            Vector3 n = v.normalized;
            Line(a + n * 0.3f, b - n * 0.3f, c, 0.09f, Layers.PreviewOver);
            float ang = Mathf.Atan2(n.y, n.x) * Mathf.Rad2Deg - 90f;
            HeadAngle(b - n * 0.3f, ang, c, 0.5f);
            HeadAngle(a + n * 0.3f, ang + 180f, c, 0.5f);
        }

        void HeadAngle(Vector3 pos, float angle, Color c, float scale)
        {
            var sr = Get(PlaceholderArt.Triangle, c, Layers.PreviewOver + 1);
            sr.transform.localPosition = pos;
            sr.transform.localRotation = Quaternion.Euler(0, 0, angle);
            sr.transform.localScale = Vector3.one * scale;
        }

        void ArrowAngle(Vector3 pos, float angle, Color c, float scale)
        {
            var sr = Get(PlaceholderArt.Arrow, c, Layers.PreviewOver);
            sr.transform.localPosition = pos;
            sr.transform.localRotation = Quaternion.Euler(0, 0, angle);
            sr.transform.localScale = Vector3.one * scale;
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
