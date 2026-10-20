using Musketeers.UI;
using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    public class NameRulesTests
    {
        [TestCase("Don Mateo the Third")]
        [TestCase("Aljaž")]
        [TestCase("Nik-2")]
        [TestCase("O'Neil")]
        [TestCase("J.R.")]
        [TestCase("Čšž")]
        [TestCase("12345678901234567890")]
        public void ValidNames(string name)
        {
            Assert.That(NameRules.IsValid(NameRules.Clean(name), out _), Is.True, name);
        }

        [TestCase("", "Type a name first.")]
        [TestCase("   ", "Type a name first.")]
        [TestCase("123456789012345678901", "20 characters at most.")]
        [TestCase("a<b", "Letters, numbers, spaces and . ' - only.")]
        [TestCase("emoji \U0001F600", "Letters, numbers, spaces and . ' - only.")]
        [TestCase("tab\there", "Letters, numbers, spaces and . ' - only.")]
        public void InvalidNames_GiveAHint(string name, string hint)
        {
            Assert.That(NameRules.IsValid(NameRules.Clean(name), out string h), Is.False, name);
            Assert.That(h, Is.EqualTo(hint));
        }

        [Test]
        public void Clean_TrimsAndCollapsesSpaces()
        {
            Assert.That(NameRules.Clean("  Don   Mateo  the    Third "), Is.EqualTo("Don Mateo the Third"));
            Assert.That(NameRules.Clean("   "), Is.EqualTo(""));
            Assert.That(NameRules.Clean(null), Is.EqualTo(""));
        }
    }
}
