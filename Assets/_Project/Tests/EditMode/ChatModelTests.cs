using Musketeers.Game;
using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    public class ChatModelTests
    {
        [Test]
        public void Add_TrimsText()
        {
            var c = new ChatModel();
            Assert.That(c.Add(1, "  hello  ", 2f), Is.True);
            Assert.That(c.Messages[0].Text, Is.EqualTo("hello"));
            Assert.That(c.Messages[0].Slot, Is.EqualTo(1));
            Assert.That(c.Messages[0].Time, Is.EqualTo(2f));
        }

        [Test]
        public void Add_IgnoresEmptyAndWhitespace()
        {
            var c = new ChatModel();
            Assert.That(c.Add(0, "", 0f), Is.False);
            Assert.That(c.Add(0, "   ", 0f), Is.False);
            Assert.That(c.Add(0, null, 0f), Is.False);
            Assert.That(c.Messages.Count, Is.EqualTo(0));
        }

        [Test]
        public void Add_CapsAt80Characters()
        {
            var c = new ChatModel();
            c.Add(0, new string('x', 200), 0f);
            Assert.That(c.Messages[0].Text.Length, Is.EqualTo(ChatModel.MaxLength));
        }

        [Test]
        public void Keeps_OnlyTheLastSix()
        {
            var c = new ChatModel();
            for (int i = 0; i < 10; i++)
                c.Add(0, "m" + i, i);
            Assert.That(c.Messages.Count, Is.EqualTo(6));
            Assert.That(c.Messages[0].Text, Is.EqualTo("m4"));
            Assert.That(c.Messages[5].Text, Is.EqualTo("m9"));
        }
    }
}
