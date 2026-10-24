# Day 6 release verification

Candidate v0.4.1, code commit `41c24c2` (following integration `e17911b` and sim/map milestone `dc59052`). Production remains v0.3.0 until the final smoke checks pass.

- Unity MCP: 589 EditMode tests passed, 0 failed/skipped, 16.38 seconds. Job `7c257334d9634f9c9991e781627c06bb`.
- Unity Console: zero errors after compile and successful Web build (131 seconds).
- Play Mode: compact opponent panel, local-only map preview, inset replay rendering and badge aspect observed. Autoplay switched off afterward.
- Chrome: three separate visible windows on this machine, localhost:8765, real Unity Relay/WSS. This is **not** two-network testing.
- Initial guest Relay joins emitted “not connected” errors at 22:22:05 UTC; existing join retry recovered without code changes. All three clients then entered the same match.
- Courtyard completed on all three clients with identical hash `d63550ea` at 22:25:55 UTC. Evidence persisted in `day6/v0.4.1-match-checkpoints.json`.
- Guest clicked Skip while montage was at clip1/2: both guest windows displayed the same round3 scoreboard afterward, confirming global skip/cleanup. Chrome showed stationary backdrop, framed inset, original-aspect badge and centered Skip artwork.
- Compact portrait/action/direction rows observed throughout execution, absent during planning; opponents' map-wide indicators removed. Match chat/emotes present; lobby chat absent.
- Host returned independently at 22:25:58 UTC; first guest at 22:26:03 UTC; remaining guest returned and Bridge started in the same session. Readiness/start gate held until returns.
- Pending in this checkpoint: complete Bridge, click final See Results while another client remains in replay, persist final hashes, deploy and production load check.

Earlier v0.4.0 candidate completed Courtyard with `d5d88967` on three clients, then began Bridge; tabs closed after interruption, so its second completion was not recovered. This evidence does not replace v0.4.1 verification.

Shared history is explicitly browser-local; secure cross-device persistence is deferred to v1.1. Autotest matches do not enter history. Subjective badge/victory audio timing and another-network friend play remain human acceptance checks.

Retry required: at22:30:04 UTC all three clients reported Relay inactivity/allocation-invalid and returned safely to menu during Bridge. No second-match completion is claimed for this attempt. The host window had entered fullscreen; fullscreen/occlusion or shared machine/network stalling are hypotheses, not an established root cause. All documents reported visible after the failure. Logs saved in `day6/v0.4.1-interrupted-bridge-logs.json`. Retry uses three fully tiled smaller windows and the normal6-point/1-heart lobby controls to reduce test duration and rendering load. No networking code changed based on this failure.
