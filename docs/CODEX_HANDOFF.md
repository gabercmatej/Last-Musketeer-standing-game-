# CODEX HANDOFF: The Last Musketeer Standing

You (Codex) are taking over from Claude Code. Read this file first, then `AGENTS.md`. **Goal: finish Day 5 (online), then Day 6 and Day 7, within 2 days, so 4 friends can play in their browsers this weekend.**

## RELEASED — v1.0.0 (2026-10-25)

- Candidate https://release-v100--last-musketeer.netlify.app, deploy `6addf1ad5a2b97579c59dd98`; final Web build117s, version1.0.0. Published through unchanged tools/deploy-web.sh to https://last-musketeer.netlify.app; production deploy `6addf75d1531105200964587`, release commit `ea3d3ec`, pushed annotated tag `v1.0.0`.
- Includes six requested0.4.4 polish fixes, additional1s victory delay, synchronized fresh5s post-replay summaries, individual immediate final See Results/natural5s final transition, accurate environmental log/negative scoreboard wording, and explicitly authorized seeded uniform0–3 crate Grit. Other scoring, gameplay and fits unchanged. No reset import.
-608/608 EditMode passed,0 failed/skipped,8.30s (job `42b60ab8395a4937a9a8e767f74fbab4`); backend6/6; zero compile errors. New regression tests cover crate RNG/ownership/caps/determinism, environmental attribution and countdown idempotency.
- Three visible Chrome Relay/WSS clients completed Bridge `20f92293`, Courtyard `84af0077`, then Courtyard with a fourth-character bot `cdc2f2df`, all hashes identical on all3. Guest global Skip after1.135s: full5.26–5.30s summary, clients advanced within15ms. Individual final See Results after3.9s left peers replaying; peers finished naturally then waited5s. Independent lobby returns passed.0 browser errors/DESYNC/recoil warnings during completed matches.
- Separate manual online crate probe: tile(12,3), owner0,+1 Grit, both joiners hash `64d538b0`; host capped5 Grit. Actual Bull Rush activation captured. The probe also completed; both joiners logged final hash `a9be7bc3`. Automated matches excluded from shared persistence. The manual-host preview match is isolated in preview storage. Production API after release: Preview:false,0 matches/all0 wins, exactly unchanged from before deployment. Live Chrome menu, shared history/leaderboard and lobby verified with0 browser errors. Screenshots production-v100-*.png. Everyone must hard-refresh to1.0.0 before joining.
- Evidence `docs/playtest/v044/RELEASE_VERIFICATION.md`, timing logs, Relay JSON and screenshots. Both final replay and Results behavior verified; audio keys/context running and mute/volume source architecture preserved. Subjective sound balance and real friends on other networks/devices remain human follow-up, not known release blockers. Benign UnityCache header/stripped unused postprocessing warnings remain forv1.1.
- Preexisting rootArt/Audio/.codex/AGENTS.md, old untracked day6 evidence and lore.txt heading removal remain local and excluded from staging. The build retains the user's existing lore edit. No protected cosmetic fit assets changed.

## RELEASED — v0.4.3 — final UI polish (2026-10-25)

- User requested leaderboard typography/counter/portrait polish and compact history cards only. No simulation, networking, ranking, backend, artwork or stored-record changes.
- Names use existing Pirata One font in ivory with subtle shadow. Portraits reduced about8%; overlays align to the banner's actual fitted rectangle, keeping numeric counts centered on existing laurel/caption art.
- History headers format UTC timestamps in the viewer's local timezone; separate light character-colored names and gold/silver/bronze/charcoal placement pills. Compact2–4-player layouts wrap long names safely; expansion unchanged.
-597/597 EditMode passed (8.64s),0 failed/skipped, zero compile errors. Web build82s; candidate deploy `6addb6aec562111ed66fd32d`.
- Chrome actual Web UI inspected at1280×720 game viewport and850×500: counts0/7/12/128;2/3/4-player cards;24-character names (including an unbroken string); all4 banners visible; correct local08:28 from07:28Z, previous-day23:28Z→local01:28 across DST; details expand.
- Visual fixtures were temporary intercepted GET responses in a separate preview tab. No API writes. Interception removed, fresh real GET restored empty preview cache, temporary viewport/tab closed. Production API remained0 records/0 wins. Fixture screenshots explicitly named ui-polish-*-fixtures.png.
- Published via existing tools/deploy-web.sh: https://last-musketeer.netlify.app, deploy `6addb8c5bcfc51a56c2e091e`, source `9b0454d`. Production Chrome menu/leaderboard pass with0 client errors, version0.4.3 and clean unchanged shared data (`Preview:false`,0 records/all0 wins). Screenshot `docs/playtest/day6/ui-polish-production.png`. Prior0.4.2 gameplay/online evidence remains below; not repeated for these UI-only edits. All game windows must refresh to0.4.3 before joining.

