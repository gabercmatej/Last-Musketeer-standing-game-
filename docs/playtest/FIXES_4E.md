# Fix Spec 4E: final polish before Day 5

Source of truth for session **4E**. Matej wrote the feedback after playing v0.2.3; this file turns it into decisions.
**Protected:** Matej's Wardrobe Studio fits (Data/Fits, cosmetic fit assets, weapon assets, FitDefaults.json) are FINAL. They were committed in 1cb98aa. Never modify them, and never run "Import Art (reset)".

---

## 1. Main menu
1. Move the 3 top-right buttons (Lore, Settings, Fullscreen) left so that the **right edge of the Fullscreen button lines up with the right edge of the name / Play panel** below.
2. **Text-field focus:** when a text field has focus (the name field, and all text fields in the game, including chat), show a **blinking caret** and a **highlighted border/glow**, so it's obvious you can type.
3. Replace the lore text with this exact text (Matej's, lightly proofread):

> **The Last Musketeer Standing**
>
> Paris, 1627. Long before anyone wrote books about musketeers, there were four of them: **Matej, Matic, Aljaž and Nik**. *Los Cuatro Mosqueteros.* Sworn brothers. One for all, all for one. They drank from the same tankard, shared their food, and saved the King more times than anyone could count.
>
> For years, they fought side by side, defending the Crown against enemies both foreign and within. Their loyalty was unquestioned, their victories legendary. But even the strongest brotherhood can be broken by ambition.
>
> One winter evening, a royal messenger arrived at the tavern *Le Coq Ivre*, carrying a sealed decree and a single golden plume: the Captain's Feather. The King had chosen to appoint a new commander of the Musketeers, granting him wealth, power, and a place beside the throne. Only one could claim the honor.
>
> What began as a celebration soon turned bitter. Each believed himself the rightful Captain. Old rivalries resurfaced, promises were questioned, and the bonds forged through years of battle began to fracture. By midnight, swords had been drawn. By dawn, the brotherhood was no more.
>
> But the King's decree concealed a darker truth. The four musketeers had become too powerful, their loyalty to one another stronger than their loyalty to the Crown. The competition was no reward. It was a carefully laid trap, designed to leave only one standing.
>
> Now, beneath the crumbling walls of the Cardinal's fortress and upon the treacherous bridges of the Seine, four former brothers face their final battle. With muskets loaded, rapiers drawn, and the ground collapsing beneath them, there can be no retreat and no surrender.
>
> The last survivor shall claim the Captain's Feather, the favor of the King, and a place in history. The others shall be forgotten.
>
> **Four brothers. One crown. One survivor.**
>
> *All for one… and one for… actually, just me.*

## 2. Character select overlay
1. **Behind the overlay, show the actual lobby** (the stage with any musketeers already in it) at lower opacity / dimmed, instead of a plain background. Online, you can then see who has already taken which musketeer. Taken musketeers can still be previewed (bio readable); only "Select" is disabled ("Taken by <name>").
2. **Alignment:**
   - Center the "Pick your Musketeer" title over the character box.
   - Back and Select sit on the same baseline (Back is currently higher), centered under the character box.
   - In the bio, the **signature name and its cost ("5 Grit") line up** on one row.
3. **New titles, bios and stats**. Use these exactly (Matej's text, lightly proofread):

| | Matej | Matic | Aljaž | Nik |
|---|---|---|---|---|
| Title | **The Renegade** | **The Unfazed** | **The Quickdraw** | **The Prodigy** |
| Strength | Hair | Abs | Finger speed | Food |
| Weakness | Stairs | Buzzcuts | **On a leash** | Women |

- **Matej, The Renegade:** Matej enters every room as though the King himself is about to announce him. He takes his duties seriously, himself considerably less so, and has talked his way out of more executions than most men survive battles. His looks and charisma once charmed everyone, from commoners to the royal court. But since falling for a Spanish princess, France has been divided: a hero to some, a traitor to others. Now he seeks command of the Musketeers to restore his reputation and prove what he has always known: he is the greatest of them all.
- **Matic, The Unfazed:** Matic approaches both battle and royal ceremonies with the same expression: absolute nonchalance. He can outrun most cavalrymen but refuses to hurry without an exceptionally good reason. The only recorded occasion on which he abandoned his legendary composure involved a beautiful thick black noblewoman visiting the royal court. He denies everything, although three palace guards have sworn otherwise. Convinced that his superior athleticism makes him unmatched among the Musketeers, he considers the title of Captain not a prize to be won, but a formality long overdue.
- **Aljaž, The Quickdraw:** There was a time when Aljaž was the first man into battle. These days, he's more reserved when lunging into combat. Instead, he's the first to make sure everyone knows exactly when and where it's happening. His wife expects him home by supper, and even the King's orders won't change that. Beloved by guards and tavern keepers alike, he believes his lightning-fast musician's fingers and impeccable reputation make him the rightful commander of the Musketeers.
- **Nik, The Prodigy:** Nik once served a distant kingdom, where he was counted among its brightest minds and finest warriors. Yet he abandoned his promising future in search of something greater, carrying only his sword, his wisdom, and a suspicious number of cooking pots. He once rejected a noblewoman whose breasts, according to local legend, could have breastfed an entire village. Despite his brilliance, Nik has always preferred the shadows to the spotlight. But now, for the first time, he is ready to step forward and claim command.

4. **Signature explanations** (update the bios, tooltips and How to play to match §8):
   - **Grappin** (5 Grit): throw a hook in a straight line (5 tiles). Hit a musketeer and you **drag them toward you**: if they're dragged across a pit or water, **they fall in**. Otherwise they land next to you. Hit a pillar, wall or crate and you pull yourself to it (the crate is not destroyed).
   - **Switcheroo** (5 Grit): unchanged.
   - **Triple Charge** (5 Grit, loaded musket): unchanged (max 3 damage per target).
   - **Bull Rush** (5 Grit): charge up to 3 tiles. Whoever you hit takes **1 damage and flies back 5 tiles**.

## 3. Lobby
1. **Top-left:** Fullscreen, Settings and Leave, in that order from left to right, **all the same size**. (It's the mirror of the menu; that's intended.)
2. **Match settings panel on the right:**
   - Clean vertical alignment (labels in one column, controls in another, equal row heights).
   - The **heart icon sits left of the number** (♥ 3).
   - **Points to win: 6–30** (default 12).
   - Add a **Default** button that sets Points to 12 and Hearts to 3.
3. **Wardrobe:**
   - Fits are exactly as Matej set them; don't touch them.
   - **Boots tab: greyed out, "Coming soon", unselectable.** Keep all boot items and code in the game but hidden from selection; everyone wears the body's own boots. Don't delete anything.
   - Three buttons at the bottom: **[Default]** (small) · **[Done]** · **[Surprise]** (small, same size as Default).
     - Default sets Hat, Face and Cape to none.
     - Surprise stays random (excluding boots).

## 4. Match HUD and maps
1. **Top-left:** Fullscreen, Settings and Leave, the same buttons and order as the lobby.
2. **Chat moves to the right side, below the scores.** The planned-moves panel on the left stays exactly where it is.
3. **Much less transparency everywhere.** Panels become mostly opaque (at least 85–90%): moves panel, Grit, action bar, clock, chat, scores.
   - Disabled actions (for example Fire when unloaded) must stay **clearly readable**: about 55–60% opacity plus desaturated, never near-invisible.
   - Dead players' score boxes are dimmed but still clearly readable (about 70%).
4. **Corner tiles removed on BOTH maps.** In each corner, delete an L of 3 tiles: the corner tile, plus the tile next to it on each of the two edges. Corners are at x∈{0,14}, y∈{0,8}, so on a 15×9 map the removed tiles are:
   - top-left: (0,0), (1,0), (0,1)
   - top-right: (14,0), (13,0), (14,1)
   - bottom-left: (0,8), (1,8), (0,7)
   - bottom-right: (14,8), (13,8), (14,7)

   That's 12 tiles in total. Each spawn now sits in the inner corner of that L. In the map files these tiles use a new symbol **`x` = removed (outside the board)**: not drawn (the background art shows), and treated exactly like off-board on that map (walls on the Courtyard, a deadly drop on the Bridge; see §8).

5. **Bridge layout.** Matej's mockup, read cell by cell. `b` = bridge plank (planks across the walking direction), `.` = plank floor, `~` = water, `#` = pillar, `x` = removed, `1–4` = spawns. There are 4 bridges: two horizontal ones (row 4) and two vertical ones (column 7).
```
xx~.........~xx
x1....~b~....2x
....~~~b~~~....
.~..~~.#.~~..~.
.~..bb...bb..~.
.~..~~.#.~~..~.
....~~~b~~~....
x3....~b~....4x
xx~.........~xx
```
6. **Courtyard layout.** Only the pillars are fixed now; pits and crates are random (§5). The edges and the `x` corners are walls.
```
@walls
xx...........xx
x1...........2x
...#.......#...
...............
.......#.......
...............
...#.......#...
x3...........4x
xx...........xx
```
(Matej's interpretation is that the random crates/pits **replace** the old fixed ones. If any fixed crate or pit should stay, say so.)

## 5. Crates and random setup
1. **Crates are always destructible**, by being **shot** (as now), by a **Shove** (new: the shove breaks the crate instead of moving it), or by an explosion.
2. A destroyed crate has a **50% chance to grant +1 Grit** (capped at max) to whoever destroyed it: the shooter, the shover, or the owner of the keg that exploded. Use the **simulation's DeterministicRng** so the online lockstep stays in sync. Show "+1 Grit" floating text and a sound.
3. **Random setup at the start of every round,** from the deterministic RNG seeded by the match seed and round number:
   - **Courtyard:** 3–6 random **pits** + 3 random **crates**.
   - **Bridge:** 3 random **crates** (no random pits).
   - Spawn rules:
     - not on edge tiles (the outer ring)
     - not on or next to a spawn point (including diagonally, so not within 1 tile in any direction)
     - only on empty plain floor (not pillars, not bridge tiles `b`, not water)
     - pits must never cut any spawn off from the others (flood-fill check; reroll if they do)
     - no two random pits next to each other, so no pit walls
   - Add tests for every rule and for determinism (same seed and round give the same layout).

## 6. Results screen
1. Badges reveal one by one, top to bottom then left to right (as now), **about 20% slower**. The badge sound plays per badge at **50% of its current volume**.
2. **Badges are clickable.** Clicking one opens a small achievement-style popup with the icon, name and description:
   - **Sharpshooter:** Landed the most musket hits this match. Every bullet had an address.
   - **Wall of Steel:** Parried the most shots. Bullets went in, ripostes came out.
   - **Bonk Lord:** Bumped into other musketeers more than anyone. Personal space is merely a suggestion.
   - **Swan Dive:** Fell into a pit or the river the most times, all on his own. Graceful. Pointless. Wet.
   - **Regicide:** Brought down the Crowned leader. Heavy is the head that wears the crown, and lighter once you're done with it.
   - **Powder Monkey:** Dealt the most damage with powder kegs. Ka-boom is a love language.
   - **Pacifist:** Dealt zero damage the whole match. A true man of peace, or just a terrible shot.
   - **Click Click:** Pulled the trigger on an empty musket the most times. Click. Click. Oops.
   - Any other badges in the code get the same treatment.
3. **Buttons:**
   - Rename "Play again" to **"Return to lobby"** (the same lobby, with the same players and bots, where you can change outfits and settings) and keep **Quit**. **Swap their current left/right order.**
   - Both use the same blue as the current Quit button.
4. Add **Fullscreen** and **Settings** buttons top-left (no Leave; Quit already does that).

## 7. Fullscreen without cropping
Fullscreen currently crops the sides on Matej's Mac. It must work on all Mac and Windows screens (16:9, 16:10, 3:2, MacBook Air ~1.54:1, 4:3, ultrawide) **without cropping anything**:
- Use the browser Fullscreen API on the page container, not just `Screen.fullScreen`.
- Size the canvas to the real window with devicePixelRatio, and recompute the camera fit and the UI layout on every resize or fullscreen change.
- Fit the board inside the safe area between the HUD bands (letterbox with background art if the aspect doesn't match, never crop).
- Set UI Toolkit scaling so the panels never overlap the board or get cut off.
- Test windowed and fullscreen at 1920×1080, 1440×900, 2560×1664 (scaled), 1280×1024 and 3440×1440, and screenshot each.
- Remove the old footer Fullscreen button from the WebGL template; the in-game ones replace it.

## 8. Gameplay changes (approved; update GAME_DESIGN, add tests, log in DECISIONS)
1. **Nobody can walk off the map** (both maps). A Move or Dash whose destination is off the board or an `x` tile is blocked: the unit stays put, no damage, and Dash Grit is still spent. The planning UI doesn't offer those tiles. Walking into pits and water is still allowed.
2. **Pushes:**
   - **Bridge:** you **can** still be pushed off the board or into an `x` tile (you fall).
   - **Courtyard:** the edges and `x` tiles are walls (wall slam). You can still be pushed into pits.
3. **Grappin:** a dragged target moves tile by tile toward Matej. If it passes over a pit or water tile, **it falls in** (kill credit to Matej). Pulling himself to a pillar, wall or crate is safe for Matej, and the crate isn't destroyed.
4. **Bull Rush:** the hit target takes **1 damage** and is knocked back **5 tiles** (normal push rules).
5. **Announcer "Signature move!" is back** (banner and voice line).
6. **Live scoring:** points for kills (+1, or +2 on the Crowned) are added **the moment they happen**, with a "+1"/"+2" pop on the score box. The +3 for surviving the round is still awarded at round end.
7. **Match point (gold border):** whenever a player's current points are **≥ points to win**, their score box (and name label) gets a **gold border/glow**. If nobody overtakes them by the end of the round, they win. Ties are decided by the ranking rule.
