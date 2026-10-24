# CODEX HANDOFF: The Last Musketeer Standing

You (Codex) are taking over from Claude Code. Read this file first, then `AGENTS.md`. **Goal: finish Day 5 (online), then Day 6 and Day 7, within 2 days, so 4 friends can play in their browsers this weekend.**

## Day 6 integration checkpoint — 2026-10-24 (current)

Day 6 is in progress; do not restart completed implementation. Day 5 production remains v0.3.0.

- Committed/pushed `dc59052`: deterministic environmental −1 scoring, Matej special blocked by Parry, fixed four-round spawn rotation, symmetrical Bridge pillars. 562 EditMode tests passed at that milestone.
- Current working tree integrates lobby name/bot controls and chat; independent Return to Lobby with returning-player gates (no voting); reveal/checkmarks/timer; chronological snapshot replay and host-authoritative any-human skip; results entrances/stats/badges; mute; Batman catalog availability and character cape defaults; local eligible match history and character standings.
- Final integration suite: **587 passed, 0 failed/skipped** (19.41 s, Unity MCP job `a0b5152f4f4e48858ea04d64343afc88`). Final wardrobe correction also passed all 13 focused cosmetic tests. The isolated new preference regression initially exposed null-versus-empty field comparison in the test and now compares serialized outfits.
- Unity MCP connected; latest compilation had no errors. Bridge camera capture confirmed symmetrical pillars and action preview (`docs/playtest/day6/bridge-play.png`). This was **not** a complete match or full UI verification.
- Final Skip button source: `Art/UI/buttons/skip-replay.png`; narrow Day 6 UI importer now includes it. Reuse existing icon-button presentation; preserve red border, Killcam badge and synchronized skip protocol.
- Shared persistence is **deferred to v1.1**, as permitted: current deployment is static, with no configured result-verification backend. History explicitly says “This browser only”; no unrestricted public submissions or fake shared standings.
- Pending release gate: refresh/import/console; full EditMode suite; Play Mode/UI review; new Web build; separate visible browser-window full match, guest Skip and independent Return to Lobby/new match; fix regressions; commit/push verified milestone; update checklist and deploy only after local checks. Genuine two-network testing remains a human check.
- Protected fits remain untouched. Preexisting untracked root `Art/`, `Audio/`, `.codex/`, `AGENTS.md` must not be bulk-staged. Day 7 has not started. Latest user froze features and requested Sol only/no delegates; Astra account limit halted its final review after existing bot-default edits were saved.

## Current status — 2026-10-23 (supersedes historical status below)

- **Live v0.3.0:** https://last-musketeer.netlify.app; deployed successfully with `tools/deploy-web.sh`, production menu loaded in Chrome with no browser errors. Deploy `6adb6f4c413752df7fe9f565`.
- **Fresh EditMode:** 539 passed, 0 failed/skipped via Unity MCP (38.98 s); no compile errors. No gameplay, networking, cosmetic or fit code/data changes were needed.
- **Browser Relay/WSS verified in three separate visible windows:** host + 2 invite-link joiners completed Courtyard with identical hash `b4b6dba0`; returned to lobby; Bridge completed with identical hash `c55facd7` on the host and remaining joiner after slot 2 left mid-match. Both remaining players logged `AUTOTEST done`; zero browser errors/DESYNC. Copy code/invite clipboard and removal of the departed lobby slot also verified.
- **MPPM verified:** host + Player 2 + Player 3 completed Courtyard, final hash `f8087ca3` identical on all three. Player 4 was unchecked by Matej for this run. Four-player support is unchanged; it can be re-enabled in the MPPM window.
- **MPPM limitation:** virtual editors emitted repeated URP package resource errors (`Host type is not matching any asset type ...FallbackError.shader`), generating large logs. Play was stopped after the first full match; the extended second-map/leave sequence was verified in browsers, not MPPM. An Editor Wire WebSocket error occurred on intentional Play-mode shutdown. No package or protected asset changes were made to suppress these diagnostics.
- Test flag/code files were removed and Play mode stopped. Temporary browser test windows were closed; the production page remains open.
- Full evidence: `docs/playtest/DAY5_VERIFICATION.md`.
- **Next: Matej + a friend on another network finish a full match on the live site**, including chat/emotes and a 20 s tab switch. Fix any resulting issue before Day 6. No Day 6 work has begun. Lobby chat remains Day 6.

