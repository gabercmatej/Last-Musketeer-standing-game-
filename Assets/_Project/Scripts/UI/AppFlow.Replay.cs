using Musketeers.Game;
using Musketeers.Net;
using Musketeers.Sim;
using Musketeers.Presentation;
using PrimeTween;
using UnityEngine;
using UnityEngine.UIElements;

namespace Musketeers.UI
{
    public sealed partial class AppFlow
    {
        readonly RoundReplay _roundReplay = new RoundReplay();
        RoundResult _replayResult;
        VisualElement _replayFrame;
        Label _replayCaption;
        int _replayTurn, _replayIndex;
        float _replayOldSpeed;
        Vector3 _replayCameraPosition;
        float _replayCameraSize;

        void InitReplay()
        {
            _replayFrame = new VisualElement { name = "killcam", pickingMode = PickingMode.Ignore };
            _replayFrame.style.position = Position.Absolute;
            _replayFrame.style.left = _replayFrame.style.right = _replayFrame.style.top = _replayFrame.style.bottom = 0;
            _replayFrame.style.borderLeftWidth = _replayFrame.style.borderRightWidth = _replayFrame.style.borderTopWidth = _replayFrame.style.borderBottomWidth = 3;
            _replayFrame.style.borderLeftColor = _replayFrame.style.borderRightColor = _replayFrame.style.borderTopColor = _replayFrame.style.borderBottomColor = new Color(0.9f, 0.12f, 0.1f);
            var badge = UiArt.Image("", UiArt.Get("UI/badges/killcam"));
            badge.style.width = 180; badge.style.height = 90; badge.style.alignSelf = Align.Center;
            _replayFrame.Add(badge);
            _replayCaption = new Label("KILLCAM") { pickingMode = PickingMode.Ignore };
            _replayCaption.style.color = Color.white; _replayCaption.style.alignSelf = Align.Center;
            _replayCaption.style.fontSize = 20; _replayFrame.Add(_replayCaption);
            var skip = CornerButtons.Add(_replayFrame, "skip-replay", "Skip Replay", "Skip replay for everyone", () =>
            {
                if (_replayResult == null) return;
                if (_netTransport != null) MatchNet.Instance?.SkipReplay(_replayTurn);
                else FinishReplay(_replayTurn);
            });
            skip.name = "skip-replay";
            skip.style.position = Position.Absolute; skip.style.right = 24; skip.style.bottom = 24;
            _root.Add(_replayFrame); _replayFrame.style.display = DisplayStyle.None;
            MatchNet.ReplayStarted += StartReplay;
            MatchNet.ReplayFinished += FinishReplay;
        }

        void BeginRoundReplay(RoundResult result)
        {
            _replayResult = result; _replayTurn = _mc.TurnSeq; _replayIndex = 0;
            _replayOldSpeed = _director.Speed;
            _replayCameraPosition = _rig.transform.position; _replayCameraSize = _rig.Camera.orthographicSize;
            _replayFrame.style.display = DisplayStyle.Flex; _replayFrame.BringToFront();
            _replayCaption.text = "KILLCAM · waiting for players";
            _hud.SetVisible(false); _planBar.SetVisible(false);
            if (_netTransport == null) StartReplay(_replayTurn);
            else if (MatchNet.Instance.LastReplayFinished == _replayTurn) FinishReplay(_replayTurn);
            else MatchNet.Instance.ReadyForReplay(_replayTurn);
        }

        void StartReplay(int turn)
        {
            if (_replayResult == null || turn != _replayTurn) return;
            _director.Speed = 1.5f;
            PlayReplayClip();
        }
        void PlayReplayClip()
        {
            if (_replayResult == null) return;
            if (_replayIndex >= _roundReplay.Clips.Count)
            {
                _replayCaption.text = "KILLCAM · finishing";
                if (_netTransport != null) MatchNet.Instance?.ReplayDone(_replayTurn);
                else FinishReplay(_replayTurn);
                return;
            }
            var clip = _roundReplay.Clips[_replayIndex++];
            _replayCaption.text = "KILLCAM · " + _replayIndex + " / " + _roundReplay.Clips.Count;
            Vector3 point = BoardMath.ToWorld(clip.Focus); point.z = _replayCameraPosition.z;
            PrimeTween.Tween.StopAll(_rig.transform);
            PrimeTween.Tween.Position(_rig.transform, Vector3.Lerp(_replayCameraPosition, point, 0.2f), 0.2f, Ease.OutQuad);
            _rig.Camera.orthographicSize = _replayCameraSize * 0.92f;
            _director.PlayReplay(clip, PlayReplayClip);
        }
        void FinishReplay(int turn)
        {
            if (_replayResult == null || turn != _replayTurn) return;
            var result = _replayResult; _replayResult = null;
            StopReplayVisuals();
            _director.ShowState(_mc.Current.After, _mc.Current.CrownedBefore);
            _hud.SetVisible(true);
            ShowRoundScoreboard(result);
        }
        void StopReplayVisuals()
        {
            _director.Stop(); _director.Speed = _replayOldSpeed > 0 ? _replayOldSpeed : 1f;
            PrimeTween.Tween.StopAll(_rig.transform);
            PrimeTween.Tween.StopAll(_rig.Camera.transform);
            _rig.Camera.transform.localPosition = Vector3.zero;
            if (_replayCameraSize > 0)
            {
                _rig.transform.position = _replayCameraPosition;
                _rig.Camera.orthographicSize = _replayCameraSize;
            }
            for (int i = 0; i < _board.UnitCount; i++)
            {
                var u = _board.Unit(i); if (u == null) continue;
                u.CancelPlayback();
            }
            if (_replayFrame != null) _replayFrame.style.display = DisplayStyle.None;
        }
        void CancelReplay()
        {
            if (_replayResult != null) StopReplayVisuals();
            _replayResult = null; _roundReplay.Clear();
        }
    }
}
