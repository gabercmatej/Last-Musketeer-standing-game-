using System;
using Musketeers.Data;
using System.Text;
using Musketeers.Game;
using Musketeers.Net;
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
    public sealed partial class AppFlow : MonoBehaviour
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
        [SerializeField] private TextAsset _lore;
        [SerializeField] private TextAsset _howToPlay;
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
        [SerializeField] private NetSession _net;    // Day 5: null in a scene built before online play; Host/Join then do nothing

        MainMenuUI _menu;
        LobbyUI _lobby;
        LoadingUI _loading;
        HudUI _hud;
        ChatUI _chat;
        readonly ChatModel _chatModel = new ChatModel();
        PlanBarUI _planBar;
        LogPanelUI _log;
        ScoreboardUI _scoreboard;
        ResultsUI _results;
        InfoWindowUI _info;
        CharacterSelectUI _charSelect;
        ConfirmDialog _confirm;
        JoinDialog _joinDialog;
        readonly NetLobbyBackend _backend = new NetLobbyBackend();
        LobbyNet _lobbyNet;               // the LobbyNet whose Changed we listen to while online
        bool _onlineBusy;
        string _pendingJoinCode = "";
        string _stageSig = "";
        int _themeMap = -1;
        bool _charSelectFromLobby;
        VisualElement _root;
        LobbyModel _model;
        Screen _screen;

        MatchController _mc;
        LocalTransport _transport;          // offline only; null while an online match runs
        NetTransport _netTransport;         // online only
        MatchStartInfo _pendingOnline;      // set by MatchStartRpc, consumed by StartMatch after the loading screen
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
        int _dialogClosedFrame = -1;
        int[] _livePoints;          // points as shown during a turn's playback: PointsBefore plus each kill as it happens (FIXES_4E §8.6)
        bool[] _matchPointMask = new bool[4];
        int _lastScreenW, _lastScreenH;
        float _origMatch = -1f;

        public MatchController Controller => _mc;

        void Start()
        {
            // 4F §5: a turn-based board game doesn't need 120 Hz; on ProMotion / high-refresh screens the browser
            // would otherwise render twice as many frames.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
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
            _joinDialog = new JoinDialog(_root);
            _confirm = new ConfirmDialog(_root);   // added last: it sits above every screen

            _model = new LobbyModel(_art.Roster, _art.Cosmetics, _art.Maps != null ? _art.Maps.Count : 1);
            _menu = new MainMenuUI(menu);
            _lobby = new LobbyUI(lobby, _model, _art, _confirm);
            _loading = new LoadingUI(loading, _tips);
            _hud = new HudUI(hud);
            _hud.BindArena(_rig);
            _chat = new ChatUI(hud.Q("chat"));
            _planBar = new PlanBarUI(hud);
            _log = new LogPanelUI(hud);
            _scoreboard = new ScoreboardUI(score);
            _results = new ResultsUI(results);
            _info = new InfoWindowUI(settings, _credits, _lore, _howToPlay);
            _charSelect = new CharacterSelectUI(menu, _model, _art);
            InitDay6Lobby();
            InitReplay();
            InitHistory();

            _menu.PlayClicked += ShowCharacterSelect;
            _menu.HostClicked += HostOnline;
            _menu.JoinClicked += () => _joinDialog.Show(_pendingJoinCode);
            _joinDialog.JoinRequested += JoinOnline;
            if (_net != null)
                _net.Ended += OnNetEnded;
            MatchNet.MatchStarted += OnMatchStartInfo;
            MatchNet.ReturnedToLobby += OnReturnedToLobby;
            MatchNet.ChatReceived += OnChatReceived;
            LobbyNet.CharacterCount = _model.RosterCount;
            LobbyNet.MapCount = _model.MapCount;
            _menu.LoreClicked += () => _info.OpenTab(InfoWindowUI.Tab.Lore);
            _menu.SettingsClicked += () => _info.OpenTab(InfoWindowUI.Tab.Settings);
            _charSelect.Selected += c =>
            {
                _model.PickCharacter(c);   // online the host decides; Character follows the snapshot
                _model.Save();
                ShowLobby();
            };
            _charSelect.Back += () => { if (_charSelectFromLobby) ShowLobby(); else LeaveToMenu(); };
            _lobby.BackClicked += LeaveToMenu;
            _lobby.StartClicked += () =>
            {
                if (_model.Online)
                    StartOnlineMatch();
                else
                    StartWithLoading(NewSetup(null));
            };
            _lobby.SettingsClicked += () => _info.OpenTab(InfoWindowUI.Tab.Settings);
            _lobby.ChangeMusketeerClicked += ChangeMusketeer;
            _lobby.Changed += () => { RefreshStage(); ApplyLobbyTheme(false); };
            _lobby.ReadyChanged += () => _stage.SetReady(_model.Ready());
            _lobby.WardrobeToggled += open => { if (open) _stage.Focus(0); else _stage.Unfocus(); };
            _lobby.Wardrobe.Equipped += () => { _stage.SetLoadout(0, _model.MyLoadout); _stage.Bounce(0); };
            _hud.SpeedClicked += CycleSpeed;
            _hud.LogClicked += () => _log.SetOpen(!_log.Open);
            _hud.LeaveClicked += AskLeaveMatch;
            _chat.Sent += OnChatSent;
            _chat.EmoteClicked += Emote;
            _hud.SettingsClicked += () => _info.OpenTab(InfoWindowUI.Tab.Settings);
            _planBar.ActionClicked += t => _input.ChooseAction(t);
            _planBar.SlotClicked += t => { if (_planning && !_builder.Locked) _builder.SelectSlot(t); };
            _planBar.LockClicked += ToggleLock;
            _scoreboard.ContinueClicked += ContinueAfterRound;
            _results.AgainClicked += ReturnToLobbyClicked;   // Return to lobby
            _results.MenuClicked += LeaveToMenu;  // Quit
            _results.SettingsClicked += () => _info.OpenTab(InfoWindowUI.Tab.Settings);
            // Return to lobby: straight back to the same lobby, same players, bots and settings (the lobby restores its art and music).
            _director.PointsScored += OnPointsScored;
            _director.Announced += c => _hud.Banner(Callouts.Text(c), new Color(1f, 0.85f, 0.4f), 0.9f, c == Callout.EnGarde || c == Callout.Draw);

            _input.IsPointerOverUI = OverUI;
            _input.IsTyping = Typing;   // typing in chat or a dialog: no gameplay hotkeys
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
            _pendingJoinCode = NetSession.ReadJoinCode();
            if (_pendingJoinCode.Length > 0 && _net != null)
            {
                // Opened from an invite link: straight to the join dialog if we already know the player's name.
                if (_menu.HasValidName)
                    _joinDialog.Show(_pendingJoinCode);
                else
                    _menu.SetNote("Invite code ready. Type your name, then press Join.");
            }
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
            AutoTestInit();
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

        // No gameplay or dev hotkeys while typing in chat or with a dialog/window open, including the frame
        // whose Esc closed it (so that Esc can't also cancel a pending action in PlanningInput).
        bool Typing() => HistoryOpen || LobbyTyping || _chat.IsTyping || _confirm.Open || _joinDialog.Open || _info.Open || Time.frameCount == _dialogClosedFrame;

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
            CloseLobbyDialogs();
            CloseHistory();
            _menu.SetVisible(false);
            _charSelect.Hide();
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

        // Menu (valid name) -> Pick your Musketeer (over the dimmed lobby stage) -> Lobby. The lobby's Change Musketeer (chunk 4) calls this too.
        public void ShowCharacterSelect()
        {
            Teardown();
            HideAll();
            _screen = Screen.Menu;
            _charSelectFromLobby = false;
            _model.Name = _menu.PlayerName;
            ShowStageBackdrop();
            _charSelect.Show(_model.Character);
        }

        // FIXES_4E §2.1: the picker is drawn over the real lobby (the tavern stage with the musketeers already in it),
        // dimmed by the overlay, instead of a plain background.
        void ShowStageBackdrop()
        {
            RefreshStage();
            _rig.SetBackground(null, new Color(0.08f, 0.06f, 0.05f));
            _rig.FrameStage(StageView.Center, StageView.Width, StageView.Height);
            var map = CurrentMap;
            var art = map != null && map.LobbyBackground != null ? map.LobbyBackground : UiArt.Get(GameArt.LobbyBackground);
            _stage.SetBackground(art, 0f);
        }

        // Lobby -> Change Musketeer: the same overlay; Back and Select both return to the lobby.
        void ChangeMusketeer()
        {
            _charSelectFromLobby = true;
            _lobby.SetVisible(false);
            ShowStageBackdrop();
            _charSelect.Show(_model.Character);
        }

        public void ShowLobby()
        {
            Teardown();
            HideAll();
            _screen = Screen.Lobby;
            _charSelectFromLobby = false;
            _model.Name = _menu.PlayerName;
            if (!_model.Online)   // online, host/guest comes from the network
            {
                bool host = !(DevFlags.Available && DevFlags.LobbyAsGuest);
                if (host != _model.IsHost)
                    _model.SetHost(host);
            }
            _lobby.SetInviteCode(_model.Online && _net != null ? _net.Code : "");
            _lobby.Refresh();
            _lobby.SetVisible(true);
            RefreshStage();
            _rig.SetBackground(null, new Color(0.08f, 0.06f, 0.05f));
            _rig.FrameStage(StageView.Center, StageView.Width, StageView.Height);
            ApplyLobbyTheme(true);
            _themeMap = _model.Map;
        }

        // The selected map's lobby art and music (MapDef). A map change fades the background and crossfades the
        // music; ShowLobby (also after Results -> Play again) applies it instantly.
        void ApplyLobbyTheme(bool instant)
        {
            var map = CurrentMap;
            var art = map != null && map.LobbyBackground != null ? map.LobbyBackground : UiArt.Get(GameArt.LobbyBackground);
            _stage.SetBackground(art, instant ? 0f : 0.4f);
            PlayLobbyMusic();
        }

        // The lobby track of the selected map (MapDef.LobbyMusic).
        public void PlayLobbyMusic()
        {
            var map = CurrentMap;
            string key = map != null && !string.IsNullOrEmpty(map.LobbyMusic) ? map.LobbyMusic : "Music/arena_lobby";
            _audio?.Music(key, 1.0f);
        }

        void RefreshStage()
        {
            var chars = _model.Characters();
            var names = _model.SeatNames();   // online: each seat's own name; offline: null
            if (names == null)
            {
                names = new string[chars.Length];
                for (int i = 0; i < chars.Length; i++)
                    names[i] = i != 0 ? "Bot" : !string.IsNullOrEmpty(_model.Name) ? _model.Name : _art.CharacterName(chars[i]);
            }
            _stage.Show(chars.Length, chars, names, null, _model.Loadouts(), _model.Ready());
            if (_lobby.Wardrobe.Open)
                _stage.Focus(0);
        }

        void Teardown()
        {
            CancelReplay();
            PrimeTween.Tween.StopAll(this);   // delayed results sounds (victory line)
            _results.Stop();                  // pending badge reveals
            _planning = false;
            _input.Active = false;
            _director.Stop();
            _preview.Clear();
            _mc?.Stop();
            _mc = null;
            _netTransport = null;
            if (MatchNet.Instance != null)
                MatchNet.Instance.Transport = null;
            _board.Clear();
            _chat?.Blur();
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

            var online = _pendingOnline;
            BeginHistory(setup, online);
            _pendingOnline = null;
            IMatchTransport transport;
            if (online != null && MatchNet.Instance != null)
            {
                _transport = null;
                _netTransport = new NetTransport(MatchNet.Instance, _net != null && _net.IsHost, online.SlotClients());
                _netTransport.Warn += w => Debug.LogWarning("[Net] " + w);
                _netTransport.SnapshotReceived += OnSnapshot;
                if (DevFlags.Available && DevFlags.PlanningSeconds > 0f)
                    _netTransport.PlanningSecondsOverride = DevFlags.PlanningSeconds;
                MatchNet.Instance.Transport = _netTransport;
                transport = _netTransport;
            }
            else
            {
                _netTransport = null;
                _transport = new LocalTransport
                {
                    ControlAll = DevFlags.Available && DevFlags.ControlAll,
                    AutoPlayHumans = DevFlags.AutoPlay,
                    PlanningSecondsOverride = DevFlags.PlanningSeconds,
                };
                transport = _transport;
            }
            _mc = new MatchController(setup, transport);
            _mc.RoundStarted += OnRoundStarted;
            _mc.PlanningStarted += OnPlanningStarted;
            _mc.TurnReady += OnTurnReady;
            _mc.RoundEnded += OnRoundEnded;
            _mc.MatchEnded += OnMatchEnded;

            _chatModel.Clear();
            _chat.Bind(_chatModel, setup.Names, _colors);
            _hud.SetVisible(true);
            _hud.BuildScores(setup.Names, _colors, _portraits);
            _planBar.SetPlayerNames(setup.Names);
            _hud.SetDevBadge(ControlAllOn);
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

        // Bots use character-colored defaults; human outfits retain explicit saved choices.
        Loadout[] SetupLoadouts()
        {
            var l = new Loadout[_setup.PlayerCount];
            for (int i = 0; i < l.Length; i++)
            {
                bool bot = _setup.IsBot != null && i < _setup.IsBot.Length && _setup.IsBot[i];
                l[i] = bot ? _art.Character(_setup.Characters[i])?.DefaultLoadout ?? default
                    : _setup.Loadouts != null && i < _setup.Loadouts.Length ? Loadout.Parse(_setup.Loadouts[i], default) : default;
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
            _log.Line($"Round {m.RoundNumber}: En garde!", "log-line--turn");
            if (m.Crowned >= 0)
            {
                _log.Line($"{_setup.Names[m.Crowned]} wears the Crown", "log-line--big");
                _audio?.Sfx("SFX/match/crown_gain", 0.8f);
            }
            _hud.Banner("EN GARDE!", new Color(1f, 0.85f, 0.4f), 1.2f, true);
            _audio?.Sfx("SFX/match/round_start", 0.7f);
            _audio?.Announce(Callouts.Clip(Callout.EnGarde));
            _audio?.Music(MusicPicker.IsFinal(m.Round, m.Cfg) ? "Music/battle_final" : "Music/battle");
        }

        void OnPlanningStarted(int turnSeq, float seconds)
        {
            _planBar.HideOpponents();
            var round = _mc.Match.Round;
            int n = _setup.PlayerCount;
            _livePoints = null;
            _plans = new TurnPlan[n];
            _locked = new bool[n];
            for (int i = 0; i < n; i++)
                _plans[i] = new TurnPlan(i);
            _planning = true;
            _lastTimerSecond = -1;
            _activeSlot = _setup.LocalSlot;
            if (!round.Units[_activeSlot].Alive && ControlAllOn)
                _activeSlot = NextControllable(_activeSlot);

            bool canPlan = round.Units[_activeSlot].Alive && !AutoHumans;
            _input.Active = canPlan;
            _planBar.SetVisible(true);
            _hud.SetRound(_mc.Match.RoundNumber, round.Turn);
            _hud.SetPhase(canPlan ? "Plan your moves" : "Watching");
            LoadActive();
            _audio?.Sfx("SFX/match/turn_start", 0.5f);
            if (!canPlan)
            {
                _preview.Clear();
                _planBar.SetMessage(AutoHumans ? "Autoplay: bots are planning for you." : "You're out this round. Watch the rest!");
            }
            AutoTestPlan();
        }

        void LoadActive()
        {
            var round = _mc.Match.Round;
            _builder.Load(round, _activeSlot, _mc.Cfg, _plans[_activeSlot], _locked[_activeSlot]);
            _input.Builder = _builder;
            for (int i = 0; i < _board.UnitCount; i++)
                _board.Unit(i)?.SetSelected(ControlAllOn && i == _activeSlot);
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
            if (!_planning || _mc == null || AutoHumans)
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
            if (lockNow && ControlAllOn)
            {
                int next = NextControllable(_activeSlot, unlockedOnly: true);
                if (next != _activeSlot)
                    SwitchUnit(next);
            }
        }

        void SwitchUnit(int slot)
        {
            if (!_planning || !ControlAllOn || slot < 0 || slot == _activeSlot)
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
            _roundReplay.Add(p);
            if (p.Outcome.RoundResult != null && _netTransport != null)
                MatchNet.Instance?.PrepareReplay(p.TurnSeq, _mc.Match.MatchOver);
            // Match completion is authoritative now, independent of each player's final replay.
            if (_mc.Match.MatchOver && _net != null && _net.IsHost && _model.Online)
                _lobbyNet?.SetMatchInProgress(false);
            _planning = false;
            _input.Active = false;
            _preview.Clear();
            _planBar.HideTooltip();
            _livePoints = (int[])p.PointsBefore.Clone();
            for (int i = 0; i < _board.UnitCount; i++)
                _board.Unit(i)?.SetSelected(false);
            _planBar.SetMessage("Plans revealed");
            _hud.SetPhase("Reveal");
            _planBar.ShowOpponents(p, _setup.LocalSlot, _setup.Characters);
            var ownPlan = new PlanBuilder();
            foreach (var plan in p.Plans)
                if (plan.Slot == _setup.LocalSlot && p.Before.Units[plan.Slot].Alive)
                {
                    ownPlan.Load(p.Before, plan.Slot, _mc.Cfg, plan, true);
                    _preview.Draw(ownPlan, _colors[plan.Slot], null);
                }
            _log.Turn(p);
            Tween(_mc.Cfg.RevealMs / 1000f, () => BeginTurnPlayback(p));
        }

        void BeginTurnPlayback(TurnPlayback p)
        {
            if (_mc == null || _screen != Screen.Match) return;
            _preview.Clear();
            _planBar.SetMessage("Resolving...");
            _hud.SetPhase("Resolving");
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

        void OnRoundEnded(RoundResult r) => BeginRoundReplay(r);

        void ShowRoundScoreboard(RoundResult r)
        {
            var m = _mc.Match;
            _scoreboard.Show(r, m, _setup.Names, _colors, _portraits);
            _scoreboardLeft = AutoHumans ? 1.5f : ScoreboardSeconds;
            _hud.SetPhase("Round over");
            if (r.Winner >= 0)
                _log.Line($"{_setup.Names[r.Winner]} wins round {r.RoundNumber}", "log-line--turn");
            else
                _director.Announce(Callout.Draw);
            _audio?.Sfx("SFX/match/points_tick", 0.8f);
            // The round winner's victory voice is played by PlaybackDirector on RoundEnded (FIXES_v0.2 §2.6),
            // never here, so it neither repeats nor spills onto the results screen.
        }

        // A kill scored during playback: the number goes up now and the score box pops "+1" / "+2".
        void OnPointsScored(int scorer, int victim, int amount)
        {
            if (_livePoints == null || scorer < 0 || scorer >= _livePoints.Length)
                return;
            _livePoints[scorer] += amount;
            _hud.PopScore(scorer, amount);
            _audio?.Sfx("SFX/match/points_tick", 0.5f);
        }

        void ContinueAfterRound()
        {
            if (_replayResult != null || _mc == null || _mc.Phase != MatchPhase.RoundOver)
                return;
            _scoreboard.SetVisible(false);
            _mc.ContinueAfterRound();
        }

        void OnMatchEnded()
        {
            CompleteHistory();
            if (_net != null && _net.IsHost && _model.Online) _lobbyNet?.SetMatchInProgress(false);
            ShowResultsScreen(_mc.Match);
        }

        // The results: the played map's podium art with everyone on their podium by place, the card on the right.
        void ShowResultsScreen(MatchState m)
        {
            _planBar.SetVisible(false);
            _hud.SetVisible(false);
            _screen = Screen.Results;
            int n = _setup.PlayerCount;
            var places = m.Places ?? Scoring.Rank(m.Points, m.RoundPoints);
            AutoTestMatchOver(m);
            Debug.Log($"[AppFlow] Match over after {m.RoundNumber} rounds; winner {_setup.Names[m.MatchWinner >= 0 ? m.MatchWinner : 0]}");

            var tags = new string[n];
            for (int i = 0; i < n; i++)
                tags[i] = $"{ResultsLayout.Ordinal(places[i])} \u00B7 {m.Points[i]} pts";
            var map = _art.Maps != null ? _art.Maps.Get(_art.Maps.IndexOf(_setup.MapId)) : null;
            _stage.Show(n, _setup.Characters, _setup.Names, tags, SetupLoadouts());
            var bg = map != null && map.ResultsBackground != null ? map.ResultsBackground : UiArt.Get(GameArt.LobbyBackground);
            bool art = map != null && map.ResultsBackground != null;
            _stage.SetBackground(bg, 0f, art ? 1f : 1.15f);
            if (art)
                _stage.ShowPodium(ResultsLayout.Order(places), places, map.PodiumSpots, map.PodiumScale);
            else
                _stage.ShowPodium(ResultsLayout.Order(places), places, new[] { new Vector2(0.3f, 0.3f), new Vector2(0.15f, 0.3f), new Vector2(0.45f, 0.3f), new Vector2(0.6f, 0.3f) }, 1f);
            _rig.SetBackground(null, new Color(0.08f, 0.06f, 0.05f));
            _rig.FrameStage(StageView.Center, StageView.Width, StageView.Height);
            _board.Clear();
            _results.Show(m, _setup.Names, _colors, _portraits);
            _stage.RevealPodium(ResultsLayout.Order(places), ResultsUI.PlacementStart, ResultsUI.PlacementGap);
            _audio?.Music("Music/results", 0.6f);
            _audio?.Sfx("SFX/match/match_win", 0.8f);
            _audio?.Announce(Callouts.Clip(Callout.LastMusketeer));
            // No victory voice here (round wins only); the badge sounds come from ResultsUI.
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
            _input.Active = units[_activeSlot].Alive && !AutoHumans;
            LoadActive();
        }

        // The four emote buttons in the chat: laugh, angry, salute, cry over your own musketeer, with one of your taunts (§8.7).
        void Emote(int index)
        {
            if (_mc == null || Time.unscaledTime < _emoteReady)
                return;
            // §8.7: emotes are for the planning phase, by a musketeer who's still standing.
            var u = _board.Unit(_setup.LocalSlot);
            if (u == null || !u.Alive || _mc.Phase != MatchPhase.Planning)
                return;
            _emoteReady = Time.unscaledTime + EmoteCooldown;
            if (_netTransport != null)
            {
                // Online the host relays it (rate limited) and every machine, this one included, plays it on arrival.
                MatchNet.Instance?.SendEmote(index);
                return;
            }
            _fx.Emote(u.transform, new Vector3(0.35f, UnitView.FeetY + 1.85f, 0f), UiArt.Get(GameArt.Emotes[index]), 2f);
            _audio?.Sfx("SFX/ui/emote_pop");
            _audio?.Line(u.CharacterId, "taunt", index);
            _chatModel.Add(_setup.LocalSlot, ChatUI.EmoteLines[Mathf.Clamp(index, 0, ChatUI.EmoteLines.Length - 1)], Time.unscaledTime, true);
        }

        // Leave: ask first, then back to the lobby (the old quit behaviour).
        void AskLeaveMatch()
        {
            if (_confirm.Open || _mc == null)
                return;
            _chat.Blur();
            _confirm.Show("Leave this match? You won't be able to rejoin.", "Leave", "Stay", _netTransport != null ? LeaveToMenu : (Action)ShowLobby);
        }

        // FIXES_4E §7: the 1280x720 reference panel is scaled to fit inside the window (min of the two ratios), so no
        // aspect crops or overlaps the HUD; the camera (CameraRig) refits on the same size change.
        void UpdatePanelScale()
        {
            int w = UnityEngine.Screen.width, h = UnityEngine.Screen.height;
            if (w == _lastScreenW && h == _lastScreenH)
                return;
            _lastScreenW = w;
            _lastScreenH = h;
            var ps = _document != null ? _document.panelSettings : null;
            if (ps != null)
            {
                if (_origMatch < 0f)
                    _origMatch = ps.match;
                ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                ps.referenceResolution = new Vector2Int((int)ViewFit.RefW, (int)ViewFit.RefH);
                ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                ps.match = ViewFit.PanelMatch(w, h);
            }
            _stage.SetViewAspect(h > 0 ? (float)w / h : 16f / 9f);
        }

        void OnDestroy()
        {
            DisposeDay6Lobby();
            MatchNet.ReplayStarted -= StartReplay;
            MatchNet.ReplayFinished -= FinishReplay;
            MatchNet.MatchStarted -= OnMatchStartInfo;
            MatchNet.ReturnedToLobby -= OnReturnedToLobby;
            MatchNet.ChatReceived -= OnChatReceived;
            // The PanelSettings asset must not keep a runtime-chosen match value in the Editor.
            if (_origMatch >= 0f && _document != null && _document.panelSettings != null)
                _document.panelSettings.match = _origMatch;
        }

        void Update()
        {
            UpdatePanelScale();
            AutoTestTick();
            float dt = Time.unscaledDeltaTime;
            _loading.Tick(dt);
            var kb = Keyboard.current;
            bool inMatch = _mc != null;
            if (kb != null && inMatch)
            {
                bool typing = Typing();
                if (DevFlags.Available && !typing && kb.f2Key.wasPressedThisFrame)
                    ToggleControlAll();
                if (DevFlags.Available && !typing && kb.lKey.wasPressedThisFrame)
                    _log.SetOpen(!_log.Open);
                // Enter opens the chat field (Esc / Enter inside it close it again); a click on the board blurs it.
                if (_screen == Screen.Match && !typing && !_confirm.Open && !_info.Open && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                    _chat.Focus();
                var mouse = Mouse.current;
                if (_chat.Focused && mouse != null && mouse.leftButton.wasPressedThisFrame && !OverUI(mouse.position.ReadValue()))
                    _chat.Blur();
            }
            if (kb != null && kb.escapeKey.wasPressedThisFrame && HistoryOpen)
                CloseHistory();
            else if (kb != null && kb.escapeKey.wasPressedThisFrame && _confirm.Open)
            {
                _confirm.Cancel();
                _dialogClosedFrame = Time.frameCount;
            }
            else if (kb != null && kb.escapeKey.wasPressedThisFrame && _joinDialog.Open)
            {
                _joinDialog.Cancel();
                _dialogClosedFrame = Time.frameCount;
            }
            else if (kb != null && kb.escapeKey.wasPressedThisFrame && _info.Open)
            {
                _info.Close();
                _dialogClosedFrame = Time.frameCount;
            }
            else if (_charSelect.Visible && !_info.Open && !_confirm.Open && !_joinDialog.Open)
                _charSelect.Tick();
            if (!inMatch || _screen != Screen.Match)
                return;

            _mc.Update(dt);
            if (_mc == null || _replayResult != null)
                return;
            var m = _mc.Match;
            bool planning = _mc.Phase == MatchPhase.Planning;
            _hud.SetTimer(_mc.PlanningSecondsLeft, planning);
            if (planning)
                TimerSounds(_mc.PlanningSecondsLeft);
            // During playback the live round is already resolved, so show who was alive at its start
            // (and the points/crown from before it, so the HUD doesn't spoil the result).
            if (_mc.Phase == MatchPhase.Playback && _mc.Current != null)
            {
                var pts = _livePoints ?? _mc.Current.PointsBefore;
                for (int i = 0; i < _matchPointMask.Length; i++)
                    _matchPointMask[i] = i < pts.Length && pts[i] >= m.Cfg.PointsToWin;   // the same rule as MatchState.IsMatchPoint, on the live numbers
                _hud.SetScores(pts, _mc.Current.CrownedBefore, _mc.Current.Before, 0, false, _matchPointMask);
            }
            else
            {
                for (int i = 0; i < _matchPointMask.Length; i++)
                    _matchPointMask[i] = m.IsMatchPoint(i);
                _hud.SetScores(m.Points, m.Crowned, m.Round, _mc.LockMask, planning, _matchPointMask);
            }
            for (int i = 0; i < _board.UnitCount; i++)
            {
                _board.Unit(i)?.SetMatchPoint(i < _matchPointMask.Length && _matchPointMask[i]);
                _board.Unit(i)?.SetPlanningReady(planning && _mc.IsLocked(i));
            }

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

        // ---------- online (Day 5, chunk A: session + lobby) ----------
        // Offline play never touches any of this. Online, the LobbyModel is switched to online mode and fed from
        // LobbyNet snapshots; every lobby action goes to the host through NetLobbyBackend. Chunk B starts the match
        // from StartOnlineMatch (MatchStartRpc on MatchNet) and calls LobbyNet.SetMatchInProgress.

        sealed class NetLobbyBackend : ILobbyBackend
        {
            static LobbyNet L => LobbyNet.Instance;
            public void Pick(int character) => L?.RequestPick(character);
            public void SetName(string name) => L?.RequestName(name);
            public void SetReady(bool ready) => L?.RequestReady(ready);
            public void SetLoadout(string serialized) => L?.RequestLoadout(serialized);
            public void SetSettings(int map, int pointsToWin, int hearts) =>
                L?.RequestSettings(new LobbySettingsData { Map = map, PointsToWin = pointsToWin, Hearts = hearts });
            public void DefaultSettings() => L?.RequestDefaultSettings();
            public void AddBot() => L?.RequestAddBot();
            public void RemoveBot() => L?.RequestRemoveBot();
        }

        async void HostOnline()
        {
            if (_net == null || _onlineBusy)
                return;
            _onlineBusy = true;
            _menu.SetOnlineBusy(true);
            _menu.SetNote("Creating lobby...");
            try
            {
                await _net.HostAsync(_menu.PlayerName);
                _menu.SetNote("");
                EnterOnline();
                ShowCharacterSelect();
            }
            catch (NetJoinException e)
            {
                Debug.Log("[NetSession] Host failed: " + e.Message);
                _menu.SetNote(e.Message);
            }
            finally
            {
                _onlineBusy = false;
                _menu.SetOnlineBusy(false);
            }
        }

        async void JoinOnline(string code)
        {
            if (_net == null || _onlineBusy)
                return;
            _onlineBusy = true;
            _joinDialog.SetBusy(true);
            _joinDialog.SetStatus("Joining...", false);
            try
            {
                await _net.JoinAsync(code, _menu.PlayerName);
                _joinDialog.Hide();
                _pendingJoinCode = "";
                _menu.SetNote("");
                EnterOnline();
                ShowCharacterSelect();
            }
            catch (NetJoinException e)
            {
                Debug.Log("[NetSession] Join failed: " + e.Message);
                _joinDialog.SetBusy(false);
                _joinDialog.SetStatus(e.Message, true);
            }
            finally
            {
                _onlineBusy = false;
            }
        }

        void EnterOnline()
        {
            ResetLobbyChat();
            _model.EnterOnline(_backend);
            _stageSig = "";
            _themeMap = -1;
            _lobbyNet = LobbyNet.Instance;
            if (_lobbyNet != null)
            {
                _lobbyNet.Changed += OnLobbyNetChanged;
                _lobbyNet.ClientLeft += OnClientLeft;
            }
            if (ApplyLobbySnapshot())
                _backend.SetLoadout(_model.MyLoadout.Serialize());   // wear what you saved for the musketeer you were given
        }

        bool ApplyLobbySnapshot()
        {
            var ln = _lobbyNet;
            return ln != null && ln.IsSpawned && _model.ApplyOnline(ln.Entries(), ln.LocalClientId, ln.Settings, ln.IsServer);
        }

        // Seats, ready flags, outfits or rules changed (anywhere): reload the model and redraw what is on screen.
        void OnLobbyNetChanged()
        {
            if (!_model.Online || !ApplyLobbySnapshot())
                return;
            bool picker = _charSelect.Visible;
            if (_screen == Screen.Lobby || picker)
            {
                if (picker)
                    _charSelect.Refresh();
                string sig = StageSignature();
                if (sig != _stageSig)
                {
                    _stageSig = sig;
                    RefreshStage();
                }
                else
                    _stage.SetReady(_model.Ready());
                if (_screen == Screen.Lobby)
                {
                    _lobby.Refresh();
                    if (_themeMap != _model.Map)
                    {
                        _themeMap = _model.Map;
                        ApplyLobbyTheme(false);   // the host changed the map: its art and music switch on every machine
                    }
                }
            }
        }

        // Everything that needs the stage rebuilt (ready flags alone do not).
        string StageSignature()
        {
            var sb = new StringBuilder();
            var chars = _model.Characters();
            var names = _model.SeatNames();
            var loadouts = _model.Loadouts();
            for (int i = 0; i < chars.Length; i++)
                sb.Append(chars[i]).Append(':').Append(names != null ? names[i] : "").Append(':').Append(loadouts[i].Serialize()).Append(';');
            return sb.ToString();
        }

        // Menu from a lobby / picker / results: online that also leaves the session.
        void LeaveToMenu()
        {
            if (_model.Online)
                ExitOnline(null, true);
            else
                ShowMenu();
        }

        void OnNetEnded(string reason) => ExitOnline(reason, false);

        void ExitOnline(string message, bool leaveSession)
        {
            if (leaveSession && _net != null)
                _ = _net.LeaveAsync();
            if (_lobbyNet != null)
            {
                _lobbyNet.Changed -= OnLobbyNetChanged;
                _lobbyNet.ClientLeft -= OnClientLeft;
            }
            _lobbyNet = null;
            _pendingOnline = null;
            if (_model.Online)
                _model.ExitOnline();
            _lobby.SetInviteCode("");
            _joinDialog.Hide();
            ShowMenu();
            if (!string.IsNullOrEmpty(message))
                _confirm.ShowNotice(message);
        }

        // ---------- dev/MCP helpers (screens without clicking) ----------

        public void DevStartFromLobby(int players, int map)
        {
            _model.Players = Mathf.Clamp(players, 2, 4);
            _model.Map = Mathf.Clamp(map, 0, _model.MapCount - 1);
            StartWithLoading(NewSetup(null));
        }

        public void DevShowCharacterSelect(int character)
        {
            ShowCharacterSelect();
            _charSelect.Show(character);
        }

        // tab: 0 Lore, 1 How to play, 2 Settings.
        public void DevOpenWindow(int tab) => _info.OpenTab((InfoWindowUI.Tab)Mathf.Clamp(tab, 0, 2));

        public void DevCloseWindow() => _info.Close();

        public void DevPlayWithName(string name)
        {
            if (_screen != Screen.Menu || _charSelect.Visible)
                ShowMenu();
            _menu.SetName(name);
            _menu.Play();
        }

        // Lobby as a non-host (read-only GM panel, Ready toggle); re-renders the lobby.
        public void DevLobbyAsGuest(bool on)
        {
            DevFlags.LobbyAsGuest = on;
            if (_screen == Screen.Lobby && _charSelect.Visible == false)
                ShowLobby();
            else
                _model.SetHost(!on);
        }

        // Screenshots: make a bot Not ready (or revert), switch the map with the stepper, press the lobby buttons.
        public void DevSetBotReady(int slot, bool ready)
        {
            _model.DevSetBotReady(slot, ready);
            _stage.SetReady(_model.Ready());
        }

        public void DevStepMap(int d) => _lobby.StepMap(d);
        public void DevAskLeave() => _lobby.AskLeave();
        public void DevPressStart() => _lobby.PressStart();
        public void DevChangeMusketeer() => ChangeMusketeer();
        public void DevCloseConfirm() => _confirm.Hide();
        public void DevAskLeaveMatch() => AskLeaveMatch();
        public void DevChat(int slot, string text) => _chatModel.Add(slot, text, Time.unscaledTime);
        public void DevEmote(int index) => Emote(index);

        // Screenshots: a fabricated finished match (several badges, a clear 1st..4th, or a shared 1st) on the results screen.
        public void DevShowResults(int map, int players, bool sharedFirst = false)
        {
            _model.Players = Mathf.Clamp(players, 2, 4);
            _model.Map = Mathf.Clamp(map, 0, _model.MapCount - 1);
            Teardown();
            HideAll();
            _stage.Hide();
            _setup = NewSetup(null);
            int n = _setup.PlayerCount;
            _colors = new Color[n];
            _portraits = new Sprite[n];
            for (int i = 0; i < n; i++)
            {
                _colors[i] = _art.CharacterColor(_setup.Characters[i]);
                _portraits[i] = _art.Character(_setup.Characters[i])?.Portrait;
            }
            var m = MatchRunner.NewMatch(MapParser.Parse(_setup.MapAscii), _setup.Abilities, _setup.Config, 1u, out _, _setup.Characters);
            Fill(m.Points, sharedFirst ? new[] { 3, 14, 14, 5 } : new[] { 3, 14, 11, 7 });
            Fill(m.RoundWins, 0, 2, 1, 1);
            m.RoundPoints.Clear();
            m.RoundPoints.Add((int[])m.Points.Clone());   // one round: ties stay ties
            m.Places = Scoring.Rank(m.Points, m.RoundPoints);
            m.MatchOver = true;
            m.MatchWinner = 0;
            for (int i = 0; i < n; i++)
            {
                if (m.Places[i] == 1) { m.MatchWinner = i; break; }
            }
            var st = m.Stats;
            Fill(st.ShotHits, 1, 6, 2, 0);
            Fill(st.ShotsParried, 2, 0, 3, 1);
            Fill(st.Bonks, 4, 1, 1, 7);
            Fill(st.SelfRingOuts, 0, 0, 1, 2);
            Fill(st.CrownKills, 0, 1, 0, 0);
            Fill(st.KegDamage, 3, 0, 0, 0);
            Fill(st.Fizzles, 0, 4, 0, 0);
            Fill(st.DamageDealt, 5, 9, 4, 0);
            Fill(st.Kills, 1, 4, 2, 0);
            ShowResultsScreen(m);
        }

        static void Fill(int[] dst, params int[] src)
        {
            for (int i = 0; i < dst.Length && i < src.Length; i++)
                dst[i] = src[i];
        }

        public void DevOpenWardrobe(bool open)
        {
            if (_screen != Screen.Lobby)
                ShowLobby();
            _lobby.SetWardrobe(open);
        }
    }
}
