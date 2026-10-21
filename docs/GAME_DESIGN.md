# The Last Musketeer Standing — Game Design

> A simultaneous-turn musketeer brawler for Los Cuatro Mosqueteros.
> 2–4 players, free-for-all. Everyone plans in secret, everything resolves at once, last musketeer standing wins the round.
> **Plays in the browser** (Chrome, Edge, Firefox, Safari on Mac and Windows). There's nothing to install: open the link, type your name, play.

This document is the **source of truth for the rules**. Every number here lives in `GameConfig` in code. If code and this doc disagree, this doc wins (or update both on purpose and log it in `DECISIONS.md`).

---

## 1. Design pillars

1. **Reads, not reflexes.** Every action has a counter. Winning comes from guessing what your friends will do.
2. **Readable in one glance.** Few actions, a small board, all information public except the plans themselves.
3. **Fast rounds, instant rematch.** A round takes 3–5 minutes. A match takes 15–25 minutes. "One more" should take 5 seconds.
4. **Chaos you can replay.** Turns resolve with clear, slow, dramatic playback, so every blunder and outplay is visible and screamable.
5. **The code decides.** Rules live in a pure, deterministic C# simulation. The visuals only play back what the simulation says happened.

---

## 2. Match structure

| Level | What happens |
|---|---|
| **Match** | Several rounds. The first player to reach **12 points** at the end of a round wins the match. |
| **Round** | All players spawn on the map. The round ends when 0 or 1 musketeer is left alive. |
| **Turn** | **Planning** (everyone plans 3 actions in secret), then **Reveal**, then **Resolution** (3 ticks play out). |
| **Tick** | One action per player, resolved simultaneously in a fixed phase order (section 6). |

### Scoring
- **Last musketeer standing:** +3
- **Kill:** +1, added **the moment it happens** (same tick, with a "+1" pop on the score box)
- **Kill on the Crowned:** +2 instead of +1 (see Crown), also added at once
- The +3 for surviving is awarded at **round end**. The **match end** is still checked only at round end. A player whose current points are already **at or above the target** is on **match point** (gold border); if nobody overtakes them by the end of the round, they win (ranking rule below for ties).
- **Draw** (everyone left dies on the same tick): nobody gets the +3; kills still count.
- **Match end and ranking:** the match ends at the end of the round in which the top total reaches the points target (12 by default). Players are ranked by total points; a tie is decided by the points scored in the last round, then the round before it, and so on backwards. Equal in every round = a **shared place** (1st, 1st, 3rd). The winner is 1st place (co-winners share it). There is no sudden death.

### The Crown (bounty)
At the start of each round, the player with strictly the most match points wears the **Crown** (it glows, has a crown icon, and the announcer calls it out). If there's a tie for first, nobody is crowned. This turns free-for-all into temporary alliances against the leader.

---

## 3. The board

- A rectangular grid, **15×9** on both built-in maps (tiles are square and fill the arena floor of the background art). Coordinates: `x` 0–14 from left to right, `y` 0–8 from bottom to top.
- **4 directions only** (N, E, S, W). No diagonal moves or shots.

| Tile | Symbol | Move through | Shots | Notes |
|---|---|---|---|---|
| Floor | `.` | yes | pass | |
| Bridge | `b` | yes | pass | Plain Floor for the rules. It only looks like a bridge; pushes off its sides drop you into the water. |
| Pillar | `#` | no | block | Indestructible |
| Crate | `o` | no | block | Always destructible: by a shot, a **Shove** (the crate breaks instead of moving) or an explosion → becomes Floor. The destroyer has a **50% chance of +1 Grit** (capped at max Grit). |
| Pit / moat / water | `~` | deadly | pass | Entering = **ring-out** (instant death). Off-board counts as pit, unless the map has walled edges. |
| Removed | `x` | no | block | Outside the board (the background shows). Treated exactly like off-board on that map: a wall on walled maps, a deadly drop otherwise. |
| Spawn | `1`–`4` | Floor | | Player slot spawn |

Kegs (section 5) are objects that sit on a floor tile and behave like crates for movement.

