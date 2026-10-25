using System.Collections;
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
        Image _replayStillImage, _replayImage;
        Label _replayCaption;
        Button _replaySkip, _replayResults;
        RenderTexture _replayStill, _replayTexture;
        Coroutine _replayCapture;
        int _replayTurn, _replayIndex;
        bool _replayFinal;
        string _replayMusic;
        float _replayOldSpeed, _replayCameraSize, _replayCameraAspect;
        Vector3 _replayCameraLocal;

        void InitReplay()
        {
            _replayFrame = new VisualElement { name = "killcam" };
            _replayFrame.style.position = Position.Absolute;
            _replayFrame.style.left = _replayFrame.style.right = _replayFrame.style.top = _replayFrame.style.bottom = 0;
            _replayStillImage = new Image { scaleMode = ScaleMode.StretchToFill, pickingMode = PickingMode.Ignore };
            _replayStillImage.StretchToParentSize();
            _replayFrame.Add(_replayStillImage);
            var shade = new VisualElement { pickingMode = PickingMode.Ignore };
            shade.StretchToParentSize(); shade.style.backgroundColor = new Color(0.02f, 0.025f, 0.06f, 0.6f);
            _replayFrame.Add(shade);
            var window = new VisualElement { name = "replay-window" };
            window.style.position = Position.Absolute;
            window.style.left = Length.Percent(14); window.style.right = Length.Percent(14);
            window.style.top = Length.Percent(16); window.style.bottom = Length.Percent(21);
            window.style.backgroundColor = new Color(0.025f, 0.035f, 0.09f);
            window.style.borderLeftWidth = window.style.borderRightWidth = window.style.borderTopWidth = window.style.borderBottomWidth = 2;
            window.style.borderLeftColor = window.style.borderRightColor = window.style.borderTopColor = window.style.borderBottomColor = new Color(0.9f, 0.12f, 0.1f);
            _replayFrame.Add(window);
            _replayImage = new Image { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            _replayImage.StretchToParentSize(); window.Add(_replayImage);
            var badge = new Image { sprite = UiArt.Get("UI/badges/killcam"), scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            badge.style.position = Position.Absolute; badge.style.width = 150; badge.style.height = 110;
            badge.style.left = Length.Percent(50); badge.style.translate = new Translate(Length.Percent(-50), 0);
            badge.style.top = -85; window.Add(badge);
            _replayCaption = new Label("KILLCAM") { pickingMode = PickingMode.Ignore };
            _replayCaption.style.position = Position.Absolute; _replayCaption.style.bottom = 8;
            _replayCaption.style.left = _replayCaption.style.right = 8;
            _replayCaption.style.unityTextAlign = TextAnchor.MiddleCenter;
            _replayCaption.style.backgroundColor = new Color(0.025f, 0.035f, 0.09f, 0.8f);
            _replayCaption.style.color = Color.white; _replayCaption.style.fontSize = 18; window.Add(_replayCaption);
            var actions = new VisualElement(); actions.style.position = Position.Absolute;
            actions.style.top = Length.Percent(100); actions.style.marginTop = 10;
            actions.style.left = Length.Percent(50); actions.style.translate = new Translate(Length.Percent(-50), 0);
            window.Add(actions);
            _replaySkip = CornerButtons.Add(actions, "skip-replay", "Skip Replay", "Skip replay for everyone", ReplayAction);
            _replaySkip.name = "skip-replay";
            _replayResults = new Button(ReplayAction) { text = "See Results", name = "see-results" };
            _replayResults.AddToClassList("btn"); _replayResults.AddToClassList("btn--primary"); actions.Add(_replayResults);
            _root.Add(_replayFrame); _replayFrame.style.display = DisplayStyle.None;
            MatchNet.ReplayStarted += StartReplay;
            MatchNet.ReplayFinished += FinishReplay;
        }

        void ReplayAction()
        {
            if (_replayResult == null) return;
            if (_replayFinal) FinishReplay(_replayTurn, true);
            else if (_netTransport == null) FinishReplay(_replayTurn);
            else MatchNet.Instance?.SkipReplay(_replayTurn);
        }

        void BeginRoundReplay(RoundResult result)
        {
            AutoTestTrace("replay begin");
            _replayResult = result; _replayTurn = _mc.TurnSeq; _replayIndex = 0;
            _replayFinal = _mc.Match.MatchOver;
            _replayMusic = _audio != null ? _audio.CurrentMusic : null;
            _audio?.Music("Music/replay", 0.45f);
            _audio?.StopPlaybackEffects();
            _rig.ResetShake();
            _fx.ClearTransient();
            _replayOldSpeed = _director.Speed;
            _replayCameraLocal = _rig.Camera.transform.localPosition;
            _replayCameraSize = _rig.Camera.orthographicSize; _replayCameraAspect = _rig.Camera.aspect;
            _planBar.HideOpponents(); _hud.SetVisible(false); _planBar.SetVisible(false);
            _replaySkip.style.display = _replayFinal ? DisplayStyle.None : DisplayStyle.Flex;
            _replayResults.style.display = _replayFinal ? DisplayStyle.Flex : DisplayStyle.None;
            _fx.SuppressShake = true;
            _replayCapture = StartCoroutine(CaptureReplayBackdrop());
        }

        IEnumerator CaptureReplayBackdrop()
        {
            // One camera, two bounded targets: freeze its arena view once, then render only the inset.
            // No duplicate board, no gameplay rerun, and no movement of the arena/background rig.
            int width = Mathf.Min(1280, UnityEngine.Screen.width);
            int height = Mathf.Max(1, Mathf.RoundToInt(width / _replayCameraAspect));
            _replayStill = new RenderTexture(width, height, 24);
            _rig.Camera.targetTexture = _replayStill;
            yield return new WaitForEndOfFrame();
            if (_replayResult == null) yield break;
            _replayStillImage.image = _replayStill;
            _replayTexture = new RenderTexture(960, 540, 24);
            _replayImage.image = _replayTexture; _rig.Camera.targetTexture = _replayTexture;
            _rig.Camera.aspect = 16f / 9f;
            _replayFrame.style.display = DisplayStyle.Flex; _replayFrame.BringToFront();
            _replayCaption.text = "KILLCAM · waiting for players";
            _replayCapture = null;
            if (_replayFinal || _netTransport == null) StartReplay(_replayTurn);
            else if (MatchNet.Instance.LastReplayFinished == _replayTurn) FinishReplay(_replayTurn);
            else MatchNet.Instance.ReadyForReplay(_replayTurn);
        }

        void StartReplay(int turn)
        {
            if (_replayResult == null || turn != _replayTurn || _replayIndex != 0) return;
            AutoTestTrace("replay started");
            _director.Speed = Mathf.Clamp(_roundReplay.Clips.Count * 0.28f, 0.65f, 0.9f);
            PlayReplayClip();
        }
        void PlayReplayClip()
        {
            if (_replayResult == null) return;
            if (_replayIndex >= _roundReplay.Clips.Count)
            {
                _replayCaption.text = "KILLCAM · finishing";
                if (_replayFinal || _netTransport == null) FinishReplay(_replayTurn);
                else MatchNet.Instance?.ReplayDone(_replayTurn);
                return;
            }
            var clip = _roundReplay.Clips[_replayIndex++];
            _replayCaption.text = "KILLCAM · " + _replayIndex + " / " + _roundReplay.Clips.Count;
            _audio?.StopPlaybackEffects();
            _fx.ClearTransient();
            for (int i = 0; i < _board.UnitCount; i++) _board.Unit(i)?.CancelPlayback();
            var min = BoardMath.ToWorld(clip.FrameMin);
            var max = BoardMath.ToWorld(clip.FrameMax);
            // Tiles mark feet. Include the full upper body, Crown and ability indicator.
            min.y -= 1.3f; max.y += 2.8f;
            Vector3 point = (min + max) * 0.5f;
            Vector3 cameraPosition = _rig.Camera.transform.position;
            point.z = cameraPosition.z;
            PrimeTween.Tween.StopAll(_rig.Camera.transform);
            PrimeTween.Tween.Position(_rig.Camera.transform, point, _replayIndex == 1 ? 0.01f : 0.25f, Ease.InOutQuad);
            float size = Mathf.Max(2.4f, (max.y - min.y) * 0.5f, ((max.x - min.x) * 0.5f + 1.3f) / _rig.Camera.aspect);
            PrimeTween.Tween.StopAll(_rig.Camera);
            PrimeTween.Tween.Custom(_rig.Camera, _rig.Camera.orthographicSize, size, _replayIndex == 1 ? 0.01f : 0.25f, (camera, value) => camera.orthographicSize = value, Ease.InOutQuad);
            _director.PlayReplay(clip, 10f / Mathf.Max(1, _roundReplay.Clips.Count), PlayReplayClip);
        }
        void FinishReplay(int turn) => FinishReplay(turn, false);
        void FinishReplay(int turn, bool immediate)
        {
            if (_replayResult == null || turn != _replayTurn) return;
            AutoTestTrace(immediate ? "replay individual results" : "replay finished");
            var result = _replayResult; bool final = _replayFinal; _replayResult = null;
            StopReplayVisuals(!final || !immediate);
            _director.ShowState(_mc.Current.After, _mc.Current.CrownedBefore);
            if (final && immediate) _mc.ContinueAfterRound();
            else { _hud.SetVisible(true); ShowRoundScoreboard(result); }
        }
        void StopReplayVisuals(bool restoreMusic = true)
        {
            if (_replayCapture != null) StopCoroutine(_replayCapture);
            _replayCapture = null;
            _director.Stop(); _director.Speed = _replayOldSpeed > 0 ? _replayOldSpeed : 1f;
            _audio?.StopPlaybackEffects();
            if (restoreMusic && !string.IsNullOrEmpty(_replayMusic))
                _audio?.Music(_replayMusic, 0.45f);
            PrimeTween.Tween.StopAll(_rig.Camera);
            _rig.ResetShake();
            _rig.Camera.targetTexture = null;
            _rig.Camera.transform.localPosition = _replayCameraLocal;
            if (_replayCameraSize > 0) { _rig.Camera.orthographicSize = _replayCameraSize; _rig.Camera.aspect = _replayCameraAspect; }
            _fx.SuppressShake = false; _fx.ClearTransient();
            for (int i = 0; i < _board.UnitCount; i++) _board.Unit(i)?.CancelPlayback();
            _replayFrame.style.display = DisplayStyle.None;
            _replayStillImage.image = _replayImage.image = null;
            ReleaseReplayTexture(ref _replayStill); ReleaseReplayTexture(ref _replayTexture);
        }
        static void ReleaseReplayTexture(ref RenderTexture texture)
        {
            if (texture == null) return;
            texture.Release(); Destroy(texture); texture = null;
        }
        void CancelReplay()
        {
            if (_replayResult != null) StopReplayVisuals();
            _replayResult = null; _roundReplay.Clear();
        }
    }
}
