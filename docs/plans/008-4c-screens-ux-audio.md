# Plan 008: Session 4C, screens + UX + audio (v0.2.3)

Spec: `docs/playtest/FIXES_v0.2.md` §2 (everything tagged [4C], incl. §2.8 per-map theming) plus Matej's five 4D follow-ups (below). Online parts (Ready, unique character lock, game master) are **local state + UI only**; Day 5 syncs them. Fit everything around 4D's HUD (plan slots left column, scores right column, action bar bottom); don't redo it. Executed by Sonnet subagents one chunk at a time; Opus plans, reviews each chunk and ships.

## Hard constraints (every chunk)
- **Never modify fit data:** `Assets/_Project/Data/Fits/*`, `Data/Cosmetics/*`, `Data/Weapons/*`, `Scripts/Editor/ArtImport/FitDefaults.json`. Never run "Import Art (reset)". Plain "Import Art" is allowed.
- CLAUDE.md architecture + web rules (pure deterministic integer sim, presentation never mutates state, no threads, UI Toolkit, scene only through `Tools/Musketeers/Build Main Scene`, PlayerPrefs only, no browser-reserved keys).
- After each batch of script edits: refresh/compile via Unity MCP, read the console, 0 errors before moving on. EditMode tests green at the end of every chunk.
- Don't touch `AGENTS.md`, `.codex/`, root `Art/` or `Audio/` (read-only sources).
- UI copy: no em dashes in text rendered in the game UI (impeccable rule). The lore text is used exactly as written in FIXES §2.2.

## New art and audio found (root `Art/`, `Audio/`, by modification date)
| File | Use |
|---|---|
| `Art/UI/backgrounds/main-menu-left.png` | menu background (art on the left, menu panel goes right) |
| `Art/splash-screen.png` (old menu splash) | loading screen art only |
| `Art/UI/backgrounds/lobby.png` | Courtyard lobby background |
| `Art/UI/backgrounds/lobby-bridge.png` | Bridge lobby background |
| `Art/UI/backgrounds/results-lobby.png` | Courtyard results podium (podiums 2-1-3-4 left half, same pixels as the bridge one) |
| `Art/UI/backgrounds/results-lobby-bridge.png` | Bridge results podium |
| `Art/UI/buttons/{settings,lore,fullscreen,exit-fullscreen,leave-game,wardrobe,change-player}.png` | Matej's button art |
| `Audio/Music/arena_lobby.wav` (was `lobby.wav`) | Courtyard lobby music |
| `Audio/Music/bridge_lobby.wav` | Bridge lobby music |

