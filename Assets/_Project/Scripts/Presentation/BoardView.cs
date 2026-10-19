using System.Collections.Generic;
using Musketeers.Sim;
using PrimeTween;
using UnityEngine;

namespace Musketeers.Presentation
{
    // Tiles, units and kegs for one round. Build() makes the views for a fresh GameState; Sync() snaps
    // every view to a state (after playback, so the screen always ends on the sim's truth).
    public sealed class BoardView : MonoBehaviour
    {
        [SerializeField] private ArtLibrary _art;
        [SerializeField] private WorldLabels _labels;

        public ArtLibrary Art => _art;
        public WorldLabels Labels => _labels;
        public GameConfig Config { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        SpriteRenderer[] _ground;
        SpriteRenderer[] _props;
        SpriteRenderer[] _warn;
        Tween[] _warnTweens;
        TileType[] _shown;
        UnitView[] _units = new UnitView[0];
        int[] _characters;
        readonly Dictionary<int, KegView> _kegs = new Dictionary<int, KegView>();
        readonly List<int> _scratch = new List<int>();
        Transform _tileRoot;
        Transform _pieceRoot;

        public int UnitCount => _units.Length;
        public UnitView Unit(int slot) => slot >= 0 && slot < _units.Length ? _units[slot] : null;

        public void Build(GameState s, GameConfig cfg, int[] characters, string[] names)
        {
            Clear();
            Config = cfg;
            Width = s.Board.Width;
            Height = s.Board.Height;
            BoardMath.Width = Width;
            BoardMath.Height = Height;
            _characters = characters;

            _tileRoot = new GameObject("Tiles").transform;
            _tileRoot.SetParent(transform, false);
            _pieceRoot = new GameObject("Pieces").transform;
            _pieceRoot.SetParent(transform, false);

            int n = Width * Height;
            _ground = new SpriteRenderer[n];
            _props = new SpriteRenderer[n];
            _warn = new SpriteRenderer[n];
            _warnTweens = new Tween[n];
            _shown = new TileType[n];
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                int i = y * Width + x;
                var p = new Int2(x, y);
                var g = _art.MakeRenderer($"Tile {x},{y}", _tileRoot, _art.FloorSprite, Color.white, Layers.Tile);
                g.transform.localPosition = BoardMath.ToWorld(p);
                _ground[i] = g;
                var prop = _art.MakeRenderer("Prop", g.transform, null, Color.white, Layers.Tile + 2);
                _props[i] = prop;
                var warn = _art.MakeRenderer("Warn", g.transform, PlaceholderArt.Square, Color.clear, Layers.TileOverlay);
                _warn[i] = warn;
                _shown[i] = (TileType)255;
            }

            _units = new UnitView[s.Units.Length];
            for (int i = 0; i < s.Units.Length; i++)
            {
                var go = new GameObject($"Unit {i} {names[i]}");
                go.transform.SetParent(_pieceRoot, false);
                var uv = go.AddComponent<UnitView>();
                uv.Build(_art, _labels, i, characters[i], names[i], cfg.MaxHp);
                _units[i] = uv;
            }
        }

        public void Clear()
        {
            if (_warnTweens != null)
            {
                foreach (var t in _warnTweens)
                    t.Stop();
            }
            foreach (var k in _kegs.Values)
            {
                if (k != null)
                    Destroy(k.gameObject);
            }
            _kegs.Clear();
            foreach (var u in _units)
            {
                if (u != null)
                    Destroy(u.gameObject);
            }
            _units = new UnitView[0];
            if (_tileRoot != null)
                Destroy(_tileRoot.gameObject);
            if (_pieceRoot != null)
                Destroy(_pieceRoot.gameObject);
        }

        public void Sync(GameState s, int crowned)
        {
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                var p = new Int2(x, y);
                SetTile(p, s.Board.Get(p));
                SetWarned(p, s.Board.Get(p) != TileType.Pit && Crumble.IsWarned(s, p, Config));
            }

            for (int i = 0; i < _units.Length; i++)
            {
                var u = s.Units[i];
                var v = _units[i];
                Tween.StopAll(v.transform);
                Tween.StopAll(v.Body);
                v.Body.localPosition = Vector3.zero;
                v.Body.localRotation = Quaternion.identity;
                v.SetGridPos(u.Pos);
                v.SyncStats(u, crowned == i);
                v.SetParry(false);
                if (!u.Alive)
                    v.SetAlive(false, s.Board.Get(u.Pos) == TileType.Pit);
            }

