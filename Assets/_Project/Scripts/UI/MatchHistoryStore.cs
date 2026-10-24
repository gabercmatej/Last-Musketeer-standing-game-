using Musketeers.Game;
using UnityEngine;

namespace Musketeers.UI
{
    // Explicitly browser-local until an authenticated shared service is configured.
    public sealed class MatchHistoryStore
    {
        public const string DefaultKey = "Musketeers.MatchHistory.v1";
        readonly string _key;
        public MatchHistoryData Data { get; private set; }
        public string Error { get; private set; }

        public MatchHistoryStore(string key = DefaultKey)
        {
            _key = key;
            try
            {
                string json = PlayerPrefs.GetString(key, "");
                Data = string.IsNullOrEmpty(json) ? new MatchHistoryData() : JsonUtility.FromJson<MatchHistoryData>(json);
                if (Data == null || Data.Matches == null)
                    throw new System.FormatException("Invalid match history");
            }
            catch
            {
                Data = new MatchHistoryData();
                Error = "Saved match history could not be read. The saved copy has been preserved.";
            }
        }

        public bool Record(MatchHistoryRecord record)
        {
            if (Error != null || !Data.Add(record)) return false;
            PlayerPrefs.SetString(_key, JsonUtility.ToJson(Data));
            PlayerPrefs.Save();
            return true;
        }
    }
}