## Decisions taken up front (log each in DECISIONS.md)
- **Triple Charge cap (Matej):** `GameConfig.TripleChargeMaxDamage = 3`. During one Triple Charge use, every unit's damage *from that use* (shot hits, the Wall Slam / Bonk / Squeeze damage of its pushes, damage to a bystander bonked by the slide, a reflected shot's damage) is counted per unit; once a unit has taken 3 from this use, further damage from it is skipped. Pushes, slams and falls still happen (events still logged so playback shows the impact). Supersedes the "Flagged for Matej: up to 6" line.
- **Grappin on walls (Matej):** on an `EdgeWalls` board, a hook that reaches the edge within range hits the wall like a pillar: you pull yourself to the last tile before the wall (`PullSelf`; already next to the wall = you stay). Unwalled boards: still a miss. Shots and keg throws unchanged. Planning preview and bots follow the sim.
- **Ranking + match winner (FIXES §2.5):** `MatchState.RoundPoints` (points scored per round per player, appended at each round end, part of the hash). `Scoring.Rank(points, roundPoints)` → places (1-based, shared on a full tie): total points, then last round's points, then the round before, …; equal in every round = shared place. The match ends at the end of a round when the top total ≥ PointsToWin; the winner is place 1 (on a full tie the co-winners share 1st; `MatchWinner` = the lowest slot of them, `MatchState.Places` has the shared places). **Sudden death is removed** (no `SuddenDeath`, no `Participants` sit-outs). `RoundWins` stays as a stat.
- **Announcer:** Bonk and Signature keep their small banner but play no voice line. The reveal sound is removed. Victory voice ("YES haha") plays for the round winner when a round ends, never on the results screen.
- **Volumes:** default Music 0.6 → 0.48, Voice 0.9 → 0.72 (−20%; saved values are untouched). Sliders: no sound on value change; one preview on pointer release (SFX: ui_click, Voice: en_garde, Music: none). AudioService ramps each channel's real volume toward the slider value (~0.15 s) instead of jumping.
- **Banners (Matej fix 3):** callout banners are small (≤ 36 px text), live in the top HUD strip (centred, under/next to the timer) or as a short translucent centre flash that stays above the board's top row; never cover tiles. Round start / Draw keep a slightly bigger version of the same style.
- **Plan slots (Matej fix 4):** each slot = action icon + short label (≤ ~10 chars, e.g. "Fire →", "Swap: Nik") with `text-overflow: ellipsis`, never wrapping; checked at 16:10, 16:9 and a narrow window.
- **Names (FIXES §2.1):** `NameRules` (pure, tested): trim, 1–20 chars, letters (any Unicode letter, incl. č š ž), digits, spaces and `.'-`. Invalid/empty: the field shakes and a hint shows; you can't continue. Every name label truncates with "…" (USS ellipsis; world labels get a max width).
- **Fullscreen in the browser:** the toggle runs on `PointerDownEvent` (Unity WebGL executes the deferred fullscreen request in the following mouseup, a user gesture), so one click works. The icon swaps fullscreen ↔ exit-fullscreen.
- **One styled window for Lore + How to play + Settings** (tabs); the Lore button opens it on Lore, Settings on Settings.
- **Character select overlay:** Menu → Pick your Musketeer → Lobby. Bio content is data on `CharacterDef` (Title, Strength, Weakness, Bio, SignatureText); the signature explanations use the 4D numbers (FIXES §2.3). Taken state is local (`LobbyModel.TakenBy(character)`, offline always free; bots take the rest).
- **Lobby (local Day-5 state):** `LobbyModel.IsHost` (offline true), `Ready[]` (host + bots always ready), `TakenBy`. Host sees Start (asks "Not everyone is ready. Start anyway?" if anyone isn't); a non-host sees a Ready / Not ready toggle (code path built, reachable via a dev flag for the screenshot). GM panel read-only for non-hosts. The "The Tavern" sign is dropped (neither lobby art is a tavern).
- **Per-map theming:** `MapDef` gains `LobbyBackground` (Sprite), `LobbyMusic` (AudioLibrary key), `ResultsBackground` (Sprite), `PodiumSpots` (4 normalized feet positions on the results art, place 1..4) and `PodiumScale`. Lobby switches background (fade) and music (crossfade) when the map changes. Results use the played map's art. In-match music is unchanged.
- **Play again** goes straight back to the lobby (same players, same settings); **Quit** goes to the menu.
- **Chat (offline local):** `ChatModel` (pure: max 80 chars, trimmed, empty ignored, keeps the last N). Top of the left HUD column under Settings/Leave: a short fading log, a text field and 4 emote buttons. An emote shows the bubble above your musketeer and plays its sound (same 1.2 s cooldown). Z/X/C/V removed. While the field has focus, PlanningInput hotkeys are ignored (Enter sends, Esc leaves the field).
- **Dev-only:** Speed, Log and the L key only when `DevFlags.Available`.

## Tasks
- [x] 1. **Sim rules (Sonnet):** Triple Charge cap, Grappin wall pull, ranking + tiebreak, sudden death removed. Scenario tests for each (cap with wall-slam pinning, cap with reflected shot, knockback still happens after cap; hook to wall on Courtyard, adjacent-to-wall, unwalled miss; ranking: points, last-round tiebreak, earlier-round tiebreak, full shared tie; match end at ≥ PointsToWin with a tie on points decided by rounds). Update PlanProjection (grappin preview) and BotPlanner if they model the hook. Update GAME_DESIGN (§2 tiebreak, §4/§5 Grappin, Triple Charge, §11 knob) and DECISIONS. Fix every compile ref to SuddenDeath/Participants (AppFlow, ScoreboardUI, MatchController, tests). Determinism + fuzz green on both maps.
- [x] 2. **Import + data + audio (Sonnet):** importer: `UI/buttons/*.png` → `Art/UI/buttons/*.png` (trim, fit 256), backgrounds `main-menu-left`, `lobby-bridge`, `results-lobby`, `results-lobby-bridge` → JPG ≤ 2048 under `Art/UI/backgrounds/`; run plain **Import Art**; remove the stale `Audio/Music/lobby.ogg` copy and its AudioLibrary key; GameArt keys. `MapDef` fields + wire both map assets (Courtyard: lobby / arena_lobby / results-lobby; Bridge: lobby-bridge / bridge_lobby / results-lobby-bridge). `CharacterDef` bio fields + fill all four assets with FIXES §2.3 text. All audio decisions above (announcer, reveal, defaults, slider fix + volume ramp, victory voice on round win). Tests: AudioWiring keys, no `reveal`/announcer bonk+signature use, MapDef wiring, CharacterDef bios non-empty.
- [x] 3. **Menu + names + window + character select (Sonnet, impeccable):** `NameRules` + tests; menu on `main-menu-left` with the panel on the right; Lore / Settings / Fullscreen top-right with Matej's art; one tabbed window (Lore exact text, How to play, Settings); fullscreen on pointer-down; character select overlay with bio panel, Select / "Taken by <name>"; ellipsis on every name label. Loading screen keeps the old splash.
- [x] 4. **Lobby redesign + per-map lobby (Sonnet, impeccable):** remove the blue bottom bar; stage up, bottom area smaller; GM panel top-left (map, points 10–30, hearts 1–5, add/remove bots) replacing 4D's steppers, read-only when not host; top-right Settings + Fullscreen; bigger Menu button with "Leave lobby?" confirm; names bigger in character colour above each musketeer ("Bot" for bots) and Ready/Not ready under; bottom-left Wardrobe + Change Musketeer (Matej's art); bottom-right Start (+ confirm) / Ready toggle; Wardrobe styled panel with Hat/Face/Cape/Boots tabs, EnabledInV1 only, live preview; per-map lobby background fade + music crossfade. LobbyModel tests (ready, taken, host).
- [x] 5. **Results podium (Sonnet, impeccable):** played map's results art; musketeers on podiums by `Places` (fitted per map via `PodiumSpots`), 4th beside, shared places labelled; winner in Victory pose with Crown; every badge each player earned, revealed one by one (pop + bounce scale, shine sweep, a sound per badge, staggered); big Play again (→ lobby) and Quit (→ menu).
- [x] 6. **In-match HUD (Sonnet):** Settings + Leave with Matej's art, Leave confirm "Leave this match? You won't be able to rejoin."; Speed/Log/L dev-only; chat + emotes top-left; banners (fix 3); plan-slot labels (fix 4) at 16:10/16:9/narrow; top strip check (Round X / Turn Y, upright clock, score boxes smaller, portraits uncut, names "…").
- [ ] 7. **Verify + ship (Opus):** parry-vs-shot F2 scenario screenshot of the block spark (fix 5); screenshots of menu, lore, character select, lobby on both maps, wardrobe, loading, arena with chat, results on both maps; fresh-context review (reviewer); all EditMode tests green, 0 console errors; version 0.2.3; Web build; deploy; commit; push (separate commands).

## Non-goals
- Online sync of anything (Day 5): ready, taken characters, GM settings, chat, map switch.
- Fit data, cosmetics, Wardrobe Studio.
- New rules beyond the two approved fixes.

## Gates
- After every chunk: compiles, 0 console errors, all EditMode tests green, Opus review of the diff + screenshots.
- End: fuzz green on both maps, screenshots checked by eye, Web build loads after deploy, fullscreen toggles in the browser.
