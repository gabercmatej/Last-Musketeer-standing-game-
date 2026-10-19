using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Musketeers.EditorTools.ArtImport
{
    // Copies root Audio/{Music,SFX,Voice} into Assets/_Project/Audio (Audio/Review = alternate takes,
    // skipped), with ASCII folder names. Music WAVs (56 MB) become ~200 kbps Ogg Vorbis through ffmpeg
    // when it's installed, which keeps the repo small; short SFX and voice WAVs are copied untouched.
    // Unity re-encodes everything per platform anyway. Unchanged sources are skipped.
    public static class AudioCopier
    {
        public const string Dst = "Assets/_Project/Audio";
        static readonly string[] Groups = { "Music", "SFX", "Voice" };

        public static void Run(List<string> log)
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            string src = Path.Combine(root, "Audio");
            if (!Directory.Exists(src))
            {
                log.Add("MISSING source folder Audio/: the game stays silent");
                return;
            }
            string ffmpeg = FindFfmpeg();
            _codec = ffmpeg != null && HasEncoder(ffmpeg, "libvorbis") ? "libvorbis" : "vorbis -strict -2";
            int converted = 0, copied = 0, skipped = 0;
            foreach (var group in Groups)
            {
                string dir = Path.Combine(src, group);
                if (!Directory.Exists(dir))
                {
                    log.Add($"MISSING Audio/{group}");
                    continue;
                }
                foreach (var f in Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories).OrderBy(p => p))
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext != ".wav" && ext != ".ogg")
                        continue;
                    string rel = ArtImporter.Ascii(f.Substring(src.Length + 1)).Replace('\\', '/').ToLowerInvariant();
                    bool toOgg = ext == ".wav" && ffmpeg != null && group == "Music";
                    string outRel = toOgg ? Path.ChangeExtension(rel, ".ogg") : rel;
                    string outPath = Path.Combine(root, Dst, CapitalGroup(outRel));
                    if (File.Exists(outPath) && File.GetLastWriteTimeUtc(outPath) >= File.GetLastWriteTimeUtc(f))
                    {
                        skipped++;
                        continue;
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(outPath));
                    if (toOgg && Convert(ffmpeg, f, outPath, group == "Music"))
                        converted++;
                    else
                    {
                        File.Copy(f, Path.ChangeExtension(outPath, ext), true);
                        copied++;
                    }
                }
            }
            log.Add($"Audio: {converted} converted to ogg, {copied} copied, {skipped} unchanged{(ffmpeg == null ? " (ffmpeg not found: WAVs copied)" : "")}");
        }

        // "music/battle.ogg" → "Music/battle.ogg" (folder names keep the ART_GUIDE capitals).
        static string CapitalGroup(string rel)
        {
            foreach (var g in Groups)
            {
                if (rel.StartsWith(g.ToLowerInvariant() + "/"))
                    return g + rel.Substring(g.Length);
            }
            return rel;
        }

        static string _codec = "libvorbis";

        static bool HasEncoder(string ffmpeg, string name)
        {
            var psi = new ProcessStartInfo(ffmpeg, "-hide_banner -encoders") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
            using (var p = Process.Start(psi))
            {
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                return output.Contains(" " + name + " ");
            }
        }

        static string FindFfmpeg()
        {
            foreach (var p in new[] { "/opt/homebrew/bin/ffmpeg", "/usr/local/bin/ffmpeg", "/usr/bin/ffmpeg" })
            {
                if (File.Exists(p))
                    return p;
            }
            return null;
        }

        // Source-quality Vorbis (q8 ≈ 210 kbps stereo with ffmpeg's built-in encoder); Unity re-encodes per platform.
        static bool Convert(string ffmpeg, string input, string output, bool music)
        {
            var psi = new ProcessStartInfo(ffmpeg, $"-y -loglevel error -i \"{input}\" -c:a {_codec} -q:a {(music ? 8 : 6)} \"{output}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            };
            using (var p = Process.Start(psi))
            {
                string err = p.StandardError.ReadToEnd();
                p.WaitForExit();
                if (p.ExitCode != 0)
                {
                    UnityEngine.Debug.LogWarning($"[AudioCopier] ffmpeg failed on {input}: {err}");
                    return false;
                }
            }
            return true;
        }
    }
}
