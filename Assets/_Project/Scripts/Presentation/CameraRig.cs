using PrimeTween;
using UnityEngine;

namespace Musketeers.Presentation
{
    // Orthographic camera with two framings: the arena (fits any board up to 13×13 between the HUD bars,
    // at any window size) and the stage (lobby/results: a fixed world area). A background sprite always
    // covers the whole view behind everything. The rig moves; the camera child shakes.
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        // Screen space the UI covers, as a fraction of screen height (top bar, plan bar).
        [SerializeField] private float _topInset = 0.095f;
        [SerializeField] private float _bottomInset = 0.235f;
        [SerializeField] private float _sideInset = 0.02f;
        [SerializeField] private float _margin = 0.45f;      // world units around the board (names, pips)
        [SerializeField] private float _topMargin = 1.05f;   // the top row's heads and hearts rise above the board

        public Camera Camera => _camera;

        enum Mode { Arena, Stage }

        Mode _mode = Mode.Arena;
        int _boardW = 9, _boardH = 9;
        Vector3 _stageCenter;
        float _stageW = 16f, _stageH = 9f;
        int _lastW, _lastH;
        SpriteRenderer _background;
        Material _material;

        public void Init(Material spriteMaterial) => _material = spriteMaterial;

        public void Fit(int boardWidth, int boardHeight)
        {
            _mode = Mode.Arena;
            _boardW = boardWidth;
            _boardH = boardHeight;
            Apply();
        }

        // Frames a world rectangle (the lobby or results stage), covering the screen.
        public void FrameStage(Vector3 center, float width, float height)
        {
            _mode = Mode.Stage;
            _stageCenter = center;
            _stageW = width;
            _stageH = height;
            Apply();
        }

        public void SetBackground(Sprite sprite, Color clear)
        {
            if (_camera != null)
                _camera.backgroundColor = clear;
            if (_background == null)
            {
                var go = new GameObject("Background");
                go.transform.SetParent(transform, false);
                _background = go.AddComponent<SpriteRenderer>();
                if (_material != null)
                    _background.sharedMaterial = _material;
                _background.sortingOrder = Layers.Background;
            }
            _background.sprite = sprite;
            _background.enabled = sprite != null;
            Cover();
        }

        void LateUpdate()
        {
            if (Screen.width != _lastW || Screen.height != _lastH)
                Apply();
        }

        void Apply()
        {
            if (_camera == null)
                return;
            _lastW = Screen.width;
            _lastH = Screen.height;
            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
            _camera.orthographic = true;
            if (_mode == Mode.Stage)
            {
                // Cover: the stage rectangle fills the screen, cropping the long side.
                float size = Mathf.Min(_stageH * 0.5f, _stageW / aspect * 0.5f);
                _camera.orthographicSize = Mathf.Max(0.5f, size);
                transform.position = new Vector3(_stageCenter.x, _stageCenter.y, -10f);
                Cover();
                return;
            }
            float needH = _boardH + _margin + _topMargin;
            float needW = _boardW + _margin * 2f;
            float usableH = 1f - _topInset - _bottomInset;
            float usableW = 1f - _sideInset * 2f;
            // Half-height so the board fits the usable band vertically and horizontally.
            float sizeV = needH / usableH * 0.5f;
            float sizeH = needW / (usableW * aspect) * 0.5f;
            float s = Mathf.Max(sizeV, sizeH);
            _camera.orthographicSize = s;
            // Shift so the board (plus its top margin) sits in the middle of the usable band.
            float bandCenter = _bottomInset + usableH * 0.5f;          // 0..1 from the bottom
            float offset = (0.5f - bandCenter) * s * 2f + (_topMargin - _margin) * 0.5f;
            transform.position = new Vector3(0f, offset, -10f);
            Cover();
        }

        // Scales the background so it covers the whole view (keeping its aspect).
        void Cover()
        {
            if (_background == null || _background.sprite == null || _camera == null)
                return;
            float h = _camera.orthographicSize * 2f;
            float w = h * (Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f);
            var b = _background.sprite.bounds.size;
            float k = Mathf.Max(w / b.x, h / b.y) * 1.04f;   // a little extra for camera shake
            _background.transform.localScale = new Vector3(k, k, 1f);
            _background.transform.localPosition = new Vector3(0f, 0f, 20f);
        }

        public void Shake(float strength, float duration)
        {
            if (_camera == null || strength <= 0f)
                return;
            Tween.StopAll(_camera.transform);
            _camera.transform.localPosition = Vector3.zero;
            Tween.ShakeLocalPosition(_camera.transform, new Vector3(strength, strength, 0f), duration, 18f);
        }
    }
}
