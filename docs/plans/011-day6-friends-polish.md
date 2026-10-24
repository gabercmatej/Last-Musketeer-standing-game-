# Day 6 — Friends polish implementation plan

> **For agentic workers:** Use superpowers:executing-plans to implement assigned tasks; the implementation lead alone operates Unity and integrates verification.

**Goal:** Complete the requested Day 6 online polish and eleven friend-play improvements, preserving cosmetic fits and deterministic simulation.
**Architecture:** Existing pure-C# sim, host-authoritative lobby and lockstep remain. Reuse current UI Toolkit screens and art. Shared history requires authenticated durable infrastructure; do not represent browser-only storage as shared or expose anonymous writes.
**Tech stack:** Installed Unity 6000.6.6f1, NGO 2.13.3, Multiplayer Services 2.4.1, UI Toolkit, PlayerPrefs.
**Spec:** User request dated 2026-10-23; docs/CODEX_HANDOFF.md Day 6; GAME_DESIGN except the explicitly requested environmental-death -1 rule and Bridge layout.

## Constraints and review focus
- Never edit Data/Fits, cosmetic/weapon fit assets, FitDefaults.json; never run reset import. No installs.
- Humans replace bots; ownership and character uniqueness survive rename/lobby changes.
- Every fall penalizes its victim exactly once, including forced falls/crumble, without losing attacker credit.
- Genuine completed matches with >=2 humans and zero bots alone qualify for history; stable IDs deduplicate retries and rematches differ.
- Settings and explicit outfit selections survive restarts; new/default feather and cape use character color.
- Match-point gold resets; interrupted result reveals cancel sounds and animations.

## A — Simulation, stats, map (worker)
- [ ] Test environmental penalties for self/forced/crumble deaths, duplicate prevention, scoring/ties, snapshot determinism.
- [ ] Implement -1 environmental death points with config/event reasons; preserve attacker kill scoring; include deaths in stats serialization.
- [ ] Bridge pillar moves from (7,4) to (2,4) and (12,4), center clear; update only corresponding map expectations, retain appearance.
- [ ] Lead runs MCP compilation and EditMode regression; review and commit verified files.

## B — Lobby and online safety (worker)
- [ ] Rename with existing art to right of Change Musketeer; sanitized host-authoritative updates and persisted local name.
- [ ] Bots stepper: offline 3 default, 1–3; online 0 default, 0–3 constrained by human count. Human joins evict bots, never reject due to bots.
- [ ] Lobby chat reuses host ChatRelay; refresh/running-match join gives clear no-rejoin message.
- [ ] Tests for rename ownership, bots replacement and constraints, chat routing. Lead integrates AppFlow wiring where needed.
- [ ] Lead runs MCP compilation/tests; review and commit verified files.

## C — Presentation and settings (lead)
- [ ] Inspect reveal, match-point reset, round victory cue; fix gaps only.
- [ ] Results placement entrance last to first, badges after standings, sound on appearance; points/round wins/kills/deaths; popup retains lower close and adds upper X.
- [ ] Reuse Change Name and Match Logs artwork. Main scoring instructions include environmental penalty.
- [ ] Persist mute independently of slider levels. Diagnose existing Batman catalog visibility without touching fits. Character-color default feather/cape preserves custom selections.
- [ ] Add Match Logs menu/results entry and compact expandable two-tab view, using safely available shared infrastructure; explicitly document blocker if credentials/service unavailable.
- [ ] Verify UI in Unity, compile and regression tests; commit verified files.

## D — Finish
- [ ] Full MCP EditMode suite excluding Balance; report pass/fail and zero console errors.
- [ ] Bump version, rebuild reproducible Main scene if necessary; Web build to Builds/Web.
- [ ] Separate visible browser windows: full online match, lobby return/rematch, chat/rename/bot replacement; distinguish local-browser verification from another network.
- [ ] Deploy via tools/deploy-web.sh. Update decisions/handoff/checklist with evidence and exact remaining backend blocker if any.
- [ ] Explicitly stage relevant files only, commit then push origin main as separate commands.

## Amendments — 2026-10-24 (latest user specification)
- [ ] Required fast chronological round-death montage from snapshots/logs; short lead-ins, simultaneous deaths grouped; existing Killcam badge, thin red frame, restrained focus/zoom, visible Skip. Any human skip is host-authoritative and ends replay for all; repeated clicks/disconnects safe; no sim mutation.
- [ ] Preserve existing combat FX and emotes; inspect before changing. Never restore BONK voice. Last Musketeer Standing announces MATCH victory only.
- [ ] Lock-in checkmarks above players; subtle red timer pulse in final five seconds; modestly larger neatly positioned Crown above hearts.
- [ ] Independent Return to Lobby, preserving session: no voting, countdown, auto-rematch, or host forcing others out of results. Reset readiness and prevent next start until remaining participants return; late returns safe.
- [ ] Grappin is blocked completely by successful Parry, with normal cost retained; deterministic regression coverage.
- [ ] Fixed four-round spawn rotation, physical clockwise TL/TR/BR/BL: ABCD, BDAC, ACDB, DABC; repeats from round 5, resets new match, stable identities and untouched spawn tiles. Cover exact schedule, adjacency, consecutive movement, fewer players, determinism.
- [ ] Shared history preference: Netlify Functions + Blobs, one idempotent record per legitimate >=2-human/zero-bot match, standings derived. Defer shared storage to v1.1 if secure submission needs disproportionate backend work; never imply local history is shared.

### Verification ledger
- 2026-10-24: workers and Astra delegate halted by account usage limit; root continues inline. Existing edits retained. Fresh Unity compile: zero errors. Full EditMode suite: 550 passed, 0 failed/skipped (8.16 s). UI wiring and amendments remain unfinished; this is not release verification.