**Walled edges.** A map whose first line is `@walls` has solid walls around it: the outside counts as a **pillar**. You can't walk, dash or Bull Rush off it, a push into the edge is a **Wall Slam**, and kicked kegs stop there. Shots and keg throws still end at the edge; a Grappin hook treats the wall like a pillar (see Grappin). Crumbling still makes pits inside. Without `@walls` the outside is a pit. Lines starting with `@` are directives and aren't part of the grid. `@randompits` and `@randomcrates` ask for the random per-round setup below.

**Nobody can walk off the map.** A Move or Dash whose destination (or, for a dash, the tile it flies over) is off the board or an `x` tile is **blocked**: the unit stays put, takes no damage, and a Dash still spends its Grit. Pits and water are still walkable. Pushes are different: on the **Bridge** you can be pushed off the board or into an `x` tile and fall; on the **Courtyard** the edge and `x` tiles are walls (Wall Slam), but pits still swallow you.

**Random setup (every round).** From a deterministic RNG seeded by the match seed and the round number, each round places random objects on the map: the **Courtyard** gets 3-6 pits and 3 crates, the **Bridge** 3 crates and no pits. They go only on plain floor (`.`): not on the outer ring, not within one tile (diagonals included) of any spawn, never on pillars, planks, water or `x`. Random pits are never next to each other (8 neighbours), and a pit that would cut any spawn off from the others (flood fill over non-pit, non-pillar tiles) is rerolled. The random crates and pits **replace** the old fixed ones.

