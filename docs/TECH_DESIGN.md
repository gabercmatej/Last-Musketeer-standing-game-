# The Last Musketeer Standing — Technical Design

## 1. Stack

| Thing | Choice | Why |
|---|---|---|
| Engine | **Unity 6 LTS** (6000.x, latest LTS) | |
| Platform | **Unity Web build (WebGL 2)** hosted on **Netlify** (or Vercel) | Friends open a link on Mac or Windows; nothing to install |
| Rendering | **URP 2D** ("Universal 2D" template). Top-down 3/4-view sprites on a grid, with an orthographic camera. | Small and fast for the web. AI-generated 2D art is much easier to keep consistent than rigged 3D models. Tweens (bob, squash, recoil) make static sprites feel alive. |
| Transport | Relay over **secure WebSockets (WSS)** | Browsers can't use UDP, and Relay supports WSS. The host is a browser too: it connects *out* to Relay, so no server is needed. |
| Networking | **Netcode for GameObjects 2.x** (`com.unity.netcode.gameobjects`) | Standard, well documented |
| Lobby + join codes | **Multiplayer Services** (`com.unity.services.multiplayer`): Sessions with Relay | Join code with no port forwarding; covered by the free tier |
| Local MP testing | **Multiplayer Play Mode** (`com.unity.multiplayer.playmode`) | Run up to 4 players inside the editor |
| UI | **UI Toolkit** (UXML + USS) | Plain text files, so Claude can write them directly with no fragile scene editing |
| Tweening | **PrimeTween** (OpenUPM) or DOTween | Smooth playback with little code |
| Input | Input System package | |
| Claude ↔ Unity | **MCP for Unity** (CoplayDev) or the Unity MCP you already use | Lets Claude read the console, run tests, enter Play mode and edit scenes |

## 2. The big idea: deterministic lockstep of *plans*

Nobody streams positions. The only things that cross the network are **each player's plan** (3 tiny actions) and a **seed**.

```
 Client A ──SubmitPlan──┐
 Client B ──SubmitPlan──┼──► HOST collects plans ──ResolveTurn(allPlans)──► everyone (incl. host)
 Client C ──SubmitPlan──┘                                                     │
                                                                              ▼
                                                       every machine runs the same pure-C# Sim
                                                       → identical state + event list
                                                       → Presentation plays events back
                                                       → sends TurnDone(stateHash) to host
```

What this gives you:
- **No lag, interpolation or NetworkTransforms.** The only NetworkObject is one `MatchNet` object with RPCs.
- **Replays and the kill-cam are free:** they're just the start state plus the plans.
- **Offline, bots and hot-seat use the same code path.** Swap the transport and nothing else changes.
- **Testable:** the sim is pure C#, so it can be unit tested headless.

The cost is that the sim must be **100% deterministic** on every machine. Everyone runs the same WebAssembly build, which helps, but these rules still apply:
- **Integers only** in the sim. No `float` or `double` anywhere.
- No `System.Random`, no `UnityEngine.Random`, and no time. Use the custom `DeterministicRng` (xorshift32), seeded by the host. The rules currently need almost no randomness, but bots and maps might.
- Never iterate a `Dictionary` or `HashSet` where the order affects the result. Use arrays/lists sorted by unit id or initiative.
- The sim never touches `UnityEngine`. Enforce this with an asmdef that has `noEngineReferences: true`.
- After each turn, every client sends `StateHash`. The host compares them and logs a **DESYNC** if they differ. Safety net: the host can send a full `GameState` snapshot.

## 3. Project layout

