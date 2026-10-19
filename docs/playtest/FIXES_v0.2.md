# Fix Spec after the v0.2.0 playtest

Source of truth for sessions **4B, 4C and 4D**. Matej wrote the feedback, and these are the decisions that follow from it.
Each section is tagged with the session that implements it: **[4B]** characters and cosmetics, **[4C]** screens, UX and audio, **[4D]** arena and gameplay.
Items marked *(Day 5)* are online-only. Build the UI and the local state for them now, so Day 5 only has to sync them.

New art Matej generated after Day 4 is in the root `Art/` folder. Find new or changed files by modification date. Expected:
- a new menu splash
- fullscreen, settings, lore (scroll) and leave buttons
- a results lobby with podiums
- a new arena floor tile
- a new bridge tile

Wardrobe and change-musketeer buttons may arrive later; use placeholders until then. Report what you find.

---

## 1. Characters and cosmetics [4B]: the most important part

### 1.1 Problems
- Hats are slightly off and too small.
- Face items are too small or in the wrong place (the source art is front-on, but the bodies are side-on).
- Footwear is the worst: each pair is one image, so it gets layered over the body's own boots.
- The fit has to work on **every pose**: idle, idle_b, hurt, victory and lunge/attack.

### 1.2 Solution: anchors per pose plus a Fitting Editor
**Per character × pose anchors.** Each character pose has its own set of anchors. Each anchor has a position, a rotation and a scale:
- `HeadTop` (the scale = the head size, so a hat fitted on one character scales to the others)
- `Face`
- `Back` (cape)
- `FootBack`
- `FootFront`
- `HandBack` (musket)
- `HandFront` (rapier)

Anchors are stored in a ScriptableObject per character.

**Per item.** Each item has:
- an offset, scale and rotation relative to its anchor
- an optional **per-character override** and **per-pose override**
- an `EnabledInV1` toggle

