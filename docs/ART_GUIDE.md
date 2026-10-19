# Art Guide — The Last Musketeer Standing

Everything to generate, how to generate it, and how to export it so it drops straight into Unity.
Put this file at `docs/ART_GUIDE.md`. It replaces `docs/ART_AND_AUDIO.md`.

---

## 0. The five rules that make implementation easy

1. **Characters face RIGHT only, shown from the side, like Among Us.** The game flips them to face left. You never draw up, down or left versions, which cuts the work by about 4×.
2. **Paper-doll layers.** A character is a **body** plus separate **hat**, **boots**, **cape** (and optional **face**) PNGs. Every layer uses the **same 512×512 canvas** and lines up with `character_template_512.png`, so the layers stack perfectly with no positioning code. This is what makes the Among Us-style wardrobe work.
3. **One body per pose, with the head and feet in exactly the same place in every pose.** That way hats and boots fit every pose.
4. **Held items are separate.** The musket and rapier are their own sprites, which the game places in the hand, rotates and makes recoil. Bodies have empty hands at their sides.
5. **Movement comes from code, not frames.** Walking, hopping, squashing, recoil and death are all tweens. You only need still images. Frame-by-frame animation is optional polish.

The board is drawn **top-down**, and the characters are **side-view standing on it** (like Into the Breach or Wargroove). This mix reads well and is easy to produce.

---

## 1. Style bible

**Look:** chunky, cartoony 2D game art. Chibi proportions (the head is about 40% of the height), thick dark outlines, flat colors with one soft shadow tone, light coming from the top-left. Readable at small sizes. Funny rather than grim.

**Palette:** warm stone and wood for the arena; strong, saturated signature colors for the characters.

| Musketeer | Signature color (suggestion) |
|---|---|
| Matej | Royal blue |
| Matic | Crimson red |
| Aljaž | Emerald green |
| Nik | Gold / amber |

Change them freely, but each one has to be instantly distinguishable from the others.

