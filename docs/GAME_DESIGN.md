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
- **Kill:** +1
- **Kill on the Crowned:** +2 instead of +1 (see Crown)
- **Draw** (everyone left dies on the same tick): nobody gets the +3; kills still count.
- **Tiebreak** at 12+: most round wins. If still tied, play a sudden-death round between only the tied players.

### The Crown (bounty)
At the start of each round, the player with strictly the most match points wears the **Crown** (it glows, has a crown icon, and the announcer calls it out). If there's a tie for first, nobody is crowned. This turns free-for-all into temporary alliances against the leader.

---

## 3. The board

- A square grid, **9×9** by default. Coordinates: `x` 0–8 from left to right, `y` 0–8 from bottom to top.
- **4 directions only** (N, E, S, W). No diagonal moves or shots.

| Tile | Symbol | Move through | Shots | Notes |
|---|---|---|---|---|
| Floor | `.` | yes | pass | |
| Pillar | `#` | no | block | Indestructible |
| Crate | `o` | no | block | Destroyed by 1 shot or explosion → becomes Floor |
| Pit / moat | `~` | deadly | pass | Entering = **ring-out** (instant death). Off-board counts as pit. |
| Spawn | `1`–`4` | Floor | | Player slot spawn |

Kegs (section 5) are objects that sit on a floor tile and behave like crates for movement.

### Map 1: "The Courtyard" (ships first)
Top row is y = 8.
```
.........
.1..o..2.
..#...#..
...~.~...
.o.....o.
...~.~...
..#...#..
.3..o..4.
.........
```
With 2 or 3 players, use the spawns in slot order.

### Map 2: "The Bridge" (Day 6, if there's time)
```
~~~...~~~
~1.....2~
~..#.#..~
...o.o...
.........
...o.o...
~..#.#..~
~3.....4~
~~~...~~~
```

Maps are plain-text files, so new maps (your hangout spots) are easy to add.

### The Cardinal's Guard closes in (shrinking arena)
Each tile has a **ring index**: `min(x, y, W-1-x, H-1-y)`. The outer ring is 0, and the center is 4.
- At the start of **turn 5**, ring 0 is **warned** (it glows red for one turn).
- At the start of **turn 6**, ring 0 **crumbles** into pit. Anyone standing on it falls.
- The next ring is then warned, and it crumbles the following turn: ring 1 is warned on turn 7 and falls on turn 8, and so on.
- On turn 14 the center falls, and anyone still alive dies. That round is a draw.

Config: `CrumbleStartTurn = 5`, `CrumbleEvery = 2`.

---

## 4. Musketeers

All four musketeers have identical base stats. They differ only in their **signature ability**, which keeps the game fair and readable.

| Stat | Value |
|---|---|
| HP | **3** |
| Grit (energy) | starts at **2**, max **5**, **+1 at the start of every turn after the first** |
| Musket | starts **loaded** |

Everyone can see everyone's HP, Grit and loaded/unloaded musket at all times. These are the tells you read.

### Roster (placeholder assignments: swap abilities to match personalities)
| Character | Signature (costs 3 Grit) | Flavor |
|---|---|---|
| **Matej** | **Grappin** (grappling hook) | The playmaker who drags you where he wants you |
| **Matic** | **Switcheroo** | Now you're here, and I'm not |
| **Aljaž** | **Double Charge** | The one who trusts powder over steel |
| **Nik** | **Bull Rush** | Doesn't go around things |

Characters are unique in a lobby. Each player picks a different one, and bots take any that are left.

---

## 5. Actions

Each turn you plan **3 actions**, one per tick. Unplanned ticks become **Idle**: nothing happens and you gain no Grit. Catch Breath only happens when you plan it.

