# Playtest — v0.2.0

**Tester:** Matej · **Date:** 2026-10-19 · **Where:** browser (https://last-musketeer.netlify.app) + Unity Wardrobe Preview

## How to log an issue
Add one line per issue to the **Issues** table at the bottom. Keep it short and concrete.

- **Screenshots:** Cmd+Shift+4 → save into `docs/playtest/v0.2.0/` named `01.png`, `02.png`, … and put that number in the table. Claude can look at screenshots, but not at videos.
- **Animations or timing that's hard to capture:** describe it in words, e.g. "the shot tracer appears before the musket recoils".
- **Severity:**
  - **B = Blocker**: breaks the game, or can't continue
  - **M = Major**: clearly wrong or confusing; must be fixed before game night
  - **m = minor**: wrong but playable
  - **P = Polish**: looks or feels off
  - **D = Design**: a rule or feel change, not a bug
  - **W = Wish**: a new idea
- Write what you **expected**, not just what happened. "Hat floats 10 px above Nik's head in the hurt pose; should sit on his hair" is fixable. "Hats look weird" isn't.

---

## Pass 1: first-time player (10 min)
Open the site in a **private/incognito window** (that starts with empty save data). Act like a friend seeing it for the first time.
- [ ] The loading bar animates. How long did it take to load? ___ s
- [ ] The menu looks finished: logo, art, buttons, text readable
- [ ] Without reading anything, did you understand what to click? Where did you hesitate?
- [ ] Name field, then the lobby: is it clear how to pick a character and open the Wardrobe?
- [ ] Is it clear how to add or remove bots, pick a map and start?
- [ ] Loading screen: are the tips readable and does it last the right amount of time?
- [ ] First turn: could you work out how to plan 3 actions without help? What was unclear?

## Pass 2: every screen (15 min)
| Screen | Check |
|---|---|
| Menu | Layout at small/large window sizes, fullscreen, settings, credits, volume sliders actually change volume |
| Lobby | Stage spacing, character pick (taken characters disabled?), add/remove bots, map switch, Start, back to menu |
| Wardrobe | Every tab (Hat/Face/Cape/Boots), the live preview updates, choices saved after a page refresh |
| Loading | Art, tips, no frozen frames |
| Arena HUD | Timer, plan slots, action bar icons and tooltips, HP/Grit/musket icons over units, crown, scoreboard strip |
| Round scoreboard | Points and reasons correct, auto-advance timing (10 s) |
| Results | Winner pose, portraits, awards, "Play again", "Back to lobby" |

## Pass 3: matches (30–40 min)
Play at least:
- [ ] 2-player on Courtyard as Matej
- [ ] 4-player on Bridge as Aljaž
- [ ] 3-player on any map as Matic or Nik

Use **every action and every signature at least once**. During a match, press **L** for the turn log and change **Speed** to check things.

For each action, note whether the **visual is clear** (do you instantly understand what happened?) and whether it **feels good** (punchy, readable):

| Action | Clear? | Feels good? | Notes |
|---|---|---|---|
| Move / Dash / pit leap | | | |
| Fire / Reload | | | |
| Parry / riposte | | | |
| Shove / Guard Break | | | |
| Keg (throw, fuse, explosion, chain) | | | |
| Catch Breath | | | |
| Grappin | | | |
| Switcheroo | | | |
| Double Charge | | | |
| Bull Rush | | | |
| Bonk / wall slam / falls | | | |
| Crumbling arena | | | |
| Hurt / death / victory poses | | | |
| Emotes Z/X/C/V | | | |

## Pass 4: gameplay feel (answer honestly, a few words each)
1. Arena size: too small, right, or too big? (Try to say why: "always in someone's line of fire from turn 1".)
2. Round length: too short, right, or too long? Typical number of turns?
3. Did turn-1 sniping happen? Should everyone start **unloaded**?
4. Is 25 s of planning too much or too little?
5. Is playback speed easy to follow at 1×? Should the default be slower?
6. Which moment was the most fun? Which felt random or unfair?
7. Any action you never wanted to use? Any that felt too strong?
8. Would you want to immediately play another match? If not, why?

## Pass 5: audio (with headphones)
- [ ] Overall balance: music vs SFX vs voices
- [ ] Any sound too loud, harsh, or repeated too often (the hurt voice on every hit?)
- [ ] Announcer timing: does the line come at the right moment, and are there too many lines?
- [ ] Music: loop points clean? The battle_final switch noticeable and good?
- [ ] Anything missing a sound, or a sound that doesn't fit?

## Pass 6: Wardrobe Preview in Unity (you can fix these yourself)
In the Unity menu bar: **Musketeers → Wardrobe Preview**. For each item, check all 4 characters, including the **hurt and victory poses**:
- [ ] Hats sit on the head (not floating, not sunk into it)
- [ ] Face items line up (your face sheet is front-on while the bodies are side-on, so some may not work at all; list those)
- [ ] Capes are the right length and stay behind the body
- [ ] Boots cover the feet (they're hidden during the lunge, which is a known limit)

Nudge offsets right there. Your changes survive re-imports. Note any item that can't be fixed by nudging and needs regenerating.

## Pass 7: browser checks
- [ ] Chrome **and** Safari
- [ ] Resize the window and go fullscreen: does the layout hold?
- [ ] Switch tabs for 10 s and come back: does the game recover?

---

## Issues
| # | Sev | Screen / area | What happened | What I expected | Screenshot |
|---|---|---|---|---|---|
| 1 | | | | | |
| 2 | | | | | |
| 3 | | | | | |

## Top 3 things that would make it more fun
1.
2.
3.
