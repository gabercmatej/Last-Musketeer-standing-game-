using System;
using System.Collections;
using UnityEngine;
using Musketeers.Game;

namespace Musketeers.Presentation
{
    public sealed partial class PlaybackDirector
    {
        bool _replaying;
        public void PlayReplay(ReplayClip clip, float minimumSeconds, Action done)
        {
            Stop(); _replaying = true; _crowned = clip.Crowned;
            _running = StartCoroutine(RunReplay(clip, minimumSeconds, done));
        }
        IEnumerator RunReplay(ReplayClip clip, float minimumSeconds, Action done)
        {
            float began = Time.unscaledTime;
            IsPlaying = true;
            _board.Sync(clip.Before, clip.Crowned);
            yield return Wait(0.25f);
            yield return PlayLog(clip.Log);
            yield return Wait(0.2f);
            _board.Sync(clip.After, clip.Crowned);
            float remaining = minimumSeconds - (Time.unscaledTime - began);
            if (remaining > 0f) yield return new WaitForSecondsRealtime(remaining);
            IsPlaying = false; _replaying = false; _running = null;
            done?.Invoke();
        }
    }
}