## RELEASED — v0.4.2 — 2026-10-25

**Play:** https://last-musketeer.netlify.app

Published through unchanged `tools/deploy-web.sh` after candidate verification. Production deploy `6addb2d00a0f2854f7e1d6e0`; source `fd984f8`, evidence commit `2d9fae2`. HTTP confirms0.4.2; production history API returns `Preview:false`,0 matches, all4 characters0 wins. Preview matches never entered production. Site-wide production store persists across deployments. Netlify’s injected public badge was covering the lower-right game area; disabled the reversible per-site `built_with_badge_enabled` setting (no source/build changes).

Rollback if required: previous v0.3.0 published deploy `6adb6f4c413752df7fe9f565` remains in Netlify deploy history. Production Chrome menu and shared four-character leaderboard rendered correctly,0 client errors; clean0-win standings and no branding overlay. Screenshot: `docs/playtest/day6/production-leaderboard.png`. No fits/reset import changes. Human follow-up only: friends on another network/device, subjective audio/badge timing and broader disconnect/roster cases. Day7 is stability/playtesting/balance feedback, no new release features.

## Final release candidate verified — 2026-10-25

Version0.4.2, latest code `fd984f8` (planner/replay milestone `e444059`, shared-history/art `935e858`). Candidate deploy `6addae11c28b5f91fdd971ab`, https://day6-final--last-musketeer.netlify.app. This candidate passed and was published in the RELEASED entry above; the checkpoints below remain historical.

- Latest source:595/595 EditMode passed,0 failed/skipped,8.51s; console0 errors; Web build67s. Backend6 tests pass, audit0 vulnerabilities; isolated authenticated API/idempotency smoke passed.
- Chrome two separate visible windows, same machine/network, actual Relay/WSS: completed Bridge then Courtyard in the same session, independently returned and readied, no client errors. Server host final hashes `0b7e544b` and `52447920`; manual mode does not print each guest hash, so none is invented. Earlier three-client identical hashes remain below.
- Full local plans visibly retained when locking before opponent, at reveal/execution, and in offline immediate resolution (Keg/Catch Breath/Catch Breath). Opponent rows separate.
- Guest global Skip photographed during non-final replay → host normal round scoreboard; clean next round and rotated rings. Final guest See Results → local Results while host still visibly replaying. Natural completion also passed. Self-caused framing, stationary backdrop, original-aspect badge and red frame visually checked; direct shot framing exercised in Bridge, bounds covered by regression tests. Audio channel cancellation/camera baseline verified via PlayMode probe; subjective sound quality still needs human ears.
- Two qualifying matches saved exactly once each under distinct IDs. Both clients showed same standings; details contain points/round wins/kills/deaths/badges. Browser reload retained both records. Completed one-human/one-bot match afterward: shared count stayed2, bot winner Matic stayed0 wins. All records are in isolated preview storage, not production.
- New royal art and both map rings inspected in Chrome; offline bot capes/feathers character-colored. Tile disappearance checked in PlayMode. Protected fits untouched.
- Evidence: docs/playtest/day6/final-*.png/json and DAY6_VERIFICATION.md. Remaining human acceptance: different-network friend session, subjective audio balance/badge timing, broad roster/disconnect combinations. No further feature work before release.

## Release-blocking presentation fixes — 2026-10-25 morning

