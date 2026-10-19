using Musketeers.Data;
using Musketeers.Game;
using NUnit.Framework;
using UnityEditor;

namespace Musketeers.Sim.Tests
{
    // Sounds are wired by file name (Day 4). If a file is renamed or missing, the game goes quiet there
    // without an error, so this pins every name the code asks for against the imported AudioLibrary.
    public class AudioWiringTests
    {
        const string LibraryPath = "Assets/_Project/Data/AudioLibrary.asset";

        static readonly string[] CodeKeys =
        {
            "Music/battle",
            "Music/battle_final",
            "Music/lobby",
            "Music/menu",
            "Music/results",
            "SFX/abilities/bull_rush",
            "SFX/abilities/bull_rush_hit",
            "SFX/abilities/double_charge",
            "SFX/abilities/grappin_throw",
            "SFX/abilities/grappin_yank",
            "SFX/abilities/switcheroo",
            "SFX/actions/catch_breath",
            "SFX/actions/dash",
            "SFX/actions/fuse_hiss",
            "SFX/actions/guard_break",
            "SFX/actions/keg_land",
            "SFX/actions/keg_throw",
            "SFX/actions/leap",
            "SFX/actions/musket_click",
            "SFX/actions/musket_fire",
            "SFX/actions/parry_clang",
            "SFX/actions/parry_ready",
            "SFX/actions/reload",
            "SFX/actions/riposte",
            "SFX/actions/shove",
            "SFX/actions/step",
            "SFX/impacts/bonk",
            "SFX/impacts/chain_explosion",
            "SFX/impacts/crate_break",
            "SFX/impacts/death",
            "SFX/impacts/explosion",
            "SFX/impacts/fall_pit",
            "SFX/impacts/hit",
            "SFX/impacts/splash",
            "SFX/impacts/squeeze",
            "SFX/impacts/wall_slam",
            "SFX/match/award_reveal",
            "SFX/match/crown_gain",
            "SFX/match/lock_in",
            "SFX/match/match_win",
            "SFX/match/points_tick",
            "SFX/match/reveal",
            "SFX/match/round_start",
            "SFX/match/round_win",
            "SFX/match/tile_crumble",
            "SFX/match/tile_warn",
            "SFX/match/timer_tick",
            "SFX/match/turn_start",
            "SFX/ui/countdown",
            "SFX/ui/emote_pop",
            "SFX/ui/plan_add",
            "SFX/ui/plan_undo",
            "SFX/ui/player_join",
            "SFX/ui/player_leave",
            "SFX/ui/ready",
            "SFX/ui/ui_back",
            "SFX/ui/ui_click",
            "SFX/ui/ui_hover",
            "SFX/ui/wardrobe_equip",
        };

        static AudioLibrary Library()
        {
            var lib = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
            Assert.That(lib, Is.Not.Null, "Run Tools/Musketeers/Import Art");
            return lib;
        }

        [Test]
        public void EverySoundTheCodeUses_Exists()
        {
            var lib = Library();
            foreach (var key in CodeKeys)
                Assert.That(lib.Has(key), Is.True, key);
        }

        [Test]
        public void EveryCallout_HasAnAnnouncerLine()
        {
            var lib = Library();
            foreach (Callout c in System.Enum.GetValues(typeof(Callout)))
            {
                var clip = Callouts.Clip(c);
                if (clip != null)
                    Assert.That(lib.Has("Voice/announcer/" + clip), Is.True, clip);
            }
        }

        [Test]
        public void EveryMusketeer_HasHurtDeathVictoryAndFourTaunts()
        {
            var lib = Library();
            var roster = AssetDatabase.LoadAssetAtPath<CharacterRoster>("Assets/_Project/Data/CharacterRoster.asset");
            foreach (var c in roster.Characters)
            {
                foreach (var line in new[] { "hurt", "death", "victory", "taunt" })
                    Assert.That(lib.Has($"Voice/{c.Id}/{line}"), Is.True, $"{c.Id}/{line}");
                Assert.That(lib.Variant($"Voice/{c.Id}/taunt", 3), Is.Not.SameAs(lib.Variant($"Voice/{c.Id}/taunt", 0)), "4 taunts, one per emote key");
            }
        }
    }
}
