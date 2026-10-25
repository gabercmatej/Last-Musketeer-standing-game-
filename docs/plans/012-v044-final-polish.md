# v0.4.4 Final Polish Implementation Plan

**Goal:** Implement only the six fixes in Matej's v0.4.4 request.
**Architecture:** Presentation/UI changes only; existing authoritative events drive FX. Existing local replay completion and network skip barriers stay intact. Import exactly two FX sprites and replay music, without running the general art importer.
**Tech Stack:** Installed Unity 6000.6.6f1, UI Toolkit, PrimeTween 1.3.3, WebGL/Relay.
**Spec:** User's six-part v0.4.4 request in this session.

- [x] Recover context, verify MCP, record preexisting edits.
- [x] Add royal How to Play title, shared gentle wheel scrolling, menu reorder.
- [x] Import requested assets; event-driven single ability indicators with cleanup.
- [x] Add upper-body-aware replay framing and replay music; picker uses lobby music.
- [x] Keep podium winner names below the unchanged action buttons.
- [x] Focused checks, full EditMode suite, zero compile errors; Unity/Chrome UI/audio/FX and multiplayer smoke.
- [x] Build 0.4.4, verify isolated draft before production; update handoff, commit/push and deploy.

Protected fits, simulation, networking, shared history and existing lore edit are out of scope. No installs or delegates.

## Final release scope (user attachment, 2026-10-25)

- [x] Add one second after victory playback, then fresh five-second summary. Host timestamp synchronizes non-final countdowns; final replay and immediate See Results remain individual.
- [x] Correct environmental death descriptions and scoreboard negative-point lines; preserve existing attribution/scoring.
- [x] Replace crate reward with seeded uniform0–3 Grit, capped, once per crate; update only this authorized rule and its tip.
- [x] Focused and complete regression tests, final1.0.0 build, three visible Chrome clients with matching hashes, guest skip and individual results.
- [ ] Commit, push, tagv1.0.0, existing production deploy, verify shared data/version and handoff.
