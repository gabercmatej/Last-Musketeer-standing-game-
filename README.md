# The Last Musketeer Standing

*All for one, and one for… actually, just me.*

A simultaneous-turn musketeer brawler made for **Los Cuatro Mosqueteros**: Matej, Matic, Aljaž and Nik. Everyone plans their moves in secret, everything resolves at the same time, and the last musketeer standing wins.

## How to play (for the musketeers)

**Join:** open the invite link Matej posts in the chat, in Chrome, Edge, Firefox or Safari, on Mac or Windows. There's nothing to install. Type your name, press **Join**, pick your musketeer and press **Ready**.

Keep the game tab open and in front while you play. Browsers pause hidden tabs.

**Each turn:**
1. You have 25 seconds to plan **3 actions** in secret.
2. Everyone's plans are revealed at once.
3. All 3 ticks play out simultaneously.

**Actions:**
| Key | Action | Cost | What it does |
|---|---|---|---|
| 1 | Move | free | 1 tile |
| 2 | Dash | 1 Grit | 2 tiles; leaps over a 1-tile pit |
| 3 | Fire | loaded musket | Line shot, range 5, 1 damage |
| 4 | Reload | free | Load your musket |
| 5 | Parry | 1 Grit | Blocks shots and explosions this tick, and **reflects shots back** |
| 6 | Shove | free | Push the adjacent tile's occupant 1 tile (2 tiles if they're parrying) |
| 7 | Powder Keg | 2 Grit | Lob within 3 tiles; it explodes one turn later in a 3×3 area |
| 8 | Catch Breath | free | +1 Grit |
| Q | Signature | 3 Grit | Your musketeer's special move |

Choose a direction with WASD or the arrow keys, and click a tile to aim a keg. **Backspace** undoes, and **Space** locks in.

**The triangle:**
- Fire beats standing still.
- Parry beats Fire.
- Shove beats Parry.
- Moving beats everything, if you guess right.

**Signatures:**
- **Matej, Grappin:** yank someone to you, or pull yourself to a wall.
- **Matic, Switcheroo:** swap places with the first person in a line.
- **Aljaž, Double Charge:** a long shot that pierces 2 people and ignores parry.
- **Nik, Bull Rush:** charge 3 tiles and launch whoever you hit 2 tiles.

**Dying:** you lose 3 HP, or you fall into a pit. Shoves, kegs and grapples can all send people into pits. From turn 6, the arena crumbles inward.

**Points:** last one standing +3, kill +1, killing the **Crowned** leader +2. First to 12 wins the match.

## For the developer

The rules are in `docs/GAME_DESIGN.md`, the architecture is in `docs/TECH_DESIGN.md`, and the 7-day plan with ready-to-paste prompts is in `docs/BUILD_PLAN.md`. Claude Code's instructions are in `CLAUDE.md`.

**Tech:**
- Unity 6 LTS, built for the **Web** (WebGL 2) and hosted on **Netlify**
- Netcode for GameObjects 2.x
- Unity Multiplayer Services (Sessions + Relay over secure WebSockets, with join codes and invite links)
- UI Toolkit
- Pure C# deterministic simulation with EditMode tests

**Run locally:**
1. Open `Main.unity`, then press Play → **Play vs Bots**.
2. To test multiplayer in the editor, go to Window → Multiplayer → Multiplayer Play Mode and enable 2–3 virtual players.
3. To test in the browser: File → Build Profiles → Web → **Build And Run**. Open a few tabs, host in one and join from the others. Don't mix Editor and browser players in one game.

**Deploy:**
1. Bump the version in Project Settings → Player.
2. Build the Web profile to `Builds/Web`.
3. Run `tools/deploy-web.sh`. The first time, run `npx netlify-cli login` before it.
4. Post the link. Anyone with an old tab open gets told to refresh.

## Docs
| File | What's in it |
|---|---|
| `docs/GAME_DESIGN.md` | Full rules, numbers, resolution order, scoring and addictive features |
| `docs/TECH_DESIGN.md` | Architecture, deterministic lockstep, network messages, testing |
| `docs/BUILD_PLAN.md` | Day-by-day plan, Definitions of Done, prompts, cut list |
| `docs/ART_AND_AUDIO.md` | Every asset to create, and where to put it |
| `docs/DECISIONS.md` | Log of rule clarifications and balance changes |
