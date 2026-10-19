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
    // The composition root for Main.unity: menu → match → scoreboard → results. It builds the match
    // (setup, transport, controller), wires the views and input to it, and owns screen switching.
    public sealed class AppFlow : MonoBehaviour
    {
        const string SpeedKey = "Musketeers.Speed";
        const float ScoreboardSeconds = 10f;

        [SerializeField] private UIDocument _document;
        [SerializeField] private VisualTreeAsset _menuUxml;
        [SerializeField] private VisualTreeAsset _hudUxml;
        [SerializeField] private VisualTreeAsset _scoreboardUxml;
        [SerializeField] private VisualTreeAsset _resultsUxml;
        [SerializeField] private StyleSheet _styles;
        [SerializeField] private ArtLibrary _art;
        [SerializeField] private BoardView _board;
        [SerializeField] private CameraRig _rig;
        [SerializeField] private PlanPreview _preview;
        [SerializeField] private PlanningInput _input;
        [SerializeField] private PlaybackDirector _director;
        [SerializeField] private WorldLabels _labels;

        MainMenuUI _menu;
        HudUI _hud;
        PlanBarUI _planBar;
        LogPanelUI _log;
        ScoreboardUI _scoreboard;
        ResultsUI _results;
        VisualElement _root;

        MatchController _mc;
        LocalTransport _transport;
        MatchSetup _setup;
        MenuChoice _lastChoice;
        Color[] _colors;
        readonly PlanBuilder _builder = new PlanBuilder();
        TurnPlan[] _plans;
        bool[] _locked;
        int _activeSlot;
        int _speedIndex = 1;
        float _scoreboardLeft;
        bool _planning;

        public MatchController Controller => _mc;

        void Start()
        {
            PrimeTween.PrimeTweenConfig.warnEndValueEqualsCurrent = false;
            _root = _document.rootVisualElement;
            _root.pickingMode = PickingMode.Ignore;
            if (_styles != null)
                _root.styleSheets.Add(_styles);

            var world = new VisualElement { name = "world-layer", pickingMode = PickingMode.Ignore };
            world.AddToClassList("world-layer");
            _root.Add(world);
            _labels.Init(world, _rig.Camera);

            var hud = AddScreen(_hudUxml);
            var menu = AddScreen(_menuUxml);
            var score = AddScreen(_scoreboardUxml);
            var results = AddScreen(_resultsUxml);

            _menu = new MainMenuUI(menu, _art.Roster);
            _hud = new HudUI(hud);
            _planBar = new PlanBarUI(hud);
            _log = new LogPanelUI(hud);
            _scoreboard = new ScoreboardUI(score);
            _results = new ResultsUI(results);

            _menu.PlayClicked += StartMatch;
            _hud.SpeedClicked += CycleSpeed;
            _hud.LogClicked += () => _log.SetOpen(!_log.Open);
            _hud.QuitClicked += BackToMenu;
            _planBar.ActionClicked += t => _input.ChooseAction(t);
            _planBar.SlotClicked += t => { if (_planning && !_builder.Locked) _builder.SelectSlot(t); };
            _planBar.LockClicked += ToggleLock;
            _scoreboard.ContinueClicked += ContinueAfterRound;
            _results.AgainClicked += () => StartMatch(WithNewSeed(_lastChoice));
            _results.MenuClicked += BackToMenu;

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
            if (DevFlags.AutoStartPlayers >= 2)
                StartMatch(new MenuChoice { Players = Mathf.Clamp(DevFlags.AutoStartPlayers, 2, 4), Character = 0, Name = "Autoplay" });
        }

        VisualElement AddScreen(VisualTreeAsset uxml)
        {
            var tc = uxml.Instantiate();
            tc.pickingMode = PickingMode.Ignore;
            tc.AddToClassList("screen");
            _root.Add(tc);
            return tc;
        }

        // ?autoplay=N starts an N-player match that plays itself: a smoke test for deployed builds.
        static void ReadUrlFlags()
        {
            string url = Application.absoluteURL;
            int q = string.IsNullOrEmpty(url) ? -1 : url.IndexOf('?');
            if (q < 0)
                return;
            foreach (var pair in url.Substring(q + 1).Split('&'))
            {
                var kv = pair.Split('=');
                if (kv.Length == 2 && kv[0] == "autoplay" && int.TryParse(kv[1], out int n))
                {
                    DevFlags.AutoPlay = true;
                    DevFlags.AutoStartPlayers = n;
                }
            }
        }

        bool OverUI(Vector2 screen)
        {
            var panel = _root?.panel;
            if (panel == null)
                return false;
            var p = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y));
            return panel.Pick(p) != null;
        }

        // ---------- screens ----------

        void ShowMenu()
        {
            _menu.SetVisible(true);
            _hud.SetVisible(false);
            _scoreboard.SetVisible(false);
            _results.SetVisible(false);
        }

        void BackToMenu()
        {
            Teardown();
            ShowMenu();
        }

        void Teardown()
        {
            _planning = false;
            _input.Active = false;
            _director.Stop();
            _preview.Clear();
            _mc?.Stop();
            _mc = null;
            _board.Clear();
        }

        static MenuChoice WithNewSeed(MenuChoice c)
        {
            c.Seed = null;
            return c;
        }

        // ---------- match ----------

        void StartMatch(MenuChoice choice)
        {
            Teardown();
            _lastChoice = choice;
            var roster = _art.Roster;
            int count = roster != null ? roster.Count : 4;
            int n = Mathf.Clamp(choice.Players, 2, Mathf.Min(4, count));

            // Slot 0 is the human; bots take the other characters in roster order (§4: characters are unique).
            var chars = new int[n];
            chars[0] = Mathf.Clamp(choice.Character, 0, count - 1);
            for (int i = 1, c = 0; i < n; c++)
            {
                if (c != chars[0])
                    chars[i++] = c;
            }

            var setup = new MatchSetup
            {
                Seed = choice.Seed ?? (uint)UnityEngine.Random.Range(1, int.MaxValue),
                Abilities = new Ability[n],
                Characters = chars,
                Names = new string[n],
                IsBot = new bool[n],
                LocalSlot = 0,
            };
            _colors = new Color[n];
            for (int i = 0; i < n; i++)
            {
                var def = _art.Character(chars[i]);
                setup.Abilities[i] = def != null ? def.Ability : (Ability)(chars[i] % 4);
                setup.IsBot[i] = i != 0;
                string charName = _art.CharacterName(chars[i]);
                setup.Names[i] = i == 0 && !string.IsNullOrEmpty(choice.Name) ? choice.Name : charName;
                _colors[i] = _art.CharacterColor(chars[i]);
            }
            _setup = setup;
            Debug.Log($"[AppFlow] Match: {n} players, seed {setup.Seed}, you are {setup.Names[0]} ({_art.CharacterName(chars[0])})");

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

            _menu.SetVisible(false);
            _results.SetVisible(false);
            _scoreboard.SetVisible(false);
            _hud.SetVisible(true);
            _hud.BuildScores(setup.Names, _colors);
            _hud.SetDevBadge(_transport.ControlAll);
            _log.Clear();
            _mc.Start();
        }

        void OnRoundStarted(EventLog opening)
        {
            var m = _mc.Match;
            _board.Build(m.Round, m.Cfg, _setup.Characters, _setup.Names);
            _rig.Fit(m.Round.Board.Width, m.Round.Board.Height);
            _director.ShowState(m.Round, m.Crowned);
            _log.Line($"Round {m.RoundNumber}{(m.SuddenDeath ? " (sudden death)" : "")}: En garde!", "log-line--turn");
            if (m.Crowned >= 0)
                _log.Line($"{_setup.Names[m.Crowned]} wears the Crown", "log-line--big");
            _hud.Banner(m.SuddenDeath ? "SUDDEN DEATH!" : "EN GARDE!", new Color(0.91f, 0.71f, 0.28f), 1.2f);
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
            _activeSlot = _setup.LocalSlot;
            if (!round.Units[_activeSlot].Alive && _transport.ControlAll)
                _activeSlot = NextControllable(_activeSlot);

            bool canPlan = round.Units[_activeSlot].Alive && !_transport.AutoPlayHumans;
            _input.Active = canPlan;
            _planBar.SetVisible(true);
            _hud.SetRound(_mc.Match.RoundNumber, _mc.Match.SuddenDeath, round.Turn);
            _hud.SetPhase(canPlan ? "Plan your moves" : "Watching");
            LoadActive();
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
            RedrawPreview();
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
            _director.Play(p, p.CrownedBefore, () => _mc?.PlaybackFinished());
        }

        void OnRoundEnded(RoundResult r)
        {
            var m = _mc.Match;
            _scoreboard.Show(r, m, _setup.Names, _colors);
            _scoreboardLeft = _transport.AutoPlayHumans ? 1.5f : ScoreboardSeconds;
            _hud.SetPhase("Round over");
            if (r.Winner >= 0)
                _log.Line($"{_setup.Names[r.Winner]} wins round {r.RoundNumber}", "log-line--turn");
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
            _planBar.SetVisible(false);
            _hud.SetPhase("Match over");
            _results.Show(_mc.Match, _setup.Names, _colors);
            Debug.Log($"[AppFlow] Match over after {_mc.Match.RoundNumber} rounds; winner {_setup.Names[_mc.Match.MatchWinner]}");
        }

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

        void Update()
        {
            var kb = Keyboard.current;
            bool inMatch = _mc != null;
            if (kb != null && inMatch)
            {
                if (DevFlags.Available && kb.f2Key.wasPressedThisFrame)
                    ToggleControlAll();
                if (kb.lKey.wasPressedThisFrame)
                    _log.SetOpen(!_log.Open);
            }
            if (!inMatch)
                return;

            _mc.Update(Time.unscaledDeltaTime);
            if (_mc == null)
                return;
            var m = _mc.Match;
            _hud.SetTimer(_mc.PlanningSecondsLeft, _mc.Phase == MatchPhase.Planning);
            // During playback the live round is already resolved, so show who was alive at its start.
            // (and the points/crown from before it, so the HUD doesn't spoil the result).
            if (_mc.Phase == MatchPhase.Playback && _mc.Current != null)
                _hud.SetScores(_mc.Current.PointsBefore, _mc.Current.CrownedBefore, _mc.Current.Before, 0, false);
            else
                _hud.SetScores(m.Points, m.Crowned, m.Round, _mc.LockMask, _mc.Phase == MatchPhase.Planning);

            if (_mc.Phase == MatchPhase.RoundOver)
            {
                _scoreboardLeft -= Time.unscaledDeltaTime;
                _scoreboard.SetCountdown(Mathf.CeilToInt(Mathf.Max(0f, _scoreboardLeft)), m.MatchOver);
                if (_scoreboardLeft <= 0f)
                    ContinueAfterRound();
            }
        }
    }
}
