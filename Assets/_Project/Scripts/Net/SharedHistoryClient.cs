using System;
using System.Collections;
using System.Text;
using Musketeers.Game;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.Networking;

namespace Musketeers.Net
{
    [Serializable] public sealed class HistoryParticipant
    {
        public string CharacterId, Name;
        public bool IsBot;
    }
    [Serializable] public sealed class HistoryRegistration
    {
        public string Action = "start", MatchId, MapId;
        public int PointsToWin, Hearts;
        public bool IsAutomated;
        public HistoryParticipant[] Roster;
    }
    [Serializable] sealed class HistorySubmission
    {
        public string Action = "complete", FinalHash;
        public bool Completed = true;
        public int RoundCount;
        public MatchHistoryRecord Record;
    }

    // Main-thread coroutines only. Shared reads/cache never import the legacy local match archive.
    public sealed class SharedHistoryClient
    {
        const string CacheKey = "Musketeers.SharedHistory.Cache.v1";
        readonly string _endpoint;
        bool _refreshing;
        public MatchHistoryData Data { get; private set; } = new MatchHistoryData();
        public bool Loaded { get; private set; }
        public bool SaveFailed { get; private set; }
        public string Status { get; private set; } = "Shared results have not been loaded yet.";
        public event Action Changed;
        public bool IsPreview { get; private set; }
        public bool Available => !string.IsNullOrEmpty(_endpoint);

        public SharedHistoryClient()
        {
            if (Uri.TryCreate(Application.absoluteURL, UriKind.Absolute, out var uri) &&
                uri.Scheme == "https" && uri.Host.EndsWith(".netlify.app", StringComparison.OrdinalIgnoreCase))
            {
                _endpoint = uri.GetLeftPart(UriPartial.Authority) + "/api/match-history";
                IsPreview = uri.Host != "last-musketeer.netlify.app";
            }
            try
            {
                var json = PlayerPrefs.GetString(CacheKey, "");
                if (!string.IsNullOrEmpty(json)) Data = JsonUtility.FromJson<MatchHistoryData>(json) ?? new MatchHistoryData();
            }
            catch { Data = new MatchHistoryData(); }
        }
        void SetStatus(string message) { Status = message; Changed?.Invoke(); }
        UnityWebRequest Request(string body = null)
        {
            var request = new UnityWebRequest(_endpoint, body == null ? "GET" : "POST");
            request.downloadHandler = new DownloadHandlerBuffer(); request.timeout = 12;
            if (body != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                request.SetRequestHeader("Content-Type", "application/json");
                if (AuthenticationService.Instance.IsSignedIn)
                    request.SetRequestHeader("Authorization", "Bearer " + AuthenticationService.Instance.AccessToken);
            }
            return request;
        }
        public IEnumerator Refresh()
        {
            if (_refreshing) yield break;
            if (!Available) { SetStatus("Shared history is available in the hosted Web game. Local match results remain available."); yield break; }
            _refreshing = true;
            using (var request = Request())
            {
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    MatchHistoryData next = null;
                    try { next = JsonUtility.FromJson<MatchHistoryData>(request.downloadHandler.text); } catch { }
                    if (next?.Matches != null)
                    {
                        Data = next; Loaded = true;
                        PlayerPrefs.SetString(CacheKey, JsonUtility.ToJson(Data)); PlayerPrefs.Save();
                        SetStatus(IsPreview ? "Preview test history · isolated from public standings" : "Shared online history · updated just now");
                    }
                    else SetStatus("Shared history could not be read. Showing the last shared snapshot.");
                }
                else SetStatus("Shared history unavailable. Showing the last shared snapshot; current match results remain local.");
            }
            _refreshing = false;
        }
        public IEnumerator Register(HistoryRegistration registration)
        {
            if (!Available || registration == null) yield break;
            using (var request = Request(JsonUtility.ToJson(registration)))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                    SetStatus("Shared match registration failed; gameplay continues and results will remain local if saving fails.");
            }
        }
        public IEnumerator Submit(HistoryRegistration registration, MatchHistoryRecord record, int rounds, uint hash)
        {
            if (!Available || registration == null || record == null) yield break;
            var submission = new HistorySubmission { Record = record, RoundCount = rounds, FinalHash = hash.ToString("x8") };
            // Retry the same stable ID; server conditional writes prevent duplicate wins.
            for (int attempt = 0; attempt < 3; attempt++)
            {
                using (var request = Request(JsonUtility.ToJson(submission)))
                {
                    yield return request.SendWebRequest();
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        SaveFailed = false;
                        SetStatus("Match saved to shared history.");
                        yield return Refresh(); yield break;
                    }
                    if (request.responseCode >= 400 && request.responseCode < 500 && request.responseCode != 429) break;
                }
                if (attempt < 2) yield return new WaitForSecondsRealtime(5);
            }
            SaveFailed = true;
            SetStatus("Shared save failed. Results are preserved locally; shared rankings have not been updated.");
        }
    }
}
