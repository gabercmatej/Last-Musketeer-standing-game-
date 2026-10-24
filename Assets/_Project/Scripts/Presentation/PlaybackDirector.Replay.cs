using System;
using System.Collections;
using Musketeers.Game;

namespace Musketeers.Presentation
{
    public sealed partial class PlaybackDirector
    {
        bool _replaying;
        public void PlayReplay(ReplayClip clip, Action done)
        {
            Stop(); _replaying = true; _crowned = clip.Crowned;
            _running = StartCoroutine(RunReplay(clip, done));
        }
        IEnumerator RunReplay(ReplayClip clip, Action done)
        {
            IsPlaying = true;
            _board.Sync(clip.Before, clip.Crowned);
            yield return Wait(0.25f);
            yield return PlayLog(clip.Log);
            yield return Wait(0.2f);
            _board.Sync(clip.After, clip.Crowned);
            IsPlaying = false; _replaying = false; _running = null;
            done?.Invoke();
        }
    }
}
