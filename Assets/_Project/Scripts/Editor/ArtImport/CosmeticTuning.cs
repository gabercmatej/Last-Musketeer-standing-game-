using System.Collections.Generic;
using Musketeers.Data;
using UnityEngine;

namespace Musketeers.EditorTools.ArtImport
{
    // The importer's starting values for item placement, display names and default outfits. Applied to
    // new items (and on a reset); once Matej nudges an item in the Wardrobe Preview it's marked Tuned and
    // keeps his values. Units are world units (256 px) from the slot's anchor.
    public static class CosmeticTuning
    {
        struct Place
        {
            public Vector2 Offset, FrontOffset;
            public float Scale, FrontScale;
            public Place(float x, float y, float s, float fx, float fy, float fs)
            {
                Offset = new Vector2(x, y); Scale = s; FrontOffset = new Vector2(fx, fy); FrontScale = fs;
            }
        }

        static readonly Dictionary<CosmeticSlot, Place> SlotDefaults = new Dictionary<CosmeticSlot, Place>
        {
            { CosmeticSlot.Hat, new Place(0f, 0.06f, 1.1f, 0f, 0.06f, 1.0f) },
            { CosmeticSlot.Face, new Place(0f, 0f, 0.75f, 0f, 0f, 0.75f) },
            { CosmeticSlot.Cape, new Place(-0.02f, -0.15f, 1f, 0f, -0.15f, 1f) },
            { CosmeticSlot.Boots, new Place(0f, 0.1f, 1f, 0f, 0.1f, 1f) },
        };

        // Per-item placement, first pass by eye on Day 4 against the exported check sheets. Boots and capes
        // are derived from the sprite height (sole on the ground, cape top at the shoulders).
        static readonly Dictionary<string, Place> Items = new Dictionary<string, Place>
        {
            { "hat_plumed_purple", new Place(0f, 0.06f, 1.1f, 0f, 0.06f, 1f) },
            { "hat_plumed_green", new Place(0f, 0.06f, 1.1f, 0f, 0.06f, 1f) },
            { "hat_plumed_blue", new Place(0f, 0.06f, 1.1f, 0f, 0.06f, 1f) },
            { "hat_plumed_red", new Place(0f, 0.06f, 1.1f, 0f, 0.06f, 1f) },
            { "hat_plumed_white", new Place(0f, 0.06f, 1.1f, 0f, 0.06f, 1f) },
            { "hat_cowboy", new Place(0f, 0.06f, 1.1f, 0f, 0.06f, 1f) },
            { "hat_propeller", new Place(0f, 0.06f, 1.1f, 0f, 0.06f, 1f) },
            { "hat_helmet_pink", new Place(0f, 0.02f, 1.15f, 0f, 0.02f, 1.1f) },
            { "hat_bat_mask", new Place(0f, -0.06f, 1.2f, 0f, -0.07f, 1.15f) },
            { "face_moustache", new Place(-0.05f, -0.1f, 0.38f, 0f, -0.1f, 0.5f) },
            { "face_beard", new Place(-0.08f, -0.14f, 0.55f, 0f, -0.14f, 0.6f) },
            { "face_monocle", new Place(-0.03f, -0.03f, 0.36f, 0.06f, -0.03f, 0.4f) },
            { "face_sunglasses", new Place(-0.06f, 0f, 0.48f, 0f, 0f, 0.6f) },
            { "face_mask_purple", new Place(-0.06f, -0.1f, 0.42f, 0f, -0.1f, 0.55f) },
            { "face_mask_green", new Place(-0.06f, -0.1f, 0.42f, 0f, -0.1f, 0.55f) },
            { "face_mask_blue", new Place(-0.06f, -0.1f, 0.42f, 0f, -0.1f, 0.55f) },
            { "face_mask_red", new Place(-0.06f, -0.1f, 0.42f, 0f, -0.1f, 0.55f) },
            { "face_mask_black", new Place(-0.06f, -0.1f, 0.42f, 0f, -0.1f, 0.55f) },
            { "cape_purple", new Place(-0.07f, -0.233f, 1.45f, 0f, -0.245f, 1.5f) },
            { "cape_green", new Place(-0.07f, -0.233f, 1.45f, 0f, -0.245f, 1.5f) },
            { "cape_blue", new Place(-0.07f, -0.233f, 1.45f, 0f, -0.245f, 1.5f) },
            { "cape_red", new Place(-0.07f, -0.233f, 1.45f, 0f, -0.245f, 1.5f) },
            { "cape_white", new Place(-0.07f, -0.233f, 1.45f, 0f, -0.245f, 1.5f) },
            { "cape_black", new Place(-0.07f, -0.233f, 1.45f, 0f, -0.245f, 1.5f) },
            { "cape_slovenia", new Place(-0.07f, -0.233f, 1.45f, 0f, -0.245f, 1.5f) },
            { "cape_bat", new Place(-0.07f, -0.256f, 1.45f, 0f, -0.233f, 1.5f) },
            { "boots_black", new Place(0f, 0.196f, 1.1f, 0f, 0.195f, 1f) },
            { "boots_purple", new Place(0f, 0.196f, 1.1f, 0f, 0.195f, 1f) },
            { "boots_green", new Place(0f, 0.196f, 1.1f, 0f, 0.195f, 1f) },
            { "boots_blue", new Place(0f, 0.196f, 1.1f, 0f, 0.195f, 1f) },
            { "boots_red", new Place(0f, 0.196f, 1.1f, 0f, 0.195f, 1f) },
            { "boots_brown", new Place(0f, 0.196f, 1.1f, 0f, 0.195f, 1f) },
            { "boots_flipflops", new Place(0f, 0.188f, 1.1f, 0f, 0.195f, 1f) },
            { "boots_crocs", new Place(0f, 0.196f, 1.1f, 0f, 0.195f, 1f) },
            { "boots_sneakers", new Place(0f, 0.205f, 1.1f, 0f, 0.195f, 1f) },
            { "boots_barefoot", new Place(0f, 0.196f, 1.1f, 0f, 0.187f, 1f) },
        };