### Map 1: "The Courtyard" (ships first, walled)
Top row is y = 8. The first lines are directives. Only the pillars are fixed; pits and crates are random each round. The 12 `x` tiles are the corner L's (3 tiles per corner), so each spawn sits in the inner corner of its L.
```
@walls
@randompits
@randomcrates
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
With 2 or 3 players, use the spawns in slot order.

### Map 2: "The Bridge"
Islands, four bridges (two horizontal on row 4, two vertical on column 7) and water. Falling off the edge, into an `x` tile or into the water is death for pushed units. The 1-tile water gaps between the bridges can be leaped with a Dash. Crates are random each round.
```
@randomcrates
xx~.........~xx
x1....~b~....2x
....~~~b~~~....
.~..~~...~~..~.
.~..bb.#.bb..~.
.~..~~...~~..~.
....~~~b~~~....
x3....~b~....4x
xx~.........~xx
```

Maps are plain-text files, so new maps (your hangout spots) are easy to add. The board size comes from the map file (any rectangle), and each map picks a tileset and background.

### The Cardinal's Guard closes in (shrinking arena)
Each tile has a **ring index**: `min(x, y, W-1-x, H-1-y)`. The outer ring is 0. On the 15×9 maps the center ring is 4, which is the middle row of 7 tiles.
- At the start of **turn 5**, ring 0 is **warned** (it glows red for one turn).
- At the start of **turn 6**, ring 0 **crumbles** into pit. Anyone standing on it falls.
- The next ring is then warned, and it crumbles the following turn: ring 1 is warned on turn 7 and falls on turn 8, and so on.
- On the 15×9 maps ring 3 falls on turn 12, which leaves only the 7-tile center row, and on turn 14 that row falls too: anyone still alive dies and the round is a draw.

Config: `CrumbleStartTurn = 5`, `CrumbleEvery = 2`.

---

## 4. Musketeers

All four musketeers have identical base stats. They differ only in their **signature ability**, which keeps the game fair and readable.

| Stat | Value |
|---|---|
| HP (hearts per round) | **3** by default; the host sets 1-5 in the lobby |
| Grit (energy) | starts at **2**, max **5**, **+1 at the start of every turn after the first** |
| Musket | starts **loaded** |

Everyone can see everyone's HP, Grit and loaded/unloaded musket at all times. These are the tells you read.

### Roster (placeholder assignments: swap abilities to match personalities)
| Character | Signature (costs 5 Grit) | Flavor |
|---|---|---|
| **Matej** | **Grappin** (grappling hook) | The playmaker who drags you where he wants you |
| **Matic** | **Switcheroo** | Now you're here, and I'm not |
| **Aljaž** | **Triple Charge** | The one who trusts powder over steel |
| **Nik** | **Bull Rush** | Doesn't go around things |

Characters are unique in a lobby. Each player picks a different one, and bots take any that are left.

---

## 5. Actions

Each turn you plan **3 actions**, one per tick. Unplanned ticks become **Idle**: nothing happens and you gain no Grit. Catch Breath only happens when you plan it.

| # | Action | Target | Cost | Effect |
|---|---|---|---|---|
| 1 | **Move** | direction | 0 | Move 1 tile. |
| 2 | **Dash** | direction | 1 Grit | Move 2 tiles in a straight line, **flying over** crates, kegs, musketeers and 1-tile pits. Only a **pillar** (or a wall) on the middle tile blocks it. The landing tile must be free, or the dash fails and the Grit is still spent. |
| 3 | **Fire** | direction | needs loaded | Instant line shot, range **5**. Hits the first unit, crate, keg or pillar for **1 damage**. Unloads your musket. |
| 4 | **Reload** | none | 0 | Musket becomes loaded. You can do it while loaded, but it's wasted. |
| 5 | **Parry** | none | 1 Grit | This tick: block all shot and explosion damage, and **reflect** shots back the way they came (riposte). Pushes still move you. |
| 6 | **Shove** | direction | 0 | Push whatever is on the adjacent tile 1 tile away. Against a **parrying** unit: **Guard Break**, which pushes 2 tiles. Shoving a keg **kicks** it (it slides up to 3 tiles). |
| 7 | **Powder Keg** | tile within 3 (Manhattan) | 2 Grit | Lob a keg. It lands this tick and **explodes 3 ticks later** (the same tick slot next turn). Max 1 active keg per player. |
| 8 | **Catch Breath** | none | 0 | +1 Grit (it can be spent from the next tick on). |
| – | **Idle** | none | 0 | Does nothing. Every tick you didn't plan is Idle. |
| Q | **Signature** | depends | 5 Grit | See below. |

### The core triangle
- **Fire** beats standing still, shoving and reloading.
- **Parry** beats Fire (the shot reflects into the shooter).
- **Shove** beats Parry (Guard Break: pushed 2 tiles, likely into a pit).
- **Moving** beats Shove and Fire, as long as you move where they didn't aim.
- **Kegs** force movement, and the movement is what you punish.

### Signature abilities
- **Grappin** (Matej). Fire a hook in a direction, range 5. The first thing it touches decides the result:
  - A **unit**: drag it **tile by tile** toward you until it is next to you. If it passes over (or lands on) a pit or water tile, it **falls in** (kill credit to you). Otherwise it ends next to you.
  - A **pillar, crate or keg**: pull yourself (safe; a crate is **not** destroyed) to the tile right before it. Exception: a keg is pulled to you instead, so it lands on the tile next to you.
  - On a **walled map** the edge wall counts as a pillar: the hook pulls you to the tile next to it (already next to the wall = you stay).
  - **Nothing:** the hook misses, and the Grit is still spent.
- **Switcheroo** (Matic). Pick **any living musketeer** and swap positions with them, wherever they are when it resolves (no range, no line of sight). It **fails** (the Grit is still spent) if Matic stands on a warned tile when it resolves, or if the target is dead. The target is credited to Matic for kill credit. Kegs can't be swapped.
- **Triple Charge** (Aljaž). Needs a loaded musket, and unloads it. Fires **3 shots in a row down one line**, each with range 5 (`FireRange`) from the shooter. Each shot that hits a unit deals **1 damage and pushes it 1 tile** away (normal push rules: Wall Slam, Bonk, fall), so the next shot meets it one tile further along: up to 3 hits of 1 damage, each knocking back 1 tile. A target pinned against a wall or another musketeer also takes the Wall Slam / Bonk damage, but **at most 3 damage to any one musketeer per use** (`TripleChargeMaxDamage`), Wall Slam and Bonk damage included, a bystander bonked by the slide counted separately and a reflected shot counted too; the knockback still happens. No pierce. A **crate** is destroyed (the next shot can pass), a **keg** is detonated and stops the shot, a pillar stops it. It **can be parried**: each shot that meets a parrying unit does nothing to it and is reflected like a normal shot.
- **Bull Rush** (Nik). Charge up to 3 tiles in a direction, stopping before any pillar, crate, keg, unit or pit. If the charge stopped because of a **unit**, that unit takes **1 damage** and is **knocked back 5 tiles** (normal push rules). If it stopped because of a **keg**, the keg is kicked.

### Invalid actions (fizzles)
When it's time for an action to happen, the simulation checks it. If you don't have enough Grit, your musket isn't loaded, or you already have an active keg, the action **fizzles**: nothing happens, it costs nothing, and a sad "click" plays. The planning UI tries to prevent this by projecting your Grit and musket state across your 3 ticks.

---

## 6. Tick resolution (the important part)

Every tick runs these phases in order. Inside a phase, everything is **simultaneous**: it's computed on one snapshot, then applied. Where order is unavoidable, **initiative** decides: the player slot order rotated by the turn number, so `(slot + turn) % playerCount` with the lowest going first. This keeps it fair across turns.

### Phase 0: Validate and pay
For every living unit, look at its action for this tick. Check the cost and requirements; if they aren't met, it **fizzles**. Otherwise pay the cost. Mark units that are **Parrying**.

### Phase 1: Self actions
- **Reload** sets the musket to loaded.
- **Catch Breath** adds +1 Grit (capped at 5).

### Phase 2: Movement (Move, Dash)
1. Compute each mover's desired destination:
   - **Move** goes to `pos + d`.
   - **Dash** goes to `pos + 2d` and flies over the middle tile. Only a **pillar** (or a wall) on the middle tile blocks it; a crate, keg, unit or pit there is flown over. The **landing** tile must not be a pillar, crate or keg, or the dash fails: you stay put, and the Grit stays spent. Landing on a pit is a fall. A middle or landing tile that is off the board or an `x` tile blocks the dash (Grit still spent).
   - A destination that is a pillar, crate or keg cancels the move, and so does one that is off the board or an `x` tile (no damage).
2. **Head-on collisions (Bonk):**
   - If two or more movers want the **same tile**, none of them move, and each takes **1 damage**.
   - If two movers try to **swap tiles** (each enters the other's start tile), neither moves, and both take **1 damage**.
   - A dash only ever *enters its landing tile*. Flying over a unit, or walking into the tile a dash flies over, never causes a Bonk. Dashes bonk only on a shared landing tile or an exact swap.
3. **Blocked:** if a mover's destination (a dash's landing tile) is occupied by a unit that ends up not moving, the mover stays put and takes no damage. Repeat this check until nothing changes, so chains of blocked units resolve. Rotations of 3 or more units all succeed.
4. Apply the moves. If a unit ends on a pit, it **falls** and dies instantly.

### Phase 2b: Movement abilities (Grappin, Switcheroo, Bull Rush)
These resolve one at a time in **initiative order**, each using the positions left by the previous one (Switcheroo swaps with its target's position at that moment). Pushes caused by Bull Rush follow the push rules in Phase 3.

### Phase 3: Shoves
1. Using the post-movement snapshot, every shover targets `pos + d`.
2. **Units** get a push vector `d` with strength 1, or strength 2 if the target is parrying (Guard Break, which also cancels their parry for the rest of this tick).
3. If a target gets pushes from **several shovers**:
   - Pushes in opposite directions cancel out.
   - Pushes in the same direction add up (max 3).
   - Pushes at right angles mean the target is **Squeezed**: it doesn't move and takes 1 damage.
4. Apply pushes in initiative order of the **targets**. A pushed unit slides one tile at a time, up to the push strength:
   - If the next tile is a pillar, crate or keg, it stops and takes 1 damage (**Wall Slam**).
   - If the next tile holds a unit, both stop and both take 1 damage (**Bonk**). There's no chain pushing.
   - If the next tile is a pit, off an unwalled board or an `x` tile on an unwalled board, it **falls** and dies instantly. (On a walled board the edge is a pillar, so it's a Wall Slam.)
   A **crate** hit by a shove is simply destroyed (it doesn't move); the shover rolls for Grit (see Tiles).
5. **Kegs** that get shoved (kicked) slide up to 3 tiles until blocked. A keg that slides into a pit is gone.

### Phase 4: Shots (Fire, Triple Charge)
1. Trace every shot on the same snapshot, starting from the shooter's tile and going in the chosen direction:
   - A **pillar** stops the shot.
   - A **crate** is destroyed and stops the shot.
   - A **keg** is detonated this tick and stops the shot.
   - A **unit** is hit for 1 damage and stops the shot.
2. **Parry:** a normal shot that hits a parrying unit does no damage and is **reflected**. A new shot traces from the parrying unit in the opposite direction, with full range, and hits the first thing it meets. A reflected shot can't be reflected again.
3. Apply all shot damage at once. Two players who shoot each other both get hit.
4. **Triple Charge**, after the shots above have landed: each Triple Charge, in initiative order, fires its 3 shots one after another on the live board. Each traces from the shooter with range 5 and ends on the first thing it meets. A unit hit takes 1 damage and slides 1 tile away (credit to the shooter); once a unit has taken 3 damage from this one use (shots, Wall Slam, Bonk, a reflected shot), further damage from it is skipped but pushes and slams still happen; the next shot traces again from the shooter, so it follows the target. A crate is destroyed at once, a keg is detonated (it explodes in Phase 5), a parrying unit takes no damage, isn't pushed, and reflects the shot (1 damage to the first thing it meets on the way back, no push, not reflectable again).

### Phase 5: Kegs
1. **Landing:** a newly thrown keg lands on its target tile. If that tile isn't empty floor (it has a unit, keg, crate, pillar or pit), the keg **explodes on impact** (a direct hit).
2. **Fuses:** every keg's fuse goes down by 1. Kegs at 0, plus any kegs detonated by shots, explode.
3. **Explosion:**
   - Every unit in the 3×3 area takes 1 damage, unless it's parrying. A unit takes at most 1 keg damage per tick.
   - Units on the 4 orthogonal neighbor tiles are **pushed 1 tile outward** (even if they parry).
   - Crates in the area are destroyed.
   - Other kegs in the area **chain-explode**.

### Phase 6: Deaths
- Units at **0 HP** die now, at the end of the tick. Units that fell already died earlier. This means a unit that takes lethal damage in Phase 2 still shoots in Phase 4, which allows double KOs.
- The round ends at the end of a tick when 1 or fewer units are alive.

### Turn start (before planning)
1. Turn ≥ 2: every unit gets +1 Grit.
2. Crumble: warned tiles fall, and the next ring gets warned (from turn 5).
3. Units standing on crumbled tiles fall.

### Kill credit
Each unit remembers its **last attacker**, meaning whoever last damaged, pushed, yanked, swapped or exploded it, and the turn it happened. The killer is the last attacker if that was this turn or last turn. Otherwise the death is a **self-kill** (no points to anyone, just public shame and an award).

---

## 7. A turn, minute by minute

1. **Planning (default 30 s).**
   - You see a clean board with everyone's HP, Grit, musket state and kegs with fuse numbers.
   - You build your 3-action plan. A ghost preview shows where you'll end up each tick, your shot lines and your keg landing spots.
   - **Lock In** sends a ✔ "confident" icon to everyone.
   - If everyone locks in, planning ends early. When the timer runs out, whatever you've planned so far is used.
2. **Reveal (1.5 s).** Everyone's planned arrows appear at once. This is the "oh no" moment.
3. **Resolution (~3–4 s).** Each tick plays out phase by phase, with impacts, shake and announcer banners.
4. If the round ended: show the scoreboard, give points, then start the next round with "En garde!"

---

## 8. Things that make it addictive

Ranked by fun-per-hour-of-work. Days 5–6 of the build plan.

1. **Reveal phase:** all arrows are shown before playback.
2. **Announcer banners** for named moments: `BONK!`, `RIPOSTE!`, `GUARD BREAK!`, `SWAN DIVE!` (self ring-out), `DOUBLE KO!`, `TOUCHÉ!` (kill by reflected shot), `THE CROWN FALLS!`, `EN GARDE!` (round start), `THE LAST MUSKETEER STANDING` (match win). `BONK!` and the signature-move banner are banner-only (no voice line). There is no reveal sound; the victory voice plays for the round winner at round end, never on the results screen.
3. **Kill-cam:** at round end, replay the deciding turn at 0.5× speed with a camera push-in. This is free, because the simulation is deterministic: store the turn's start state and plans, then replay them.
4. **Quick rematch:** the results screen has **Play again**, which takes everyone straight back to the lobby with the same players and settings (Quit goes to the menu).
5. **Post-match awards** (computed from the event log). Rename them to inside jokes:
   - *Sharpshooter*: most shot hits
   - *Wall of Steel*: most shots parried
   - *Bonk Lord*: most collisions
   - *Swan Dive*: fell into a pit on his own
   - *Regicide*: killed the Crowned
   - *Powder Monkey*: most keg damage
   - *Pacifist*: dealt zero damage
   - *Click Click*: most fizzles
6. **The Crown:** the bounty on the leader, from section 2.
7. **Emotes:** 4 per character (laugh, angry, salute, cry) as **buttons next to the chat** (top of the left HUD column) during planning, by a musketeer who is still standing, 1.2 s cooldown. Each shows a bubble over your musketeer, plays one of your taunts and posts a small "*laughs*" line in the chat. There are no emote keys. Use them for taunts and inside jokes. (Alt, Ctrl and F-key combos are avoided because browsers grab them.)
8. **Hall of Fame:** every player's browser saves the match results locally (in PlayerPrefs), so you get all-time wins, kills and awards per musketeer.

---

## 9. Screen flow, lobby and online

### Screens (one scene; the UI switches between them)
1. **Main menu:** menu art with the panel on the right: name field (remembered; 1-20 characters, letters, numbers, spaces and `.'-`, shakes with a hint when invalid), **Play vs Bots**, **Host**, **Join** (code field). Top-right: **Lore**, **Settings**, **Fullscreen**. Lore, **How to play** and Settings share one tabbed window.
1b. **Character select** ("Pick your Musketeer"): four portraits and a bio panel (title, strength, weakness, story, signature); **Select** or **Back**. Online, a musketeer someone else holds can be previewed but shows "Taken by <name>".
2. **Lobby** (Among Us style): the musketeers stand on the stage of the selected map's lobby art (no bottom bar).
   - **Names and status:** the player name above each musketeer in their colour (bots show "Bot"), a **Ready** (green) or **Not ready** (red) line under the feet. Bots are always ready.
   - **Top-left, game-master panel:** map (every `MapDef`, with its description), points to win 10-30, hearts 1-5, Players 2-4 (- Bot / + Bot). Read-only ("Set by the host") for a non-host.
   - **Top-right:** **Menu** (asks "Leave lobby?"), **Settings**, **Fullscreen**.
   - **Bottom-left:** **Wardrobe** (Hat / Face / Cape / Boots tabs on the right half, live preview on your musketeer, only enabled items, saved per character, bots wear nothing) and **Change Musketeer** (reopens character select; Back or Select returns to the lobby).
   - **Bottom-right:** the host's **Start** (asks "Not everyone is ready. Start anyway?" if anyone is not ready); a non-host gets a **Ready / Not ready** toggle instead.
