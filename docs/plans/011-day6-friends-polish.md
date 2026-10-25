# Day 6 — Friends polish implementation plan

> **For agentic workers:** Use superpowers:executing-plans to implement assigned tasks; the implementation lead alone operates Unity and integrates verification.

**Goal:** Complete the requested Day 6 online polish and twelve friend-play improvements, preserving cosmetic fits and deterministic simulation.
**Architecture:** Existing pure-C# sim, host-authoritative lobby and lockstep remain. Reuse current UI Toolkit screens and art. Shared history requires authenticated durable infrastructure; do not represent browser-only storage as shared or expose anonymous writes.
**Tech stack:** Installed Unity 6000.6.6f1, NGO 2.13.3, Multiplayer Services 2.4.1, UI Toolkit, PlayerPrefs.
**Spec:** User request dated 2026-10-23; docs/CODEX_HANDOFF.md Day 6; GAME_DESIGN with explicit Oct 23–24 amendments: environmental-death -1, Bridge pillars, Grappin/Parry and four-round spawn rotation.

## Constraints and review focus
- Never edit Data/Fits, cosmetic/weapon fit assets, FitDefaults.json; never run reset import. Pinned Netlify SDK install explicitly approved on2026-10-25.
- Humans replace bots; ownership and character uniqueness survive rename/lobby changes.
- Every fall penalizes its victim exactly once, including forced falls/crumble, without losing attacker credit.
- Genuine completed matches with >=2 humans and zero bots alone qualify for history; stable IDs deduplicate retries and rematches differ.
- Settings and explicit outfit selections survive restarts; new/default feather and cape use character color.
- Match-point gold resets; interrupted result reveals cancel sounds and animations.

## Status notation
Checked implementation items mean the code is present/reviewed, not that browser behavior was observed. Separate runtime/release gates remain unchecked until root records evidence. Shared cross-device history is implemented in the final amendment below; earlier deferral entries are historical.

## A — Simulation, stats, map (worker)
- [x] Test environmental penalties for self/forced/crumble deaths, duplicate prevention, scoring/ties, snapshot determinism.
- [x] Implement -1 environmental death points with config/event reasons; preserve attacker kill scoring; reuse existing deaths stats/hash/snapshot serialization.
- [x] Bridge pillar moves from (7,4) to (2,4) and (12,4), center clear; update only corresponding map expectations, retain appearance.
- [x] Lead runs MCP compilation and EditMode regression; review and commit verified files.

## B — Lobby and online safety (worker)
- [x] Rename with existing art to right of Change Musketeer; sanitized host-authoritative updates and persisted local name.
- [x] Bots stepper: offline 3 default, 1–3; online 0 default, 0–3 constrained by human count. Human joins evict bots, never reject due to bots.
- [x] Lobby chat reuses host ChatRelay; refresh/running-match join gives clear no-rejoin message.
- [x] Tests for rename ownership, bots replacement and constraints, chat routing. Lead integrates AppFlow wiring where needed.
- [x] Lead runs MCP compilation/tests; review and commit verified files.

## C — Presentation and settings (lead)
- [x] Inspect reveal, match-point reset, round victory cue; fix gaps only.
- [x] Results placement entrance last to first, badges after standings, sound on appearance; points/round wins/kills/deaths; popup retains lower close and adds upper X.
- [x] Reuse Change Name and Match Logs artwork. Main scoring instructions include environmental penalty.
- [x] Persist mute independently of slider levels. Diagnose existing Batman catalog visibility without touching fits. Character-color default feather/cape preserves custom selections.
- [x] Add Match Logs menu/results entry and compact expandable two-tab browser-local view; immutable start roster eligibility, host-issued match ID, legitimate completion hook, persistence/dedup and leaderboard derived from saved records. Shared implementation deferred explicitly below.
- [x] Verify UI in Unity, compile and regression tests; commit verified files.

## D — Finish
- [x] Full MCP EditMode suite excluding Balance; report pass/fail and zero console errors.
- [x] Bump version, rebuild reproducible Main scene if necessary; Web build to Builds/Web.
- [ ] Separate visible browser windows: full online match, lobby return/rematch, chat/rename/bot replacement; distinguish local-browser verification from another network.
- [x] Deploy via tools/deploy-web.sh. Update decisions/handoff/checklist with exact evidence; shared backend operational.
- [x] Explicitly stage relevant files only, commit then push origin main as separate commands.

## Amendments — 2026-10-24 (latest user specification)
- [x] Required fast chronological round-death montage from snapshots/logs; short lead-ins, simultaneous deaths grouped; existing Killcam badge, thin red frame, restrained focus/zoom, visible Skip. Any human skip is host-authoritative and ends replay for all; repeated clicks/disconnects safe; no sim mutation.
- [x] Preserve existing combat FX and emotes; inspect before changing. Never restore BONK voice. Last Musketeer Standing announces MATCH victory only.
- [x] Lock-in checkmarks above players; subtle red timer pulse in final five seconds; modestly larger neatly positioned Crown above hearts.
- [x] Independent Return to Lobby, preserving session: no voting, countdown, auto-rematch, or host forcing others out of results. Reset readiness and prevent next start until remaining participants return; late returns safe.
- [x] Grappin is blocked completely by successful Parry, with normal cost retained; deterministic regression coverage.
- [x] Fixed four-round spawn rotation, physical clockwise TL/TR/BR/BL: ABCD, BDAC, ACDB, DABC; repeats from round 5, resets new match, stable identities and untouched spawn tiles. Cover exact schedule, adjacency, consecutive movement, fewer players, determinism.
- [x] Shared history decision recorded (shared implementation deferred to v1.1): Netlify Functions + Blobs, one idempotent record per legitimate >=2-human/zero-bot match, standings derived. Defer shared storage to v1.1 if secure submission needs disproportionate backend work; never imply local history is shared.

