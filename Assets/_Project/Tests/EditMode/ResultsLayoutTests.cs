using Musketeers.Data;
using Musketeers.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Musketeers.Sim.Tests
{
    public class ResultsLayoutTests
    {
        [Test]
        public void Ordinal_NamesTheFirstFourPlaces()
        {
            Assert.That(ResultsLayout.Ordinal(1), Is.EqualTo("1st"));
            Assert.That(ResultsLayout.Ordinal(2), Is.EqualTo("2nd"));
            Assert.That(ResultsLayout.Ordinal(3), Is.EqualTo("3rd"));
            Assert.That(ResultsLayout.Ordinal(4), Is.EqualTo("4th"));
        }

        [Test]
        public void Order_SortsByPlaceThenSlot_AndSharedPlacesKeepSlotOrder()
        {
            Assert.That(ResultsLayout.Order(new[] { 3, 1, 4, 2 }), Is.EqualTo(new[] { 1, 3, 0, 2 }));
            Assert.That(ResultsLayout.Order(new[] { 1, 1, 3, 4 }), Is.EqualTo(new[] { 0, 1, 2, 3 }));
            Assert.That(ResultsLayout.Order(new[] { 2, 1, 1 }), Is.EqualTo(new[] { 1, 2, 0 }));
        }

        [TestCase("Assets/_Project/Data/Maps/Courtyard.asset")]
        [TestCase("Assets/_Project/Data/Maps/Bridge.asset")]
        public void PodiumSpots_AreNormalized_AndPlaceOneIsTheTallestPodium(string path)
        {
            var map = AssetDatabase.LoadAssetAtPath<MapDef>(path);
            Assert.That(map, Is.Not.Null);
            Assert.That(map.PodiumSpots.Length, Is.EqualTo(4));
            foreach (var s in map.PodiumSpots)
            {
                Assert.That(s.x, Is.InRange(0f, 1f));
                Assert.That(s.y, Is.InRange(0f, 1f));
            }
            for (int i = 1; i < 4; i++)
                Assert.That(map.PodiumSpots[0].y, Is.GreaterThan(map.PodiumSpots[i].y), "place 1 stands highest on the art (y up)");
            Assert.That(map.PodiumScale, Is.GreaterThan(0f));
        }
    }
}
