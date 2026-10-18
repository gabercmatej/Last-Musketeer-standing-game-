using System.Collections.Generic;
using UnityEngine;

namespace Musketeers.Sandbox
{
    // On-screen + console log for the Day 0 network smoke test (throwaway, delete after Day 4).
    public static class NetSmokeLog
    {
        const int MaxLines = 14;
        static readonly List<string> s_Lines = new List<string>();

        public static IReadOnlyList<string> Lines => s_Lines;
        public static event System.Action<string> LineAdded;

        public static void Add(string line)
        {
            Debug.Log("[NetSmoke] " + line);
            Push(line);
        }

        public static void Error(string line)
        {
            Debug.LogError("[NetSmoke] " + line);
            Push("ERROR: " + line);
        }

        static void Push(string line)
        {
            s_Lines.Add(line);
            if (s_Lines.Count > MaxLines)
                s_Lines.RemoveAt(0);
            LineAdded?.Invoke(line);
        }
    }
}
