using PrimeTween;
using UnityEngine;

namespace Musketeers.Presentation
{
    // Orthographic top-down camera that fits the board between the HUD bars at any window size,
    // plus a small shake for impacts. The rig moves; the camera child shakes.
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        // Screen space the UI covers, as a fraction of screen height (top bar, plan bar).
        [SerializeField] private float _topInset = 0.095f;
        [SerializeField] private float _bottomInset = 0.235f;
        [SerializeField] private float _sideInset = 0.02f;
        [SerializeField] private float _margin = 0.45f;   // world units around the board (labels, hearts)

        public Camera Camera => _camera;

        int _boardW = 9, _boardH = 9;
        int _lastW, _lastH;

        public void Fit(int boardWidth, int boardHeight)
        {
            _boardW = boardWidth;
            _boardH = boardHeight;
            Apply();
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
            float needH = _boardH + _margin * 2f;
            float needW = _boardW + _margin * 2f;
            float usableH = 1f - _topInset - _bottomInset;
            float usableW = 1f - _sideInset * 2f;
            // Half-height so the board fits the usable band vertically and horizontally.
            float sizeV = needH / usableH * 0.5f;
            float sizeH = needW / (usableW * aspect) * 0.5f;
            float size = Mathf.Max(sizeV, sizeH);
            _camera.orthographic = true;
            _camera.orthographicSize = size;
            // Shift so the board's center sits in the middle of the usable band.
            float bandCenter = _bottomInset + usableH * 0.5f;          // 0..1 from the bottom
            float offset = (0.5f - bandCenter) * size * 2f;
            transform.position = new Vector3(0f, offset, -10f);
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