```
Assets/_Project/
  Scripts/
    Sim/                        asmdef Musketeers.Sim  (noEngineReferences: true)
      Core/        Int2, Dir, Tile, Board, Unit, Keg, GameState (Clone, ComputeHash), DeterministicRng
      Actions/     ActionType, PlannedAction, TurnPlan
      Rules/       TurnResolver (partial: Phase0Validate, Phase2Movement, Phase2bAbilities,
                   Phase3Shoves, Phase4Shots, Phase5Kegs, Phase6Deaths), TurnStart (grit, crumble), Crumble
      Events/      SimEvent (struct, Kind enum + fields), EventLog
      Match/       MatchState, MatchRunner (Match → Round → Turn), Scoring (+ Crown), MatchStats
      Maps/        MapParser (ASCII → Board + spawns)
      Bots/        BotPlanner (simple heuristic bot, deterministic given rng)
      Config/      GameConfig (all numbers from GAME_DESIGN §11)
    Game/                       asmdef Musketeers.Game  (refs Sim)
      MatchController           Owns MatchState, drives turn loop via IMatchTransport
      IMatchTransport           LocalTransport (offline + bots) | NetTransport
      TurnTimer, PlanBuilder (client-side plan editing + projection)
    Net/                        asmdef Musketeers.Net  (refs Sim, Game, NGO, Services)
      ServicesBootstrap         UnityServices.InitializeAsync + anonymous sign-in (profile per MPPM player)
      SessionService            Create/Join session by code, leave
      LobbyNet (NetworkBehaviour)   NetworkList<LobbySlot>, character pick, ready, bots, settings, version check
      MatchNet (NetworkBehaviour)   RPCs for the turn loop (section 5)
      NetSerialization           INetworkSerializable wrappers for PlannedAction/TurnPlan
    Presentation/               asmdef Musketeers.Presentation (refs Sim, Game)
      BoardView, TileView, UnitView, KegView
      PlanningInput (mouse + keyboard), PlanPreview (ghosts, lines, markers)
      PlaybackDirector          Plays EventLog grouped by tick/phase with tweens
      CameraRig (shake, kill-cam), FxLibrary, Announcer, AudioHooks
    UI/                         UI Toolkit controllers: MainMenuUI, LobbyUI, HudUI, PlanBarUI, ScoreboardUI, ResultsUI
    Data/                       CharacterDef, CosmeticDef, MapDef (ScriptableObject definitions)
    Editor/                     SceneBuilder ("Tools/Musketeers/Build Main Scene"), validators
  Tests/
    EditMode/                   asmdef Musketeers.Sim.Tests  (Scenario helper + rule tests)
  Data/       Characters/*.asset, Maps/courtyard.txt, Maps/bridge.txt
  UI/         *.uxml, *.uss
  Art/        Placeholder/ (primitives, materials) → your art later
  Audio/
  Scenes/     Main.unity   ← ONE scene for everything
```

**One scene.** The menu, lobby and game all live in `Main.unity`, and the UI switches between states. That means no networked scene loading, which removes a whole class of first-multiplayer bugs.

## 4. Sim API (sketch)

```csharp
// Pure C#. No UnityEngine.
public enum Dir : byte { N, E, S, W }
public enum ActionType : byte { Idle, CatchBreath, Move, Dash, Fire, Reload, Parry, Shove, Keg, Signature }  // Idle = 0 = unplanned
public enum Ability : byte { Grappin, Switcheroo, DoubleCharge, BullRush }

public struct PlannedAction { public ActionType Type; public Dir Dir; public Int2 Target; }
public struct TurnPlan { public byte Slot; public PlannedAction A0, A1, A2; }   // fixed size, no allocs

public sealed class GameState {
    public int Turn; public Board Board; public Unit[] Units; public List<Keg> Kegs;
    public GameState Clone(); public uint ComputeHash();
}

public static class TurnResolver {
    // Mutates state, returns events for playback. Deterministic.
    public static EventLog ResolveTurn(GameState s, TurnPlan[] plans, GameConfig cfg);
}
public static class TurnStart { public static EventLog Begin(GameState s, GameConfig cfg); }

public struct SimEvent {
    public SimEventKind Kind; public byte Tick; public byte Phase;
    public sbyte Unit, Other; public Int2 From, To; public Dir Dir; public sbyte Amount;
}
// Kinds: Moved, MoveBlocked, Bonk, Dashed, Leaped, Shoved, GuardBreak, Squeezed, WallSlam, Fell,
// ShotFired(path end), ShotHit, Parried, Reflected, Damaged, Reloaded, CaughtBreath, Fizzled,
// KegThrown, KegLanded, KegKicked, KegExploded, CrateDestroyed, Grappled, Swapped, BullRushed,
// TileWarned, TileCrumbled, Died(killer, cause), RoundEnded(winner or draw)
```

The Presentation layer **never changes game state**. It reads `GameState` for the static view and plays back `EventLog`.

## 5. Network protocol

All RPCs use NGO 2.x universal RPCs (`[Rpc(SendTo.Server)]`, `[Rpc(SendTo.ClientsAndHost)]`). The host is a player too.

