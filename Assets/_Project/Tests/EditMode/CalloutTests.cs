using Musketeers.Game;
using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    // Named moments for the announcer (GAME_DESIGN §8.2), detected from one tick's events.
    public class CalloutTests
    {
        static Callout Top(Scenario s, int crowned = -1) => Callouts.Top(s.Log, crowned);

        [Test]
        public void Bonk_WhenTwoMoversCollide()
        {
            var s = Scenario.From("A.B").Plan('A', Act.Move(Dir.E)).Plan('B', Act.Move(Dir.W)).ResolveTick();
            Assert.That(Top(s), Is.EqualTo(Callout.Bonk));
        }

        [Test]
        public void Riposte_WhenAShotIsReflected()
        {
            var s = Scenario.From("A..B").Plan('A', Act.Fire(Dir.E)).Plan('B', Act.Parry()).ResolveTick();
            Assert.That(Top(s), Is.EqualTo(Callout.Riposte));
        }

        [Test]
        public void Touche_WhenAReflectedShotKills()
        {
            var s = Scenario.From("A..B").WithHp('A', 1).Plan('A', Act.Fire(Dir.E)).Plan('B', Act.Parry()).ResolveTick();
            s.AssertDead('A');
            Assert.That(Top(s), Is.EqualTo(Callout.Touche));
        }

        [Test]
        public void GuardBreak_WhenAParryIsShoved()
        {
            var s = Scenario.From("AB...").Plan('A', Act.Shove(Dir.E)).Plan('B', Act.Parry()).ResolveTick();
            Assert.That(Top(s), Is.EqualTo(Callout.GuardBreak));
        }

        [Test]
        public void SwanDive_WhenSomeoneWalksIntoAPit()
        {
            var s = Scenario.From("~A.B").Plan('A', Act.Move(Dir.W)).ResolveTick();
            Assert.That(Top(s), Is.EqualTo(Callout.SwanDive));
        }

        [Test]
        public void DoubleKo_BeatsEverythingButTheCrown()
        {
            var s = Scenario.From("A...B").WithHp('A', 1).WithHp('B', 1)
                .Plan('A', Act.Fire(Dir.E)).Plan('B', Act.Fire(Dir.W)).ResolveTick();
            s.AssertDead('A').AssertDead('B');
            Assert.That(Top(s), Is.EqualTo(Callout.DoubleKo));
            Assert.That(Top(s, crowned: 1), Is.EqualTo(Callout.CrownFalls));
        }

        [Test]
        public void Signature_WhenAnAbilityIsUsed()
        {
            var s = Scenario.From("A...B").WithAbility('A', Ability.Switcheroo).WithGrit('A', 5)
                .Plan('A', Act.Switcheroo('B')).ResolveTick();
            Assert.That(Top(s), Is.EqualTo(Callout.Signature));
        }

        [Test]
        public void Signature_IsAnnouncedFromTheSignatureUsedEvent()
        {
            var log = new EventLog();
            log.Add(SimEventKind.SignatureUsed, unit: 0, amount: (int)Ability.Grappin);
            Assert.That(Callouts.Top(log, crowned: -1), Is.EqualTo(Callout.Signature));
        }

        [Test]
        public void CrumbleFalls_AreNotSwanDives_ButTheCrownStillFalls()
        {
            var log = new EventLog();
            log.Add(SimEventKind.Died, unit: 1, other: -1, reason: (byte)DeathCause.Fell);
            Assert.That(Callouts.Top(log, crowned: -1, turnStart: true), Is.EqualTo(Callout.None));
            Assert.That(Callouts.Top(log, crowned: 1, turnStart: true), Is.EqualTo(Callout.CrownFalls));
            log.Add(SimEventKind.Died, unit: 2, other: -1, reason: (byte)DeathCause.Fell);
            Assert.That(Callouts.Top(log, crowned: -1, turnStart: true), Is.EqualTo(Callout.DoubleKo));
        }

        [Test]
        public void QuietTick_HasNoCallout()
        {
            var s = Scenario.From("A...B").Plan('A', Act.Move(Dir.E)).ResolveTick();
            Assert.That(Top(s), Is.EqualTo(Callout.None));
        }

        [Test]
        public void Bonk_IsBannerOnly_SignatureIsVoicedAgain()
        {
            Assert.That(Callouts.Voiced(Callout.Bonk), Is.False);
            Assert.That(Callouts.Text(Callout.Signature), Is.EqualTo("SIGNATURE MOVE!"));
            Assert.That(Callouts.Clip(Callout.Signature), Is.EqualTo("signature"));
            foreach (var c in new[] { Callout.Signature, Callout.Riposte, Callout.GuardBreak, Callout.SwanDive, Callout.Touche, Callout.DoubleKo,
                                      Callout.CrownFalls, Callout.EnGarde, Callout.Draw, Callout.LastMusketeer })
                Assert.That(Callouts.Voiced(c), Is.True, c.ToString());
        }
    }
}
