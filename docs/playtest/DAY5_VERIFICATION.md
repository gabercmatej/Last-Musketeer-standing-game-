# Day 5 online verification — 2026-10-23

Runtime: Unity 6000.6.6f1, v0.3.0, existing Web output built 2026-10-22 12:05 after the NetSystems prefab fix (`a70f408`). No Scripts/UI/ProjectSettings inputs were newer than the output. No gameplay or cosmetic data changed in this verification session.

## Fresh automated checks

- Unity MCP EditMode job `27bd845bd15845958bfaa34067cea1a4`: **539 passed, 0 failed, 0 skipped**, 38.98 seconds. Includes determinism/fuzz and networking logic tests. Balance category was not requested.
- Pre-MPPM Unity console query: **0 error entries**, with no compile errors. MPPM runtime and intentional-stop diagnostics are recorded below; this is not a claim that the final console was empty.

## Real browser/Relay run

Local server `http://localhost:8765`, three separate visible Chrome windows, one player per window. Host `?autotest=1`; guests `?autotest=1&join=CODE` and `?autotest=leave&join=CODE`. Runtime logged secure WebSocket Relay connection.

| Check | Evidence |
|---|---|
| Host and two real clients join | All three entered Courtyard; no missing NetworkPrefab errors |
| Full Courtyard match | All three final hashes `b4b6dba0` at 14:23:28 UTC |
| Return to lobby and change map | Automatically started a second match on Bridge |
| Guest leaves during second match | Slot 2 logged `AUTOTEST leaving mid-match (slot 2)` at 14:23:42 UTC |
| Remaining players finish Bridge | Host and remaining guest hash `c55facd7`, both `AUTOTEST done` at 14:25:08 UTC |
| Desync/connection health | Zero error or DESYNC logs on all three; connection survived the complete sequence |
| Return after second match | Host click returned both remaining players to lobby; departed slot purged, two humans shown |
| Copy code | Clipboard exactly matched lobby code |
| Copy invite link | Clipboard contained `http://localhost:8765/?join=CODE`, dropping autotest query |

Complete transient console capture and results screenshot are in `.superpowers/sdd/010-day5-online/` (gitignored).

## Remaining checks

- **MPPM passed one full Courtyard match:** host + Player 2 + Player 3 all logged final hash `f8087ca3`. Matej disabled Player 4 for this exact three-player run; this does not change the four-player game limit.
- MPPM virtual editors spammed URP package errors (`Host type is not matching any asset type ...FallbackError.shader`), generating roughly 1 GB of logs across the two clones. Play was stopped after the first completed match, before the extended map/rematch/leave sequence. That extended sequence passed in browsers. The main console logged one Wire WebSocket error during intentional Play-mode shutdown; no game desync was logged. Package diagnostics were left visible rather than suppressed.
- Play mode stopped and autotest flag/code files removed. Player 4 remains unchecked; it can be re-enabled manually.
- **Netlify deployment passed:** `tools/deploy-web.sh` exited 0; deploy `6adb6f4c413752df7fe9f565` at https://last-musketeer.netlify.app. Production v0.3.0 loaded to the menu in Chrome, zero browser errors.
- Match chat was not visually confirmed by this browser run; chat length/rate and forwarding logic are covered by EditMode tests.
- A friend on another network, other browser/OS combinations, and the 20-second hidden-tab recovery check remain manual acceptance checks. These are not implied by the local browser run.
