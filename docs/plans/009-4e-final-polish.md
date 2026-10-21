# Plan 009: Session 4E, final polish before Day 5

Spec: `docs/playtest/FIXES_4E.md` (source of truth). Target version 0.2.4.

## Tasks
- [ ] Text/data (Haiku): lore (§1.3), titles/strengths/weaknesses/bios (§2.3), signature texts + How to play (§2.4), badge descriptions (§6.2)
- [ ] Chunk A, sim (Sonnet): `x` tiles + map loader, new Bridge + Courtyard (§4.4–4.6), crates (§5.1–5.2), random round setup (§5.3), §8.1–8.4 rules, live scoring events (§8.6), match-point query (§8.7), signature announcer event (§8.5); tests; GAME_DESIGN + DECISIONS
- [ ] Short Sonnet review of chunk A diff, fixes applied
- [ ] Chunk B, UI (Sonnet): menu (§1.1–1.2), char select over dimmed lobby (§2.1–2.2), lobby (§3), HUD + opacity (§4.1–4.3), results (§6), fullscreen without cropping (§7), +1 Grit pop, live score pop, gold match-point border, "Signature move!" banner/voice
- [ ] Gates run, ≤8 screenshots in docs/playtest/4e/
- [ ] Version 0.2.4, Web build, deploy, commit, push

## Non-goals
- No changes to Data/Fits, cosmetic/weapon fit assets or FitDefaults.json. Never run "Import Art (reset)".
- No deletion of boot items or code (Boots tab only hidden).
- No online-sync work (Day 5).

## Gates
- All EditMode tests green (Balance excluded), 0 console errors.
- Determinism + fuzz tests green.
- `git diff --stat 1cb98aa -- Assets/_Project/Data/Fits Assets/_Project/Data/Cosmetics Assets/_Project/Data/Weapons` shows nothing from this session; FitDefaults.json untouched.