        public static void Apply(CosmeticDef def)
        {
            var p = Items.TryGetValue(def.Id, out var item) ? item : SlotDefaults[def.Slot];
            def.Offset = p.Offset;
            def.Scale = p.Scale;
            def.FrontOffset = p.FrontOffset;
            def.FrontScale = p.FrontScale;
            def.SortOrder = CosmeticDef.DefaultOrder(def.Slot);
            def.Tuned = false;
        }

        // Hand-tuned anchors per character and pose ("body", "hurt", "victory", "lunge"), Day 4 first pass:
        // read off each pose's eye line; hats/faces follow the head (tilted -25° when hurt). Boots stretch on
        // the wide hurt/victory stance and hide on the lunge. Unlisted poses use the silhouette estimate.
        static readonly Dictionary<string, PoseAnchors> Anchors = new Dictionary<string, PoseAnchors>
        {
            { "matej/body", A(-0.02f, 1.18f, 0.17f, 1f, -0.2f, 0.7f, 0.1f, 0.4f, 0f, 0f, false) },
            { "matej/hurt", A(0.264f, 1.173f, 0.36f, 0.93f, -0.12f, 0.8f, 0.121f, 0.465f, -25f, 1.4f, false) },
            { "matej/victory", A(0f, 1.23f, 0f, 1.06f, 0f, 0.76f, 0.145f, 0.465f, 0f, 1.3f, false) },
            { "matej/lunge", A(0.01f, 1.18f, 0.2f, 1f, -0.12f, 0.72f, 0.45f, 0.68f, 0f, 0f, true) },
            { "matic/body", A(-0.03f, 1.17f, 0.16f, 0.99f, -0.21f, 0.69f, 0.1f, 0.4f, 0f, 0f, false) },
            { "matic/hurt", A(0.254f, 1.163f, 0.35f, 0.92f, -0.12f, 0.8f, 0.055f, 0.465f, -25f, 1.4f, false) },
            { "matic/victory", A(0f, 1.2f, 0f, 1.03f, 0f, 0.73f, 0.141f, 0.465f, 0f, 1.3f, false) },
            { "matic/lunge", A(0.03f, 1.08f, 0.22f, 0.9f, -0.1f, 0.62f, 0.45f, 0.58f, 0f, 0f, true) },
            { "aljaz/body", A(-0.07f, 1.17f, 0.12f, 0.99f, -0.25f, 0.69f, 0.1f, 0.4f, 0f, 0f, false) },
            { "aljaz/hurt", A(0.234f, 1.163f, 0.33f, 0.92f, -0.12f, 0.8f, 0.036f, 0.465f, -25f, 1.4f, false) },
            { "aljaz/victory", A(0f, 1.22f, 0f, 1.05f, 0f, 0.75f, 0.114f, 0.465f, 0f, 1.3f, false) },
            { "aljaz/lunge", A(0.01f, 1.13f, 0.2f, 0.95f, -0.12f, 0.67f, 0.45f, 0.63f, 0f, 0f, true) },
            { "nik/body", A(-0.05f, 1.18f, 0.14f, 1f, -0.23f, 0.7f, 0.1f, 0.4f, 0f, 0f, false) },
            { "nik/hurt", A(0.274f, 1.143f, 0.37f, 0.9f, -0.12f, 0.8f, 0.079f, 0.465f, -25f, 1.4f, false) },
            { "nik/victory", A(0f, 1.2f, 0f, 1.03f, 0f, 0.73f, 0.141f, 0.465f, 0f, 1.3f, false) },
            { "nik/lunge", A(0.03f, 1.11f, 0.22f, 0.93f, -0.1f, 0.65f, 0.45f, 0.61f, 0f, 0f, true) },
        };

