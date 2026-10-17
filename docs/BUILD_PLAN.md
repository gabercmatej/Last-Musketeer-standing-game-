# Build Plan: 7 days to game night

Goal: play a full match with all four of you on **Saturday, Oct 24**.

Each day has a goal, a **Definition of Done (DoD)**, and a **prompt** to paste into Claude Code from this folder. Do the days in order: each one builds on the last, and the riskiest parts (rules, then networking) come first.

> **Milestone after Day 4: "Playable with friends."** It has ugly cubes, but it's a full game online. If the week goes badly, you still play on Saturday.

| Day | Focus | Hours (you) | Risk |
|---|---|---|---|
| 0 | Setup + **web multiplayer smoke test** | 2–3 | medium |
| 1 | Sim: board, movement, shoves, shots | 2–3 | medium |
| 2 | Sim: kegs, abilities, rounds, scoring, bots | 2–3 | medium |
| 3 | Visuals + planning UI + offline vs bots | 3 | medium |
| 4 | **Online:** sessions, lobby, turn loop | 3–4 | **high** |
| 5 | Game feel: reveal, juice, kill-cam, emotes | 2–3 | low |
| 6 | Meta: awards, rematch, Hall of Fame, map 2, balance | 2–3 | low |
| 7 | Your art, builds, test with one friend, fixes | 3+ | low |

---

## Day 0: Setup (do this yourself, ~1–2 h)

1. **Install** the latest **Unity 6 LTS** via Unity Hub, with the **Web Build Support** module. That's the only platform you need: everyone plays in the browser.
2. **Create the project in this folder.** Unity Hub won't create a project inside a folder that already has files, so:
   - In Unity Hub, create a new project with the **Universal 2D** template, named `TLMS_Temp`, in the `Games` folder.
   - Close Unity. Move the `Assets`, `Packages` and `ProjectSettings` folders from `TLMS_Temp` into `The last musketeer standing`.
   - Delete `TLMS_Temp`.
   - In Unity Hub, choose **Add → Add project from disk** and pick `The last musketeer standing`.
3. **Unity Cloud:** go to Edit → Project Settings → Services and link the project to a new Unity Cloud project (sign in with your Unity ID). This is needed for Relay/Sessions, and the free tier is plenty.
4. **Packages** (Window → Package Manager → Unity Registry):
   - Netcode for GameObjects
   - Multiplayer Services
   - Multiplayer Play Mode
   - Input System (say yes to the restart)
5. **Unity MCP:**
   - Install **MCP for Unity** (github.com/CoplayDev/unity-mcp; follow its README) or keep the Unity MCP you already use.
   - Open **Window → MCP for Unity** and configure it for Claude Code.
   - Check with `claude mcp list` from this folder.
6. **Git:**
   - Run `git init`. The `.gitignore` is already here.
   - Make the first commit.
   - Optional: push to a private GitHub repo.
7. **Smoke test:** start Claude Code here and say: *"Read CLAUDE.md. Use the Unity MCP to read the console and tell me the Unity version and installed packages."* If it answers, you're ready.
8. **Web multiplayer smoke test.** This is the riskiest assumption in the project, so prove it tonight. Paste this into Claude Code:
   ```
   Read CLAUDE.md and docs/TECH_DESIGN.md §5 and §7. Make a throwaway scene
   Assets/_Sandbox/NetSmoke.unity (delete it after Day 4) with a minimal UI: Host button
   showing the session code, a code field + Join button, and a "Ping" button that sends
   an RPC everyone logs on screen with the sender's name. Use Multiplayer Services
   sessions with Relay exactly as in TECH_DESIGN §5 (random auth profile per page load
   on Web), log the relay protocol in use, set up the Web player settings from §7, and
   tell me how to build it for Web and deploy it to Netlify.
   ```
   - Deploy it to Netlify.
   - Open the link in **two different browsers**, host in one and join from the other, and ping.
   - Then send the link to one friend to try from their home.
   - If browser hosting fails, use the fallback in TECH_DESIGN §5 (desktop host). Decide now, not on Day 4.

**DoD:**
- Unity opens the project, the packages are installed, and Claude can see the Unity console.
- **Two browsers ping each other through a Netlify URL.**

---

## Day 1: The simulation core

