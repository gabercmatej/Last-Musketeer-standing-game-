# Decisions Log

Record every rule clarification, balance change and architecture choice here, one line each, with the newest at the bottom.
Format: `YYYY-MM-DD — Decision — Why`

- 2026-10-17 — Deterministic lockstep of plans (only plans + seed go over the network) — simplest multiplayer for a turn-based game; free replays/kill-cam.
- 2026-10-17 — One scene (Main.unity) for menu, lobby and game — avoids networked scene loading.
- 2026-10-17 — UI Toolkit for all menus/HUD — file-based UI that Claude Code can author directly.
- 2026-10-17 — Platform: Unity Web build on Netlify, everyone (incl. host) in the browser; Relay over WSS (RelayProtocol.Default) — no downloads, works on Mac + Windows.
- 2026-10-17 — URP 2D (Universal 2D template), top-down 3/4-view sprites — light for web, AI-generated 2D art is easier to keep consistent, tweens give life to static sprites.
- 2026-10-17 — Web build uses Gzip + Decompression Fallback — zero server header config on any static host.
- 2026-10-17 — Emotes on Z/X/C/V instead of Alt+1..4 — browsers intercept Alt/F-key combos.
- 2026-10-17 — Signature ability assignments are placeholders (Matej Grappin, Matic Switcheroo, Aljaž Double Charge, Nik Bull Rush) — swap to fit personalities.
- 2026-10-18 — Package versions: NGO 2.13.3, Multiplayer Services 2.4.1, Multiplayer Play Mode 3.0.0, PrimeTween 1.3.3 (OpenUPM); Unity Transport comes in via NGO as 6.6.0 (now versioned with the editor, not 2.x) — latest compatible with Unity 6000.6.6f1.
- 2026-10-18 — Removed visualscripting, collab-proxy, learn.iet-framework, timeline, 2d.aseprite, 2d.psdimporter, 2d.spriteshape; deleted template SampleScene + Welcome folder — smaller web build.
- 2026-10-18 — Sessions don't set UnityTransport.UseWebSockets; we set it to (RelayProtocol.Default == WSS) before create/join — otherwise UTP errors when the relay data is WSS but the transport isn't.
- 2026-10-18 — Editor auth profile = "ed" + FNV hash of Application.dataPath (each MPPM virtual player runs from its own Library/VP copy); Web = "web" + 8 random chars per page load — unique per player without depending on MPPM tags.
- 2026-10-18 — NetSmoke sandbox uses IMGUI and lives in Assembly-CSharp (no asmdef) — throwaway until Day 4; the real UI stays UI Toolkit.
- 2026-10-18 — Web player settings live in code (Tools/Musketeers/Apply Web Player Settings, Build Web → Builds/Web) — reproducible, matches TECH_DESIGN §7.
- 2026-10-18 — Relay protocol is set with `.WithRelayNetwork().WithNetworkOptions(new NetworkOptions { RelayProtocol = RelayProtocol.Default })`, on both SessionOptions and JoinSessionOptions — the TECH_DESIGN §5 `RelayNetworkOptions(protocol, …)` ctor is obsolete in Multiplayer Services 2.4.1, and joiners can now pick a protocol too.
- 2026-10-18 — RelayProtocol.Default is WSS in the Editor too while the active build target is Web (UNITY_WEBGL is defined), so MPPM tests also run over WSS — Editor↔Editor still match each other.
- 2026-10-18 — Enter Play Mode Options turned off (domain reload on every Play) — the template disabled domain reload, which leaked Services/NGO/session statics between Play sessions; correctness over a faster Play button.
- 2026-10-18 — In-scene NetworkObjects created by editor scripts must be re-validated after the scene is saved and in Build Settings (NGO only marks InScenePlaced when buildIndex ≥ 0) — the scene builders do save → add to build → reopen → OnValidate → save.
- 2026-10-18 — NetSmokeAutoTest (editor-only, enabled by a flag file in the temp dir) lets Claude drive MPPM host+join+ping without clicks; uses System.IO in Editor only.
- 2026-10-18 — Netlify site: last-musketeer (https://last-musketeer.netlify.app), linked via .netlify/state.json (gitignored).
- 2026-10-18 — Turns are 1-based; `TurnResolver.ResolveTurn` advances `GameState.Turn` after its ticks (TurnStart.Begin then runs for the new turn) — one owner for the turn counter; initiative and kill credit read it.
- 2026-10-18 — ResolveTurn stops resolving ticks as soon as the round ends — nothing should happen after the last musketeer stands.
- 2026-10-18 — RevealSeconds 1.5 is stored as `RevealMs = 1500` — the sim is integer-only.
- 2026-10-18 — Costs are paid in Phase 0 (Fire unloads there too), so an action cancelled later (dash into a pillar, dash over a unit) keeps its cost; only Phase 0 fizzles are free — "fizzle = free" in §5 refers to the Phase 0 check.
- 2026-10-18 — Dash: a stationary unit on the landing tile blocks the whole dash (stays on its start tile, §6 2.3); only a pillar/crate/keg on the second tile shortens it to the first — literal reading of §6 2.1 vs 2.3.
- 2026-10-18 — Dash crossing paths with another mover (no shared tile, middle empty at tick start) passes through it — §6 only checks shared destinations, swaps and the middle tile at tick start.
- 2026-10-18 — Phase 2 movement bonks set no kill credit, and a unit bonks at most once per tick (1 damage) even if it's in a same-tile group and a swap — collisions are mutual accidents; dying from one is a self-kill unless someone hit you this/last turn.
- 2026-10-18 — Two or more movers wanting the same pit/off-board tile bonk like any same-tile collision and nobody falls — same rule everywhere is simplest.
- 2026-10-18 — A 3-unit rotation can't occur on a 4-direction grid (bipartite + dashes can't pass a unit in the middle tile); the smallest rotation is a 2×2 square of 4, which is what the test pins.
- 2026-10-18 — Pushes on one target combine as a vector sum: opposite pushes subtract (equal ones cancel), same direction adds (cap 3), both axes non-zero = Squeeze — one rule covers §6 3.3's three cases and mixed ones (e.g. N+S+E = pushed E 1).
- 2026-10-18 — Same-direction stacking is unreachable with plain shoves (only one unit fits behind a target); it's implemented and tested on PushMath for later push sources.
- 2026-10-18 — Push kill credit: first shover (initiative order) pushing in the net direction; for a squeeze the first shover overall; a fully cancelled push changes nothing and gives no credit. A unit bonked by a pushed unit is also credited to that shover.
- 2026-10-18 — Guard Break cancels the parry whenever a parrying unit is shoved at all (even if the pushes cancel or squeeze) — "Shove beats Parry".
- 2026-10-18 — Crates (and pillars, empty tiles, pits) can't be shoved; the Shoved event still fires so playback can show the whiff — §6 Phase 3 only pushes units and kegs.
- 2026-10-18 — Keg kicks resolve after all unit pushes, in keg id order (independent of initiative); several kicks on one keg combine like pushes (direction only); a kicked keg stops before pillars, crates, kegs and units — units settle first, then the kegs slide.
- 2026-10-18 — Shots on one snapshot: a crate or keg hit this tick still blocks other shots this tick, a crate is destroyed once, and a shot-hit keg is only marked `Detonating` until Phase 5 lands on Day 2.
- 2026-10-18 — Several shots hitting one unit in a tick: all damage lands; kill credit goes to the first shooter in initiative order (reflections count in the order of the shot they came from) — deterministic, no "simultaneous last attacker" ambiguity.
- 2026-10-18 — A reflected shot meeting a parrying unit is blocked (no damage) but not reflected again; in practice a reflection always meets the original shooter first, so this only matters for future abilities.
- 2026-10-18 — "Lethal damage in Phase 2 still shoots in Phase 4": Phase 2 damage only hits movers, who can't also fire, so the reachable case is lethal Phase 3 damage (wall slam/bonk/squeeze) on a unit that fires; that's what the Double KO test pins.
- 2026-10-18 — Units at 0 HP stay on the board (block, can be shoved and shot) until Phase 6; HP is clamped at 0; fallen units are removed instantly and stop acting.
- 2026-10-18 — Self-kill is `Died.Other == -1`; kill credit window = `KillCreditTurns = 1` (this turn or last).
- 2026-10-18 — Bonk is logged once per participant in both Phase 2 and Phase 3 (pushed unit and bystander each get one) — awards and the announcer count it the same way everywhere.
- 2026-10-18 — Added `SimEventKind.ParryRaised` (Phase 0, on a paid parry) — Parrying is cleared at tick end, so playback needs an event to show the guard.
- 2026-10-18 — TurnResolver throws if two plans claim the same slot; empty plans must be `new TurnPlan(slot)` — a `default(TurnPlan)` is slot 0 and would silently overwrite that player's plan.
