using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    // Walled edges (FIXES_v0.2 §3.1, Courtyard): Board.EdgeWalls makes the outside a Pillar, so nothing
    // can be pushed, walked or dashed off. On an open board the same moves fall into the pit.
    public class WallTests
    {
        [Test]
        public void Move_IntoTheWall_IsBlocked_OnAnOpenBoardItFalls()
        {
            Scenario.From("@walls\nA")
                .Plan('A', Act.Move(Dir.E))
                .ResolveTick()
                .AssertAlive('A')
                .AssertEvent(SimEventKind.MoveBlocked, 'A');
            Scenario.From("A")
                .Plan('A', Act.Move(Dir.E))
                .ResolveTick()
                .AssertDead('A');
        }

        [Test]
        public void Shove_IntoTheWall_IsAWallSlam()
        {
            Scenario.From("@walls\nAB")
                .Plan('A', Act.Shove(Dir.E))
                .ResolveTick()
                .AssertPos('B', 1, 0)
                .AssertHp('B', 2)
                .AssertEvent(SimEventKind.WallSlam, 'B');
            Scenario.From("AB")
                .Plan('A', Act.Shove(Dir.E))
                .ResolveTick()
                .AssertDead('B');
        }

        [Test]
        public void Dash_OffTheWall_IsBlocked_GritSpent()
        {
            // Middle tile on the board, landing outside.
            Scenario.From("@walls\nA.")
                .Plan('A', Act.Dash(Dir.E))
                .ResolveTick()
                .AssertPos('A', 0, 0)
                .AssertGrit('A', 1)
                .AssertEvent(SimEventKind.MoveBlocked, 'A');
            // Middle tile outside.
            Scenario.From("@walls\n.A")
                .Plan('A', Act.Dash(Dir.E))
                .ResolveTick()
                .AssertPos('A', 1, 0)
                .AssertEvent(SimEventKind.MoveBlocked, 'A');
            Scenario.From("A.")
                .Plan('A', Act.Dash(Dir.E))
                .ResolveTick()
                .AssertDead('A');
        }

        [Test]
        public void BullRush_PushIntoTheWall_IsAWallSlam()
        {
            Scenario.From("@walls\nA.B")
                .WithAbility('A', Ability.BullRush).WithGrit('A', 5)
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertPos('A', 1, 0)
                .AssertPos('B', 2, 0)
                .AssertHp('B', 2)
                .AssertEvent(SimEventKind.WallSlam, 'B');
        }

        [Test]
        public void BullRush_StopsAtTheWall()
        {
            Scenario.From("@walls\nA..")
                .WithAbility('A', Ability.BullRush).WithGrit('A', 5)
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertPos('A', 2, 0)
                .AssertAlive('A');
        }

        [Test]
        public void KegKick_StopsAtTheWall()
        {
            Scenario.From("@walls\nAK")
                .Plan('A', Act.Shove(Dir.E))
                .ResolveTick()
                .AssertKegAt(1, 0)
                .AssertNoEvent(SimEventKind.Fell);
        }

        [Test]
        public void Grappin_HitsTheWallLikeAPillar()
        {
            Scenario.From("@walls\nA..")
                .WithAbility('A', Ability.Grappin).WithGrit('A', 5)
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertPos('A', 2, 0)
                .AssertAlive('A');
        }

        [Test]
        public void TripleCharge_PushIntoTheWall_Slams()
        {
            Scenario.From("@walls\nA.B")
                .WithAbility('A', Ability.TripleCharge).WithGrit('A', 5).WithHp('B', 9)
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertPos('B', 2, 0)
                .AssertEventCount(SimEventKind.WallSlam, 3)
                .AssertHp('B', 6);   // 3 hits + 3 slams, capped at 3 damage per use
        }

        [Test]
        public void CrumblingStillMakesPitsInsideAWalledBoard()
        {
            Scenario.From("@walls\n...\n...\n...")
                .WithTurn(6).TurnStart()
                .AssertTile(0, 0, TileType.Pit)
                .AssertTile(1, 1, TileType.Floor);
        }
    }
}
