using Musketeers.Game;
using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    // battle_final plays when the round is about to be decided: two or fewer alive, or the arena nearly gone.
    public class MusicPickerTests
    {
        [Test]
        public void Final_WhenTwoOrFewerAlive()
        {
            var s = Scenario.From(@"
                .......
                .A...B.
                .......
                .......
                .......
                ...C...
                .......");
            Assert.That(MusicPicker.IsFinal(s.State, s.Config), Is.False);
            s.Unit('C').Alive = false;
            Assert.That(MusicPicker.IsFinal(s.State, s.Config), Is.True);
        }

        [Test]
        public void Final_WhenAtMostTheInnerRingIsLeft()
        {
            var cfg = new GameConfig();
            var m = MatchRunner.NewMatch(MapParser.Parse(BuiltInMaps.Courtyard), new[] { Ability.Grappin, Ability.Switcheroo, Ability.TripleCharge, Ability.BullRush }, cfg, 1, out _);
            var s = m.Round;
            // 9×9: rings 0..4. Ring 2 falls on turn CrumbleStartTurn + 2*Every + 1 = 11, leaving rings 3 and 4.
            s.Turn = 10;
            Assert.That(MusicPicker.IsFinal(s, cfg), Is.False);
            s.Turn = 11;
            Assert.That(MusicPicker.IsFinal(s, cfg), Is.True);
        }
    }
}
