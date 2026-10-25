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
        public Camera Camera => _camera;

        // The board's rect on screen: normalized viewport (0-1, y up). The HUD lays itself out around it.
        public Rect ArenaViewport { get; private set; } = new Rect(0.1f, 0.1f, 0.8f, 0.8f);
        // Same rect in UI Toolkit panel pixels (y down) for a panel of the given size.
        public Rect ArenaPanelRect(float panelWidth, float panelHeight) => BoardLayout.ToPanelRect(ArenaViewport, panelWidth, panelHeight);
        public event System.Action ArenaChanged;

        enum Mode { Arena, Stage }

        Mode _mode = Mode.Arena;
        int _boardW = 9, _boardH = 9;
        Vector3 _stageCenter;
        float _stageW = 16f, _stageH = 9f;
        int _lastW, _lastH;
        SpriteRenderer _background;
        Rect _arena;              // the board's place on the background art (normalized), if it has one
        bool _hasArena;
        SpriteRenderer[] _mirrors;
        readonly Vector2[] _mirrorOffset = new Vector2[8];
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

        // `arena` (optional) is where the board sits on the sprite; in the arena framing the sprite is then
        // placed so that rect lands exactly on the board. The art can't stretch past that (it is pinned to the
        // board), so mirrored copies of it tile the rest of the screen: the edges join seamlessly.
        public void SetBackground(Sprite sprite, Color clear, Rect arena = default)
        {
            _hasArena = sprite != null && arena.width > 0.01f && arena.height > 0.01f;
            _arena = arena;
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
            SetMirrors(sprite);
            if (_camera != null && _mode == Mode.Arena)
                Apply();
            else
                Cover();
        }

        void SetMirrors(Sprite sprite)
        {
            if (_mirrors == null)
            {
                _mirrors = new SpriteRenderer[8];
                int k = 0;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0)
                        continue;
                    var go = new GameObject("Mirror");
                    go.transform.SetParent(_background.transform, false);
                    var sr = go.AddComponent<SpriteRenderer>();
                    if (_material != null)
                        sr.sharedMaterial = _material;
                    sr.sortingOrder = Layers.Background;
                    sr.flipX = dx != 0;
                    sr.flipY = dy != 0;
                    _mirrorOffset[k] = new Vector2(dx, dy);
                    _mirrors[k++] = sr;
                }
            }
            for (int i = 0; i < _mirrors.Length; i++)
            {
                _mirrors[i].sprite = sprite;
                _mirrors[i].enabled = sprite != null && _hasArena;
                if (sprite != null)
                {
                    var b = sprite.bounds.size;
                    _mirrors[i].transform.localPosition = new Vector3(_mirrorOffset[i].x * b.x, _mirrorOffset[i].y * b.y, 0f);
                }
            }
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
                // The whole stage rectangle always shows (never cropped): the long side gets spare background.
                _camera.orthographicSize = ViewFit.StageOrtho(_stageW, _stageH, aspect);
                transform.position = new Vector3(_stageCenter.x, _stageCenter.y, -10f);
                Cover();
                return;
            }
            // FIXES_4E §7: the board plus its margins is fitted inside the safe area between the HUD bands, at any
            // aspect. The art's own cover framing is kept when it already fits; otherwise the camera zooms out and the
            // mirrored copies of the art fill the rest (letterbox with background, never a crop).
            var panel = ViewFit.PanelSize(Screen.width, Screen.height);
            float s;
            Vector2 c;
            if (_hasArena && _background != null && _background.sprite != null)
            {
                BoardLayout.CoverFraming(_arena, _background.sprite.bounds.size, _boardW, _boardH, aspect, out float s0, out var c0, out _);
                ViewFit.Fit(panel, _boardW, _boardH, c0, s0, out s, out c);
            }
            else
                ViewFit.Fit(panel, _boardW, _boardH, Vector2.zero, 0f, out s, out c);
            _camera.orthographicSize = s;
            transform.position = new Vector3(c.x, c.y, -10f);
            ArenaViewport = BoardLayout.ArenaViewport(c, s, aspect, _boardW, _boardH, Vector2.zero);
            Cover();
            ArenaChanged?.Invoke();
        }

        // Scales the background so it covers the whole view (keeping its aspect).
        void Cover()
        {
            if (_background == null || _background.sprite == null || _camera == null)
                return;
            if (_mode == Mode.Arena && _hasArena)
            {
                // Pinned to the board: the arena rect of the art lands on the board's world rect.
                var pl = BoardLayout.PlaceBackground(_arena, _background.sprite.bounds.size, _boardW, _boardH, Vector2.zero);
                _background.transform.localScale = new Vector3(pl.Scale, pl.Scale, 1f);
                _background.transform.position = new Vector3(pl.Center.x, pl.Center.y, 10f);
                return;
            }
            float h = _camera.orthographicSize * 2f;
            float w = h * (Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f);
            var b = _background.sprite.bounds.size;
            float k = Mathf.Max(w / b.x, h / b.y) * 1.04f;   // a little extra for camera shake
            _background.transform.localScale = new Vector3(k, k, 1f);
            _background.transform.localPosition = new Vector3(0f, 0f, 20f);
        }

        public void ResetShake()
        {
            if (_camera == null) return;
            Tween.StopAll(_camera.transform);
            _camera.transform.localPosition = Vector3.zero;
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
