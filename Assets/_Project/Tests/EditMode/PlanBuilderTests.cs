using Musketeers.Game;
using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    // The planning UI's projection of position, Grit, musket and kegs (Day 3), and plan editing.
    public class PlanBuilderTests
    {
        static PlanBuilder Builder(Scenario s, char c = 'A')
        {
            var b = new PlanBuilder();
            b.Begin(s.State, Scenario.Slot(c), s.Config);
            return b;
        }

        [Test]
        public void Grit_RunsOut_ThirdDash_IsGreyedOut()
        {
            var b = Builder(Scenario.From("A........B"));   // StartGrit 2
            Assert.That(b.Choose(ActionType.Dash) && b.CommitDirection(Dir.E));
            Assert.That(b.Choose(ActionType.Dash) && b.CommitDirection(Dir.E));
            Assert.That(b.IssueFor(ActionType.Dash), Is.EqualTo(PlanIssue.NoGrit));
            Assert.That(b.IssueFor(ActionType.Parry), Is.EqualTo(PlanIssue.NoGrit));
            Assert.That(b.IssueFor(ActionType.Move), Is.EqualTo(PlanIssue.None));
            Assert.That(b.Choose(ActionType.Dash), Is.False);
            Assert.That(b.Projection.States[2].Pos, Is.EqualTo(new Int2(4, 0)));
        }

        [Test]
        public void CatchBreath_MakesTheSignatureAffordable_NextTick()
        {
            var b = Builder(Scenario.From("A...B").WithGrit('A', 4));   // signature costs 5
            Assert.That(b.IssueFor(ActionType.Signature), Is.EqualTo(PlanIssue.NoGrit));
            Assert.That(b.Choose(ActionType.CatchBreath));
            Assert.That(b.IssueFor(ActionType.Signature), Is.EqualTo(PlanIssue.None));
        }

        [Test]
        public void Fire_Unloads_UntilReload()
        {
            var b = Builder(Scenario.From("A...B"));
            Assert.That(b.Choose(ActionType.Fire) && b.CommitDirection(Dir.E));
            Assert.That(b.IssueFor(ActionType.Fire), Is.EqualTo(PlanIssue.Unloaded));
            Assert.That(b.Choose(ActionType.Reload));
            Assert.That(b.IssueFor(ActionType.Fire), Is.EqualTo(PlanIssue.None));
        }

        [Test]
        public void StartUnloaded_FireGreyedOut()
        {
            var b = Builder(Scenario.From("A...B", new GameConfig { StartLoaded = false }));
            Assert.That(b.IssueFor(ActionType.Fire), Is.EqualTo(PlanIssue.Unloaded));
        }

        [Test]
        public void Keg_AlreadyActive_FromTheBoard_OrEarlierInThePlan()
        {
            var s = Scenario.From("A..K..B").WithGrit('A', 5).WithKegOwner(0, 'A');
            Assert.That(Builder(s).IssueFor(ActionType.Keg), Is.EqualTo(PlanIssue.KegActive));

            var b = Builder(Scenario.From("A.....B").WithGrit('A', 5));
            Assert.That(b.Choose(ActionType.Keg) && b.CommitTarget(new Int2(2, 0)));
            Assert.That(b.IssueFor(ActionType.Keg), Is.EqualTo(PlanIssue.KegActive));
        }

        [Test]
        public void KegTarget_OutOfRange_IsRefused()
        {
            var b = Builder(Scenario.From("A.....B"));
            Assert.That(b.Choose(ActionType.Keg));
            Assert.That(b.TargetIssue(new Int2(4, 0)), Is.EqualTo(PlanIssue.OutOfRange));
            Assert.That(b.TargetIssue(new Int2(-1, 0)), Is.EqualTo(PlanIssue.OutOfRange));
            Assert.That(b.CommitTarget(new Int2(4, 0)), Is.False);
            Assert.That(b.CommitTarget(new Int2(3, 0)), Is.True);
        }

        [Test]
        public void Movement_StopsAtPillars_Units_AndFallsIntoPits()
        {
            var b = Builder(Scenario.From("A#.\n...\n.~B"));
            Assert.That(b.Choose(ActionType.Move) && b.CommitDirection(Dir.E));   // pillar: stays
            Assert.That(b.Projection.States[1].Pos, Is.EqualTo(new Int2(0, 2)));
            Assert.That(b.Choose(ActionType.Dash) && b.CommitDirection(Dir.S));
            Assert.That(b.Projection.States[2].Pos, Is.EqualTo(new Int2(0, 0)));
            Assert.That(b.Choose(ActionType.Move) && b.CommitDirection(Dir.E));   // into the pit
            Assert.That(b.Projection.States[3].Alive, Is.False);
        }

        [Test]
        public void ActionsAfterAFizzle_AreFlagged_AndDeathFlagsTheRest()
        {
            var s = Scenario.From("A~...B");
            var plan = new TurnPlan(0) { A0 = PlannedAction.Fire(Dir.E), A1 = PlannedAction.Fire(Dir.E), A2 = PlannedAction.Move(Dir.E) };
            var p = PlanProjection.Project(s.State, 0, plan, s.Config);
            Assert.That(p.Issues[0], Is.EqualTo(PlanIssue.None));
            Assert.That(p.Issues[1], Is.EqualTo(PlanIssue.Unloaded));
            Assert.That(p.Issues[2], Is.EqualTo(PlanIssue.None));
            Assert.That(p.States[3].Alive, Is.False);

            var b = Builder(s);
            Assert.That(b.Choose(ActionType.Move) && b.CommitDirection(Dir.E));
            Assert.That(b.IssueFor(ActionType.Reload), Is.EqualTo(PlanIssue.Dead));
        }

        [Test]
        public void Undo_ClearsTheLastSlot_SelectSlot_Replaces()
        {
            var b = Builder(Scenario.From("A...B"));
            b.Choose(ActionType.CatchBreath);
            b.Choose(ActionType.CatchBreath);
            b.Choose(ActionType.CatchBreath);
            Assert.That(b.IsFull);
            Assert.That(b.IssueFor(ActionType.Move), Is.EqualTo(PlanIssue.PlanFull));
            b.Undo();
            Assert.That(b.Cursor, Is.EqualTo(2));
            Assert.That(b.Plan[2].Type, Is.EqualTo(ActionType.Idle));
            b.SelectSlot(0);
            Assert.That(b.Choose(ActionType.Parry));
            Assert.That(b.Plan[0].Type, Is.EqualTo(ActionType.Parry));
            Assert.That(b.Cursor, Is.EqualTo(2), "the cursor moves to the next empty slot");
        }

        [Test]
        public void Editing_Unlocks()
        {
            var b = Builder(Scenario.From("A...B"));
            b.SetLocked(true);
            b.Choose(ActionType.CatchBreath);
            Assert.That(b.Locked, Is.False);
        }

        [Test]
        public void Undo_CancelsAPendingAction_First()
        {
            var b = Builder(Scenario.From("A...B"));
            b.Choose(ActionType.CatchBreath);
            b.Choose(ActionType.Fire);
            Assert.That(b.HasPending);
            b.Undo();
            Assert.That(b.HasPending, Is.False);
            Assert.That(b.Plan[0].Type, Is.EqualTo(ActionType.CatchBreath));
        }

        [TestCase(Ability.Grappin, 4)]       // pulls itself to the tile before the pillar
        [TestCase(Ability.BullRush, 3)]      // 3 tiles, stops before nothing
        public void Signature_MovesTheProjection(Ability ability, int x)
        {
            var s = Scenario.From("A....#\n......\n.....B").WithAbility('A', ability).WithGrit('A', 5);
            var b = Builder(s);
            Assert.That(b.Choose(ActionType.Signature) && b.CommitDirection(Dir.E));
            Assert.That(b.Projection.States[1].Pos, Is.EqualTo(new Int2(x, 2)));
        }

        [Test]
        public void Switcheroo_ProjectsOntoTheTargetsTile()
        {
            var s = Scenario.From("A....#\n......\n.....B").WithAbility('A', Ability.Switcheroo).WithGrit('A', 5);
            var b = Builder(s);
            Assert.That(b.Choose(ActionType.Signature) && b.PendingNeedsUnit);
            Assert.That(b.CommitDirection(Dir.E), Is.False, "Switcheroo takes a target, not a direction");
            Assert.That(b.CommitUnit(1));
            Assert.That(b.Plan[0].Unit, Is.EqualTo(1));
            Assert.That(b.Projection.States[1].Pos, Is.EqualTo(new Int2(5, 0)));
        }

        [Test]
        public void Switcheroo_FromTheWarnedRing_IsAnIssue_AndKeepsThePosition()
        {
            // Turn 5 warns ring 0 (the outer ring); A stands on it, so the sim would fail the swap.
            var s = Scenario.From("A....\n.....\n.....\n.....\n....B").WithAbility('A', Ability.Switcheroo).WithGrit('A', 5).WithTurn(5);
            var b = Builder(s);
            Assert.That(b.IssueFor(ActionType.Signature), Is.EqualTo(PlanIssue.Warned));
            Assert.That(b.Choose(ActionType.Signature), Is.False);

            // A plan that already holds the swap (made before the ring warned) reports it per tick.
            var plan = new TurnPlan(0) { A0 = PlannedAction.Switcheroo(1) };
            var proj = PlanProjection.Project(s.State, 0, plan, s.Config);
            Assert.That(proj.Issues[0], Is.EqualTo(PlanIssue.Warned));
            Assert.That(proj.States[1].Pos, Is.EqualTo(s.State.Units[0].Pos), "a failed swap moves nobody");

            // Off the ring it is fine.
            var safe = Scenario.From(".....\n.A...\n.....\n.....\n....B").WithAbility('A', Ability.Switcheroo).WithGrit('A', 5).WithTurn(5);
            Assert.That(Builder(safe).IssueFor(ActionType.Signature), Is.EqualTo(PlanIssue.None));
        }

        [Test]
        public void Switcheroo_IsPlannedByTargetSlot_NotYourself_NotTheDead()
        {
            var s = Scenario.From("A...B\n.....\nC...D").WithAbility('A', Ability.Switcheroo).WithGrit('A', 5);
            s.Unit('C').Alive = false;
            var b = Builder(s);
            Assert.That(b.Choose(ActionType.Signature));
            Assert.That(b.CommitUnit(0), Is.False, "not Matic himself");
            Assert.That(b.CommitUnit(2), Is.False, "not a dead musketeer");
            Assert.That(b.IsSwitcherooTarget(1) && b.IsSwitcherooTarget(3));
            Assert.That(b.CommitUnit(3));
            Assert.That(b.Plan[0], Is.EqualTo(PlannedAction.Switcheroo(3)));
            Assert.That(b.Projection.States[1].Pos, Is.EqualTo(new Int2(4, 0)));
        }

        [Test]
        public void MoveAndDash_OntoAnotherMusketeersTile_AreAllowed_AndMarkedContested()
        {
            var s = Scenario.From("A.B..\n.....");
            var b = Builder(s);
            Assert.That(b.Choose(ActionType.Move) && b.CommitDirection(Dir.E));
            Assert.That(b.Choose(ActionType.Move) && b.CommitDirection(Dir.E));
            Assert.That(b.Projection.Issues[1], Is.EqualTo(PlanIssue.None));
            Assert.That(b.Projection.Contested[0], Is.False);
            Assert.That(b.Projection.Contested[1], Is.True);
            Assert.That(b.Projection.States[2].Pos, Is.EqualTo(new Int2(2, 1)));

            var d = Builder(s);
            Assert.That(d.Choose(ActionType.Dash) && d.CommitDirection(Dir.E));
            Assert.That(d.Projection.Contested[0], Is.True, "the landing tile holds B");
            Assert.That(d.Projection.States[1].Pos, Is.EqualTo(new Int2(2, 1)));
        }

        [Test]
        public void Dash_IsRefusedOnlyByThePillarOrSolidLanding()
        {
            var over = Builder(Scenario.From("AoB..").WithGrit('A', 5));      // hops a crate onto B's tile
            Assert.That(over.Choose(ActionType.Dash) && over.CommitDirection(Dir.E));
            Assert.That(over.Projection.States[1].Pos, Is.EqualTo(new Int2(2, 0)));
            var pillar = Builder(Scenario.From("A#...B").WithGrit('A', 5));
            Assert.That(pillar.Choose(ActionType.Dash) && pillar.CommitDirection(Dir.E));
            Assert.That(pillar.Projection.States[1].Pos, Is.EqualTo(new Int2(0, 0)), "a pillar on the middle tile blocks");
            var solid = Builder(Scenario.From("A.o..B").WithGrit('A', 5));
            Assert.That(solid.Choose(ActionType.Dash) && solid.CommitDirection(Dir.E));
            Assert.That(solid.Projection.States[1].Pos, Is.EqualTo(new Int2(0, 0)), "a crate landing blocks");
        }

        [Test]
        public void FireAndReload_FollowTheProjectedMusket()
        {
            var b = Builder(Scenario.From("A...B"));   // starts loaded
            Assert.That(b.IssueFor(ActionType.Fire), Is.EqualTo(PlanIssue.None));
            Assert.That(b.IssueFor(ActionType.Reload), Is.EqualTo(PlanIssue.AlreadyLoaded));
            Assert.That(b.Choose(ActionType.Fire) && b.CommitDirection(Dir.E));
            Assert.That(b.IssueFor(ActionType.Fire), Is.EqualTo(PlanIssue.Unloaded));
            Assert.That(b.IssueFor(ActionType.Reload), Is.EqualTo(PlanIssue.None));
            Assert.That(b.Choose(ActionType.Reload));
            Assert.That(b.IssueFor(ActionType.Fire), Is.EqualTo(PlanIssue.None));
            Assert.That(b.IssueFor(ActionType.Reload), Is.EqualTo(PlanIssue.AlreadyLoaded));
            b.SelectSlot(0);   // editing the first tick again: loaded there
            Assert.That(b.IssueFor(ActionType.Reload), Is.EqualTo(PlanIssue.AlreadyLoaded));
        }

        [Test]
        public void ShotTiles_AreExactlyTheTilesInRange_StoppedByThePillar()
        {
            var open = Scenario.From("A........B");
            var tiles = PlanProjection.ShotTiles(open.State, 0, new Int2(0, 0), Dir.E, open.Config.FireRange);
            Assert.That(tiles.Count, Is.EqualTo(5));
            for (int i = 0; i < 5; i++)
                Assert.That(tiles[i], Is.EqualTo(new Int2(i + 1, 0)));

            var blocked = Scenario.From("A..#.....B");
            tiles = PlanProjection.ShotTiles(blocked.State, 0, new Int2(0, 0), Dir.E, blocked.Config.FireRange);
            Assert.That(tiles, Is.EqualTo(new[] { new Int2(1, 0), new Int2(2, 0) }), "the pillar is not a target and ends the line");

            var crate = Scenario.From("A.o......B");
            tiles = PlanProjection.ShotTiles(crate.State, 0, new Int2(0, 0), Dir.E, crate.Config.FireRange);
            Assert.That(tiles, Is.EqualTo(new[] { new Int2(1, 0), new Int2(2, 0) }), "a crate takes the shot");

            var unit = Scenario.From("A.B.....C");
            tiles = PlanProjection.ShotTiles(unit.State, 0, new Int2(0, 0), Dir.E, unit.Config.FireRange);
            Assert.That(tiles.Count, Is.EqualTo(2), "stops at the first unit");
            Assert.That(PlanProjection.ShotTiles(unit.State, 0, new Int2(0, 0), Dir.W, 5).Count, Is.EqualTo(0), "the board edge");
        }

        [Test]
        public void BullRushPreview_StopsLikeTheSim_AndLandsThePushedUnit()
        {
            var s = Scenario.From("A..B.......");
            var r = PlanProjection.BullRush(s.State, 0, new Int2(0, 0), Dir.E, s.Config.BullRushDistance, s.Config.BullRushPush);
            Assert.That(r.End, Is.EqualTo(new Int2(2, 0)));
            Assert.That(r.HasImpact && r.HitUnit);
            Assert.That(r.ImpactTile, Is.EqualTo(new Int2(3, 0)));
            Assert.That(r.Landing, Is.EqualTo(new Int2(7, 0)), "pushed BullRushPush (4) tiles");
            Assert.That(r.Slam, Is.False);

            var wall = Scenario.From("A..B..#....");
            r = PlanProjection.BullRush(wall.State, 0, new Int2(0, 0), Dir.E, 3, 4);
            Assert.That(r.Landing, Is.EqualTo(new Int2(5, 0)));
            Assert.That(r.Slam, Is.True);

            var far = Scenario.From("A....B.....");   // out of distance: no impact
            r = PlanProjection.BullRush(far.State, 0, new Int2(0, 0), Dir.E, 3, 4);
            Assert.That(r.End, Is.EqualTo(new Int2(3, 0)));
            Assert.That(r.HasImpact, Is.False);

            var pit = Scenario.From("A.B...~....");
            r = PlanProjection.BullRush(pit.State, 0, new Int2(0, 0), Dir.E, 3, 4);
            Assert.That(r.End, Is.EqualTo(new Int2(1, 0)));
            Assert.That(r.Landing, Is.EqualTo(new Int2(6, 0)));
            Assert.That(r.Falls, Is.True);
        }

        [Test]
        public void DashHops_OnlyOverSomething()
        {
            var s = Scenario.From("A.....\n.o....\nB.....");
            Assert.That(PlanProjection.DashHops(s.State, 0, new Int2(0, 2), Dir.E), Is.False);
            Assert.That(PlanProjection.DashHops(s.State, 0, new Int2(1, 2), Dir.S), Is.True, "crate below");
            Assert.That(PlanProjection.DashHops(s.State, 0, new Int2(0, 1), Dir.S), Is.True, "a musketeer on the middle tile");
        }

        [Test]
        public void SlideEnd_StopsAtUnitsAndEdges()
        {
            var s = Scenario.From("A.B..");
            Assert.That(PlanProjection.SlideEnd(s.State, new Int2(0, 0), Dir.E, 4, out bool slam, out bool falls), Is.EqualTo(new Int2(1, 0)));
            Assert.That(slam && !falls);
        }

        [Test]
        public void TripleCharge_UnloadsTheProjection()
        {
            var b = Builder(Scenario.From("A...B").WithAbility('A', Ability.TripleCharge).WithGrit('A', 5));
            Assert.That(b.Choose(ActionType.Signature) && b.CommitDirection(Dir.E));
            Assert.That(b.CursorState.Loaded, Is.False);
            Assert.That(b.IssueFor(ActionType.Fire), Is.EqualTo(PlanIssue.Unloaded));
        }

        [Test]
        public void OwnKeg_ExplodingThisTick_FreesTheLimit_NextTick()
        {
            var s = Scenario.From("A..K..B").WithGrit('A', 5).WithKegOwner(0, 'A').WithKegFuse(0, 1);
            var b = Builder(s);
            Assert.That(b.IssueFor(ActionType.Keg), Is.EqualTo(PlanIssue.KegActive), "still out during tick 1");
            Assert.That(b.Choose(ActionType.CatchBreath));
            Assert.That(b.IssueFor(ActionType.Keg), Is.EqualTo(PlanIssue.None), "blew up in tick 1");
            Assert.That(b.Choose(ActionType.Keg) && b.CommitTarget(new Int2(0, 0)));

            // The sim agrees: the tick-2 throw doesn't fizzle.
            s.PlanTurn('A', b.Plan[0], b.Plan[1], b.Plan[2]);
            s.ResolveTurn();
            s.AssertEvent(SimEventKind.KegThrown, 'A');
        }

        [Test]
        public void Cursor_WrapsBackToAnEarlierEmptySlot()
        {
            var b = Builder(Scenario.From("A...B"));
            b.Choose(ActionType.CatchBreath);
            b.SelectSlot(2);
            b.Choose(ActionType.CatchBreath);
            Assert.That(b.Cursor, Is.EqualTo(1));
            Assert.That(b.IsFull, Is.False);
        }

        // Property: with nobody else acting nearby, the projection matches the sim tick by tick.
        [Test]
        public void Projection_MatchesTheSim_ForASoloUnit()
        {
            var rng = new DeterministicRng(99);
            var types = new[] { ActionType.Move, ActionType.Dash, ActionType.Fire, ActionType.Reload, ActionType.Parry, ActionType.CatchBreath,
                ActionType.Idle, ActionType.Signature, ActionType.Keg };
            var abilities = new[] { Ability.TripleCharge, Ability.BullRush, Ability.Switcheroo };
            const string map = @"
                B........
                .........
                ..#...#..
                ...~.~...
                .o.....o.
                ...~.~...
                ..#...#..
                ....A....
                .........";
            for (int run = 0; run < 1000; run++)
            {
                var s = Scenario.From(map).WithGrit('A', rng.Range(0, 6)).WithLoaded('A', rng.Range(0, 2) == 0)
                    .WithAbility('A', abilities[rng.Range(0, abilities.Length)]);
                var plan = new TurnPlan(0);
                for (int t = 0; t < 3; t++)
                {
                    var target = s.Unit('A').Pos + new Int2(rng.Range(-3, 4), rng.Range(-3, 4));
                    plan[t] = new PlannedAction(types[rng.Range(0, types.Length)], DirExt.All[rng.Range(0, 4)], target);
                }
                var proj = PlanProjection.Project(s.State, 0, plan, s.Config);

                var state = s.State.Clone();
                var plans = new[] { plan, new TurnPlan(1) };
                for (int t = 0; t < 3; t++)
                {
                    var log = TurnResolver.ResolveTick(state, plans, t, s.Config);
                    // Known limitation: the projection doesn't model our own board changes (a broken crate,
                    // a landed keg) or contact with the other unit, so stop comparing once those happen.
                    if (log.Contains(SimEventKind.CrateDestroyed) || log.Contains(SimEventKind.KegLanded) ||
                        log.Contains(SimEventKind.Pushed) || log.Contains(SimEventKind.Bonk) || log.Contains(SimEventKind.Swapped))
                        break;
                    var u = state.Units[0];
                    var p = proj.States[t + 1];
                    Assert.That(u.Alive, Is.EqualTo(p.Alive), $"run {run} tick {t} alive");
                    if (!u.Alive)
                        break;
                    Assert.That(u.Pos, Is.EqualTo(p.Pos), $"run {run} tick {t} pos");
                    Assert.That(u.Grit, Is.EqualTo(p.Grit), $"run {run} tick {t} grit");
                    Assert.That(u.Loaded, Is.EqualTo(p.Loaded), $"run {run} tick {t} loaded");
                }
            }
        }
    }
}