### Master style prompt (paste at the start of EVERY image prompt)
```
2D game asset, cartoon chibi style, thick dark brown outline, flat cel shading with one
shadow tone, soft light from top-left, clean readable shapes, 17th-century French
musketeer theme, playful and funny, high detail but simple silhouette,
centered, isolated on a plain flat #00FF00 green background, no text, no watermark
```
- Use plain **green (#00FF00)** for most things. If the asset itself is green (Aljaž), use **magenta (#FF00FF)** instead.
- Remove the background afterwards (section 2).

### Consistency workflow
1. **Make a style anchor first.** Generate Matej's body until you love it. That image becomes the **style reference** for everything that follows; attach it as a reference image in every later generation.
2. For each friend, attach **their photo plus the style anchor**, and ask for "this person as a chibi musketeer in the same style as the reference".
3. Use the same model and settings for the whole project. Don't switch tools partway through.
4. Generate 4–8 options, then pick and keep only the best one. Store the rest in `art-source/` (outside Unity) in case you change your mind.

---

## 2. From generation to Unity-ready PNG (about 5 min per asset)

1. **Remove the background:** use Higgsfield's remove-background, remove.bg, or Photopea (Magic Wand on the green, then Delete).
2. **Align it on the template:**
   - Open the template in **Photopea** (free, in the browser).
   - Paste your image as a new layer and scale/move it so it matches the guides:
     - **Bodies:** feet on the red line, centered on the blue line, top of the head at the "head top" line.
     - **Hats:** sit on the head of the body layer. Keep a body visible at 50% opacity while you place the hat, then hide it.
     - **Boots:** cover the body's feet.
     - **Capes:** go behind the torso.
3. **Export:** hide the template layer, then File → Export as → PNG. Keep the canvas size exactly the same as the template.
4. **Name and file it** following section 3. For a quick check, paste a hat onto each of the 4 bodies to make sure it fits all of them.

---

## 3. Technical specs (don't deviate from these)

| Asset type | Canvas | Pivot | Notes |
|---|---|---|---|
| Character body / pose | **512×512** | feet point (0.5, 0.125) | Use `character_template_512.png`. The body is about 320 px tall, which is ~1.25 tiles. |
| Hat / Boots / Cape / Face | **512×512** | same | Draw the item **only**, in position, on the same template |
| Floor tile | **256×256** | center | Top-down, fills the whole square, seamless edges |
| Pillar / crate / keg / tall props | **256×384** | bottom center (0.5, 0) | Use `tall_object_template_256x384.png`. The bottom 256×256 is the tile it sits on. |
| Musket / rapier | **512×128** | grip | Horizontal, pointing RIGHT |
| Portrait | **512×512** | — | Head and shoulders, plain background allowed |
| Action / status icon | **256×256** | — | Simple silhouette, fills about 80% of the canvas |
| FX sprite | **256×256** | center | Single frame, white or bright on transparent |
| Backgrounds / key art | **2560×1440** | — | No transparency needed |
| Logo | **2048×1024** | — | Transparent background |

- Format: **PNG, RGBA, sRGB**. Keep your raw generations in `art-source/`; the final PNGs go in `Assets/_Project/Art/`.
- **Scale:** in Unity, 1 tile = 256 px (Pixels Per Unit = 256). Claude will set this automatically by folder, so you don't need to change import settings yourself.

### Folder and naming (lowercase, underscores; Claude wires assets by these names)
```
Assets/_Project/Art/
  Characters/matej/   body.png  hurt.png  victory.png  portrait.png  (optional: idle_b.png)
  Characters/matic/   …same names…
  Characters/aljaz/   …
  Characters/nik/     …
  Cosmetics/hats/     hat_plumed.png  hat_tricorne.png  …
  Cosmetics/boots/    boots_leather.png …
  Cosmetics/capes/    cape_classic.png …
  Cosmetics/face/     face_moustache.png …        (optional slot)
  Weapons/            musket.png  rapier.png
  Board/courtyard/    floor_01.png floor_02.png floor_03.png pillar.png crate.png
                      crate_broken.png pit.png crack_overlay.png warn_overlay.png background.png
  Objects/            keg.png  crown.png  grapple_hook.png  shadow.png
  FX/                 muzzle_flash.png smoke_puff.png spark.png explosion.png dust.png
                      splash.png stars.png swap_swirl.png
  UI/logo/            logo.png  logo_small.png
  UI/backgrounds/     key_art.png  lobby.png  loading.png
  UI/icons/actions/   move.png dash.png fire.png reload.png parry.png shove.png keg.png
                      catch_breath.png grappin.png switcheroo.png double_charge.png bull_rush.png
  UI/icons/status/    heart_full.png heart_empty.png grit.png musket_loaded.png
                      musket_empty.png lock_in.png crown_small.png skull.png
  UI/panels/          panel.png  button.png  button_pressed.png  banner.png  bubble.png
  UI/badges/          sharpshooter.png wall_of_steel.png bonk_lord.png swan_dive.png
                      regicide.png powder_monkey.png pacifist.png click_click.png
  UI/emotes/          laugh.png angry.png salute.png cry.png
  Fonts/              (downloaded .ttf files)
Assets/_Project/Audio/
  SFX/  Voice/  Music/
```

---

## 4. Asset list by priority

**Tier 1 = needed for game night. Tier 2 = makes it feel finished. Tier 3 = bonus.**
Do the tiers in order. A complete Tier 1 is worth more than a half-done Tier 3.

### 4.1 Characters (spend the most time here)
| # | Asset | Tier | Prompt add-on (after the master style) |
|---|---|---|---|
| 1 | `body.png` ×4 | 1 | `full body chibi musketeer, side view facing right, standing idle, BARE HEAD showing hair, empty hands relaxed at sides, simple plain boots, [COLOR] tunic with white cross emblem, belt, rapier sheath on hip, based on the person in the reference photo` |
| 2 | `portrait.png` ×4 | 1 | `head and shoulders portrait of the same character, wearing a classic plumed musketeer hat, confident smirk, 3/4 view, [COLOR] background` |
| 3 | `hurt.png` ×4 | 2 | the same body, `wincing in pain, eyes squeezed shut, leaning back slightly`. **Same head and feet position as body.png.** |
| 4 | `victory.png` ×4 | 2 | the same body, `triumphant pose, one fist raised, big grin`. Same head and feet position. |
| 5 | `idle_b.png` ×4 | 3 | the same body, mid-breath, very slightly squashed. The game alternates it with body.png for breathing. |

Tips:
- The base body has **no hat**, because hats are cosmetics. Everyone starts wearing `hat_plumed`.
- Keep arms close to the body and the hands empty, since the musket is added in code.
- Exaggerate what makes each friend recognizable: hair, glasses, beard, build.

### 4.2 Cosmetics (the Among Us wardrobe)
Every item is drawn **alone, in position**, on the 512 template. **Hats must fit all 4 heads**, so test each hat on every body.

| Slot | Items (✱ = default everyone starts with) | Tier |
|---|---|---|
| Hat | ✱ plumed musketeer hat, tricorne | 1 |
| Hat | beret, top hat, chef toque, party cone, viking helmet | 2 |
| Hat | 2–3 inside-joke hats (your call!) | 2 |
| Boots | ✱ tall leather musketeer boots | 1 |
| Boots | cowboy boots, sneakers, golden boots, slippers/flip-flops | 2 |
| Cape | ✱ none, classic short cape (tinted to the player color in code; draw it **white or light grey**) | 2 |
| Cape | royal ermine, tattered pirate cape | 3 |
| Face | big musketeer moustache, eyepatch, sunglasses, monocle | 3 |

Prompt for a cosmetic item:
```
[master style] single item only: [ITEM], side view facing right, as worn by a chibi
character, no character, no head, no body, isolated
```
Generated items rarely come out at the right size, so scale them in Photopea to fit the template.

### 4.3 Weapons and objects
| Asset | Tier | Prompt add-on |
|---|---|---|
| `musket.png` | 1 | `long flintlock musket, side view pointing right, wooden stock, brass details` |
| `rapier.png` | 2 | `musketeer rapier sword with ornate guard, side view pointing right` |
| `keg.png` | 1 | `small wooden gunpowder keg with skull mark and short lit fuse, 3/4 top-down view` (tall-object template) |
| `crown.png` | 1 | `small golden crown, front view, shiny` |
| `grapple_hook.png` | 2 | `iron grappling hook, side view pointing right` (the rope is drawn in code) |
| `shadow.png` | 1 | A soft black ellipse. You can make it yourself in Photopea in 10 seconds. |

### 4.4 Board, map 1 "The Courtyard" (top-down)
| Asset | Tier | Prompt add-on |
|---|---|---|
| `floor_01–03.png` | 1 | `top-down seamless cobblestone floor tile, square, warm grey stone, subtle variation` (3 variants) |
| `pillar.png` | 1 | `stone column with banner, 3/4 top-down view, round base` (tall-object template) |
| `crate.png` | 1 | `wooden crate, 3/4 top-down view` (tall-object template) |
| `crate_broken.png` | 2 | `pile of broken wooden planks and splinters, top-down` |
| `pit.png` | 1 | `top-down dark moat water hole tile, deep blue-black water, seamless, stone rim shadow at top edge` |
| `crack_overlay.png` | 2 | `top-down cracks in stone, transparent, overlay` (drawn on tiles that are about to crumble) |
| `warn_overlay.png` | 2 | You can just make a red glow square yourself. The game also tints in code. |
| `background.png` | 2 | `top-down view of a French château courtyard surroundings, garden hedges, walls, torches, the center left empty as a flat stone area`, 2560×1440 (the board sits on top of it) |

Map 2 "The Bridge" (Tier 3): the same list with `wooden bridge planks` floor and `river water` pits.

### 4.5 FX (single frames; the game animates them)
`muzzle_flash`, `smoke_puff`, `spark` (parry), `explosion` (big starburst), `dust`, `splash`, `stars` (bonk), `swap_swirl`. **Tier 2** (code placeholders work until then).
Prompt: `[master style] single cartoon [EFFECT] effect sprite, bright, bold shapes, isolated`

### 4.6 Screens: the "complete game" feel
| Screen | Assets | Tier |
|---|---|---|
| Title / main menu | `logo.png` ("The Last Musketeer Standing" with crossed rapiers and a feathered hat), `key_art.png` (all 4 musketeers posing heroically, chibi style) | 1 (logo), 2 (key art) |
| **Lobby** (Among Us style) | `lobby.png`: a cozy 17th-century tavern interior, side view, with an empty wooden floor/stage in the foreground where the 4 characters stand. The wardrobe opens as a panel on the side. | 2 |
| Loading screen | `loading.png`: the 4 musketeers charging toward the camera, dramatic. Tips text is shown on top in code. | 2 |
| HUD / panels | `panel.png`, `button.png`, `button_pressed.png`: parchment and wood, **9-slice friendly** (plain center, decorative border, no text); `banner.png`: a ribbon for "RIPOSTE!" announcements; `bubble.png`: a speech bubble for emotes | 1 (panel, button), 2 (others) |
| Results | Uses the victory poses plus the badges | — |

Logo prompt: `game logo text "The Last Musketeer Standing", ornate French baroque lettering, crossed rapiers, feathered hat, gold and royal blue, isolated`. AI tools often garble text, so if it does, generate only the emblem and let the game add the title in a font.

### 4.7 Icons (256×256, simple silhouettes, readable at 48 px)
- **Actions (Tier 1):** move (boot print), dash (double arrow), fire (musket with bang), reload (powder horn), parry (crossed blade with shield flash), shove (open palm push), keg (keg), catch breath (wind swirl), plus the 4 signatures: grappin (hook), switcheroo (circular arrows), double charge (two bullets), bull rush (bull horns).
- **Status (Tier 1):** heart full/empty, grit (golden drop or bolt), musket loaded/empty, lock-in ✔, small crown, skull.
- **Award badges (Tier 2):** 8 round medals, one per award: sharpshooter, wall of steel, bonk lord, swan dive, regicide, powder monkey, pacifist, click click.
- **Emotes (Tier 3):** laugh, angry, salute ("En garde!"), cry. These are shared by all characters and shown in a bubble.

Icon prompt: `[master style] game UI icon of [THING], bold simple silhouette, centered, thick outline, single dominant color [COLOR]`. Generate the whole set in one session, with one finished icon as the reference.

---

## 5. Audio (Tier 2; the game works silently until then)
| Group | Files | Source |
|---|---|---|
| SFX | step, dash_whoosh, musket_shot, click, reload, parry_clang, shove, keg_throw, fuse_hiss, explosion, splash, bonk, wall_slam, crate_break, grapple, swap, bull_rush, lock_in, timer_tick, ui_click | ElevenLabs Sound Effects, Higgsfield audio, or freesound.org (CC0) |
| Voice (do it yourselves!) | announcer: en_garde, bonk, riposte, guard_break, touche, double_ko, crown_falls, last_musketeer; plus each friend: hurt ×2, victory ×1, taunt ×4 | Your phones, in a quiet room, 4 of you over a beer. This is the funniest part of the whole project. |
| Music | menu_loop, lobby_loop, battle_loop, victory_sting | Suno or Udio ("baroque harpsichord meets upbeat game music"), or royalty-free |

Export as **.ogg or .wav**, 44.1 kHz. Trim silence. Normalize so everything is roughly equally loud.

## 6. Fonts (download, don't generate)
From Google Fonts:
- **Title:** *Pirata One* or *IM Fell English SC*
- **UI and numbers:** *Fredoka* or *Nunito* (bold)

Put the .ttf files in `Art/Fonts/`.

---

## 7. Checklist for game night (minimum)
☐ 4 bodies ☐ 4 portraits ☐ plumed hat + tricorne ☐ leather boots ☐ musket ☐ keg ☐ crown ☐ 3 floors + pillar + crate + pit ☐ logo ☐ 12 action icons ☐ 8 status icons ☐ panel + button ☐ 2 fonts

That's about 45 images, which is realistic over a few evenings. Everything else is Tier 2 and 3.

---

## 8. Handing it to Claude (paste when the art is in)
```
Read docs/ART_GUIDE.md. My art is in Assets/_Project/Art/ following its folder and
naming rules. Write an AssetPostprocessor that sets import settings by folder (sprite,
PPU 256, pivots from §3, no mipmaps, bilinear, good-quality compression for web), wire
everything into CharacterDefs, CosmeticDefs, the board/tile views, FX, UI Toolkit
screens and icons, keep placeholder fallbacks for anything missing, and list which
files from §7 are still missing. Then make a Web build and deploy.
```
