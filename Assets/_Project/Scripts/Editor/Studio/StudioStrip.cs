using UnityEditor;
using UnityEngine;

namespace Musketeers.EditorTools.Studio
{
    // The other three musketeers in the same pose, outfit and weapon state. Click one to switch to it.
    public sealed class StudioStrip
    {
        public const int Cell = 180;
        const float CellZoom = 0.28f;
        readonly StudioState _s;
        readonly StudioTextures _tex;
        int _rev = -1;

        public StudioStrip(StudioState s, StudioTextures tex)
        {
            _s = s;
            _tex = tex;
        }

        public void Draw(bool dragging)
        {
            var items = _s.Outfit();
            if (!dragging && _rev != _s.Rev)
            {
                for (int i = 0; i < _s.Roster.Count; i++)
                {
                    if (i == _s.Char)
                        continue;
                    var bg = i % 2 == 0 ? new Color32(70, 70, 78, 255) : new Color32(84, 84, 92, 255);
                    _tex.Upload("strip" + i, RigCompositor.Compose(_s.Roster.Get(i), _s.Pose, items, Cell, CellZoom, _s.Options(bg)));
                }
                _rev = _s.Rev;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < _s.Roster.Count; i++)
                {
                    if (i == _s.Char)
                        continue;
                    var r = GUILayoutUtility.GetRect(Cell, Cell + 16, GUILayout.Width(Cell), GUILayout.Height(Cell + 16));
                    var t = _tex.Get("strip" + i);
                    var img = new Rect(r.x, r.y, Cell, Cell);
                    if (t != null && Event.current.type == EventType.Repaint)
                        GUI.DrawTexture(img, t);
                    GUI.Label(new Rect(r.x, r.y + Cell, Cell, 16), _s.Roster.Get(i).DisplayName, EditorStyles.centeredGreyMiniLabel);
                    EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);
                    if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                    {
                        _s.Char = i;
                        _s.Touch();
                    }
                }
            }
        }
    }
}
