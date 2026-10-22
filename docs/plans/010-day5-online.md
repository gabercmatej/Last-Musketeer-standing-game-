# 010 — Day 5: online play (v0.3.0)

Goal: Host/Join in the browser over Relay/WSS, lockstep of plans per TECH_DESIGN §2/§5/§7.
Offline vs bots must behave exactly as now (LocalTransport path untouched).

Reuse: `Assets/_Sandbox/Scripts/NetSmokeServices.cs` (init/auth/create/join, verified on 2.4.1),
`NetSmokeAutoTest.cs` (flag-file MPPM automation pattern). Packages: NGO 2.13.3,
Multiplayer Services 2.4.1, MPPM 3.0.0. Check APIs against these versions.

## Architecture (both chunks follow this)
- New asmdef `Musketeers.Net` in `Assets/_Project/Scripts/Net` (refs Sim, Game, Data, NGO, Services).
  `Musketeers.UI` references `Musketeers.Net`. Sim stays untouched except a snapshot (de)serializer.
- **Pure, testable logic is separated from RPC plumbing:**
  - `LobbyRules` (pure C#): slot add/remove, unique character lock, ready, bots, settings clamp
    (points 6–30, hearts 1–5, map id valid), name sanitize (UTF-8, ≤20 chars, ≤63 bytes).
  - `NetMatchHostLogic` / client logic driven through an `INetLink` interface (send-to-host /
    send-to-all callbacks), so EditMode tests can run host + 2 clients in-process with a fake link.
  - `NetTransport : IMatchTransport` wraps that logic; `MatchNet` (one NetworkBehaviour) only
    serializes and forwards RPCs. `LobbyNet` (one NetworkBehaviour) holds `NetworkList<LobbySlot>`
    + `NetworkVariable<LobbySettings>`.
- `NetSession` (MonoBehaviour service): init services, random auth profile per page load (Web),
  per-MPPM-player profile in Editor, `UseWebSockets = RelayProtocol.Default == WSS`,
  `DisconnectTimeoutMS = 60000`, create/join/leave, events for kicked/host-left/disconnected.
- NetworkManager + UnityTransport + LobbyNet/MatchNet objects are created by `MainSceneBuilder`
  (idempotent), not hand-edited in the scene.
- Version = `Application.version`, sent in `JoinInfoRpc`; mismatch → host kicks with reason.

## Chunk A — session + lobby (Sonnet subagent A)
- [x] A0 plan written
- [x] A1 `Musketeers.Net` asmdef, `NetSession` (from NetSmokeServices), scene builder adds NetworkManager etc.
- [x] A2 `LobbySlot` (INetworkSerializable, IEquatable): clientId, FixedString64Bytes name, characterId,
      ready, isBot, hat/face/cape ids. `LobbySettings`: mapId, points, hearts.
- [x] A3 `LobbyRules` + `LobbyNet` RPCs: JoinInfo(name, version), PickCharacter, SetReady, SetCosmetics;
      host-only SetSettings / AddBot / RemoveBot / Default. Unique musketeer enforced on host.
      Max 4 (bots count). Join while match running → kick "Match in progress, try again after".
- [x] A4 Menu: enable Host and Join. Join dialog with code field, prefilled from `?join=CODE`
      (`Application.absoluteURL`). Joiner flow: name → Pick your Musketeer overlay (lobby dimmed
      behind, taken = previewable, "Taken by <name>") → lobby.
- [x] A5 Lobby UI online mode: LobbyModel fed from LobbyNet; non-hosts see settings read-only,
      per-map background + music switch everywhere; Ready/Not ready; host Start with
      "Not everyone is ready. Start anyway?" confirm.
- [x] A6 `Assets/Plugins/WebGL/Clipboard.jslib` + Copy code / Copy invite link
      (absoluteURL without query + `?join=CODE`).
- [x] A7 Leaving: client leave frees slot; host leave/disconnect → everyone back to menu with
      "The host left. The match has ended." Kick messages shown in a dialog.
- [x] A8 EditMode tests: slot rules, unique character lock, settings validation, name sanitize (č/š/ž/ć/đ).

## Chunk B — match + chat + resync (Sonnet subagent B)
- [x] B1 `MatchStartRpc(seed, map, settings, roster incl. bots + cosmetics)` → every machine builds the
      same `MatchSetup` (LocalSlot differs) and starts `MatchController` with `NetTransport`.
- [x] B2 `NetTransport`: BeginPlanning(turn, duration), SubmitPlan on every change, LockStatus,
      host 30 s timer (+0.5 s grace), host plans bots (same BotPlanner seeding as LocalTransport),
      ResolveTurn(all plans), TurnDone(hash). Next turn begins when all connected humans reported
      TurnDone or 10 s passed.
- [x] B3 Hash mismatch → host sends full MatchState snapshot to that client (chunked if > RPC limit),
      client replaces state and continues; log "DESYNC turn N slot S" on host. Sim gets
      `MatchStateSerializer` (deterministic binary, round-trip test).
- [x] B4 Disconnected human: host submits `TurnPlan.AllCatchBreath(slot)` each turn; unit stays killable.
- [x] B5 Chat + emotes over MatchNet (and lobby): ≤80 chars, ~1 msg/s rate limit (host enforces), bubble
      + sound on every machine.
- [x] B6 Results: Return to lobby (host) → everyone back to the same LobbyNet with same players;
      Quit → leave session. Non-host "Return to lobby" waits for host.
- [x] B7 Autotest hook (Editor MPPM via flag file like NetSmokeAutoTest; Web via `?autotest=1`):
      auto name/pick/ready, host auto-starts when 3 humans ready, humans auto-play (AutoPlayHumans),
      logs `AUTOTEST match over hash=<x>` per machine; then returns to lobby and plays a 2nd match.
- [x] B8 EditMode tests: in-process host + 2 clients over a fake link play a full match on both maps
      with identical final hashes; forced desync → snapshot resync → hashes equal again;
      disconnect → Catch Breath plans; chat rate limit / length.

## Finish (Haiku subagents)
- [x] F1 EditMode tests all green (539/539), 0 console errors (2026-10-22)
- [ ] F2 MPPM autotest: host + 2 virtual players, both maps, return to lobby, 2nd match, one leaves mid-match — NOT RUN: MPPM 3.0 has no public API to activate virtual players; needs manual activation
- [ ] F3 Web build, 3 local tabs via invite link (autotest) — FAILED on first build (in-scene Net object not spawnable on clients; fixed via NetSystems prefab, a70f408). Re-test pending
- [ ] F4 Version 0.3.0, DECISIONS.md lines, deploy, commit, push — version is 0.3.0; NOT deployed

## Non-goals
- Reconnecting a dropped player into a running match; host migration; spectators beyond 4.
- Public session browser / matchmaking. Desktop host fallback.
- Changing any game rule, balance, or fit data (Data/Fits, fit assets, FitDefaults.json are PROTECTED;
  never run "Import Art (reset)").

## Gates
- Offline vs bots unchanged (existing tests green; LocalTransport not modified).
- Sim stays pure/deterministic (no floats, no Unity refs).
- Compiles with 0 console errors after each chunk; all EditMode tests pass.
- Opus reviews the networking diff after A and after B.
