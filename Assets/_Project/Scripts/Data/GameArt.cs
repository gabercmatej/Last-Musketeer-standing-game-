using System;
using System.Collections.Generic;
using UnityEngine;

namespace Musketeers.Data
{
    // Shared sprites (objects, weapons, FX, UI icons, panels, badges, emotes, backgrounds), keyed by
    // their path under Assets/_Project/Art without the extension, e.g. "FX/muzzle_flash". The importer
    // fills it from the folders, so new art is wired by file name; a missing key returns null and the
    // caller falls back to a placeholder.
    [CreateAssetMenu(menuName = "Musketeers/Game Art", fileName = "GameArt")]
    public sealed class GameArt : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string Key;
            public Sprite Sprite;
        }

        public List<Entry> Sprites = new List<Entry>();

        Dictionary<string, Sprite> _map;

        public Sprite Get(string key)
        {
            if (_map == null || _map.Count != Sprites.Count)
            {
                _map = new Dictionary<string, Sprite>();
                foreach (var e in Sprites)
                {
                    if (!string.IsNullOrEmpty(e.Key))
                        _map[e.Key] = e.Sprite;
                }
            }
            return key != null && _map.TryGetValue(key, out var s) ? s : null;
        }

        public void Invalidate() => _map = null;

        // Well-known keys (ART_GUIDE §3 names).
        public const string Keg = "Objects/keg", Crown = "Objects/crown", Shadow = "Objects/shadow", Hook = "Objects/lasso";
        public const string Musket = "Weapons/musket", Rapier = "Weapons/rapier";
        public const string MuzzleFlash = "FX/muzzle_flash", Smoke = "FX/smoke_puff", Spark = "FX/spark", Explosion = "FX/explosion",
            Dust = "FX/dust", Splash = "FX/splash", Stars = "FX/stars", SwapSwirl = "FX/swap_swirl";
        public const string Logo = "UI/logo/logo", LogoSmall = "UI/logo/logo_small";
        public const string KeyArt = "UI/backgrounds/key_art", LobbyBackground = "UI/backgrounds/lobby", SplashArt = "UI/backgrounds/splash";
        public const string MenuBackground = "UI/backgrounds/menu", LobbyBridge = "UI/backgrounds/lobby_bridge",
            Results = "UI/backgrounds/results", ResultsBridge = "UI/backgrounds/results_bridge";
        public static string UiButton(string name) => "UI/buttons/" + name;
        public const string Panel = "UI/panels/panel", Button = "UI/panels/button", ButtonPressed = "UI/panels/button_pressed",
            Banner = "UI/panels/banner", Bubble = "UI/panels/bubble";
        public const string HeartFull = "UI/icons/status/heart_full", HeartEmpty = "UI/icons/status/heart_empty", Grit = "UI/icons/status/grit",
            MusketLoaded = "UI/icons/status/musket_loaded", MusketEmpty = "UI/icons/status/musket_empty", LockIn = "UI/icons/status/lock_in",
            CrownSmall = "UI/icons/status/crown_small", Skull = "UI/icons/status/skull";
        public static readonly string[] Emotes = { "UI/emotes/laugh", "UI/emotes/angry", "UI/emotes/salute", "UI/emotes/cry" };

        public static string ActionIcon(string name) => "UI/icons/actions/" + name;
        public static string Badge(string name) => "UI/badges/" + name;
    }
}