**Goal:** the rules engine for the board, movement, collisions, shoves, shots, parry and reload, proven by tests. No visuals yet.

**DoD:**
- `Musketeers.Sim` compiles with `noEngineReferences`.
- 40 or more EditMode tests are green, covering every rule in GAME_DESIGN §6 Phases 0–4 and 6.
- The Courtyard map parses.

**Prompt:**
```
Read CLAUDE.md, docs/GAME_DESIGN.md and docs/TECH_DESIGN.md fully.
Day 1: build the pure C# simulation core in Assets/_Project/Scripts/Sim with asmdef
Musketeers.Sim (noEngineReferences: true), plus Musketeers.Sim.Tests (EditMode).
Implement: Int2, Dir, Tile, Board, Unit, GameState (Clone + ComputeHash), GameConfig
(all §11 numbers), DeterministicRng, MapParser (Courtyard map from GAME_DESIGN §3 saved
to Assets/_Project/Data/Maps/courtyard.txt), PlannedAction/TurnPlan, SimEvent/EventLog,
and TurnResolver phases 0, 1, 2, 3, 4 and 6 exactly as GAME_DESIGN §6 specifies
(kegs and abilities come tomorrow—stub them as fizzles). Build the ASCII Scenario test
helper from TECH_DESIGN §6 first, then write tests for every rule and edge case in §6
(bonk same-tile, bonk swap, blocked chains, 3-cycle rotation, dash leap over pit,
dash blocked, guard break into pit, squeeze, wall slam, riposte, double KO, fizzles,
falls are instant, HP deaths at end of tick, initiative rotation).
Run the tests through the Unity MCP and iterate until all pass. Log any rule
ambiguity you had to decide in docs/DECISIONS.md.
```

---

## Day 2: Finish the rules, plus bots

**Goal:** the full game runs headless, from bot vs bot through to a match winner.

**DoD:**
- Kegs, all 4 abilities, turn start (Grit, crumble), rounds, scoring, Crown, kill credit and match end are implemented.
- `BotPlanner` works.
- The determinism and fuzz tests (TECH_DESIGN §6.2–6.3) pass.

**Prompt:**
```
Read CLAUDE.md and docs/GAME_DESIGN.md. Day 2: complete the simulation.
Implement Phase 5 (kegs: throw, land/impact, fuse, explosion 3×3 dmg + orthogonal push,
chain reactions, kicking kegs, max 1 per player), Phase 2b abilities (Grappin,
Switcheroo, Bull Rush) and Double Charge in Phase 4, TurnStart (grit, crumble warn/fall
schedule), kill credit (last attacker this/last turn), RoundEnd, MatchState + Scoring
(+3 win, +1 kill, +2 crowned kill, draws, tiebreak), Crown, and MatchStats (counters
needed for the awards in §8). Then add a BotPlanner: per tick, avoid pits/warned tiles
using a projected position, fire if an enemy is in line and loaded, shove an adjacent
enemy toward a pit, throw a keg near clustered enemies when it has grit, use its signature
when it has 3+ grit and a sensible target, otherwise move toward the nearest enemy;
use DeterministicRng for variety. Tests: one per new rule, plus the determinism test
(same seed → identical hash every turn, also from a mid-match Clone) and a fuzz test
(1000 bot matches: no exceptions, every match ends, invariants hold). All green via MCP.
```

---

## Day 3: Make it visible and playable offline

**Goal:** play a full match against 3 bots inside the Unity editor, with placeholder colored squares and circles.

**DoD:**
- `Main.unity` is built by an editor script.
- Planning works with mouse and keyboard, with a ghost preview.
- Playback animates every event kind.
- The HUD shows HP, Grit, musket, timer, plan slots and the scoreboard.
- A full match vs bots works from menu → game → results.