### Verification ledger
- 2026-10-24: workers and Astra delegate halted by account usage limit; root continues inline. Existing edits retained. Fresh Unity compile: zero errors. Full EditMode suite: 550 passed, 0 failed/skipped (8.16 s). UI wiring and amendments remain unfinished; this is not release verification.


- 2026-10-24 root-reported latest integrated suite: 586/586 EditMode passed, zero skipped. This is test evidence reported by the integration owner, not browser acceptance. Subsequent minimal AutoTest independent-return adaptation still needs refresh/compile before build.
- Code review caught wardrobe Default clearing the loadout at `LobbyModel.ClearMyLoadout`; root reports correction to CharacterDef default and is adding regression coverage. Presentation/settings runtime verification remains unchecked.
- AutoTest first-match returns now stagger host at 3s and guests at 6s + 2s × slot; everyone returns independently only after host marks match complete. Existing second-match slot-2 leave variant retained. This is test harness behavior only, never production auto-rematch.
- Secure shared backend: existing Netlify deploy credentials work, but no deployed shared-history functions, durable store configuration or server validation service exists. No public writable endpoint was introduced. Browser-local fallback is explicit in UI; cross-device aggregation remains v1.1.

### Remaining runtime acceptance (integration owner)
- [ ] Actual Bridge rendering: pillars at (2,4)/(12,4), center clear, unchanged scale/appearance.
- [ ] Rename/chat/default wardrobe/Bat Cowl/mute and restore, history open/expand/close at practical window sizes.
- [ ] Results reverse placement reveal + badges/sound; gold match-point entry/reset; round victory voice vs match-only final announcement.
- [ ] Every round-death montage including crumble, skip from guest, repeated skip and disconnect; immediate final state restoration and synchronized scoreboard.
- [ ] Separate visible browser full matches, staggered independent results returns, next match only after required players return/ready; map switch and leave variant.
- [ ] Latest version Web build, production deploy, final commit/push and exact evidence in handoff.

## Final presentation amendments (2026-10-25)
- [x] Replace full-screen moving replay with framed inset over stationary frozen arena; badge preserves aspect; chronological clips target ~10 seconds with slower presentation.
- [x] Between rounds: any human Skip uses host barrier. Final round: local See Results/natural finish; authoritative completion permits early lobby return while returning-player flags gate next match.
- [x] Remove lobby chat interface; retain reusable match chat/emotes.
- [x] Replace opponents' arena arrows with compact portrait/action/direction rows, resolved plans only, cleared next planning; own preview retained.
- [x] 589/589 fresh MCP EditMode tests; zero compile errors; Play Mode inset/panel rendered. Web v0.4.1 built successfully in 131s. Commit41c24c2 pushed.
- [x] Revised candidate Chrome smoke: global skip, local final results, independent returns and full second match; evidence in DAY6_VERIFICATION.md.

## Final shared-history and bug-fix acceptance (2026-10-25)
- [x] Local gold/opponent spawn rings, fixed to tiles; rotated assignments; pit cleanup. Both maps and offline/online inspected.
- [x] Netlify Functions + Blobs, JWT/project validation, host registration, immutable deduplication, server timestamps, isolated preview stores. SDK installed with explicit approval.
- [x] Royal supplied artwork, sliced icons/banners, character-based portraits, live shared wins/recency ties. Both browsers show same records; reload persistence; detailed stats/badges.
- [x] Two qualifying real-Relay matches saved exactly once each; completed bot match excluded and leaderboard unchanged. Automated production submissions rejected.
- [x] Own planner preserved through edit/lock/reveal/execution, including offline synchronous submission; independent opponent panel.
- [x] Replay audio/tween/pose/recoil cleanup; baseline camera; natural/global Skip/local final exit. Cause-aware bounds, simultaneous grouping and no sim mutation.
- [x] Final595 EditMode +6 service tests;0 compile errors; successful67-second build. Candidate two full matches plus bot completion,0 browser error logs.
- [x] Production publish + post-publish URL/version/API verification:0.4.2, deploy `6addb2d00a0f2854f7e1d6e0`, Preview:false and clean initial standings.

Earlier mixed runtime checkboxes remain conservative: not every cosmetic/disconnect/audio combination was manually exercised. Different-network play and subjective audio remain human acceptance checks, not claimed passes.

## Released UI-only follow-up — v0.4.3
- [x] Ivory Pirata One names, smaller portraits and image-relative centered victory counters.
- [x] Compact2–4-player history with local time, readable character-colored names and distinct placement pills; expansion preserved.
- [x] Chrome1280×720 and850×500 visual fixtures (long names,0/7/12/128 counts), no server writes; fixtures removed and genuine cache restored.
- [x]597 tests/0 failures,0 compile errors,82-second Web build; published deploy `6addb8c5bcfc51a56c2e091e`,0 production browser errors. Ranking/storage/multiplayer unchanged.
