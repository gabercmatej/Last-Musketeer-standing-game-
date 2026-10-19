# Day 4: Art, audio and the full screen flow — Implementation Plan

> Executed inline (superpowers:executing-plans). Tick boxes as tasks finish. On "continue", re-read this file.

**Goal:** every playtest from now on looks and sounds like the real game: Matej's art and audio wired in, and the full offline flow Menu → Lobby (wardrobe, map) → Loading → Arena → Scoreboard → Results → Lobby.

**Spec:** Matej's Day 4 brief (session of 2026-10-19), `docs/ART_GUIDE.md`, GAME_DESIGN §9–§10 (updated in task 0).

**Architecture:** Source art/audio in root `Art/` + `Audio/` is never edited. `Tools/Musketeers/Import Art` (editor C#, re-runnable) cleans, trims, scales and slices it into `Assets/_Project/Art`, copies audio (ogg via ffmpeg when present) into `Assets/_Project/Audio`, and wires data assets (CharacterDef poses + anchors, CosmeticDefs + catalog, BoardThemes, MapDefs, GameArt, AudioLibrary) without overwriting anchors/offsets Matej has nudged. An `ArtPostprocessor` sets import settings by folder. At runtime a `CharacterRig` stacks body + cosmetics + held weapon using `CosmeticLayout` (the same math the editor Wardrobe Preview composites with). Lobby and results are a world-space `StageView` the camera frames; menus are UI Toolkit. Named moments come from a pure `Callouts` detector (tested); audio goes through one `AudioService`.

**Tech:** Unity 6000.6.6f1, URP 2D, UI Toolkit, PrimeTween, Input System. No new packages.

## Global constraints
- Sim untouched except the bridge map constant; determinism/fuzz tests stay green.
- Presentation never mutates GameState. No threads, no runtime System.IO, PlayerPrefs + Save only. No browser-reserved keys (emotes Z/X/C/V).
- Every art slot keeps a placeholder fallback (PlaceholderArt) — a missing file never breaks the game.
- Scene only via `Tools/Musketeers/Build Main Scene`.
- ART_GUIDE §3 specs: characters/cosmetics 512 canvas, feet pivot (0.5, 0.125); tiles 256; tall props bottom-centre pivot; PPU 256. Max size 512 characters/cosmetics/props/icons, 1024 tiles, 2048 backgrounds; no mipmaps; crunched DXT for Web.
- UI copy rendered in the game: no em dashes.
- Host/Join visible but disabled ("coming tomorrow").

## Files
- Editor: `ArtImport/ImageOps.cs`, `ArtImport/ArtImporter.cs`, `ArtImport/ArtWiring.cs`, `ArtImport/AudioImporter.cs`, `ArtImport/ArtPostprocessor.cs`, `WardrobePreviewWindow.cs`, `WardrobeCompositor.cs`; `MainSceneBuilder` (new refs), `WebBuildSettings` (version).
- Data: `CharacterDef` (+Id, PoseArt ×4 with anchors, default loadout), `CosmeticDef`, `CosmeticCatalog`, `Loadout`, `CosmeticLayout`, `BoardTheme` (+variants/overlays/background), `MapDef`, `MapCatalog`, `GameArt`, `AudioLibrary`, `TileVariant`.
- Game: `Callouts` (named-moment detector), `MusicPicker`, `Awards`, `Tips`; `BuiltInMaps.Bridge` (Sim) + `Data/Maps/bridge.txt`.
- Presentation: `CharacterRig`, `UnitView`, `BoardView`, `KegView`, `Fx`, `PlaybackDirector`, `CameraRig` (stage mode + background cover), `StageView`, `EmoteBubble`, `AudioService`.
- UI: `AppFlow` (state machine), `MainMenuUI`, `LobbyUI`, `WardrobeUI`, `LoadingUI`, `SettingsUI`, `HudUI`, `PlanBarUI`, `ScoreboardUI`, `ResultsUI`; UXML/USS + fonts (Pirata One, Fredoka).
- Web: `Assets/WebGLTemplates/Musketeers/index.html` (indeterminate bar).
- Tests: `LoadoutTests`, `CosmeticLayoutTests`, `CalloutTests`, `MusicPickerTests`, `AwardsTests`, `MapTests` (bridge), `BridgeBotTests`.

## Tasks
- [ ] 0. Housekeeping: commit ART_GUIDE; BUILD_PLAN reorder (D4 art+audio+flow, D5 online + cosmetic ids in LobbySlot, D6 = old D5+D6, D7 balance/bigger maps/friend test/deploy); ART_AND_AUDIO.md → pointer; GAME_DESIGN §9/§10 screen flow; DECISIONS lines.
- [ ] 1. ImageOps + ArtImporter: clean (alpha<16 → 0, drop detached specks), trim, area-resize; characters normalised (idle height → 330 px of 512, per-pose scale, feet centre on x=256, y=64); sheets sliced (alpha≥230 components ≥1% area, specks merged to nearest item, row-major names); tiles 256; props trimmed + bottom pivot; FX/icons/badges/emotes 256; weapons 512 wide; UI panels kept for 9-slice; backgrounds JPG ≤2048. Postprocessor by folder. Run it, inspect contact sheet.
- [ ] 2. Data + wiring: CharacterDef poses/anchors (auto-estimated, kept if already set), CosmeticDefs + catalog (slot, sprite, front sprite, offset, scale, order), BoardTheme ×2, MapDef ×2 + bridge map, GameArt, AudioLibrary. Tests: Loadout round-trip/defaults, CosmeticLayout flip/scale, bridge map parses + file matches constant, bot matches on bridge finish.
- [ ] 3. CharacterRig + WardrobeCompositor + "Musketeers/Wardrobe Preview" window; export sheet; first-pass offsets for every item by eye (all 4 characters, side + front).
- [ ] 4. Arena art: board tiles (deterministic floor variants), props, debris, pit, crack/warn overlays, map backgrounds (cover), ground shadows; camera fits any map ≤13×13; units = rig (cosmetics, musket at hand anchor aiming/recoil, rapier on parry, hurt/lunge/victory poses, breathing, flip); keg/crown/lasso + rope; every FX sprite on its event.
- [ ] 5. UI Toolkit skin: fonts, 9-slice panels/buttons (normal/pressed), banner, action + status icons in plan bar and HUD, logo.
- [ ] 6. Screen flow: Main menu (splash, logo, name, Play vs Bots, Settings, Host/Join disabled) → Lobby stage (pick, add/remove bot, map, wardrobe tabs + live preview, loadout in PlayerPrefs, bots random cosmetics) → Loading (splash, tip, ~3 s) → Arena → Scoreboard (portraits) → Results (winner victory pose on stage, portraits, badges) → Lobby. Emotes Z/X/C/V (bubble + taunt). Tests: Awards, Callouts.
- [ ] 7. Audio: AudioService (SFX/voice/music, ±5% pitch, crossfades), hooks for every playback event + UI, announcer + character voices, music per screen (battle_final when ≤2 alive or ≤1 ring left), settings sliders in PlayerPrefs; import Vorbis, music streamed, SFX decompress on load. Test: MusicPicker.
- [ ] 8. Web template indeterminate bar; Version 0.2.0; scene rebuild.
- [ ] 9. Verify: EditMode all green, 0 console errors; Play Mode every screen + full match on both maps; screenshots (menu, lobby+wardrobe, arena mid-turn FX, results).
- [ ] 10. Web build (report size), deploy, browser check (?autoplay=4), DECISIONS, commit, repo size + Art/Audio recommendation, push.

## Non-goals
Networking/LobbyNet (Day 5), reveal phase, kill-cam, rematch vote, Hall of Fame, awards data asset (Day 6), balance and bigger maps (Day 7). Frame animation. Editing source art.

## Gates
- EditMode tests all green (261 + new), counts reported.
- Console: 0 errors after import, scene build, and Play Mode matches on both maps.
- Web build loads on Netlify, plays with ?autoplay=4, no console errors; build size reported.
