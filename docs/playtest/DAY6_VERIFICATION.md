# Day 6 release verification

Candidate v0.4.2, code commit `935e858` (following v0.4.1 `41c24c2`, integration `e17911b`, sim/map `dc59052`). Production remains v0.3.0 until the final candidate checks pass.

Final checkpoint: 590/590 EditMode tests, 0 failed/skipped (9.16s), job `8040aed1067d4bfd894b6190b2f80e1a`; 6/6 Node service tests; npm audit0 vulnerabilities; zero Unity compile errors. Latest0.4.2 Web rebuild succeeded116s.

- Unity MCP: 589 EditMode tests passed, 0 failed/skipped, 16.38 seconds. Job `7c257334d9634f9c9991e781627c06bb`.
- Unity Console: zero errors after compile and successful Web build (131 seconds).
- Play Mode: compact opponent panel, local-only map preview, inset replay rendering and badge aspect observed. Autoplay switched off afterward.
- Chrome: three separate visible windows on this machine, localhost:8765, real Unity Relay/WSS. This is **not** two-network testing.
- Initial guest Relay joins emitted “not connected” errors at 22:22:05 UTC; existing join retry recovered without code changes. All three clients then entered the same match.
- Courtyard completed on all three clients with identical hash `d63550ea` at 22:25:55 UTC. Evidence persisted in `day6/v0.4.1-match-checkpoints.json`.
- Guest clicked Skip while montage was at clip1/2: both guest windows displayed the same round3 scoreboard afterward, confirming global skip/cleanup. Chrome showed stationary backdrop, framed inset, original-aspect badge and centered Skip artwork.
- Compact portrait/action/direction rows observed throughout execution, absent during planning; opponents' map-wide indicators removed. Match chat/emotes present; lobby chat absent.
- Host returned independently at 22:25:58 UTC; first guest at 22:26:03 UTC; remaining guest returned and Bridge started in the same session. Readiness/start gate held until returns.
- Successful subsequent retry: Courtyard `de8b930c`, Bridge `62ea6cdf` on all3 clients in the same session. Guest See Results opened only that guest’s final Results while another guest remained in the final replay; independent lobby returns and second-match start completed. Final hashes preserved in `day6/latest-match-checkpoints.json`.

Earlier v0.4.0 candidate completed Courtyard with `d5d88967` on three clients, then began Bridge; tabs closed after interruption, so its second completion was not recovered. This evidence does not replace v0.4.1 verification.

Shared history supersedes the earlier local-only deferral: authenticated host registration/submission, server validation and conditional immutable Netlify Blobs writes. Six backend tests and isolated live API smoke pass, including real Unity JWT, anonymous401, foreign-origin403, bot400 and deduplicated completion. Preview test records are isolated per deploy; production store remains untouched. See `../SHARED_HISTORY.md`. Subjective badge/victory audio and another-network friend play remain human acceptance checks.

Retry required: at22:30:04 UTC all three clients reported Relay inactivity/allocation-invalid and returned safely to menu during Bridge. No second-match completion is claimed for this attempt. The host window had entered fullscreen; fullscreen/occlusion or shared machine/network stalling are hypotheses, not an established root cause. All documents reported visible after the failure. Logs saved in `day6/v0.4.1-interrupted-bridge-logs.json`. Retry uses three fully tiled smaller windows and the normal6-point/1-heart lobby controls to reduce test duration and rendering load. No networking code changed based on this failure.

## Final additions

- Spawn markers and illustrated leaderboard inspected in Unity Play Mode. Gold assigned localSlot, other slots regular; markers remain on initial tiles after movement; direct presentation SetTile(Pit)/restore check hides/restores without moving. Four rank banners and correct character portraits render without stretching.
- Chrome hosted0.4.2 candidate checks and final deployment: pending below.

## Morning presentation fix checkpoint

595/595 EditMode tests passed (8.59s, job `04c1d51ce1b445bc96957155736f178b`), zero errors. Added planner icon/direction/label retention through lock/opponent visibility, direct-shot/forced-fall/environmental/old-credit framing checks. PlayMode active audio channels1→0; repeated stop preserved music; reset camera stayed at zero on a later probe. Latest hosted Chrome and rebuild checks pending. The previous0.4.2 qualifying manual match was interrupted before completion.

