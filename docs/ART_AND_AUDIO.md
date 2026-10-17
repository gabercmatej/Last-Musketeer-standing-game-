# Art and Audio Asset List

The game runs on placeholder primitives until you swap these in. Everything is looked up through data assets (`CharacterDef`, `CosmeticDef`, tile/FX tables), so swapping art never touches gameplay code.

**Look:** 2D, top-down 3/4 view (like Into the Breach or Stardew Valley), on a square grid. Generate everything in **one consistent style and one camera angle**, on transparent backgrounds. A tile is **128×128 px**, and characters are about **128×160 px** (they overlap the tile above a little). Static poses plus code tweens are enough; full frame-by-frame animation is optional.

Drop files into `Assets/_Project/Art/` using the folder names below, then ask Claude to wire them in.

## Characters (`Art/Characters/<Name>/`), ×4: Matej, Matic, Aljaž, Nik

| Asset | Notes |
|---|---|
| Body sprites | Musketeer outfit: tunic, cape, wide hat and musket. Give each person a signature color. Facing **down, up and side** (flip the side sprite for the other direction). |
| Pose sprites | idle, fire (recoil), parry (blade up), shove, hurt, victory, and 4 emotes. Walking, dash, fall and death are done with tweens. |
| Portrait | 512×512, for the lobby, HUD and results |
| Cosmetics (later) | Draw hats, capes and boots as **separate transparent layers** aligned to the body sprite, so they stack on top |

## Board (`Art/Board/`)
- Floor tiles: cobblestone, 2–3 variants, each 1×1 unit
- Pillar: a stone column
- Crate: wooden, plus a broken version or splinter particles
- Pit/moat: water or a dark void, with edge trim
- Warned tile overlay (red glow or cracks) and a crumble animation or sprite
- Arena surroundings for each map: a courtyard wall with banners, and a bridge over a river

## Objects and FX (`Art/FX/`)
- Powder keg, with a fuse spark and number decal
- Explosion: flash, smoke and debris
- Musket muzzle flash, smoke puff and bullet tracer
- Parry spark and riposte tracer (a different color from a normal tracer)
- Guard-break burst, bonk stars and a splash for falling into the pit
- Grapple hook and rope, swap swirl, bull-rush dust trail
- Crown (worn by the leader)

## UI (`Art/UI/`)
- Game logo: "The Last Musketeer Standing"
- Action icons, 128×128: Move, Dash, Fire, Reload, Parry, Shove, Keg, Catch Breath, plus 4 signature icons
- HUD icons: heart (full/empty), Grit pip, musket loaded/unloaded, lock-in ✔, crown
- Award badges, one per award
- Menu and lobby backgrounds, plus a button and panel style

## Audio (`Audio/`)
| Group | Cues |
|---|---|
| Actions | step, dash whoosh, musket shot, click (fizzle), reload, parry clang, shove grunt, keg throw, fuse hiss, explosion, splash/fall |
| Hits | hurt grunt per character, bonk, wall slam, crate break |
| Abilities | grapple, swap, bull-rush stampede, double charge boom |
| Announcer | En garde!, Bonk!, Riposte!, Guard break!, Touché!, Double KO!, The Crown falls!, The last musketeer standing! (record your own voices) |
| UI | lock-in, timer tick (last 5 s), round win sting, match win fanfare |
| Music | menu loop, battle loop, results sting |

## Personal text (`Data/Text/`), no code needed
- Death messages, for example "{victim} took a swim, courtesy of {killer}"
- Award names and descriptions
- Announcer lines
- Victory quote per character
- Map names