| Message | Direction | Payload | When |
|---|---|---|---|
| `JoinInfoRpc` | client→host | name, version | After connecting. Host kicks on version mismatch. |
| LobbySlots (`NetworkList<LobbySlot>`) | host→all | slot, clientId, name (FixedString32), characterId, ready, isBot | Lobby state |
| `PickCharacterRpc` / `SetReadyRpc` | client→host | id / bool | Host validates that characters are unique |
| `MatchStartRpc` | host→all | seed, mapId, config overrides, roster (slot→character, bot flags) | Host presses Start |
| `BeginPlanningRpc` | host→all | turn, durationMs | Each turn, after everyone has finished playback (or after 10 s) |
| `SubmitPlanRpc` | client→host | turn, TurnPlan, locked | **Every time the plan changes** (it's tiny), so the latest plan is always ready when time runs out |
| `LockStatusRpc` | host→all | bitmask of locked slots | When locks change |
| `ResolveTurnRpc` | host→all | turn, TurnPlan[] (humans + bots) | Everyone locked, or timer expired |
| `TurnDoneRpc` | client→host | turn, stateHash | After local resolve and playback |
| `EmoteRpc` | client→host→all | slot, emoteId | Any time |
| `RematchRpc` | client→host | — | Results screen |

- **Bots are planned on the host** and included in `ResolveTurnRpc`, so clients never run bot AI.
- **Timer:** the host is authoritative. Clients show a local countdown from `durationMs`, and the host adds 0.5 s of grace for latency.
- **Disconnected player:** the host submits `TurnPlan.AllCatchBreath(slot)` for them (GAME_DESIGN §9). An empty plan would be all Idle.

### Sessions code (Multiplayer Services **2.4.1**, verified on Day 0 in `Assets/_Sandbox/Scripts/NetSmokeServices.cs`)
```csharp
var init = new InitializationOptions();
init.SetProfile(MakeProfileName());                       // see "Auth profiles" below
await UnityServices.InitializeAsync(init);
await AuthenticationService.Instance.SignInAnonymouslyAsync();

// Both roles: sessions set the relay server data but NOT UseWebSockets, and UnityTransport
// errors if the two disagree. Match it to the protocol before create/join.
var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
utp.UseWebSockets = RelayProtocol.Default == RelayProtocol.WSS;
utp.DisconnectTimeoutMS = 60000;                          // survive a quick tab-switch (§7)

// Host. The old RelayNetworkOptions(protocol, …) ctor is obsolete in 2.4.x:
// the protocol goes through WithNetworkOptions instead.
var opts = new SessionOptions { MaxPlayers = 4, IsPrivate = true }
    .WithRelayNetwork()
    .WithNetworkOptions(new NetworkOptions { RelayProtocol = RelayProtocol.Default });
ISession session = await MultiplayerService.Instance.CreateSessionAsync(opts);
string code = session.Code;                               // show this in the lobby

// Client: joiners can pick a protocol too in 2.4.x, so pass the same one.
var joinOpts = new JoinSessionOptions()
    .WithNetworkOptions(new NetworkOptions { RelayProtocol = RelayProtocol.Default });
ISession session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code, joinOpts);
```
The session sets up Relay and starts NGO host/client for you. The `NetworkManager` needs a `UnityTransport` component, and Relay is configured by the session (except `UseWebSockets`, above).

**Relay protocol: keep everyone on the same one.**
- `RelayProtocol.Default` picks per build target: **WSS in Web builds, and also in the Editor while the active build target is Web** (verified Day 0), otherwise UDP/DTLS.
- In 2.4.x joiners pass a protocol through `JoinSessionOptions.WithNetworkOptions`; we always pass `RelayProtocol.Default`.
- So test **Editor ↔ Editor** (Multiplayer Play Mode) or **browser ↔ browser**. Never mix them.
- In the Web build, log the transport's `UseWebSockets` and the relay connection type at startup, and confirm it's WSS.
- (`RelayProtocol.WSS` exists for forcing it, but forcing it only on the host causes a mismatch with Editor joiners.)

**Auth profiles.**
- **Multiplayer Play Mode:** virtual players share one machine. Use the player index or `CurrentPlayer.ReadOnlyTags()` as the profile.
- **Web:** two tabs of the same browser share storage, so they'd sign in as the same anonymous player and the second join fails. Use a **random profile per page load** (for example, 8 random characters). There's no persistent identity, which the game doesn't need.

### Known risk to test on Day 0 and Day 4
There are reports of **Distributed Authority** sessions hosted *from a Web build* not accepting joiners. This game uses **client-hosted Relay**, not Distributed Authority, so it shouldn't apply. Still, the Day 0 smoke test checks a browser-hosted session with a browser joiner before any real work depends on it.

**Fallback if browser hosting fails:** the host runs a **desktop build** that creates the session with `RelayProtocol.WSS` forced, and friends still join in the browser, where WSS is their default. Only the host downloads anything.

## 6. Testing strategy

1. **EditMode tests for every rule** in GAME_DESIGN §6, using an ASCII scenario helper:
   ```csharp
   var s = Scenario.From(@"
       .....
       .A.B.
       .....");
   s.Plan('A', Act.Fire(Dir.E));
   s.Plan('B', Act.Parry());
   s.ResolveTick();
   s.AssertHp('A', 2);         // riposte
   s.AssertHp('B', 3);
   s.AssertEvent(SimEventKind.Reflected);
   ```
   Capital letters A–D are units in slots 0–3, `K` is a keg with fuse 3, and the other symbols follow the map legend.
2. **Determinism test:** run a full bot-vs-bot match from seed X twice and assert identical hashes every turn. Also run it from a `Clone()` mid-match.
3. **Fuzz test:** 1,000 random bot matches with no exceptions, every match ends, and HP and Grit always stay within bounds.
4. **Balance sim (Day 6):** 2,000 bot matches, then report win rate per ability and average round length.
5. **Multiplayer Play Mode:** host plus 2–3 virtual players in the editor.
6. **Local web test:** File → Build Profiles → Web → **Build And Run**. Unity serves it on localhost. Open 2–3 tabs or browsers, host in one and join from the others.
7. **Real test:** deploy to Netlify, then send the link to one friend on a different network (and ideally Windows if you're on Mac).

## 7. Web build and hosting

### Player settings (Web)
| Setting | Value | Why |
|---|---|---|
| Compression Format | **Gzip** | |
| **Decompression Fallback** | **On** | The loader decompresses in JavaScript, so **no server header config is needed**. It works on Netlify, Vercel or anything static. |
| Name Files As Hashes | On | Every deploy gets new file names, so there are no stale caches |
| Data Caching | On | Faster reloads |
| Run In Background | On | Helps when the window is visible but not focused. It can't stop browsers throttling hidden tabs. |
| Managed Stripping Level | Minimal (raise later) | NGO and Services use reflection, and heavy stripping breaks them. Add a `link.xml` if needed. |
| Exceptions | Explicitly Thrown Only | Size and speed |
| WebGL Template | `Musketeers` (custom, see below) | |
| Default canvas | 1600×900, scales to the window | |

(Optimization for later: Brotli without fallback plus `Content-Encoding` headers. Not worth the hassle for a friends game.)

### Custom WebGL template (`Assets/WebGLTemplates/Musketeers/`)
- Start from Unity's Minimal template.
- Restyle it: a full-window canvas, a dark background, the logo, and a loading bar.
- Add a **Fullscreen** button.
- Add a line of text: "Keep this tab open and visible while playing."

### Browser-only things the code must handle
- **Clipboard:** `GUIUtility.systemCopyBuffer` doesn't work in WebGL. Use a tiny `.jslib` (`Assets/Plugins/WebGL/Clipboard.jslib`) that calls `navigator.clipboard.writeText` from a click handler.
- **Invite link:** read `Application.absoluteURL` and parse `?join=CODE` to prefill the join field. Build the invite link from `Application.absoluteURL` without its query string.
- **No threads:** `async/await` on the main thread is fine. Never use `Task.Run`, `Thread` or `Parallel`. The balance-report test runs in the Editor only, so it's unaffected.
- **Audio** only starts after the first click or keypress. The menu's first click covers this.
- **Storage:** use `PlayerPrefs` (backed by IndexedDB) for the name, settings and Hall of Fame JSON. Call `PlayerPrefs.Save()` after writing. Keep it under ~1 MB.
- **Hidden tabs freeze Unity.** The host's tab drives the turn loop. Clients that come back late catch up, because RPCs queue up. Raise the transport's disconnect timeout to about 60 s so a quick tab-switch doesn't drop someone.
- **Keyboard:** avoid browser-reserved keys (Ctrl/Cmd+anything, F1–F12, Alt combos). Space and Backspace are fine while the canvas has focus.

### Deploying (Netlify)
1. Build to `Builds/Web/` (gitignored).
2. First time: either drag the `Builds/Web` folder onto app.netlify.com/drop, or run `npx netlify-cli login`, then `npx netlify-cli deploy --prod --dir "Builds/Web"`. Rename the site to something like `last-musketeer` → `https://last-musketeer.netlify.app`.
3. Every update: bump the version (Player Settings → Version), build, then run `tools/deploy-web.sh` (or drag the folder onto the site's **Deploys** tab).
4. Vercel alternative: `npx vercel deploy "Builds/Web" --prod`.

If your Renault racer is already deployed somewhere, the same setup works here. The only difference is that multiplayer goes through Unity's Relay, not your hosting.