The sections below retain Claude's 2026-10-22 handoff history; their “not verified/not deployed” statements describe the earlier state.

## 1. Where things stand (2026-10-22)
- **Live:** https://last-musketeer.netlify.app runs **v0.2.5**: offline vs bots, fully polished (Days 0–4F).
- **Repo:** github.com/gabercmatej/Last-Musketeer-standing-game- (branch `main`). The last commit on main is `f583499` (v0.2.5).
- **Unity 6000.6.6f1** with URP 2D, NGO 2.13.3, Multiplayer Services 2.4.1, Multiplayer Play Mode 3.0.0, Unity Transport 6.6.0 and PrimeTween. Web build only.
- **Docs that matter:**
  - `docs/GAME_DESIGN.md` (rules), `docs/TECH_DESIGN.md` (architecture and network protocol), `docs/DECISIONS.md` (every decision, newest at the bottom)
  - `docs/plans/010-day5-online.md` (Day 5 plan and checklist)
  - `docs/playtest/FIXES_v0.2.md` and `FIXES_4E.md` (what the last sessions implemented)
  - `docs/WARDROBE_STUDIO.md` (the cosmetics fitting tool)
- **Tests:** 539 EditMode tests, all green at the last run.

## 2. FIRST: finish and verify Day 5
Day 5 code is **written but not verified**. Chunk A (session + lobby) and chunk B (match + chat + resync) are both ticked in `docs/plans/010-day5-online.md`. The **Finish** items F1–F4 are not done.

The work was **uncommitted** when Claude stopped. If Matej hasn't committed it yet, run `git status` and commit it first as `Day 5 WIP (unverified)`. Don't commit the root `Art/`, `Audio/` (source assets) or `.playwright-mcp/`; add them to `.gitignore`.

The last change before the stop (uncommitted, compiled, 539/539 tests green, **not yet checked in Play mode**):
- `LobbyNet` + `MatchNet` moved from an in-scene NetworkObject to a **prefab the host spawns**: `Assets/_Project/Prefabs/NetSystems.prefab`, registered in `NetworkPrefabs.asset` on the NetworkManager.
- `NetSession` spawns it on host start and despawns it on leave. `MainSceneBuilder` creates it.
- Reason: in-scene NetworkObjects weren't matched on clients with scene management off.

**Problems seen in the last Web test** (3 tabs, Playwright, local build on localhost:8765):
1. The **host lost the Relay connection after ~100 s**: "Failed to establish connection with the Relay server" → "Transport failure! Relay allocation needs to be recreated". Most likely cause: all players were **tabs in one browser window**, so the hidden tabs were throttled or paused and heartbeats stopped. **Re-test with each player in its own visible window or browser** before changing code. If it still happens with visible windows, investigate the relay keep-alive, UnityTransport heartbeat/timeout settings and whether `DisconnectTimeoutMS = 60000` is applied on Web.
2. **Join retries:** after a failed attempt, retries hit `SessionConflict: player is already a member of the session` (HTTP 409) and then `429 Too Many Requests`. Fix:
   - Before retrying a join, leave or clean up the half-joined session, or treat "already a member" as joined and continue.
   - Back off between retries: at least 3–6 s, max 3 tries.
   - Show the player a clear message instead of retrying forever.
3. The autotest's 3rd tab got "session is full". That's expected if earlier ghost memberships still count. Re-test after the fix in item 2.

**Finish checklist (do in order):**
- [ ] F1 Compile with 0 console errors. All EditMode tests green.
- [ ] Play mode: host locally, then check the NetSystems prefab spawns and the lobby works.
- [ ] F2 Multiplayer Play Mode: host + 2 virtual players. Full match on both maps, Return to lobby, a 2nd match, one player leaving mid-match. (The autotest flag file is described in DECISIONS.md, "Day 5 B: autotest".)
- [ ] F3 Web build to `Builds/Web`. Host + 2 joiners in **separate visible windows** via the invite link `?join=CODE`. Play a full match.
- [ ] F4 Version **0.3.0**, DECISIONS lines, `tools/deploy-web.sh`, commit, push.
- [ ] Then **Matej tests with a real friend** on another network. Fix whatever breaks before anything else.

## 3. PROTECTED: never touch
- Matej's cosmetic fits: `Data/Fits`, cosmetic and weapon fit assets, `FitDefaults.json`. Never run **"Import Art (reset)"**. Plain "Import Art" is fine.
- Game rules in `GAME_DESIGN.md`: don't change them without asking Matej.
- The sim must stay pure C#, deterministic and integer-only (see AGENTS.md).