**Boots: one boot, rendered twice.**
- The importer crops a **single boot** out of each pair (the front boot, the most complete one).
- The game renders it at `FootBack` (sorted behind the body's legs, slightly darker) and at `FootFront` (in front).
- **Body boot masking:** for each character pose, define a polygon or rectangle around the body's own boots. When footwear is equipped, a SpriteMask hides exactly that region of the body, so the cosmetic boot never sits on top of the old one.
- Tall boots (all the colors) are the target for v1.
- For short footwear (flip-flops, crocs, sneakers, barefoot), the mask only removes the foot part, so dark "socks" stay visible above. Matej decides per item with `EnabledInV1` whether it looks acceptable.

**Hand overlay** (makes weapons look held, with no new art):
- Layer order from back to front: cape → back boot → **musket** → body → front boot → **rapier** → **front-hand overlay**.
- The front-hand overlay is a second SpriteRenderer of the same body sprite, masked to a hand-shaped region (per character per pose). It's drawn above the rapier, so the fingers wrap around the grip.
- The musket sits behind the body, so the body's own back hand already covers its grip.
- Musket = back hand, rapier = front hand, in every pose. The musket rotates toward the shot direction and recoils.

**Fitting Editor** (`Musketeers/Fitting Editor`), replacing the Wardrobe Preview:
1. **Anchors tab:** pick a character and pose, then drag gizmo handles on the sprite for every anchor, the boot-mask polygon and the hand-mask polygon. There's a "copy anchors from pose X" button and onion-skin to compare poses.
2. **Items tab:** pick an item, then see a **grid of all 4 characters × all poses** wearing it at once. Sliders/handles for offset, scale and rotation, with a choice to apply them globally, per character or per pose, plus an `EnabledInV1` checkbox.
3. **Outfit test tab:** random full outfits on all 4 characters × poses, to catch combinations that clash.

Changes are saved to assets, survive re-imports and get committed. Claude does a careful first pass of every anchor and item; Matej polishes afterwards.

**Fallback for v1:** anything that still looks bad after fitting gets `EnabledInV1 = false` and is hidden from the wardrobe. Hats (except the helmet and bat mask, which are likely to need extra work) and capes are expected to work.

### 1.3 Other character rules [4B]
- **Bots never wear cosmetics.** Remove that everywhere.
- Idle breathing: reduce the amplitude to about a third of the current value. Subtle, not bouncy.

---

## 2. Screens, UX and audio [4C]

### 2.1 Name rules
- 1–20 characters, required. You can't continue without a name, and the field shakes with a hint if it's empty.
- Trim spaces. Allow letters (including č, š, ž), numbers, spaces and `.'-`. "Don Mateo the Third" is valid.
- Every place a name appears truncates with "…" when it doesn't fit, never wrapping or overflowing. That covers the scoreboard boxes, the HUD, the lobby, results and chat.

### 2.2 Main menu
- Use the **new menu splash** as the menu background. The old splash becomes the **loading screen** art.
- Top-right: **Lore**, **Settings** and **Fullscreen**, using Matej's button art.
  - Fullscreen must actually toggle in the browser (it's triggered from the click).
  - Lore and Settings open the same styled window.

**Lore window text:**

> **The Last Musketeer Standing**
>
> Paris, 1627. Long before anyone wrote books about musketeers, there were four of them: **Matej, Matic, Aljaž and Nik**. *Los Cuatro Mosqueteros.* Sworn brothers. One for all, all for one. They drank from the same tankard, shared the last croissant (mostly), and saved the King exactly once, by accident.
>
> Then came the night at the tavern *Le Coq Ivre*. The King's messenger arrived with a single golden plume: the Captain's Feather, to be worn by the greatest musketeer in all of France. Just one. Four hands reached for it at the same time. Words were said. Someone mentioned hair. Someone mentioned abs. Someone threw a powder keg (Nik insists it was a cheese wheel). By dawn the tavern had no roof, and the brotherhood had no future.
>
> Now they meet in the Cardinal's crumbling courtyard and on the rotten bridges over the Seine to settle it the only way musketeers know how: muskets, rapiers, kegs of extremely questionable gunpowder, and absolutely no friendship. The ground falls away beneath their boots. The Crown goes to whoever is winning, and paints a target on his head. And when the smoke clears, only one name will be remembered.
>
> *All for one… and one for… actually, just me.*

Optional second tab, **How to play**: a short version of the README's action table and the core counters (Fire beats standing still, Parry beats Fire, Shove beats Parry, moving beats everything if you guess right).

### 2.3 Character select (a new overlay before the lobby)
Flow: Menu (name) → **Pick your Musketeer** overlay → Lobby.
- The same 4 round portraits as now.
- Clicking one shows a **bio panel**: big portrait, name in their color, title, strength, weakness, a short story, and the **signature ability** with its icon, cost and a clear explanation.
- *(Day 5)* Only one player can hold each musketeer. Taken ones can still be **previewed**, but the Select button is disabled and shows "Taken by <name>". Offline, bots take whatever is left.

| | Matej | Matic | Aljaž | Nik |
|---|---|---|---|---|
| Color | Purple | Red | Blue | Green |
| Title | The Golden Mane | The Chiseled | Quickfingers | The Bottomless |
| Strength | Hair | Abs | Finger speed | Food |
| Weakness | Stairs | Buzzcuts | Being tied up | Women |
| Signature | Grappin | Switcheroo | Triple Charge | Bull Rush |

**Bios:**
- **Matej:** His curls have their own fan club and possibly their own lawyer. Matej never loses a duel, only his footing: he has fallen down every staircase in the Louvre, twice. He carries a grappling hook "for emergencies", which means any time someone stands near a pit.
- **Matic:** Sculptors in Florence have asked to borrow his torso. Matic would agree, if they promised to keep the clippers away. The last barber who came near him with a buzzcut in mind was swapped into the Seine before he finished his sentence.
- **Aljaž:** He reloads faster than you can blink, and once won a duel before his opponent finished saying "En garde". Tie his hands, though, and he's just a very angry man with very nice glasses.
- **Nik:** He can eat a whole roast boar and still charge through a stone wall. He's fearless against swords, bulls and the Cardinal's entire guard, but if a lady says "bonjour", he forgets how doors work.

**Signature explanations** (also used in tooltips):
- **Grappin** (5 Grit): throw a hook in a straight line (5 tiles). Hit a musketeer and you yank them to the tile next to you, over pits too. Hit a pillar or crate and you pull yourself to it.
- **Switcheroo** (5 Grit): click any musketeer to swap places with them, wherever they are when the swap happens. Fails if you're standing on a cracked tile.
- **Triple Charge** (5 Grit, loaded musket): fire 3 shots down one line (5 tiles). Each hit deals 1 damage and knocks back 1 tile, so up to 3 damage and 3 tiles of knockback. A Parry reflects them.
- **Bull Rush** (5 Grit): charge up to 3 tiles in a straight line. Whoever you hit flies back **4 tiles**.

### 2.4 Lobby redesign
- **Remove the big blue bar** across the bottom half.
- Move the stage characters **up a little** and make the bottom area **smaller**.
- Fix the **tavern sign**: centered, cleaner, or drop it if it can't look good.
- Above each musketeer: the player name **bigger**, in the character's color (bots show "Bot"). Below: a status line, **Ready** (green) or **Not ready** (red). Bots are always ready.
- **Bottom-left:** **Wardrobe** and **Change Musketeer** (which reopens the select overlay). Placeholder buttons until Matej's art arrives.
- **Top-right:** Settings and Fullscreen (same as the menu). The Menu/back button gets bigger, with a "Leave lobby?" confirmation.
- **Top-left, game-master panel** (host only; others see it read-only):
  - map choice (Courtyard, Bridge, and future maps automatically)
  - **points to win** 10–30 (default 12)
  - **hearts per round** 1–5 (default 3) → `MaxHp`
  - add/remove bots
- **Bottom-right:**
  - The host sees **Start**. If anyone isn't ready, Start asks: "Not everyone is ready. Start anyway?"
  - Everyone else sees **Ready / Not ready** *(Day 5)*.
- The Wardrobe opens as a styled panel with tabs (Hat / Face / Cape / Boots) and a live preview on your character. It shows only `EnabledInV1` items.

### 2.5 Results screen
- Use Matej's new **results lobby** art. Musketeers stand on their **podiums** (1st, 2nd, 3rd, and 4th beside them), fitted precisely to the art.
- **Ranking and tiebreak:**
  1. Total points.
  2. If tied, compare the **last round's points**. If that's still tied, the round before, and so on backwards.
  3. If every round was equal, they share the place.

  This **replaces** the old round-wins/sudden-death tiebreak for deciding the match winner too. Update GAME_DESIGN and log it.
- **Badges:** award **every** badge each player earned (several per player are fine). Reveal them one by one with an animation: a pop and scale in with a bounce, a shine sweep, a sound per badge, and staggered timing.
- Buttons: **Play again** (same lobby, same players, straight back to the lobby) and **Quit** (to the menu). Big and obvious.

### 2.6 Audio
- Remove the announcer lines for **Bonk** and for **signature moves**.
- Remove the **reveal** sound completely.
- Default volumes: **Music −20%** and **Voice −20%** from the current defaults. Players can still change them.
- Fix the **ticking when you drag a volume slider**:
  - Don't play a UI sound on every value change. Play one short preview at most, on release.
  - Ramp the volume smoothly instead of jumping.
- Victory voice ("YES haha"): plays when a player **wins a round**, but **not** on the results screen.

### 2.7 In-match HUD
- **Top bar thinner:**
  - "Round X" with "Turn Y" underneath it.
  - The clock upright, not tilted.
  - Score boxes less tall and less wide; portraits never cut off; names with "…".
- **Left side:**
  - Keep Settings (with Matej's button art).
  - Add **Leave** (Matej's art) with the popup "Leave this match? You won't be able to rejoin."
  - Remove **Speed** and **Log** from player builds. Keep them only behind a dev flag.
- **Emotes and chat:** remove the Z/X/C/V keybinds. Add a **small chat** in the top-left:
  - A text field (max 80 characters) with a row of emote buttons.
  - Messages appear in a short fading log; an emote also shows a bubble above your musketeer and plays its sound.
  - Offline: local only. *(Day 5: synced.)*
  - Typing in chat must not trigger action hotkeys.

---

## 3. Arena and gameplay [4D]

### 3.1 Board fills the whole arena
- The playable grid should cover the **whole arena floor of the background art**, not a small board in the middle.
- Pick the grid dimensions so tiles are square and fill the arena area of each background, around **15×9**. You can adjust by ±2 to fit the art, but keep the map symmetric.
- Make musketeers and props bigger so they fill their tiles nicely.
- **Every tile must read like a chess square:**
  - Use the **new arena tile**. The old courtyard tile looked like 2×2 tiles per square.
  - Add a subtle checker tint and thin grid lines.
- **Courtyard:** the edges are walls, so you **can't be pushed off**. A push into the edge is a wall slam. The crumbling arena still creates pits inside.
- **Bridge:** rotate every second plank tile by 90° in a chess pattern so the squares are obvious. Use the **new bridge tile** for actual bridges. The map is islands, bridges and water; water and off-map are deadly.

Proposed layouts (`.` floor, `#` pillar, `o` crate, `~` pit/water, `b` bridge, `1–4` spawns). Adapt the size only if the background art requires it.

**Courtyard 15×9** (walled edges):
```
...............
.1....o.o....2.
...#.......#...
.....~...~.....
.o.....#.....o.
.....~...~.....
...#.......#...
.3....o.o....4.
...............
```

**Bridge 15×9** (falling off the edge or into water = death; the 1-tile water gaps on the bridges can be dash-leaped):
```
~~~~~~~~~~~~~~~
~1..~~~~~~~..2~
~...bbb~bbb...~
~.o.~~.#.~~.o.~
~...~~...~~...~
~.o.~~.#.~~.o.~
~...bbb~bbb...~
~3..~~~~~~~..4~
~~~~~~~~~~~~~~~
```

- **Bridge tiles** are normal walkable floor, but **pushes off their sides drop you into the water**. That's the whole point of fighting on them.
- **Crumble timing** must scale with the map size: rings fall every 2 turns starting from turn 5, until only the center is left. Check that a round still ends within about 15 turns.

### 3.2 Rule changes (update GAME_DESIGN, add tests, log in DECISIONS)
1. **Dash** moves 2 tiles and passes **over crates, kegs, players and 1-tile pits**. Only **pillars** block it.
   - The **landing** tile must be free (no crate, keg or pillar, and no unit that stays). Otherwise the dash fails: you stay put, and the Grit is spent.
   - Passing over a player causes no bonk.
2. **Planning onto occupied tiles:** players can plan a Move or Dash onto a tile **another musketeer is standing on**. The simulation decides: if that unit moves away, you get the tile; if not, you're blocked, as the rules already say. The preview shows a "contested" marker instead of refusing the move.
3. **Musket range:** keep it at 5 (`FireRange`), and enforce it in the simulation, the preview *and* the tracer. The preview highlights exactly the tiles in range. If there's a place where the range isn't limited today, fix it.
4. **Signature cost: 5 Grit** for all four (max Grit stays 5).
5. **Switcheroo:** instead of a direction, you pick a **target musketeer**: click any living musketeer.
   - It resolves in Phase 2b with **wherever the target is at that moment**.
   - It **fails** (Grit spent) if Matic is standing on a **warned/cracked** tile when it resolves, or the target is dead.
6. **Bull Rush:** knockback **4 tiles** (was 2). Charge distance stays 3.
7. **Double Charge becomes Triple Charge:**
   - Needs a loaded musket; unloads it.
   - Fires 3 shots in sequence down one line, each with range 5.
   - Each shot that hits a unit deals 1 damage and pushes it **1 tile** away, with normal push rules (wall slam, bonk, falls). The next shot traces from the target's new position.
   - Up to **3 damage and 3 tiles** of knockback in total.
   - **No pierce. It can be parried:** each shot is reflected like a normal shot.
8. **Hearts per round** come from the lobby (`MaxHp` 1–5). **Points to win** come from the lobby (10–30).
9. **Planning time: 30 s.** It ends early when everyone has locked in.
10. All actions in a tick still resolve **simultaneously**, the same as now.

### 3.3 Planning UI and readability
- **The action bar is grouped:**
  - **Movement:** Move · Dash · Catch Breath
  - **Combat:** Fire · Reload · Shove · Parry · Keg
  - **Special:** a big Signature button with the character's ability icon

  Each group is visually separated.
- **Grit:** a big, readable counter **at the bottom, next to the action bar**, for example 5 pips with the number. It shows the projected Grit for the tick you're planning.
- **Fire and Reload states** follow the musket state projected for the tick being planned:
  - **Unloaded:** Fire at low opacity and disabled; Reload active.
  - **Loaded:** Reload at low opacity and disabled; Fire active.

  Make the loaded/unloaded indicator above each unit bigger too.
- **Previews:**
  - **Triple Charge:** the line with 3 bullet markers and knockback arrows.
  - **Bull Rush:** the charge path, the impact tile and a 4-tile knockback arrow with the landing marker.
  - **Switcheroo:** click-to-target highlighting on every valid musketeer.
  - **Grappin:** the existing preview is fine.
- **Parry visual:** replace the plain blue circle with something nicer: the rapier raised, a shimmering crescent or shield arc in the character's color, and a spark when it blocks.
- **Deaths:** after the death animation, the musketeer is **removed from the board completely** (no dimmed body).