**Prompt:**
```
Read CLAUDE.md, docs/GAME_DESIGN.md §5–§7 and docs/TECH_DESIGN.md. Day 3: presentation +
offline play. Create asmdefs Musketeers.Game and Musketeers.Presentation. Implement
MatchController with IMatchTransport and LocalTransport (bots fill empty slots).
Create an idempotent editor menu "Tools/Musketeers/Build Main Scene" that builds
Assets/_Project/Scenes/Main.unity (URP 2D): orthographic top-down camera (CameraRig),
BoardView (tiles as SpriteRenderers with generated placeholder sprites: floor, pillar,
crate, pit, warned glow; y-based sorting so taller things overlap correctly), UnitView
(circle sprite tinted by CharacterDef color with a name label, floating HP hearts / grit
pips / musket loaded icon), KegView with fuse number. Create CharacterDef ScriptableObjects for
Matej, Matic, Aljaž, Nik (abilities per GAME_DESIGN §4, distinct colors).
PlanningInput: action bar buttons + hotkeys (1 Move, 2 Dash, 3 Fire, 4 Reload, 5 Parry,
6 Shove, 7 Keg, 8 Catch Breath, Q Signature; WASD/arrows pick direction; click tile for
keg; Backspace undo; Space lock in; no browser-reserved keys), PlanBuilder projecting grit/loaded/position per
tick and greying out invalid actions, PlanPreview ghosts + shot lines + keg markers.
PlaybackDirector: plays EventLog grouped by tick then phase with PrimeTween (install
via OpenUPM, or DOTween if simpler), ~0.7 s per tick. UI Toolkit screens: MainMenu
(name field, Play vs Bots, Host, Join – the last two disabled for now), HUD with timer
and 3 plan slots, Scoreboard between rounds, Results. Verify by entering Play Mode via
MCP, reading the console, and fixing all errors. Also create the custom WebGL template
Assets/WebGLTemplates/Musketeers (TECH_DESIGN §7) and select it in Player Settings.
Tell me exactly how to test it by hand.
```

**You test:**
- Play 3 matches vs bots in the Editor.
- Then do one **Web build** and play vs bots in the browser, so web-only problems (stripping, input, UI scaling) show up early.
- Write down anything that feels confusing and give that list to Claude.

---

## Day 4: Online (the important day)

**Goal:** you host, friends join with a code, pick characters, ready up and play a full match online.

**DoD:**
- A host plus 2 virtual players complete a match in Multiplayer Play Mode.
- Then a real test: the game is **deployed on Netlify**, and you plus a friend at their home finish a match, both in the browser.
- The invite link works.
- The version check, the disconnect handling and the hash/desync log all work.
- Switching tabs for 20 s doesn't kick anyone.

**Prompt:**
```
Read CLAUDE.md and docs/TECH_DESIGN.md §2 and §5 carefully. Day 4: networking.
Create asmdef Musketeers.Net. Implement ServicesBootstrap (init + anonymous sign-in,
unique auth profile per Multiplayer Play Mode player), SessionService using the
Multiplayer Services Sessions API with WithRelayNetwork (check the installed package
version's API before writing code), a NetworkManager + UnityTransport in Main.unity
(via the scene builder), LobbyNet (NetworkList<LobbySlot>, unique character pick,
ready, host Add/Remove Bot, settings, version check + kick), MatchNet implementing the
exact message table in TECH_DESIGN §5 (plans sent on every change, host-authoritative
timer, host plans bots, ResolveTurnRpc, TurnDoneRpc with state hash + DESYNC logging,
disconnect → empty plans, host leaving → back to menu with message), and NetTransport
implementing IMatchTransport so MatchController is unchanged. Lobby UI in UI Toolkit:
join code big with Copy code + Copy invite link (Clipboard.jslib), ?join=CODE prefill
from Application.absoluteURL, player list, 4 character portraits, ready, host controls,
Start, and a "keep this tab visible" hint for the host. Raise the transport disconnect
timeout to ~60 s. Enable Host/Join in the main menu. Test with Multiplayer Play Mode
(host + 2 virtual players) and fix everything. Then build for Web, test 3 browser tabs
locally (Build And Run), and deploy with tools/deploy-web.sh.
```

**You test:**
- MPPM with 3 players.
- Then the Netlify link with one friend at their home. Do it tonight: it's the riskiest thing in the project, so find out early.

---

## Day 5: Game feel (juice)

**Goal:** turns feel dramatic and readable.

**DoD:**
- A reveal phase.
- Impact feedback: hit-stop, shake, flashes, particles for shots, smoke, explosions and falls.
- Announcer banners (GAME_DESIGN §8.2).
- Sound hooks with placeholder sounds.
- A kill-cam at round end.
- Lock-in ✔ icons and emotes.
- The planning timer pulses in the last 5 seconds.