## 4. Day 6 (after Day 5 works online with a real friend)
The focus is **making online play solid and fun for 4 friends.** Do these in priority order and stop when time runs out:
1. **Fix every bug from the friend test**, top priority.
2. **Lobby chat** (chat currently works only in matches). Reuse `ChatRelay`.
3. **Reconnect safety:** if a player refreshes the page mid-match, they get a clear message. Rejoining a running match is out of scope; they rejoin the lobby after the match.
4. **Reveal phase check:** all players' planned arrows show for ~1.5 s before playback (GAME_DESIGN §7). Implement it if it's missing.
5. **Hall of Fame** (GAME_DESIGN §8.8): each browser stores match results in PlayerPrefs, with a small screen from the menu: wins, kills and badges per musketeer.
6. Only if there's time: a **kill-cam** that replays the deciding turn at 0.5× (the state + plans are stored per turn).

## 5. Day 7 (final)
1. **Balance:** run the bot balance report (test category `Balance`) on both maps. Compare it with Matej's notes from playing with friends. Change only `GameConfig` numbers, and log every change.
2. Bug fixes from a 4-player session. Check Chrome **and** Safari, Mac **and** Windows.
3. Version **1.0.0**, deploy, commit, push.
4. Give Matej a short "how to play with friends" text for the group chat: open link → name → pick musketeer → Ready. The host shares the invite link and keeps the tab visible.

## 6. How to work (keep usage low)
- **Before coding:** write a short plan in `docs/plans/NNN-*.md` with a checklist. Work in small chunks.
- **After each chunk:**
  - Refresh Unity via MCP, read the console (0 errors), run the EditMode tests.
  - Commit, then push (as separate commands).
- **Unity MCP:**
  - `.codex/config.toml` points to `http://127.0.0.1:8080/mcp` (MCP for Unity).
  - Unity must be open with **Window → MCP for Unity** running.
  - If the MCP is unreachable, tell Matej. Don't work around it.
- **Scene:** `Main.unity` is generated by **Tools → Musketeers → Build Main Scene**. Change the builder, not the scene.
- **Deploy:** bump Player Settings → Version, build Web to `Builds/Web`, run `tools/deploy-web.sh`. Netlify is already linked.
- Log every decision in `docs/DECISIONS.md` (one line each, dated).
- **End every task with:** what changed, test counts, how Matej can test it (max 8 lines), and the commit hash.

## Day 5 online (v0.3.0) — status 2026-10-22

Verified:
- EditMode 539/539 green, 0 console errors (incl. LobbyRulesTests, NetMatchTests: in-process host + 2 clients full match on both maps, snapshot resync, disconnect -> Catch Breath, chat limits).
- Editor Play mode: NetSession.HostAsync creates a Relay session (code returned), NetSystems prefab spawns, no errors.
- Version bumped to 0.3.0. Offline vs bots path untouched (LocalTransport unchanged).

Not verified:
- Any real client join after the fix. First browser test failed: joiners could not spawn the in-scene Net object
  ("NetworkPrefab could not be found", hash 3536938858) because NGO scene management is off. Fixed by making Net a
  registered prefab (Assets/_Project/Prefabs/NetSystems.prefab) that the host spawns in NetSession.HostAsync. Untested.
- Join retries: max 3, 3/6 s backoff, "already a member" leaves stale sessions first (NetSession.JoinAsync). Untested.
- Full online match, Return to lobby + 2nd match, mid-match leave, chat sync, invite link, Copy buttons. MPPM not run.
- Not deployed. Builds/Web may contain a fresh v0.3.0 build started at the end of the session (check it finished).

Next steps:
1. Build Web (Tools/Musketeers/Build Web). Serve Builds/Web (python3 -m http.server 8765).
2. Run 3 SEPARATE visible windows (hidden tabs freeze Unity and drop Relay): node script in Claude's scratchpad
   `tabs3.js` pattern = chromium headed, 3 browser contexts, flags --disable-background-timer-throttling
   --disable-renderer-backgrounding --disable-backgrounding-occluded-windows; host ?autotest=1, joiners
   ?autotest=1&join=CODE and ?autotest=leave&join=CODE. Expect "AUTOTEST match over map=.. hash=.." equal per machine,
   no DESYNC, "AUTOTEST done".
3. If green: tools/deploy-web.sh, commit, push.