- User added planner retention, replay cleanup and cause-aware framing before final release. Production remains v0.3.0; do not publish before latest candidate checks.
- Own planner refresh now happens before submission (local submission can reveal synchronously). Reveal explicitly renders the authoritative local plan, independently of opponent rows, and keeps it through execution. Existing layout unchanged.
- Replay boundaries stop one-shot/voice channels while preserving music/preferences; camera shake resets before baseline capture and after replay. FX custom tweens now have cancellable targets and nested effects are cancelled. New rounds proactively clear playback leftovers. Rig pose/hold/flash timers and weapon recoil are cancelled too; body offsets normalize without changing cosmetic fits.
- Replay bounds include victim/hazard, plus attacker only when this clip has a direct damage/push/grapple/rush event. Historical credit alone does not frame an unrelated attacker. Simultaneous deaths share bounds; fit rendering avoids cropping; framing/zoom tween smoothly. Simulation unchanged.
- Full suite595/595 passed again after rig cleanup,0 failed/skipped,8.51s; job `13a63d5205304230b7b959b6423adb39`. Zero compile errors. PlayMode probe: active one-shot1→0, repeat cleanup safe, music preserved, camera baseline zero and unchanged later.
- Latest Web rebuild and hosted Chrome checks pending. Earlier0.4.2 manual qualifying preview match was interrupted before completion, so no claim of Web-to-shared completion yet. Earlier successful v0.4.1 two-match hashes remain valid historical evidence, not verification of these latest presentation fixes.

## Spawn markers and shared leaderboard — 2026-10-25 (current)

- Latest attachment adds tile-anchored local-gold/opponent spawn rings and shared Netlify history with illustrated leaderboard. Keep Sol only/no subagents. Production still v0.3.0 until candidate verification.
- Prior v0.4.1 smoke recovered both completions: Courtyard `de8b930c`, Bridge `62ea6cdf`, identical on all3 clients; global guest Skip, local final See Results (another guest remained in replay), and independent lobby returns verified. Earlier long attempt had simultaneous Relay inactivity timeout; fully tiled smaller-window retry completed. Evidence saved under `docs/playtest/day6/`.
- New code: BoardView owns marker sprites by initial round tile; only localSlot gets gold, SetTile(Pit) hides them; fresh Build uses rotated spawns. Play Mode confirmed fixed marker positions and pit hide/restore; fits unchanged.
- Final supplied art imported/sliced via narrow Day6 importer; ArtPostprocessor has exact-path exceptions preserving multi-sprite sheets and high quality. Illustrated four-banner UI inspected in Play Mode; rank decorations follow rank, portraits follow character.
- Shared backend now implemented (supersedes v1.1 deferral): Netlify Functions + site-wide Blobs, Unity JWT/project verification, host-bound start registration, human-only eligibility, limits, immutable conditional writes and server timestamps. Preview stores isolated by deployID. See `docs/SHARED_HISTORY.md` for security limits and setup.
- User explicitly approved pinned SDK installation; npm audit0 vulnerabilities. All6 Node backend tests pass. Live isolated draft API verified real Unity JWT, anonymous401, foreign-origin403, bot400, one saved completion and duplicate Created:false. No production test records.
- Unity latest full suite590/590 passed, zero errors. v0.4.2 Web build succeeded143s, then a small explicit local-save-failure UI fallback was added; refresh/tests and incremental rebuild pending.
- Remaining gate: rebuild latest0.4.2; deploy draft including functions; Chrome shared-data/illustrated UI + online ring perspectives and completed qualifying preview match; verify no multiplayer regression; commit/push; existing tools/deploy-web.sh production deploy; verify Preview:false/empty initial production history and live menu.

## Final presentation corrections — 2026-10-25 (current)

- Latest user requires Sol only, no delegates; freeze features after these corrections. Production remains v0.3.0 until new smoke tests pass.
- Previous candidate `e17911b` pushed, v0.4.0 built: 3 visible Chrome windows completed Courtyard with identical `d5d88967`; staggered results returns at 21:46:48/53/56 UTC and second Bridge match started in the same session. Old test tabs closed at interruption; **second-match final hashes were not recovered** and completion is not claimed.
- New requested changes integrated: inset replay render target over frozen arena backdrop; original-aspect Killcam badge/red frame/approximately ten-second chronological montage. Between rounds Skip stays global; final See Results and natural completion are local. Authoritative completion opens independent returns while InResults gates prevent next start. Global final replay skip is rejected by the host barrier.
- Lobby chat UI removed; match chat/emotes retained. Opponents' resolved plans show as compact portrait/action/direction rows below planner; cleared for planning, no opponents' map-wide arrows. Own preview remains.
- Play Mode: inset rendering and compact panel observed, zero errors. **589/589 EditMode passed, zero skipped** (16.38s, job `7c257334d9634f9c9991e781627c06bb`). Tests cover panel resolved plans/default Idle/clearing and final replay skip policy.
- v0.4.1 Web build requested; Unity MCP temporarily disconnects during blocking build as before. Pending: successful build console, visible Chrome full match + global guest skip + local final See Results + independent returns/second match, final commit/push/deploy. Do not claim Day 6 release acceptance yet.

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