            // Kegs: drop views that no longer exist, add or move the rest.
            _scratch.Clear();
            foreach (var id in _kegs.Keys)
                _scratch.Add(id);
            foreach (var id in _scratch)
            {
                if (FindKeg(s, id) == null)
                    RemoveKeg(id);
            }
            foreach (var k in s.Kegs)
            {
                var kv = Keg(k.Id) ?? SpawnKeg(k.Id, k.Owner, k.Pos);
                Tween.StopAll(kv.transform);
                kv.transform.localScale = Vector3.one;
                kv.SetGridPos(k.Pos);
                kv.SetFuse(k.Fuse);
                kv.ShowFuse(true);
            }
        }

        static Keg FindKeg(GameState s, int id)
        {
            foreach (var k in s.Kegs)
            {
                if (k.Id == id)
                    return k;
            }
            return null;
        }

        public KegView Keg(int id) => _kegs.TryGetValue(id, out var k) ? k : null;

        public KegView SpawnKeg(int id, int owner, Int2 pos)
        {
            var go = new GameObject($"Keg {id}");
            go.transform.SetParent(_pieceRoot, false);
            var kv = go.AddComponent<KegView>();
            var ownerColor = owner >= 0 && _characters != null && owner < _characters.Length
                ? _art.CharacterColor(_characters[owner])
                : new Color(0.8f, 0.8f, 0.8f);
            kv.Build(_art, _labels, id, ownerColor);
            kv.SetGridPos(pos);
            kv.SetFuse(Config.KegFuseTicks);
            _kegs[id] = kv;
            return kv;
        }

        public void RemoveKeg(int id)
        {
            if (_kegs.TryGetValue(id, out var k))
            {
                if (k != null)
                    Destroy(k.gameObject);
                _kegs.Remove(id);
            }
        }

        public SpriteRenderer Ground(Int2 p) => InBounds(p) ? _ground[p.Y * Width + p.X] : null;
        public SpriteRenderer Prop(Int2 p) => InBounds(p) ? _props[p.Y * Width + p.X] : null;

        bool InBounds(Int2 p) => p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height;

        public void SetTile(Int2 p, TileType t)
        {
            if (!InBounds(p))
                return;
            int i = p.Y * Width + p.X;
            var g = _ground[i];
            var prop = _props[i];
            g.transform.localScale = Vector3.one;
            prop.transform.localScale = Vector3.one;
            prop.transform.localPosition = Vector3.zero;
            if (_shown[i] == t)
                return;
            _shown[i] = t;
            var theme = _art.Theme;
            bool odd = ((p.X + p.Y) & 1) == 1;
            Color floor = theme != null ? (odd ? theme.FloorB : theme.FloorA) : new Color(0.38f, 0.35f, 0.3f);
            if (t == TileType.Pit)
            {
                g.sprite = _art.PitSprite;
                g.color = theme != null ? theme.PitColor : new Color(0.05f, 0.06f, 0.1f);
            }
            else
            {
                g.sprite = _art.FloorSprite;
                g.color = _art.FloorSprite == PlaceholderArt.Floor ? floor : Color.white;
            }

            switch (t)
            {
                case TileType.Pillar:
                    prop.sprite = _art.PillarSprite;
                    prop.color = _art.PillarSprite == PlaceholderArt.Pillar && theme != null ? theme.PillarColor : Color.white;
                    prop.enabled = true;
                    break;
                case TileType.Crate:
                    prop.sprite = _art.CrateSprite;
                    prop.color = _art.CrateSprite == PlaceholderArt.Crate && theme != null ? theme.CrateColor : Color.white;
                    prop.enabled = true;
                    break;
                default:
                    prop.enabled = false;
                    break;
            }
        }

        // §3: a warned ring glows red and pulses for the turn before it falls.
        public void SetWarned(Int2 p, bool warned)
        {
            if (!InBounds(p))
                return;
            int i = p.Y * Width + p.X;
            var w = _warn[i];
            bool active = _warnTweens[i].isAlive;
            if (warned == active)
                return;
            if (!warned)
            {
                _warnTweens[i].Stop();
                w.color = Color.clear;
                return;
            }
            var c = _art.Theme != null ? _art.Theme.WarnColor : new Color(0.95f, 0.15f, 0.12f, 0.55f);
            w.color = new Color(c.r, c.g, c.b, c.a * 0.35f);
            _warnTweens[i] = Tween.Color(w, c, 0.6f, Ease.InOutSine, -1, CycleMode.Yoyo);
        }

        public bool IsWarned(Int2 p) => InBounds(p) && _warnTweens[p.Y * Width + p.X].isAlive;
    }
}
