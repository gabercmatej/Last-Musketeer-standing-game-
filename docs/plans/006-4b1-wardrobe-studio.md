# Plan 006: Session 4B.1, Wardrobe Studio + weapon layering

Spec: Matej's 4B.1 brief (2026-10-19 session). Builds on Plan 005. Executed by Sonnet subagents, one chunk at a time; Opus reviews and tests each chunk before the next.

## Data model (what the chunks share)
- **Item fit per pose, unchanged storage:** `CosmeticDef.Global` = the Idle master; a pose override (`Character ""`, `AnyPose false`) = that pose is "custom"; "Reset to Idle" deletes it. "Override for this musketeer" = musketeer+pose override. Existing assets need no migration.
- **Link all poses:** an edit applies the same delta (move / rotate / scale ×) to every pose's fit, so existing per-pose differences survive.
- **WeaponDef** (new, `Data/Weapons/musket.asset`, `rapier.asset`): sprite + two fit sets, `Rest` and `Active` (musket aimed / rapier raised), each with Global + per-pose overrides + masks, anchored to the hand. Defaults reproduce today's look.
- **Masks = `BezierPath`** (points with in/out handles, broken flag, closed, invert). Flattened to a polygon, then the existing cached `PolygonMask` sprite drives a `SpriteMask` at runtime and the pixel test in the editor: same polygon both places.
  - Item masks live in item space (child of the item's transform), one base mask per item + optional per-pose override.
  - Body boot masks and the hand mask stay in body space per musketeer and pose (body art differs per musketeer); existing polygons convert to corner-only paths.
  - **Copy mask from <musketeer>** (Matej's addition): moves a body mask from one musketeer to the others through its anchor (BootMaskBack ↔ FootBack, BootMaskFront ↔ FootFront, HandMask ↔ HandFront): into the source anchor's local space, then out through the target's. Default: from Matej to the other three, current pose or all poses. Then touch up.
- **Checked ✔** is per item × pose (and per weapon state × pose), stored on the asset.
- **Layering:** rest musket behind the body as now. Musket aimed (Fire, Double Charge, Reload) and rapier raised (Parry): body → weapon → front-hand overlay; the aimed musket uses the front hand, so the rapier is hidden while aiming (about 0.6 s). "Triple Charge" in the brief = Aljaž's signature shot, which the code calls Double Charge. Aiming north/south still tilts the aimed fit ±80°.
- **Studio UI is IMGUI** (an editor tool with a drawing canvas, like the 4B editor); the UI Toolkit rule covers game UI.

## Tasks
- [x] 0. Commit Matej's uncommitted fit nudges (`matej.asset`, `cape_purple.asset`) as a restore point
- [x] 1. Data + layout (Sonnet): `BezierPath` + flattening, item masks on `CosmeticDef`, `WeaponDef` assets, body masks as paths (convert polygons), `CosmeticLayout.Weapon(state)` + new order, fit helpers (IsCustom, ResetToIdle, SetFit with link-all delta, musketeer override), Checked flags, `BodyMaskTransfer` (copy a body mask between musketeers through the anchor). EditMode tests for each.
- [x] 2. Runtime + import (Sonnet): `CharacterRig` item SpriteMasks (both boot renderers too), weapon states + hand overlay for aimed musket and raised rapier; `PlaybackDirector` aims on Fire / Double Charge / Reload; `FitDefaults.json` carries weapons, masks and checked flags; Import Art never undoes Studio work (re-import byte-identical test).
- [x] 3. Compositor + sheets (Sonnet): `RigCompositor` draws item masks, both boot masks and weapon states; "Export Fitting Sheets" writes one sheet per item (4 musketeers × all poses, weapons × states) to `docs/playtest/fitting-v0.2.1b/`.
- [ ] 4. Wardrobe Studio A (Sonnet): `Musketeers/Wardrobe Studio` replaces the Fitting Editor. Left: musketeer picker, pose tabs + weapon-state tabs, slots (on/off + thumbnail grid + Enabled in v1 / Checked ✔ per item; bat cowl and monocle shown). Center: big canvas (click item or layer list to select, drag = move, handles = rotate/scale) + strip of the other 3. Custom badge, Reset to Idle, Link all poses, Override for this musketeer, progress list, SAVE (assets + FitDefaults.json) with a clear "unsaved changes" state. Anchors stay editable under an "Anchors" toggle.
- [ ] 5. Wardrobe Studio B (Sonnet): pen tool (click point, click-drag handles, click first point closes, Delete, Alt-drag breaks handle, Invert, Show/Hide masks) for item, body-boot and hand masks; "Copy mask from <musketeer>" button (default Matej → other three, this pose / all poses); Copy fit (+ mask option) / Paste to… (this pose, all poses, checklist of items in the slot); shortcuts (arrows / Shift ×10, [ ], - +, Tab / Shift+Tab, PgUp/PgDn, Cmd+C/V, Cmd+Z via Unity Undo).
- [ ] 6. Verify + ship (Opus): demo masks on the pink helmet and one boot, runtime captures (helmet, boot with both masks, musket aimed) next to the sheets, all EditMode tests green, 0 console errors, DECISIONS + GAME_DESIGN §10 wording, version 0.2.1b, Web build, deploy, commit, push (separate commands).

## Non-goals
- Anything 4C/4D (screens, menu/results art, arena items, parry redesign)
- New art, per-foot boots, IdleB tuning (no art yet)
- Changing which items are Enabled in v1 (Matej's call)

## Gates
- After every chunk: compiles, 0 console errors, chunk tests green, Opus review of the diff
- End: all EditMode tests green (Balance excluded), re-import leaves fits unchanged, sheets + runtime captures checked by eye, Web build loads in the browser after deploy