3. **Loading screen:** splash art, a random gameplay tip and a bar, for a few seconds.
4. **Arena:** the match. The HUD sits around the board: left column = **Settings** and **Leave** (Matej's icon buttons; Leave asks "Leave this match? You won't be able to rejoin." and goes to the lobby), the **chat** (short fading log, text field max 80, 4 emote buttons; Enter opens it, Enter sends, Esc leaves it, no gameplay hotkey fires while it has focus; local only offline) and the plan slots (action icon plus a short label such as "Fire ←", "Keg (3,4)", "Swap: Nik"); right column = scores; bottom = action bar; thin top strip with Round/Turn and the timer. Announcer banners are small and sit in the top strip beside the timer, never over the board. Speed, Log and the L key exist only in dev builds.
5. **Scoreboard** between rounds (points with reasons, portraits).
6. **Results:** the played map's podium art; musketeers stand on the podiums by final place (1st tallest and a bit bigger in the Victory pose with the Crown, 2nd and 3rd Idle, 4th Hurt on the small block beside them; shared places show the same ordinal, 2-3 players leave podiums empty). A card on the right lists the winner ("Shared victory" on a tied 1st), the standings in rank order with portraits and points, and every badge each player earned, revealed one by one with a pop, a shine sweep and a sound. **Play again** goes back to the **Lobby**, **Quit** to the menu.

Each map defines its own lobby background, lobby music and results podium art (`MapDef`); the lobby switches when the map changes and the results show the art of the map that was played. In-match music is the same on every map.

Offline (vs bots) the lobby is local; online (Day 5) the same lobby is shared and adds the items below.

### Online additions
- **Invite link:** the site URL with `?join=CODE`, for example `https://tlms.netlify.app/?join=K7Q2PX`. Opening it fills in the code, so the friend only types a name and presses Join.
- **Lobby:**
  - Shows the join code big at the top, with **Copy code** and **Copy invite link** buttons.
  - Player list.
  - Character picker: 4 portraits, with already-taken characters greyed out.
  - Ready toggle.
  - Host only: **Add Bot**, **Remove Bot**, match settings (planning time, points to win 10-30 (default 12), hearts per round 1-5 (default 3), and map), and **Start**. Start needs at least 2 players and everyone human ready.
- **Version check:** clients send their build version when they join. If it doesn't match the host's, they're kicked with the message "New version is out. Refresh the page (Cmd/Ctrl+Shift+R)." This is required, because the simulation runs on every machine, and a tab left open from an older deploy would otherwise desync.
- **Disconnects:**
  - If a client leaves or closes the tab, their musketeer just Catches Breath every tick, and it can still be killed.
  - If the host leaves, the match ends for everyone, with a message saying the host left.
- **Keep the game tab in front.** Browsers pause hidden tabs. If the **host** switches to another tab, the game freezes for everyone until they come back. Show a "⚠ Keep this tab open and visible" hint in the lobby for the host. Having Discord or a call on a second monitor is fine. Switching tabs, minimizing, or completely covering the browser window is not. Clients who tab away just fall behind and catch up when they return, but the host's tab drives the game.

---

## 10. Personalization layer (after gameplay works)

- `CharacterDef` assets hold everything for each friend:
  - name, color and portrait
  - body poses (idle, optional idle_b, hurt, victory, lunge) with per-pose anchors, and a default outfit
  - signature ability
  - victory line, death lines and emote set
  - announcer callouts that mention them
- Cosmetic slots: **Hat, Face, Cape, Boots** (musket skins later). They come from `CosmeticDef` assets (slot, side sprite, front sprite for the victory pose, fit, overrides, EnabledInV1; capes draw behind the body). Each pose has **HeadTop, Face, Back, FootBack, FootFront, HandBack** (musket) and **HandFront** (rapier) anchors (position, rotation, scale) plus boot-mask and hand-mask outlines, stored per musketeer in a `CharacterFit` asset, so one item fits all four musketeers. Items have an offset, rotation and scale per pose (Idle is the master; other poses inherit it until changed), optional per-musketeer overrides, optional pen-tool masks in the item's own space, and an `EnabledInV1` toggle. Footwear is one boot drawn on both feet, and it hides the body's own boots. The musket and rapier are fitted the same way (`WeaponDef`: rest, and aimed / raised). Tune everything in **Musketeers/Wardrobe Studio**. The lobby's Wardrobe panel equips them.
- Maps can be your hangout spots, as new map text files plus a tileset.
- Death messages and award names can be your inside jokes. They're all text in data assets, with no code changes needed.

---

## 11. Tuning knobs (`GameConfig`)

| Knob | Default |
|---|---|
| BoardWidth / BoardHeight (both maps) | 15 / 9 |
| MaxHp | 3 (lobby: hearts per round 1-5) |
| StartGrit / MaxGrit / GritPerTurn | 2 / 5 / 1 |
| StartLoaded (muskets start loaded, §4) | true |
| TicksPerTurn | 3 |
| PlanningSeconds | 30 |
| RevealSeconds | 1.5 |
| FireRange | 5 |
| TripleChargeShots / TripleChargePush / TripleChargeMaxDamage | 3 / 1 / 3 |
| KegThrowRange / KegFuseTicks / MaxKegsPerPlayer | 3 / 3 / 1 |
| KegKickDistance | 3 |
| Costs: Dash / Parry / Keg / Signature | 1 / 1 / 2 / 5 |
| GrappleRange / BullRushDistance / BullRushPush / BullRushDamage | 5 / 3 / 5 / 1 |
| CrateGritChancePct / CrateGrit | 50 / 1 |
| RandomPitsMin / RandomPitsMax / RandomCrates | 3 / 6 / 3 |
| CrumbleStartTurn / CrumbleEvery | 5 / 2 |
| PointsToWin / WinPoints / KillPoints / CrownKillPoints | 12 (lobby: 10-30) / 3 / 1 / 2 |

Balance with bot simulations (build plan Day 6), then with friends.
