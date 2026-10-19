# Art and Audio

Replaced by **[ART_GUIDE.md](ART_GUIDE.md)** (specs, naming, asset list, audio and fonts).

How the files get into the game: source files live in the root `Art/` and `Audio/` folders and are never edited in place. `Tools/Musketeers/Import Art` (in Unity) cleans, trims, scales and slices them into `Assets/_Project/Art` and `Assets/_Project/Audio`, then wires them into the data assets. Re-run it whenever you add or change a source file.