        static PoseAnchors A(float hx, float hy, float fx, float fy, float bx, float by, float px, float py, float angle, float stretch, bool hideBoots) =>
            new PoseAnchors
            {
                HeadTop = new Vector2(hx, hy), Face = new Vector2(fx, fy), Back = new Vector2(bx, by), Feet = Vector2.zero,
                Hand = new Vector2(px, py), HeadAngle = angle, FeetStretch = stretch, HideBoots = hideBoots,
            };

        public static bool TryAnchors(string character, string pose, out PoseAnchors a) =>
            Anchors.TryGetValue(character + "/" + pose, out a);

        // Everyone starts in a plumed hat of their own colour and leather boots (ART_GUIDE §4.2 defaults).
        public static Loadout DefaultLoadout(string character)
        {
            string color;
            switch (character)
            {
                case "matej": color = "purple"; break;
                case "matic": color = "red"; break;
                case "aljaz": color = "blue"; break;
                case "nik": color = "green"; break;
                default: color = "white"; break;
            }
            return new Loadout { Hat = "hat_plumed_" + color, Face = "", Cape = "", Boots = "boots_brown" };
        }

        static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            { "hat_cowboy", "Cowboy Hat" }, { "hat_propeller", "Propeller Cap" }, { "hat_helmet_pink", "Pink Helmet" },
            { "hat_bat_mask", "Bat Cowl" }, { "cape_slovenia", "Slovenian Cape" }, { "cape_bat", "Bat Cape" },
            { "boots_flipflops", "Flip-flops" }, { "boots_crocs", "Pink Crocs" }, { "boots_sneakers", "Sneakers" },
            { "boots_barefoot", "Barefoot" }, { "face_moustache", "Moustache" }, { "face_beard", "Big Beard" },
            { "face_monocle", "Monocle" }, { "face_sunglasses", "Sunglasses" },
        };

        public static string DisplayName(string id)
        {
            if (Names.TryGetValue(id, out var n))
                return n;
            var parts = id.Split('_');
            string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
            // hat_plumed_purple → Purple Plumed Hat; cape_red → Red Cape; face_mask_blue → Blue Mask.
            switch (parts[0])
            {
                case "hat" when parts.Length == 3 && parts[1] == "plumed": return $"{Cap(parts[2])} Plumed Hat";
                case "cape" when parts.Length == 2: return $"{Cap(parts[1])} Cape";
                case "boots" when parts.Length == 2: return $"{Cap(parts[1])} Boots";
                case "face" when parts.Length == 3 && parts[1] == "mask": return $"{Cap(parts[2])} Mask";
            }
            var sb = new List<string>();
            for (int i = 1; i < parts.Length; i++)
                sb.Add(Cap(parts[i]));
            return string.Join(" ", sb);
        }
    }
}