| # | Action | Target | Cost | Effect |
|---|---|---|---|---|
| 1 | **Move** | direction | 0 | Move 1 tile. |
| 2 | **Dash** | direction | 1 Grit | Move 2 tiles in a straight line. **Can leap over a 1-tile pit.** |
| 3 | **Fire** | direction | needs loaded | Instant line shot, range **5**. Hits the first unit, crate, keg or pillar for **1 damage**. Unloads your musket. |
| 4 | **Reload** | none | 0 | Musket becomes loaded. You can do it while loaded, but it's wasted. |
| 5 | **Parry** | none | 1 Grit | This tick: block all shot and explosion damage, and **reflect** shots back the way they came (riposte). Pushes still move you. |
| 6 | **Shove** | direction | 0 | Push whatever is on the adjacent tile 1 tile away. Against a **parrying** unit: **Guard Break**, which pushes 2 tiles. Shoving a keg **kicks** it (it slides up to 3 tiles). |
| 7 | **Powder Keg** | tile within 3 (Manhattan) | 2 Grit | Lob a keg. It lands this tick and **explodes 3 ticks later** (the same tick slot next turn). Max 1 active keg per player. |
| 8 | **Catch Breath** | none | 0 | +1 Grit (it can be spent from the next tick on). |
| – | **Idle** | none | 0 | Does nothing. Every tick you didn't plan is Idle. |
| Q | **Signature** | depends | 3 Grit | See below. |

### The core triangle
- **Fire** beats standing still, shoving and reloading.
- **Parry** beats Fire (the shot reflects into the shooter).
- **Shove** beats Parry (Guard Break: pushed 2 tiles, likely into a pit).
- **Moving** beats Shove and Fire, as long as you move where they didn't aim.
- **Kegs** force movement, and the movement is what you punish.

### Signature abilities
- **Grappin** (Matej). Fire a hook in a direction, range 5. The first thing it touches decides the result:
  - A **unit**: yank it to the tile right next to you (it passes over pits safely).
  - A **pillar, crate or keg**: pull yourself to the tile right before it. Exception: a keg is pulled to you instead, so it lands on the tile next to you.
  - **Nothing:** the hook misses, and the Grit is still spent.
