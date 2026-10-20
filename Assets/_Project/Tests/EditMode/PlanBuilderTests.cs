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
            b.Choose(ActionType.Reload);
            b.Choose(ActionType.CatchBreath);
            b.Choose(ActionType.Reload);
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
            b.Choose(ActionType.Reload);
            Assert.That(b.Locked, Is.False);
        }

        [Test]
        public void Undo_CancelsAPendingAction_First()
        {
            var b = Builder(Scenario.From("A...B"));
            b.Choose(ActionType.Reload);
            b.Choose(ActionType.Fire);
            Assert.That(b.HasPending);
            b.Undo();
            Assert.That(b.HasPending, Is.False);
            Assert.That(b.Plan[0].Type, Is.EqualTo(ActionType.Reload));
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
            Assert.That(b.Choose(ActionType.Signature) && b.CommitDirection(Dir.E));
            Assert.That(b.Plan[0].Unit, Is.EqualTo(1));
            Assert.That(b.Projection.States[1].Pos, Is.EqualTo(new Int2(5, 0)));
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
            Assert.That(b.Choose(ActionType.Reload));
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
            b.Choose(ActionType.Reload);
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
