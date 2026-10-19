using System;
using System.Collections.Generic;
using System.IO;
using Musketeers.Data;
using UnityEditor;
using UnityEngine;

namespace Musketeers.EditorTools.ArtImport
{
    // The importer's starting fits: FitDefaults.json next to this file holds every item's fit and every
    // pose's anchors and masks (the committed first pass). Import Art applies them to new items/poses and
    // on a reset; once Matej fits something in the Wardrobe Studio it's marked Tuned and keeps his values.
    // "Save Fits as Import Defaults" writes the current assets back into the JSON.
    public static class CosmeticTuning
    {
        const string DefaultsPath = "Assets/_Project/Scripts/Editor/ArtImport/FitDefaults.json";

        // Public so the round-trip test can build one; the file format is just JsonUtility on these.
        [Serializable]
        public sealed class Defaults
        {
            public List<CharDefaults> Characters = new List<CharDefaults>();
            public List<ItemDefaults> Items = new List<ItemDefaults>();
            public List<WeaponDefaults> Weapons = new List<WeaponDefaults>();
        }

        [Serializable]
        public sealed class CharDefaults
        {
            public string Id;
            public List<PoseFit> Poses = new List<PoseFit>();
        }

        [Serializable]
        public sealed class ItemDefaults
        {
            public string Id;
            public bool Enabled = true;
            public ItemFit Fit;
            public List<FitOverride> Overrides = new List<FitOverride>();
            public ItemMasks Masks = new ItemMasks();
            public int Checked;
        }

        [Serializable]
        public sealed class WeaponDefaults
        {
            public string Id;
            public FitSet Rest;
            public FitSet Active;
        }

        // Same data in gives the same bytes out: JsonUtility writes fields in declaration order and the lists
        // keep the catalog's order.
        public static string ToJson(Defaults d) => JsonUtility.ToJson(d, true);
        public static Defaults FromJson(string json) => JsonUtility.FromJson<Defaults>(json);

        static BezierPath CloneOrEmpty(BezierPath p) => p != null ? p.Clone() : new BezierPath();

        public static ItemMasks CloneMasks(ItemMasks m)
        {
            var r = new ItemMasks { Base = CloneOrEmpty(m?.Base) };
            if (m?.PerPose != null)
            {
                foreach (var pm in m.PerPose)
                {
                    if (pm != null)
                        r.PerPose.Add(new PoseMask { Pose = pm.Pose, Path = CloneOrEmpty(pm.Path) });
                }
            }
            return r;
        }

        static List<FitOverride> CloneOverrides(List<FitOverride> list)
        {
            var r = new List<FitOverride>();
            if (list == null)
                return r;
            foreach (var o in list)
            {
                if (o != null)
                    r.Add(new FitOverride { Character = o.Character ?? "", AnyPose = o.AnyPose, Pose = o.Pose, Fit = o.Fit });
            }
            return r;
        }

        public static FitSet CloneSet(FitSet f)
        {
            if (f == null)
                return null;
            return new FitSet { Name = f.Name, Fit = f.Fit, FitOverrides = CloneOverrides(f.FitOverrides), ItemMasks = CloneMasks(f.ItemMasks), Checked = f.Checked };
        }

        static Defaults _defaults;

        static Defaults Load()
        {
            if (_defaults != null)
                return _defaults;
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, DefaultsPath);
            _defaults = File.Exists(path) ? JsonUtility.FromJson<Defaults>(File.ReadAllText(path)) : new Defaults();
            return _defaults;
        }

        static readonly Dictionary<CosmeticSlot, ItemFit> SlotDefaults = new Dictionary<CosmeticSlot, ItemFit>
        {
            { CosmeticSlot.Hat, new ItemFit(0f, 0.06f, 1.35f) },
            { CosmeticSlot.Face, new ItemFit(0f, 0f, 0.6f) },
            { CosmeticSlot.Cape, new ItemFit(-0.07f, -0.23f, 1.45f) },
            { CosmeticSlot.Boots, new ItemFit(0.02f, 0.13f, 0.7f) },
        };

        public static void Apply(CosmeticDef def)
        {
            var d = Load().Items.Find(i => i.Id == def.Id);
            def.Global = d != null ? d.Fit : SlotDefaults[def.Slot];
            def.Overrides = CloneOverrides(d?.Overrides);
            def.Masks = CloneMasks(d?.Masks);
            def.CheckedMask = d != null ? d.Checked : 0;
            def.EnabledInV1 = d == null || d.Enabled;
            def.Tuned = false;
        }

        // The committed fit for one pose, if there is one.
        public static bool TryPose(string character, PoseKind kind, out PoseFit fit)
        {
            fit = null;
            var c = Load().Characters.Find(x => x.Id == character);
            var p = c?.Poses.Find(x => x.Pose == kind);
            if (p == null)
                return false;
            fit = new PoseFit { Pose = kind };
            fit.CopyFrom(p);
            return true;
        }

        // The committed fit sets for one weapon (deep copies), if the JSON has the weapon.
        public static bool TryWeapon(string id, out FitSet rest, out FitSet active)
        {
            rest = active = null;
            var w = Load().Weapons.Find(x => x.Id == id);
            if (w == null || w.Rest == null || w.Active == null)
                return false;
            rest = CloneSet(w.Rest);
            active = CloneSet(w.Active);
            return true;
        }

        [MenuItem("Tools/Musketeers/Save Fits as Import Defaults")]
        public static void SaveDefaults()
        {
            var roster = AssetDatabase.LoadAssetAtPath<CharacterRoster>("Assets/_Project/Data/CharacterRoster.asset");
            var catalog = AssetDatabase.LoadAssetAtPath<CosmeticCatalog>("Assets/_Project/Data/CosmeticCatalog.asset");
            if (roster == null || catalog == null)
            {
                Debug.LogWarning("[Fitting] Nothing saved: CharacterRoster or CosmeticCatalog is missing (run Import Art).");
                return;
            }
            var d = new Defaults();
            foreach (var c in roster.Characters)
            {
                if (c == null || c.Fit == null)
                    continue;
                var cd = new CharDefaults { Id = c.Id };
                foreach (var p in c.Fit.Poses)
                {
                    var copy = new PoseFit { Pose = p.Pose };
                    copy.CopyFrom(p);
                    cd.Poses.Add(copy);
                }
                d.Characters.Add(cd);
            }
            foreach (var item in catalog.Items)
            {
                if (item != null)
                    d.Items.Add(new ItemDefaults { Id = item.Id, Enabled = item.EnabledInV1, Fit = item.Global, Overrides = item.Overrides, Masks = item.Masks, Checked = item.CheckedMask });
            }
            foreach (var w in new[] { catalog.Musket, catalog.Rapier })
            {
                if (w != null)
                    d.Weapons.Add(new WeaponDefaults { Id = w.Id, Rest = w.Rest, Active = w.Active });
            }
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, DefaultsPath);
            File.WriteAllText(path, ToJson(d));
            _defaults = null;
            AssetDatabase.ImportAsset(DefaultsPath);
            Debug.Log("[Fitting] Saved the current fits as Import Art defaults: " + DefaultsPath);
        }

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
