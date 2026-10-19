using System;
using System.Collections.Generic;
using UnityEngine;

namespace Musketeers.Data
{
    // Every sound, keyed by its path under Assets/_Project/Audio without the extension and without a
    // trailing _N variant number: "SFX/actions/musket_fire_1" and "_2" are both "SFX/actions/musket_fire",
    // and Pick() chooses one at random. Built by the importer, so sounds are wired by file name.
    [CreateAssetMenu(menuName = "Musketeers/Audio Library", fileName = "AudioLibrary")]
    public sealed class AudioLibrary : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string Key;
            public AudioClip[] Clips = new AudioClip[0];
        }

        public List<Entry> Entries = new List<Entry>();

        Dictionary<string, Entry> _map;

        public AudioClip Pick(string key)
        {
            var e = Find(key);
            if (e == null || e.Clips.Length == 0)
                return null;
            return e.Clips[UnityEngine.Random.Range(0, e.Clips.Length)];
        }

        // Variant i (0-based, wraps), for "taunt 1..4 on Z/X/C/V".
        public AudioClip Variant(string key, int index)
        {
            var e = Find(key);
            if (e == null || e.Clips.Length == 0)
                return null;
            return e.Clips[((index % e.Clips.Length) + e.Clips.Length) % e.Clips.Length];
        }

        public bool Has(string key) => Find(key) != null;

        Entry Find(string key)
        {
            if (_map == null || _map.Count != Entries.Count)
            {
                _map = new Dictionary<string, Entry>();
                foreach (var e in Entries)
                {
                    if (e != null && !string.IsNullOrEmpty(e.Key))
                        _map[e.Key] = e;
                }
            }
            return key != null && _map.TryGetValue(key, out var found) ? found : null;
        }

        public void Invalidate() => _map = null;

        // "musket_fire_2" → "musket_fire"; "step_1" → "step"; "hurt_1" → "hurt".
        public static string StripVariant(string name)
        {
            int u = name.LastIndexOf('_');
            if (u > 0 && u < name.Length - 1)
            {
                for (int i = u + 1; i < name.Length; i++)
                {
                    if (!char.IsDigit(name[i]))
                        return name;
                }
                return name.Substring(0, u);
            }
            return name;
        }
    }
}
