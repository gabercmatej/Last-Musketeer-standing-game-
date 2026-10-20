using System;
using Musketeers.Data;
using Musketeers.Game;
using Musketeers.Presentation;
using Musketeers.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Musketeers.UI
{
    // The composition root for Main.unity and its screen flow (GAME_DESIGN §9):
    //   Menu → Lobby (stage, wardrobe, map) → Loading → Arena ↔ Scoreboard → Results → Lobby.
    // It builds the match (setup, transport, controller), wires views, input, music and UI sounds to it,
    // and owns screen switching. Presentation and UI never touch the live GameState.
    public sealed class AppFlow : MonoBehaviour
    {
        const string SpeedKey = "Musketeers.Speed";
        const float ScoreboardSeconds = 10f;
        const float EmoteCooldown = 1.2f;

        enum Screen { Menu, Lobby, Loading, Match, Results }

        [SerializeField] private UIDocument _document;
        [SerializeField] private VisualTreeAsset _menuUxml;
        [SerializeField] private VisualTreeAsset _lobbyUxml;
        [SerializeField] private VisualTreeAsset _loadingUxml;
        [SerializeField] private VisualTreeAsset _hudUxml;
        [SerializeField] private VisualTreeAsset _scoreboardUxml;
        [SerializeField] private VisualTreeAsset _resultsUxml;
        [SerializeField] private VisualTreeAsset _settingsUxml;
        [SerializeField] private StyleSheet _styles;
        [SerializeField] private TextAsset _tips;
        [SerializeField] private TextAsset _credits;
        [SerializeField] private ArtLibrary _art;
        [SerializeField] private BoardView _board;
        [SerializeField] private StageView _stage;
        [SerializeField] private CameraRig _rig;
        [SerializeField] private PlanPreview _preview;
        [SerializeField] private PlanningInput _input;
        [SerializeField] private PlaybackDirector _director;
        [SerializeField] private WorldLabels _labels;
        [SerializeField] private Fx _fx;
        [SerializeField] private AudioService _audio;

        MainMenuUI _menu;
        LobbyUI _lobby;
        LoadingUI _loading;
        HudUI _hud;
        PlanBarUI _planBar;
        LogPanelUI _log;
        ScoreboardUI _scoreboard;
        ResultsUI _results;
        SettingsUI _settings;
        VisualElement _root;
        LobbyModel _model;
        Screen _screen;

        MatchController _mc;
        LocalTransport _transport;
        MatchSetup _setup;
        Color[] _colors;
        Sprite[] _portraits;
        readonly PlanBuilder _builder = new PlanBuilder();
        TurnPlan[] _plans;
        bool[] _locked;
        int _activeSlot;
        int _speedIndex = 1;
        float _scoreboardLeft;
        bool _planning;
        int _lastPlanCount;
        bool _lastLocked;
        int _lastTimerSecond = -1;
        float _emoteReady;

        public MatchController Controller => _mc;

        void Start()
        {
            PrimeTween.PrimeTweenConfig.warnEndValueEqualsCurrent = false;
            UiArt.Art = _art;
            _rig.Init(_art.SpriteMaterial);
            _root = _document.rootVisualElement;
            _root.pickingMode = PickingMode.Ignore;
            if (_styles != null)
                _root.styleSheets.Add(_styles);

            var world = new VisualElement { name = "world-layer", pickingMode = PickingMode.Ignore };
            world.AddToClassList("world-layer");
            _root.Add(world);
            _labels.Init(world, _rig.Camera);

            var hud = AddScreen(_hudUxml);
            var lobby = AddScreen(_lobbyUxml);
            var score = AddScreen(_scoreboardUxml);
            var results = AddScreen(_resultsUxml);
            var menu = AddScreen(_menuUxml);
            var loading = AddScreen(_loadingUxml);
            var settings = AddScreen(_settingsUxml);
            UiArt.WireButtonSounds(_root);

            _model = new LobbyModel(_art.Roster, _art.Cosmetics, _art.Maps != null ? _art.Maps.Count : 1);
            _menu = new MainMenuUI(menu);
            _lobby = new LobbyUI(lobby, _model, _art);
            _loading = new LoadingUI(loading, _tips);
            _hud = new HudUI(hud);
            _planBar = new PlanBarUI(hud);
            _log = new LogPanelUI(hud);
            _scoreboard = new ScoreboardUI(score);
            _results = new ResultsUI(results);
            _settings = new SettingsUI(settings, _credits);

            _menu.PlayClicked += ShowLobby;
            _menu.SettingsClicked += () => _settings.SetVisible(true);
            _lobby.BackClicked += ShowMenu;
            _lobby.StartClicked += () => StartWithLoading(NewSetup(null));
            _lobby.Changed += RefreshStage;
            _lobby.WardrobeToggled += open => { if (open) _stage.Focus(0); else _stage.Unfocus(); };
            _lobby.Wardrobe.Equipped += () => { _stage.SetLoadout(0, _model.MyLoadout); _stage.Bounce(0); };
            _hud.SpeedClicked += CycleSpeed;
            _hud.LogClicked += () => _log.SetOpen(!_log.Open);
            _hud.QuitClicked += ShowLobby;
            _hud.SettingsClicked += () => _settings.SetVisible(true);
            _planBar.ActionClicked += t => _input.ChooseAction(t);
            _planBar.SlotClicked += t => { if (_planning && !_builder.Locked) _builder.SelectSlot(t); };
            _planBar.LockClicked += ToggleLock;
            _scoreboard.ContinueClicked += ContinueAfterRound;
            _results.AgainClicked += () => StartWithLoading(NewSetup(null));
            _results.MenuClicked += ShowLobby;
            _director.Announced += c => _hud.Banner(Callouts.Text(c), new Color(1f, 0.85f, 0.4f), 0.9f);

            _input.IsPointerOverUI = OverUI;
            _input.UnitAtTile = p => _mc?.Match?.Round?.UnitAt(p)?.Slot ?? -1;
            _input.LockToggled += ToggleLock;
            _input.CycleUnit += () => SwitchUnit(NextControllable(_activeSlot));
            _input.UnitClicked += SwitchUnit;
            _input.HoverChanged += RedrawPreview;
            _builder.Changed += OnPlanChanged;

            _speedIndex = Mathf.Clamp(PlayerPrefs.GetInt(SpeedKey, 1), 0, PlaybackDirector.Speeds.Length - 1);
            _director.Speed = PlaybackDirector.Speeds[_speedIndex];
            _hud.SetSpeed(_director.Speed);

            ShowMenu();
            ReadUrlFlags();
#if UNITY_EDITOR
            // Dev hook (Tools/Musketeers/Dev): start a bot match on a chosen map from Play mode.
            string devMap = UnityEditor.EditorPrefs.GetString("Musketeers.DevAutoplayMap", "");
            if (!string.IsNullOrEmpty(devMap) && _art.Maps != null && _art.Maps.IndexOf(devMap) >= 0)
            {
                _model.Map = _art.Maps.IndexOf(devMap);
                DevFlags.AutoPlay = true;
                DevFlags.AutoStartPlayers = 4;
            }
            // Dev hook: a human-planned match (control-all) on a chosen map, for planning-preview checks.
            string humanMap = UnityEditor.EditorPrefs.GetString("Musketeers.DevHumanMap", "");
            if (!string.IsNullOrEmpty(humanMap) && _art.Maps != null && _art.Maps.IndexOf(humanMap) >= 0)
            {
                _model.Map = _art.Maps.IndexOf(humanMap);
                DevFlags.AutoPlay = false;
                DevFlags.ControlAll = true;
                DevFlags.PlanningSeconds = 600f;
                DevFlags.AutoStartPlayers = 4;
            }
#endif
            if (DevFlags.AutoStartPlayers >= 2)
            {
                _model.Players = Mathf.Clamp(DevFlags.AutoStartPlayers, 2, 4);
                StartMatch(NewSetup("Autoplay"));
            }
        }

        VisualElement AddScreen(VisualTreeAsset uxml)
        {
            var tc = uxml.Instantiate();
            tc.pickingMode = PickingMode.Ignore;
            tc.AddToClassList("screen");
            _root.Add(tc);
            return tc;
        }

        // ?autoplay=N starts an N-player match that plays itself (deploy smoke test); &map=bridge picks the map.
        void ReadUrlFlags()
        {
            string url = Application.absoluteURL;
            int q = string.IsNullOrEmpty(url) ? -1 : url.IndexOf('?');
            if (q < 0)
                return;
            foreach (var pair in url.Substring(q + 1).Split('&'))
            {
                var kv = pair.Split('=');
                if (kv.Length != 2)
                    continue;
                if (kv[0] == "autoplay" && int.TryParse(kv[1], out int n))
                {
                    DevFlags.AutoPlay = true;
                    DevFlags.AutoStartPlayers = n;
                }
                if (kv[0] == "map" && _art.Maps != null && _art.Maps.IndexOf(kv[1]) >= 0)
                    _model.Map = _art.Maps.IndexOf(kv[1]);
            }
        }

        bool OverUI(Vector2 screen)
        {
            var panel = _root?.panel;
            if (panel == null)
                return false;
            var p = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, UnityEngine.Screen.height - screen.y));
            return panel.Pick(p) != null;
        }

        // ---------- screens ----------

        void HideAll()
        {
            _menu.SetVisible(false);
            _lobby.SetVisible(false);
            _hud.SetVisible(false);
            _scoreboard.SetVisible(false);
            _results.SetVisible(false);
            _loading.Cancel();
        }

        public void ShowMenu()
        {
            Teardown();
            HideAll();
            _stage.Hide();
            _screen = Screen.Menu;
            _menu.SetVisible(true);
            _audio?.Music("Music/menu");
        }

        public void ShowLobby()
        {
            Teardown();
            HideAll();
            _screen = Screen.Lobby;
            _model.Name = _menu.PlayerName;
            _lobby.Refresh();
            _lobby.SetVisible(true);
            RefreshStage();
            _rig.SetBackground(null, new Color(0.08f, 0.06f, 0.05f));
            _rig.FrameStage(StageView.Center, StageView.Width, StageView.Height);
            _audio?.Music("Music/lobby");
        }

        void RefreshStage()
        {
            var chars = _model.Characters();
            var names = new string[chars.Length];
            var tags = new string[chars.Length];
            for (int i = 0; i < chars.Length; i++)
            {
                names[i] = i == 0 && !string.IsNullOrEmpty(_model.Name) ? _model.Name : _art.CharacterName(chars[i]);
                tags[i] = i == 0 ? (string.IsNullOrEmpty(_model.Name) ? "You" : _art.CharacterName(chars[i])) : "Bot";
            }
            _stage.Show(chars.Length, chars, names, tags, _model.Loadouts());
            if (_lobby.Wardrobe.Open)
                _stage.Focus(0);
        }

        void Teardown()
        {
            PrimeTween.Tween.StopAll(this);   // delayed results sounds (victory line, award reveal)
            _planning = false;
            _input.Active = false;
            _director.Stop();
            _preview.Clear();
            _mc?.Stop();
            _mc = null;
            _board.Clear();
        }

        MapDef CurrentMap => _art.Maps != null ? _art.Maps.Get(_model.Map) : null;

        // Everything the match needs, from the lobby (offline: slot 0 is you, the rest are bots).
        MatchSetup NewSetup(string nameOverride)
        {
            var chars = _model.Characters();
            int n = chars.Length;
            var map = CurrentMap;
            var setup = new MatchSetup
            {
                Seed = _menu.DevSeed ?? (uint)UnityEngine.Random.Range(1, int.MaxValue),
                MapAscii = map != null && map.Layout != null ? map.Layout.text : BuiltInMaps.Courtyard,
                MapId = map != null ? map.Id : "courtyard",
                Abilities = new Ability[n],
                Characters = chars,
                Names = new string[n],
                IsBot = new bool[n],
                Loadouts = new string[n],
                LocalSlot = 0,
            };
            for (int i = 0; i < n; i++)
            {
                var def = _art.Character(chars[i]);
                setup.Abilities[i] = def != null ? def.Ability : (Ability)(chars[i] % 4);
                setup.IsBot[i] = i != 0;
                string name = nameOverride ?? _model.Name;
                setup.Names[i] = i == 0 && !string.IsNullOrEmpty(name) ? name : _art.CharacterName(chars[i]);
                setup.Loadouts[i] = setup.IsBot[i] ? "" : _model.LoadoutFor(i).Serialize();
            }
            _model.ApplyTo(setup);
            return setup;
        }

        void StartWithLoading(MatchSetup setup)
        {
            Teardown();
            HideAll();
            _screen = Screen.Loading;
            _audio?.Sfx("SFX/ui/countdown", 0.6f);
            var map = CurrentMap;
            _loading.Show(map != null ? map.DisplayName : "The Courtyard", LoadingUI.Seconds, () => StartMatch(setup));
        }

        // ---------- match ----------

        void StartMatch(MatchSetup setup)
        {
            Teardown();
            HideAll();
            _stage.Hide();
            _screen = Screen.Match;
            _setup = setup;
            int n = setup.PlayerCount;
            _colors = new Color[n];
            _portraits = new Sprite[n];
            for (int i = 0; i < n; i++)
            {
                _colors[i] = _art.CharacterColor(setup.Characters[i]);
                _portraits[i] = _art.Character(setup.Characters[i])?.Portrait;
            }
            var map = _art.Maps != null ? _art.Maps.Get(_art.Maps.IndexOf(setup.MapId)) : null;
            var theme = map != null ? map.Theme : null;
            _art.SetTheme(theme);
            _rig.SetBackground(theme != null ? theme.BackgroundArt : null, theme != null ? theme.Background : new Color(0.09f, 0.08f, 0.11f), theme != null ? theme.ArenaRect : default);
            Debug.Log($"[AppFlow] Match: {n} players on {setup.MapId}, seed {setup.Seed}, you are {setup.Names[0]} ({_art.CharacterName(setup.Characters[0])})");

            _transport = new LocalTransport
            {
                ControlAll = DevFlags.Available && DevFlags.ControlAll,
                AutoPlayHumans = DevFlags.AutoPlay,
                PlanningSecondsOverride = DevFlags.PlanningSeconds,
            };
            _mc = new MatchController(setup, _transport);
            _mc.RoundStarted += OnRoundStarted;
            _mc.PlanningStarted += OnPlanningStarted;
            _mc.TurnReady += OnTurnReady;
            _mc.RoundEnded += OnRoundEnded;
            _mc.MatchEnded += OnMatchEnded;

            _hud.SetVisible(true);
            _hud.BuildScores(setup.Names, _colors, _portraits);
            _hud.SetDevBadge(_transport.ControlAll);
            _log.Clear();
            _audio?.Music("Music/battle");
            _mc.Start();
        }

        // Which tiles of the map are bridge planks (presentation only), read from the map's layout text.
        bool[] BridgesOf(string mapId)
        {
            var layout = _art.Maps != null ? _art.Maps.Get(_art.Maps.IndexOf(mapId))?.Layout : null;
            if (layout == null)
                return null;
            try { return MapParser.Parse(layout.text).Bridges; }
            catch (System.FormatException) { return null; }
        }

        // Bots never wear cosmetics (FIXES_v0.2 §1.3), whatever the setup carries.
        Loadout[] SetupLoadouts()
        {
            var l = new Loadout[_setup.PlayerCount];
            for (int i = 0; i < l.Length; i++)
            {
                bool bot = _setup.IsBot != null && i < _setup.IsBot.Length && _setup.IsBot[i];
                l[i] = !bot && _setup.Loadouts != null && i < _setup.Loadouts.Length ? Loadout.Parse(_setup.Loadouts[i], default) : default;
                if (_art.Cosmetics != null)
                    l[i] = _art.Cosmetics.Sanitize(l[i]);
            }
            return l;
        }

        void OnRoundStarted(EventLog opening)
        {
            var m = _mc.Match;
            _board.Build(m.Round, m.Cfg, _setup.Characters, _setup.Names, SetupLoadouts(), BridgesOf(_setup.MapId));
            _rig.Fit(m.Round.Board.Width, m.Round.Board.Height);
            _director.ShowState(m.Round, m.Crowned);
            _log.Line($"Round {m.RoundNumber}{(m.SuddenDeath ? " (sudden death)" : "")}: En garde!", "log-line--turn");
            if (m.Crowned >= 0)
            {
                _log.Line($"{_setup.Names[m.Crowned]} wears the Crown", "log-line--big");
                _audio?.Sfx("SFX/match/crown_gain", 0.8f);
            }
            _hud.Banner(m.SuddenDeath ? "SUDDEN DEATH!" : "EN GARDE!", new Color(1f, 0.85f, 0.4f), 1.2f);
            _audio?.Sfx("SFX/match/round_start", 0.7f);
            _audio?.Announce(Callouts.Clip(Callout.EnGarde));
            _audio?.Music(MusicPicker.IsFinal(m.Round, m.Cfg) ? "Music/battle_final" : "Music/battle");
        }

        void OnPlanningStarted(int turnSeq, float seconds)
        {
            var round = _mc.Match.Round;
            int n = _setup.PlayerCount;
            _plans = new TurnPlan[n];
            _locked = new bool[n];
            for (int i = 0; i < n; i++)
                _plans[i] = new TurnPlan(i);
            _planning = true;
            _lastTimerSecond = -1;
            _activeSlot = _setup.LocalSlot;
            if (!round.Units[_activeSlot].Alive && _transport.ControlAll)
                _activeSlot = NextControllable(_activeSlot);

            bool canPlan = round.Units[_activeSlot].Alive && !_transport.AutoPlayHumans;
            _input.Active = canPlan;
            _planBar.SetVisible(true);
            _hud.SetRound(_mc.Match.RoundNumber, _mc.Match.SuddenDeath, round.Turn);
            _hud.SetPhase(canPlan ? "Plan your moves" : "Watching");
            LoadActive();
            _audio?.Sfx("SFX/match/turn_start", 0.5f);
            if (!canPlan)
            {
                _preview.Clear();
                _planBar.SetMessage(_transport.AutoPlayHumans ? "Autoplay: bots are planning for you." : "You're out this round. Watch the rest!");
            }
        }

        void LoadActive()
        {
            var round = _mc.Match.Round;
            _builder.Load(round, _activeSlot, _mc.Cfg, _plans[_activeSlot], _locked[_activeSlot]);
            _input.Builder = _builder;
            for (int i = 0; i < _board.UnitCount; i++)
                _board.Unit(i)?.SetSelected(_transport.ControlAll && i == _activeSlot);
            var def = _art.Character(_setup.Characters[_activeSlot]);
            _planBar.SetCharacter(def, _mc.Cfg, round.Units[_activeSlot].Ability, _setup.Names[_activeSlot], _colors[_activeSlot]);
            _planBar.Refresh(_builder, _mc.Cfg, def);
            _lastPlanCount = PlanCount(_builder.Plan);
            _lastLocked = _builder.Locked;
            RedrawPreview();
        }

        static int PlanCount(TurnPlan p)
        {
            int c = 0;
            for (int t = 0; t < TurnPlan.Length; t++)
            {
                if (p[t].Type != ActionType.Idle)
                    c++;
            }
            return c;
        }

        void OnPlanChanged()
        {
            if (!_planning || _mc == null || _transport.AutoPlayHumans)
                return;
            _plans[_activeSlot] = _builder.Plan;
            _locked[_activeSlot] = _builder.Locked;
            _mc.SubmitPlan(_builder.Plan, _builder.Locked);
            _planBar.Refresh(_builder, _mc.Cfg, _art.Character(_setup.Characters[_activeSlot]));
            RedrawPreview();
            // UI feedback: added / removed an action, locked in.
            int count = PlanCount(_builder.Plan);
            if (_builder.Locked && !_lastLocked)
                _audio?.Sfx("SFX/match/lock_in");
            else if (count > _lastPlanCount)
                _audio?.Sfx("SFX/ui/plan_add");
            else if (count < _lastPlanCount)
                _audio?.Sfx("SFX/ui/plan_undo");
            _lastPlanCount = count;
            _lastLocked = _builder.Locked;
        }

        void RedrawPreview()
        {
            if (!_planning || _mc == null || !_input.Active)
                return;
            _preview.Draw(_builder, _colors[_activeSlot], _builder.Locked ? null : _input.Hover);
        }

        void ToggleLock()
        {
            if (!_planning || !_input.Active)
                return;
            bool lockNow = !_builder.Locked;
            _builder.SetLocked(lockNow);
            // Control-all: locking one unit moves on to the next one that still needs a plan.
            if (lockNow && _transport.ControlAll)
            {
                int next = NextControllable(_activeSlot, unlockedOnly: true);
                if (next != _activeSlot)
                    SwitchUnit(next);
            }
        }

        void SwitchUnit(int slot)
        {
            if (!_planning || !_transport.ControlAll || slot < 0 || slot == _activeSlot)
                return;
            if (!_mc.Match.Round.Units[slot].Alive)
                return;
            _activeSlot = slot;
            LoadActive();
        }

        int NextControllable(int from, bool unlockedOnly = false)
        {
            var round = _mc.Match.Round;
            int n = _setup.PlayerCount;
            for (int k = 1; k <= n; k++)
            {
                int s = (from + k) % n;
                if (round.Units[s].Alive && (!unlockedOnly || !_locked[s]))
                    return s;
            }
            return from;
        }

        void OnTurnReady(TurnPlayback p)
        {
            _planning = false;
            _input.Active = false;
            _preview.Clear();
            _planBar.HideTooltip();
            for (int i = 0; i < _board.UnitCount; i++)
                _board.Unit(i)?.SetSelected(false);
            _planBar.SetMessage("Resolving...");
            _hud.SetPhase("Resolving");
            _log.Turn(p);
            _audio?.Sfx("SFX/match/reveal", 0.7f);
            _director.Play(p, p.CrownedBefore, () =>
            {
                if (_mc == null)
                    return;
                var m = _mc.Match;
                if (p.Outcome.RoundResult == null && MusicPicker.IsFinal(p.After, m.Cfg))
                    _audio?.Music("Music/battle_final", 0.8f);
                _mc.PlaybackFinished();
            });
        }

        void OnRoundEnded(RoundResult r)
        {
            var m = _mc.Match;
            _scoreboard.Show(r, m, _setup.Names, _colors, _portraits);
            _scoreboardLeft = _transport.AutoPlayHumans ? 1.5f : ScoreboardSeconds;
            _hud.SetPhase("Round over");
            if (r.Winner >= 0)
                _log.Line($"{_setup.Names[r.Winner]} wins round {r.RoundNumber}", "log-line--turn");
            else
                _director.Announce(Callout.Draw);
            _audio?.Sfx("SFX/match/points_tick", 0.8f);
        }

        void ContinueAfterRound()
        {
            if (_mc == null || _mc.Phase != MatchPhase.RoundOver)
                return;
            _scoreboard.SetVisible(false);
            _mc.ContinueAfterRound();
        }

        void OnMatchEnded()
        {
            var m = _mc.Match;
            _planBar.SetVisible(false);
            _hud.SetVisible(false);
            _screen = Screen.Results;
            Debug.Log($"[AppFlow] Match over after {m.RoundNumber} rounds; winner {_setup.Names[m.MatchWinner]}");

            // The results stage: everyone in their outfits, the winner in the victory pose with the Crown.
            var tags = new string[_setup.PlayerCount];
            for (int i = 0; i < tags.Length; i++)
                tags[i] = $"{m.Points[i]} pts";
            _stage.Show(_setup.PlayerCount, _setup.Characters, _setup.Names, tags, SetupLoadouts());
            _stage.ShowPodium(m.MatchWinner);
            _rig.SetBackground(null, new Color(0.08f, 0.06f, 0.05f));
            _rig.FrameStage(StageView.Center, StageView.Width, StageView.Height);
            _board.Clear();
            _results.Show(m, _setup.Names, _colors, _portraits);
            _audio?.Music("Music/results", 0.6f);
            _audio?.Sfx("SFX/match/match_win", 0.8f);
            _audio?.Announce(Callouts.Clip(Callout.LastMusketeer));
            if (m.MatchWinner >= 0)
            {
                var def = _art.Character(_setup.Characters[m.MatchWinner]);
                Tween(3.2f, () => _audio?.Line(def != null ? def.Id : null, "victory"));
            }
            Tween(1.5f, () => _audio?.Sfx("SFX/match/award_reveal", 0.6f));
        }

        void Tween(float delay, Action a) => PrimeTween.Tween.Delay(this, delay, _ => a());

        void CycleSpeed()
        {
            _speedIndex = (_speedIndex + 1) % PlaybackDirector.Speeds.Length;
            _director.Speed = PlaybackDirector.Speeds[_speedIndex];
            _hud.SetSpeed(_director.Speed);
            PlayerPrefs.SetInt(SpeedKey, _speedIndex);
            PlayerPrefs.Save();
        }

        void ToggleControlAll()
        {
            DevFlags.ControlAll = !DevFlags.ControlAll;
            if (_transport == null)
                return;
            _transport.ControlAll = DevFlags.ControlAll;
            _hud.SetDevBadge(DevFlags.ControlAll);
            if (!_planning)
                return;
            var units = _mc.Match.Round.Units;
            if (!DevFlags.ControlAll)
                _activeSlot = _setup.LocalSlot;
            else if (!units[_activeSlot].Alive)
                _activeSlot = NextControllable(_activeSlot);
            _input.Active = units[_activeSlot].Alive && !_transport.AutoPlayHumans;
            LoadActive();
        }

        // Z / X / C / V: laugh, angry, salute, cry over your own musketeer, with one of your taunts (§8.7).
        void Emote(int index)
        {
            if (_mc == null || Time.unscaledTime < _emoteReady)
                return;
            // §8.7: emotes are for the planning phase, by a musketeer who's still standing.
            var u = _board.Unit(_setup.LocalSlot);
            if (u == null || !u.Alive || _mc.Phase != MatchPhase.Planning)
                return;
            _emoteReady = Time.unscaledTime + EmoteCooldown;
            _fx.Emote(u.transform, new Vector3(0.35f, UnitView.FeetY + 1.85f, 0f), UiArt.Get(GameArt.Emotes[index]), 2f);
            _audio?.Sfx("SFX/ui/emote_pop");
            _audio?.Line(u.CharacterId, "taunt", index);
        }

        static readonly Key[] EmoteKeys = { Key.Z, Key.X, Key.C, Key.V };

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _loading.Tick(dt);
            var kb = Keyboard.current;
            bool inMatch = _mc != null;
            if (kb != null && inMatch)
            {
                if (DevFlags.Available && kb.f2Key.wasPressedThisFrame)
                    ToggleControlAll();
                if (kb.lKey.wasPressedThisFrame)
                    _log.SetOpen(!_log.Open);
                for (int i = 0; i < EmoteKeys.Length; i++)
                {
                    if (kb[EmoteKeys[i]].wasPressedThisFrame)
                        Emote(i);
                }
            }
            if (kb != null && kb.escapeKey.wasPressedThisFrame && _settings.Open)
                _settings.SetVisible(false);
            if (!inMatch || _screen != Screen.Match)
                return;

            _mc.Update(dt);
            if (_mc == null)
                return;
            var m = _mc.Match;
            bool planning = _mc.Phase == MatchPhase.Planning;
            _hud.SetTimer(_mc.PlanningSecondsLeft, planning);
            if (planning)
                TimerSounds(_mc.PlanningSecondsLeft);
            // During playback the live round is already resolved, so show who was alive at its start
            // (and the points/crown from before it, so the HUD doesn't spoil the result).
            if (_mc.Phase == MatchPhase.Playback && _mc.Current != null)
                _hud.SetScores(_mc.Current.PointsBefore, _mc.Current.CrownedBefore, _mc.Current.Before, 0, false);
            else
                _hud.SetScores(m.Points, m.Crowned, m.Round, _mc.LockMask, planning);

            if (_mc.Phase == MatchPhase.RoundOver)
            {
                _scoreboardLeft -= dt;
                _scoreboard.SetCountdown(Mathf.CeilToInt(Mathf.Max(0f, _scoreboardLeft)), m.MatchOver);
                if (_scoreboardLeft <= 0f)
                    ContinueAfterRound();
            }
        }

        // The last five seconds tick; zero gets the end sound (only while you can still plan).
        void TimerSounds(float left)
        {
            int s = Mathf.CeilToInt(left);
            if (s == _lastTimerSecond)
                return;
            _lastTimerSecond = s;
            if (!_input.Active || _builder.Locked)
                return;
            if (s <= 5 && s >= 1)
                _audio?.Sfx("SFX/match/timer_tick", 0.6f);
        }

        // ---------- dev/MCP helpers (screens without clicking) ----------

        public void DevStartFromLobby(int players, int map)
        {
            _model.Players = Mathf.Clamp(players, 2, 4);
            _model.Map = Mathf.Clamp(map, 0, _model.MapCount - 1);
            StartWithLoading(NewSetup(null));
        }

        public void DevOpenWardrobe(bool open)
        {
            if (_screen != Screen.Lobby)
                ShowLobby();
            _lobby.SetWardrobe(open);
        }
    }
}
