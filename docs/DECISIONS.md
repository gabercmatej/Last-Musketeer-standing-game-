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