- **Switcheroo** (Matic). Swap positions with the first unit or keg in a direction (range 5, blocked by pillars and crates; passes over pits). If there's nothing to swap with, it misses.
- **Double Charge** (Aljaž). Needs a loaded musket. A shot with range **7** that **pierces** up to 2 units and **ignores Parry**, dealing 1 damage to each unit hit. It stops at pillars and crates, and destroys the crate it hits. Unloads your musket.
- **Bull Rush** (Nik). Charge up to 3 tiles in a direction, stopping before any pillar, crate, keg, unit or pit. If the charge stopped because of a **unit**, that unit is **pushed 2 tiles**. If it stopped because of a **keg**, the keg is kicked.

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
   - **Dash** goes to `pos + 2d`. If the first tile is a pillar, crate or keg, the dash is cancelled. If only the second tile is blocked, the dash stops on the first tile. The middle tile may be a pit (that's a leap). If the middle tile holds a unit at the start of the tick, the dash is cancelled.
   - A destination that is a pillar, crate or keg cancels the move.
2. **Head-on collisions (Bonk):**
   - If two or more units want the **same tile**, none of them move, and each takes **1 damage**.
   - If two units try to **swap tiles**, neither moves, and both take **1 damage**.
   - **Crossing paths** count as a swap: if two units' paths cross or pass through each other in the same tick (they would both enter the same tile at any point of their move, or cross the same edge in opposite directions), neither moves, and both take **1 damage**. Examples: two dashes toward each other, a dash through a unit that is moving the opposite way, or a walker stepping into the tile a dash passes through.
3. **Blocked:** if a mover's destination is occupied by a unit that ends up not moving, the mover stays put and takes no damage. Repeat this check until nothing changes, so chains of blocked units resolve. Rotations of 3 or more units all succeed.
4. Apply the moves. If a unit ends on a pit, it **falls** and dies instantly.

### Phase 2b: Movement abilities (Grappin, Switcheroo, Bull Rush)
These resolve one at a time in **initiative order**, each using the positions left by the previous one. Pushes caused by Bull Rush follow the push rules in Phase 3.

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
   - If the next tile is a pit or off the board, it **falls** and dies instantly.
5. **Kegs** that get shoved (kicked) slide up to 3 tiles until blocked. A keg that slides into a pit is gone.

### Phase 4: Shots (Fire, Double Charge)
1. Trace every shot on the same snapshot, starting from the shooter's tile and going in the chosen direction:
   - A **pillar** stops the shot.
   - A **crate** is destroyed and stops the shot.
   - A **keg** is detonated this tick and stops the shot.
   - A **unit** is hit for 1 damage and stops the shot. The exception is Double Charge, which pierces up to 2 units.
2. **Parry:** a normal shot that hits a parrying unit does no damage and is **reflected**. A new shot traces from the parrying unit in the opposite direction, with full range, and hits the first thing it meets. A reflected shot can't be reflected again.
3. Apply all shot damage at once. Two players who shoot each other both get hit.

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

1. **Planning (default 25 s).**
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
2. **Announcer banners** for named moments: `BONK!`, `RIPOSTE!`, `GUARD BREAK!`, `SWAN DIVE!` (self ring-out), `DOUBLE KO!`, `TOUCHÉ!` (kill by reflected shot), `THE CROWN FALLS!`, `EN GARDE!` (round start), `THE LAST MUSKETEER STANDING` (match win).
3. **Kill-cam:** at round end, replay the deciding turn at 0.5× speed with a camera push-in. This is free, because the simulation is deterministic: store the turn's start state and plans, then replay them.
4. **Quick rematch:** the results screen has **Rematch**, and play restarts when everyone presses it, or after 10 seconds.
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
7. **Emotes:** 4 per character on the **Z / X / C / V** keys during planning. Use them for taunts and inside jokes. (Alt, Ctrl and F-key combos are avoided because browsers grab them.)
8. **Hall of Fame:** every player's browser saves the match results locally (in PlayerPrefs), so you get all-time wins, kills and awards per musketeer.

---

## 9. Lobby and online flow

- **Main menu:** name field (remembered), **Host**, **Join** (code field), Settings, Fullscreen.
- **Invite link:** the site URL with `?join=CODE`, for example `https://tlms.netlify.app/?join=K7Q2PX`. Opening it fills in the code, so the friend only types a name and presses Join.
- **Lobby:**
  - Shows the join code big at the top, with **Copy code** and **Copy invite link** buttons.
  - Player list.
  - Character picker: 4 portraits, with already-taken characters greyed out.
  - Ready toggle.
  - Host only: **Add Bot**, **Remove Bot**, match settings (planning time 15/20/25/30 s, points to win 8/12/16, map), and **Start**. Start needs at least 2 players and everyone human ready.
- **Version check:** clients send their build version when they join. If it doesn't match the host's, they're kicked with the message "New version is out. Refresh the page (Cmd/Ctrl+Shift+R)." This is required, because the simulation runs on every machine, and a tab left open from an older deploy would otherwise desync.
- **Disconnects:**
  - If a client leaves or closes the tab, their musketeer just Catches Breath every tick, and it can still be killed.
  - If the host leaves, the match ends for everyone, with a message saying the host left.
- **Keep the game tab in front.** Browsers pause hidden tabs. If the **host** switches to another tab, the game freezes for everyone until they come back. Show a "⚠ Keep this tab open and visible" hint in the lobby for the host. Having Discord or a call on a second monitor is fine. Switching tabs, minimizing, or completely covering the browser window is not. Clients who tab away just fall behind and catch up when they return, but the host's tab drives the game.

---

## 10. Personalization layer (after gameplay works)

- `CharacterDef` assets hold everything for each friend:
  - name, color and portrait
  - model/sprite prefab
  - signature ability
  - victory line, death lines and emote set
  - announcer callouts that mention them
- Cosmetic slots: **Hat, Cape, Boots, Musket skin**. They come from `CosmeticDef` assets, and the prefab has attachment anchors for each slot. The lobby gets a "Wardrobe" tab later.
- Maps can be your hangout spots, as new map text files plus a tileset.
- Death messages and award names can be your inside jokes. They're all text in data assets, with no code changes needed.

---

## 11. Tuning knobs (`GameConfig`)

| Knob | Default |
|---|---|
| BoardSize | 9 |
| MaxHp | 3 |
| StartGrit / MaxGrit / GritPerTurn | 2 / 5 / 1 |
| StartLoaded (muskets start loaded, §4) | true |
| TicksPerTurn | 3 |
| PlanningSeconds | 25 |
| RevealSeconds | 1.5 |
| FireRange / DoubleChargeRange | 5 / 7 |
| KegThrowRange / KegFuseTicks / MaxKegsPerPlayer | 3 / 3 / 1 |
| KegKickDistance | 3 |
| Costs: Dash / Parry / Keg / Signature | 1 / 1 / 2 / 3 |
| GrappleRange / SwitcherooRange / BullRushDistance / BullRushPush | 5 / 5 / 3 / 2 |
| CrumbleStartTurn / CrumbleEvery | 5 / 2 |
| PointsToWin / WinPoints / KillPoints / CrownKillPoints | 12 / 3 / 1 / 2 |

Balance with bot simulations (build plan Day 6), then with friends.
