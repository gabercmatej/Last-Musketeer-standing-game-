# Plan 005: Session 4B, characters and cosmetics fitting

Spec: `docs/playtest/FIXES_v0.2.md` §1 (everything tagged [4B]).

## Tasks
- [x] Data: `CharacterFit` asset per character (per-pose anchors HeadTop/Face/Back/FootBack/FootFront/HandBack/HandFront with pos/rot/scale, boot-mask + hand-mask polygons, weapon visibility); `PoseKind.IdleB`
- [x] Data: `CosmeticDef` offset/scale/rotation + per-character / per-pose / per-character-and-pose overrides + `EnabledInV1`; front values migrate into a Victory override
- [x] `CosmeticLayout`: one placement function (anchor ∘ item fit), boots placed twice (back darker), weapons at hand anchors
- [x] `PolygonMask`: polygon → mask sprite (runtime) / pixel test (editor), shared
- [x] `CharacterRig`: nested SortingGroup + unique order base (isolates SpriteMasks), layer order cape → back boot → musket → body → front boot → face → hat → rapier → front-hand overlay; body boot mask when footwear is on; breathing ÷3; IdleB alternation when art exists
- [x] Importer: crop a single front boot (polygon per item, side + front sheets); fits/anchors survive re-imports
- [x] Catalog: `EnabledInV1` filter in the wardrobe, Sanitize drops disabled; bots never wear cosmetics (LobbyModel, RandomLoadout removed)
- [x] Fitting Editor (`Musketeers/Fitting Editor`): Anchors / Items / Outfit test tabs, replaces Wardrobe Preview
- [x] First fitting pass: every anchor (4 × 4 poses) and every item
- [x] Screenshots: 4 × poses in (a) tall boots + plumed hat + cape, (b) each face item, (c) musket + rapier
- [x] Tests (layout precedence, anchor scale, masks, catalog filter, no bot cosmetics, wiring), 0 console errors
- [x] Docs: DECISIONS, GAME_DESIGN §9/§10 wording
- [x] Version 0.2.1, Web build, deploy, commit, push

## Non-goals
- 4C screens/UX/audio and 4D arena/gameplay items, including the new menu/results/tile art (reported only)
- New character art (no idle_b yet), per-foot boot art
- Parry visual redesign (4D); the rapier just raises on parry

## Gates
- EditMode tests green (Balance excluded), console has 0 errors
- Screenshots reviewed by eye for every character × pose
- Web build runs in the browser after deploy