## Final hosted candidate — passed

- Latest source `fd984f8`, preview deploy `6addae11c28b5f91fdd971ab`, v0.4.2. Full595/595 EditMode again after rig cleanup (8.51s), no skipped/failed; console0 errors, final Web build67s.
- Two visible Chrome windows, actual Relay/WSS, same network: full Bridge (`0b7e544b`) then Courtyard (`52447920`) in lobby `DCPHPD`. Hashes here are host values in the server records; no guest hash logging in manual mode. Both clients showed matching results and no error logs. Earlier three-client hash comparisons remain separate historical evidence.
- Local plan visible locked before opponent; own icons/directions/names survived reveal/execution. Offline immediate submission also kept all3 slots (Keg/Catch Breath/Catch Breath). Screenshots final-host-locked / final-bot-planner-reveal.
- Non-final guest Skip captured with replay then host round scoreboard; next-round arena clean, rings rotated. Final guest See Results captured with guest Results and host still replaying; independent lobby return and natural final completion observed. Self-caused framing inspected; player-shot replay exercised in Bridge; causal bounds regression-tested. Audio channel cancellation/baseline covered by PlayMode probe; no claim of subjective listening.
- Real client registered/submitted two qualifying matches: shared store exactly2 distinct IDs, Matej2 wins. Matching host/guest illustrated standings; expanded statistics/badges; both matches survived browser reload. Completed bot match afterward produced no record and Matic remained0 shared wins. See final-shared-records.json and screenshots.
- Final production URL/version/API check recorded after publish below. Another network/device and subjective audio remain friend acceptance checks.

## Production publication

Published v0.4.2 through tools/deploy-web.sh; deploy `6addb2d00a0f2854f7e1d6e0`, https://last-musketeer.netlify.app. HTTP page version0.4.2 and shared API `Preview:false`,0 matches, four0-win characters verified. No preview/test records imported. Source `fd984f8`; candidate evidence `2d9fae2`. Production Chrome menu/leaderboard check follows.

Production Chrome: menu starts with no client error logs. Netlify public badge initially overlapped the lower-right action area; disabled the official per-site setting (`built_with_badge_enabled:false`), verified injected hud script absent on the next HTTP request. No rebuild required. [Official setting documentation](https://docs.netlify.com/manage/projects/powered-by-netlify-badge/).

Final production Chrome check passed:0.4.2 menu, working shared leaderboard status, all four illustrated rows and0 wins,0 error logs, no badge overlay. Screenshot: day6/production-leaderboard.png. No production match was created during verification.

## v0.4.3 presentation-only polish

597/597 EditMode tests passed,0 failed/skipped (8.64s), job `4c4e0695801e4dccae0564f55873d4ee`. Zero compile errors; Web build82s. No sim/network/ranking/persistence/backend changes.

Actual Chrome Web UI at1280×720 game area plus850×500: ivory Pirata One character names, smaller contained portraits, counts0/7/12/128 centered within fitted laurels, caption alignment, compact2–4-player cards, character-name colors distinct from placement pills, long24-character names without overlap, local timestamps08:28/01:28 across DST, click-to-expand still working. Screenshots ui-polish-*-fixtures.png are visual fixtures, not real standings.

Fixtures supplied through temporary preview-tab GET response interception only; no server writes. Disabled interception, reloaded and fetched actual empty preview history to replace fixture cache, cleared viewport override and closed test tab. Production remained0 records/0 wins. Candidate `6addb6aec562111ed66fd32d`; production publish recorded next.

Published0.4.3 via tools/deploy-web.sh, deploy `6addb8c5bcfc51a56c2e091e`, source `9b0454d`. Canonical HTTP version0.4.3; production API Preview:false, unchanged0 records/all0 wins. Production Chrome new typography, four fitted portraits and centered laurel counters inspected;0 client errors. Screenshot ui-polish-production.png. No further gameplay tests claimed for this presentation-only patch.
