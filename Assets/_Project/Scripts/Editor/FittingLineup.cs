using System;
using System.Collections.Generic;
using System.IO;
using Musketeers.Data;
using Musketeers.Presentation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Musketeers.EditorTools
{
    // Tools/Musketeers/Capture Fitting Lineups (Play mode): real CharacterRigs (SpriteMasks, sorting, hand
    // overlay — everything the CPU preview only imitates) for all 4 musketeers × every pose, photographed
    // off-screen into Temp/Fitting/runtime: (a) tall boots + plumed hat + cape, (b) each face item,
    // (c) musket + rapier.
    public static class FittingLineup
    {
        const float DX = 1.55f, DY = 1.7f;
        const int PxPerUnit = 150;

        [MenuItem("Tools/Musketeers/Capture Fitting Lineups (Play mode)")]
        public static void CaptureAll()
        {
            var art = Object.FindFirstObjectByType<ArtLibrary>();
            if (!Application.isPlaying || art == null)
            {
                Debug.LogWarning("[Fitting] Enter Play mode first (the lineup uses the real runtime rig).");
                return;
            }
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp/Fitting/runtime");
            Directory.CreateDirectory(dir);
            Capture(art, Path.Combine(dir, "a_outfit.png"), c => new Loadout { Hat = c.DefaultLoadout.Hat, Cape = "cape_" + Colour(c.Id), Boots = "boots_brown" }, false);
            foreach (var item in art.Cosmetics.BySlot(CosmeticSlot.Face))
            {
                var id = item.Id;
                Capture(art, Path.Combine(dir, "b_" + id + ".png"), c => new Loadout { Face = id }, false);
            }
            Capture(art, Path.Combine(dir, "c_weapons.png"), c => default, true);
            Debug.Log("[Fitting] Captured runtime lineups to " + dir);
        }

        static string Colour(string id) => id == "matej" ? "purple" : id == "matic" ? "red" : id == "aljaz" ? "blue" : "green";

        public static void Capture(ArtLibrary art, string path, Func<CharacterDef, Loadout> outfit, bool weapons)
        {
            var roster = art.Roster;
            var poses = new List<PoseKind> { PoseKind.Idle, PoseKind.Hurt, PoseKind.Victory, PoseKind.Lunge };
            if (roster.Get(0).HasPose(PoseKind.IdleB))
                poses.Insert(1, PoseKind.IdleB);
            var root = new GameObject("FittingLineup");
            root.transform.position = new Vector3(-500f, -500f, 0f);
            for (int ci = 0; ci < roster.Count; ci++)
            for (int pi = 0; pi < poses.Count; pi++)
            {
                var def = roster.Get(ci);
                var go = new GameObject($"{def.Id} {poses[pi]}");
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(pi * DX, -ci * DY, 0f);
                var rig = go.AddComponent<CharacterRig>();
                rig.Build(art, def, art.Cosmetics.Sanitize(outfit(def)), def.Color);
                rig.ShowWeapons(weapons);
                rig.SetPose(poses[pi]);
            }
            // Frame: a little past the lunge blade and the raised rapier.
            float w = (poses.Count - 1) * DX + 2.0f, h = (roster.Count - 1) * DY + 2.1f;
            var cam = new GameObject("FittingCam").AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = h * 0.5f;
            cam.transform.position = root.transform.position + new Vector3(-0.65f + w * 0.5f, 1.95f - h * 0.5f, -20f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.27f, 0.27f, 0.31f);
            int pw = Mathf.RoundToInt(w * PxPerUnit), ph = Mathf.RoundToInt(h * PxPerUnit);
            var rt = new RenderTexture(pw, ph, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.aspect = (float)pw / ph;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(pw, ph, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, pw, ph), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            cam.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(cam.gameObject);
            Object.DestroyImmediate(root);
        }
    }
}
