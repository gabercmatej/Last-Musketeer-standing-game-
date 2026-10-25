using Musketeers.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace Musketeers.Sim.Tests
{
    public class FinalPolishTests
    {
        [Test]
        public void ReplayMusicAndAbilitySpritesAreWired()
        {
            var audio = AssetDatabase.LoadAssetAtPath<AudioLibrary>("Assets/_Project/Data/AudioLibrary.asset");
            Assert.That(audio.Has("Music/replay"), Is.True);
            var art = AssetDatabase.LoadAssetAtPath<GameArt>("Assets/_Project/Data/GameArt.asset");
            Assert.That(art.Get("FX/bull_rush"), Is.Not.Null);
            Assert.That(art.Get("FX/triple_shot"), Is.Not.Null);
        }

        [Test]
        public void HowToPlayHasRoyalHeadingAboveUnchangedInstructions()
        {
            var root = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/_Project/UI/Settings.uxml").CloneTree();
            var scroll = root.Q("page-howto").Q<ScrollView>();
            var heading = scroll.ElementAt(0) as Label;
            Assert.That(heading.text, Is.EqualTo("Tactical Arena Brawler"));
            Assert.That(heading.ClassListContains("info-title"), Is.True);
            Assert.That(scroll.ElementAt(1).name, Is.EqualTo("howto-text"));
        }
    }
}
