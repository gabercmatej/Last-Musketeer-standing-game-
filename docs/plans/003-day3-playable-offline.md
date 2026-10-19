# Day 3: Playable offline vs bots — Implementation Plan

> Executed inline (superpowers:executing-plans). Tick boxes as tasks finish. On "continue", re-read this file.

**Goal:** a full match vs bots from menu → game → results, in the Editor and in the browser, with placeholder art.

**Spec:** Matej's Day 3 brief (session of 2026-10-18) + `docs/BUILD_PLAN.md` Day 3 + GAME_DESIGN §5–§7 + TECH_DESIGN §3, §7.

**Architecture:** `MatchController` (Game) owns `MatchState`, talks only to `IMatchTransport`. `LocalTransport` acts as host: timer, bot plans, early end on lock. Each resolved turn becomes a `TurnPlayback` (state before, plans, per-tick state snapshots computed on a clone, the `TurnOutcome`). `PlaybackDirector` (Presentation) animates it, then snaps the views to the true state. UI Toolkit screens are driven by `AppFlow` (UI asmdef, the composition root).

## Global constraints
- Sim stays pure and deterministic. Presentation never mutates `GameState` (it only reads clones).
- No threads, no `System.IO` at runtime, PlayerPrefs + `Save()` only, no browser-reserved keys except F2 (dev-only, Matej asked).
- All numbers in `GameConfig`; personal content in `CharacterDef` assets; placeholder art generated in code, so a missing sprite never breaks anything.
- One scene, built by `Tools/Musketeers/Build Main Scene` (idempotent). Main.unity is the only build scene.
- Every SpriteRenderer uses the project's unlit sprite material (no 2D lights needed).

## Files
- Sim: `Config/GameConfig.cs` (+`StartLoaded`), `Core/Unit.cs` (spawn uses it).
- Data (`Musketeers.Data` → Sim): `CharacterDef`, `CharacterRoster`, `BoardTheme`.
- Game (`Musketeers.Game` → Sim): `IMatchTransport`, `LocalTransport`, `MatchSetup`, `MatchController`, `TurnPlayback`, `PlanBuilder`, `PlanProjection`, `DevFlags`, `EventText`.
- Presentation (`Musketeers.Presentation` → Sim, Game, Data, PrimeTween, InputSystem): `PlaceholderArt`, `ArtLibrary`, `BoardMath`, `CameraRig`, `BoardView`, `UnitView`, `KegView`, `WorldLabels`, `PlanPreview`, `PlanningInput`, `PlaybackDirector`, `FxPool`.
- UI (`Musketeers.UI` → all above): `AppFlow`, `MainMenuUI`, `HudUI`, `PlanBarUI`, `LogPanelUI`, `ScoreboardUI`, `ResultsUI`; `Assets/_Project/UI/*.uxml|uss`, PanelSettings + theme.
- Editor (`Musketeers.EditorTools`): `MainSceneBuilder`, `WebBuildSettings` (+template, version).
- Web: `Assets/WebGLTemplates/Musketeers/index.html` (+ TemplateData/style.css).
- Tests: `StartLoadedTests`, `PlanBuilderTests`, `MatchControllerTests` (test asmdef gains a Game reference).

## Tasks
- [x] 1. `GameConfig.StartLoaded` (default true) + tests for both values; GAME_DESIGN §11 row; DECISIONS lines (StartLoaded, blast push keeps slam/bonk damage).
- [x] 2. Asmdefs Data/Game/Presentation/UI/EditorTools; compile clean.
- [x] 3. Game: transport + LocalTransport + MatchController + TurnPlayback (tick snapshots) + DevFlags; headless test: a 4-player match with auto-locked bot humans reaches MatchOver, and the per-tick snapshot events equal `outcome.Turn`.
- [x] 4. PlanBuilder + projection (pos/grit/loaded/keg per tick, reasons: no grit, unloaded, keg already active, out of range) + tests.
- [x] 5. Data assets: CharacterDefs (Matej Grappin blue, Matic Switcheroo green, Aljaž Double Charge purple, Nik Bull Rush orange), roster, board theme.
- [x] 6. Presentation views: art, camera fit, board/unit/keg views, world labels.
- [x] 7. Planning input + plan preview (ghosts 1–3, shot lines, keg 3×3, shove arrows, ability lines, hover preview).
- [x] 8. PlaybackDirector: every SimEventKind animated, speed 0.5×/1×/2×, tick snapshots, text log.
- [x] 9. UI Toolkit: menu (name in PlayerPrefs, 2–4 players, character pick, Host/Join disabled "Day 4", dev seed), HUD, plan bar + tooltips, log panel, scoreboard, results. Readable at 1280×720 and 1920×1080.
- [x] 10. Dev: F2 control-all (click/Tab to switch unit), autoplay flag (`DevFlags.AutoPlay`, `?autoplay=N` URL) for the checks.
- [x] 11. Scene builder + WebGL template selected + Version 0.1.0.
- [x] 12. Verify: EditMode tests green, console clean, Play Mode 4p autoplay several turns with no errors.
- [x] 13. Web build, deploy, browser console clean.
- [x] 14. DECISIONS, commit "Day 3: playable offline vs bots", push.

## Non-goals
Networking, lobby, reveal phase, announcer, kill-cam, emotes, awards, audio, real art (Days 4–7).

## Gates
- EditMode tests all green (234 + new), with counts reported.
- Console shows no errors after compiling, building the scene, and running a 4p autoplay match.
- Web build loads on Netlify with no console errors.
