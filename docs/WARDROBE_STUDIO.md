# Wardrobe Studio — how to use it (v0.2.1b)

1. Open **Musketeers → Wardrobe Studio**. Pick a musketeer (Matej is the default), a pose tab, and **Rest / Musket aimed / Rapier raised**.
2. Click a thumbnail to wear and select it, or click the item on the canvas. Drag to move, use the **yellow knob** to rotate and the **corners** to scale.
3. **Idle is the master.** Edits on other poses get a *custom* badge with **Reset to Idle**. **Link all poses** applies one edit to every pose.
4. Edits apply to **all four musketeers** through the anchors. Tick **Override for this musketeer** only when one musketeer needs a different fit.
5. **Masks:** select an item → **Add mask**. Click to place points, click-drag for curves, click the first point to close. Alt-drag breaks a handle, Delete removes a point, Enter finishes.
6. **Body masks:** select boots or a weapon → **Body boot mask** or **Hand mask**. **Copy mask from Matej** fills in the other three, then you touch them up.
7. **Copy fit / Paste to…** works for this pose, all poses, or a checklist of other items in the same slot.
8. **Shortcuts:** arrows nudge (Shift ×10), `[ ]` rotate, `- =` scale, Tab next item, PgUp/PgDn pose, Cmd+C/V copy/paste, Cmd+Z undo.
9. Tick **✔** per item and pose as you finish. The progress list on the right counts them.
10. Press the big **SAVE** when the bar turns orange. It writes the assets and FitDefaults.json, so a re-import never undoes your work. Then commit and push.

Notes:
- Musket and rapier: "Reset to Idle" and "Paste to all poses" are off, because each pose has its own hand angle. "Link all poses" still works.
- The demo masks on `hat_helmet_pink` and `boots_brown` are examples. Reshape or delete them.
- Known gaps to fix by hand: the aimed musket in Victory; small boot gaps on Lunge/Victory for a few boots.
- **Never run "Import Art (reset)"** after manual fitting: it restores FitDefaults.json. Plain "Import Art" is safe once you've pressed SAVE.