**Prompt:**
```
Read CLAUDE.md and docs/GAME_DESIGN.md §7–§8. Day 5: game feel. Add the Reveal phase
(1.5 s, everyone's planned arrows/lines/keg markers drawn at once, per-player colors).
Juice in PlaybackDirector: anticipation + squash on moves, musket smoke + tracer line,
hit flash + hit-stop (60 ms) + camera shake scaled by impact, parry spark + reflected
tracer, guard-break burst, fall-into-pit spin + shrink, keg fuse flicker and explosion
ring, crate splinters, crumbling tiles shaking a turn before they fall. Announcer
system: detects named moments from the EventLog (BONK, RIPOSTE, GUARD BREAK, SWAN DIVE,
DOUBLE KO, TOUCHÉ, THE CROWN FALLS, EN GARDE, THE LAST MUSKETEER STANDING) and shows a
punchy banner; lines come from a data asset so I can rewrite them. AudioHooks: one
AudioCue per event kind via a ScriptableObject table (placeholder or silent clips).
Kill-cam: store each turn's start state + plans; at round end replay the deciding turn
at 0.5× with a camera push-in, skippable. Lock-in ✔ icons over units during planning,
and emotes (Z/X/C/V, EmoteRpc, speech bubble). Timer pulses red for the last 5 s.
```

---

## Day 6: Make it addictive, then balance

**Goal:** you finish a match and immediately want another one.

**DoD:**
- A results screen with awards.
- Rematch with everyone voting.
- The Crown is visible.
- A Hall of Fame screen.
- Map 2 (The Bridge) with a map choice in settings.
- A balance report from 2,000 bot matches, with tweaks applied.

**Prompt:**
```
Read CLAUDE.md and docs/GAME_DESIGN.md §2, §8, §11. Day 6: meta + balance. Results
screen: winner pose area, final points, awards computed from MatchStats (names and
descriptions from a data asset so I can rename them to inside jokes), Rematch button
(RematchRpc; restart when all humans vote or 10 s). Crown visuals on the leader at
round start + announcer call-out. Scoreboard between rounds showing point gains with
reasons (+3 win, +1 kill, +2 crown kill). Hall of Fame: every client appends match
results to a JSON string in PlayerPrefs (+ PlayerPrefs.Save, works in the browser); screen shows all-time wins,
kills, awards per musketeer. Add The Bridge map and a map selector in lobby settings.
Then write an EditMode "balance report" test (excluded from normal runs via a category)
that plays 2000 bot matches and prints win rate per ability, avg turns per round, cause-
of-death breakdown and keg/parry usage. Propose GameConfig tweaks if any ability is
outside 20–30% win rate, and log changes in docs/DECISIONS.md.
```

---

## Day 7: Your art, builds and the real test

**Goal:** it looks like *your* game, and everyone has a working build.

**Steps:**
1. Drop your generated assets in following `docs/ART_AND_AUDIO.md`, then ask Claude to wire them in. Prompt:
   ```
   Wire in the art in Assets/_Project/Art/<folder> per docs/ART_AND_AUDIO.md:
   assign to CharacterDefs, tiles, FX, UI. Keep placeholders as fallback.
   ```
2. Personalize: names, colors, death lines, award names, announcer lines and the game logo on the menu.
3. Bump the version, build for Web, and deploy. Then post the link in the group chat.
4. **Friday-night smoke test** with at least one friend, online, for a full match. Try a Windows PC and Safari on a Mac if anyone uses them.
5. Fix only what breaks. No new features after Friday.

---

## If you fall behind: cut list (cut from the top)

1. Map 2
2. Hall of Fame
3. Kill-cam
4. Emotes
5. Bot intelligence (bots can just be random-but-safe)
6. Reveal phase

**Never cut:**
- Sim tests
- The version check
- Lobby ready/start
- Rematch
- The planning timer

## Ideas for after game night (v1.1+)
- Cosmetics wardrobe (hats, capes, boots, musket skins) with unlocks tied to awards ("Swan Dive ×5 → Swim Cap")
- More signature abilities, or a draft where everyone picks abilities
- 2v2 team mode ("Deux contre Deux")
- Hidden-information ability (a trap or smoke) once you're comfortable with filtering state per client
- Map editor (it's just text)
- Spectator mode for a 5th friend
- Phone support (touch planning UI): the web build already runs on phones, it only needs touch controls
