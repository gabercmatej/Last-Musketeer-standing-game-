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
