# Room Transfer Flow

This document summarizes the current RoomTransfer and MatchManager flow for moving a matched 4-player party from Town to the Bakal/Dungeon scene and returning the cleared party to private MyRoom state.

## Purpose

The goal is to move a matched party from `TownRoom` to a dungeon room safely, then return it to Town without leaking dungeon/public coordinates into another player's private MyRoom.

The current implementation intentionally avoids:

- Redis
- per-player loading progress synchronization beyond the existing loading overlay
- RoomScheduler or worker pool changes
- DummyClient simulation

## RoomTransfer v1

RoomTransfer v1 was a direct 1-player test flow.

Flow:

```text
Town portal
 -> C_CreateRoom
 -> RoomTransferService starts immediately
 -> target Bakal room created
 -> source TownRoom Leave
 -> S_SceneMove
 -> Unity BakalScene LoadSceneAsync
 -> C_EnterGame
 -> pending target room EnterRoom
 -> S_EnterGame / S_Spawn
 -> UDP movement sync in dungeon
```

This verified that the server can create a target room, leave the source room, wait for Unity scene load completion through the existing `C_EnterGame` callback, and then enter the pending target room.

Limitation:

- Existing PartyPopUp UI was skipped because `C_CreateRoom` was reused as an immediate dungeon-entry request.

## RoomTransfer v1.5

RoomTransfer v1.5 restores the Party UI / Start button flow while keeping the v1 transfer mechanics.

Flow:

```text
Town portal
 -> C_CreateRoom
 -> server creates solo party entry state
 -> S_CreateRoom
 -> Unity PartyPopUp UI
 -> Start button
 -> C_SceneMove
 -> RoomTransferService starts
 -> target Bakal room created
 -> source TownRoom Leave
 -> S_SceneMove with target room / transfer info
 -> Unity BakalScene LoadSceneAsync
 -> C_SceneReady
 -> pending target room EnterRoom
 -> S_EnterGame / S_Spawn
 -> UDP movement sync in dungeon
```

v1.5 roles:

- `C_CreateRoom`: creates a solo party or party entry state.
- `S_CreateRoom`: opens the existing Unity PartyPopUp UI.
- `C_SceneMove`: sent by the PartyPopUp Start button and starts `RoomTransferService`.
- `S_SceneMove`: tells Unity which scene to load and carries target room / transfer data.
- `C_SceneReady`: sent by Unity after scene load completes.
- `C_EnterGame`: kept only as a temporary pending-transfer fallback.
- `RoomTransferService`: owns source leave, pending room tracking, and target room enter.

## MatchManager v1.7

MatchManager changes the normal party entry flow from leader-click Start to automatic 4-player matching entry. The current queue implementation is behind MatchTicket, IMatchQueueStore, and InMemoryMatchQueueStore.

Current automatic flow:

```text
Client A/B/C/D login
 -> each client enters Town
 -> each client enters portal or sends C_CreateRoom
 -> MatchManager enqueues each ClientSession in the in-memory queue
 -> S_CreateRoom opens PartyPopUp for each requester
 -> PartyPopUp button text is Matching...
 -> PartyPopUp button is disabled
 -> four valid sessions create one MatchParty
 -> S_EnterParty.PartyMembers = [A, B, C, D]
 -> MatchManager automatically calls RoomTransferService.StartPartyDungeonTransfer(members, RoomType.Bakal)
 -> S_SceneMove with target room / transfer info
 -> Unity BakalScene LoadSceneAsync
 -> C_SceneReady
 -> server validates targetRoomId / transferId / targetRoomType / sceneType
 -> all four players enter the same Bakal DungeonRoom
 -> S_EnterGame / S_Spawn
 -> UDP movement sync in dungeon
```

Current roles:

- `C_CreateRoom`: requests matchmaking entry.
- `S_CreateRoom`: opens PartyPopUp while the player is waiting.
- PartyPopUp Start/Ready UI: replaced in normal flow by disabled `Matching...` state.
- `S_EnterParty`: sends the matched party member list to all four clients before scene transfer.
- `MatchManager.AutoStartPartyTransfer(...)`: validates the matched party and starts transfer outside the `MatchManager` lock.
- `RoomTransferService.StartPartyDungeonTransfer(...)`: creates the target room, assigns pending transfer state, and sends `S_SceneMove`.
- `C_SceneMove`: still exists as a debug fallback path and should not be sent in the normal automatic matching UI flow.

## Ready Handling

`C_SceneReady` is now the normal scene-load-complete signal for RoomTransfer.

Current ready flow:

```text
S_SceneMove received by Unity
 -> BakalScene LoadSceneAsync
 -> load callback sends C_SceneReady
 -> server validates targetRoomId / transferId / targetRoomType / sceneType
 -> target room EnterRoom
```

`S_SceneMove` now carries:

- `targetRoomType`
- `targetRoomId`
- `transferId`
- `sceneType`

`C_EnterGame` fallback remains in server code for stabilization, but normal RoomTransfer tests should not hit the fallback log.

Fallback log to watch for:

```text
[TRANSFER] Fallback C_EnterGame used as SceneReady...
```

## Current Validation

Verified:

- Town portal sends `C_CreateRoom`.
- Server logs `[MATCH] Waiting for party member` while fewer than four valid players are queued.
- Server logs `[MATCH] Party matched` when four players join the in-memory queue.
- Server logs `[MATCH] Auto start party transfer` immediately after party creation.
- PartyPopUp UI appears.
- PartyPopUp button text is `Matching...`.
- PartyPopUp button is disabled.
- PartyPopUp UI displays the matched members.
- UDP movement still works while waiting in Party UI.
- Normal UI flow does not send `C_SceneMove`.
- The old `C_SceneMove` path remains as a debug fallback, including non-leader rejection.
- MatchManager automatically begins party RoomTransfer.
- Server logs `[TRANSFER] Party transfer start`.
- Source TownRoom leave runs before `S_SceneMove`.
- Unity loads BakalScene.
- Unity sends `C_SceneReady` after scene load.
- Server validates `targetRoomId`, `transferId`, `targetRoomType`, and `sceneType`.
- Server enters the pending target room.
- Existing `S_EnterGame / S_Spawn` flow is reused.
- UDP movement sync continues inside the dungeon.
- A previous dungeon UDP issue was caused by legacy `UdpHelloTestClient.OnDestroy()` closing the shared UDP client during scene transition. The helper is now inert and `NetworkManager` owns UDP registration/movement.
- After Bakal clear, `S_DungeonClear` announces the delayed Town return.
- `StartReturnToTownTransfer` sends every valid member through the pending-room/`S_SceneMove` flow back to Town.
- `TryEnterPendingRoom` applies `ApplyMyRoomSpawn(..., ReturnToTown)` before Town `EnterRoom`, resetting position to `(0,0)`, state to `Idle/Right`, and `IsInPublicTownArea=false`.
- Town AOI excludes private players, so returned party members do not receive each other's MyRoom `S_Spawn` or `S_Move`.

## Current Multi-Room State

The transfer path now uses explicit target room ids for dungeon entry.

Current state:

- `RoomManager.Add(RoomType)` assigns explicit room metadata.
- Multiple Bakal rooms can exist at the same time.
- RoomTransfer stores `PendingRoomId`, `PendingTransferId`, and pending target type on each session.
- `C_SceneReady` validates the transfer fields before target room entry.
- Normal dungeon entry must look up the target room by `PendingRoomId`, not by room type.
- RoomSnapshot and Monitoring API can show separate Bakal rooms during `fill-dummy` regression tests.

Useful verification docs:

- [DummyClient Regression and Demo Guide](dummyclient-regression.md)
- [Monitoring API](monitoring-api.md)

## Dungeon Clear -> Authoritative MyRoom Return - 2026-07-20

Current return flow:

```text
Bakal HP reaches 0
 -> GameRoom broadcasts S_DungeonClear
 -> five-second delayed StartReturnToTownTransfer
 -> select available Town room
 -> set pending Town room / transfer / SceneTown
 -> leave Bakal room
 -> S_SceneMove
 -> Unity loads Town and sends C_SceneReady
 -> TryEnterPendingRoom
 -> ApplyMyRoomSpawn: Pos=(0,0), Idle, Right, Public=false
 -> Town EnterRoom
 -> S_EnterGame for self
 -> no private-party fanout through Town AOI
```

Why the spawn reset happens before `EnterRoom`:

- `S_EnterGame` and initial spawn lists must be built from the final authoritative Town state.
- Reusing the Bakal root position or a prior public Lobby position would make different clients observe party members at inconsistent Town coordinates.
- Applying MyRoom after spawn would be too late; clients could briefly instantiate a remote object and only correct it after movement.
- Resetting UDP accepted-position state during transfer prevents movement validation from comparing the old room coordinate with the new MyRoom coordinate.

Current channel policy:

- Return currently uses the server's available Town room returned by `RoomManager.Find(RoomType.Town)`.
- Exact original-channel affinity is not stored/restored yet.
- Regardless of which Town room is selected, every returned player starts private and must cross the Seria portal before joining public Town AOI.

Validation:

- Four clients clear Bakal and all receive the Town scene transfer.
- Each server self-spawn is `Pos=(0.00,0.00)` with `Public=False`.
- Initial spawn lists do not contain the other returned private party members.
- No returned client sees another member walking at stale Bakal or Lobby coordinates while still in MyRoom.
- Moving through the Seria portal produces the public transition logs and only then begins normal AOI visibility.

## Current TODO

Logging cleanup:

- `EnterRoom` logs now distinguish Player / Enemy / Object entries.
- UDP move detail logs are hidden behind a debug flag by default.
- Remaining legacy room spawn broadcast debug logs should stay disabled unless debugging spawn fan-out.

Protocol cleanup:

- Remove the temporary `C_EnterGame` pending-transfer fallback after `C_SceneReady` remains stable.
- Consider adding loading/progress UI later, not as part of MatchManager v1.7.

Matchmaking:

- `MatchManager` v1.7 currently uses an in-memory 4-player queue.
- Keep Redis out until the queue responsibility is separated behind an in-memory store abstraction.
- Redis should be considered later for match tickets, queue state, and TTL-backed stale ticket cleanup, not for real-time movement or skill processing.

Party expansion:

- Add disconnect/cancel handling for waiting and matched parties.
- Keep the manual Start path as a debug fallback only.
- Later add ready/progress state for all party members if the UX needs a visible countdown or loading state.

## MatchManager v1.7

MatchManager v1.7 keeps the in-memory 4-player queue and changes the normal entry flow to automatic transfer.

Current behavior:

- Each `C_CreateRoom` request enters the in-memory waiting queue.
- Each requester receives `S_CreateRoom` so the existing PartyPopUp can open while waiting.
- PartyPopUp shows disabled `Matching...` UI instead of Start/Ready.
- When four valid sessions are available, `MatchManager` creates one `MatchParty`.
- The first queued session is the leader.
- `S_EnterParty.PartyMembers` is sent to all members in queue order.
- `AutoStartPartyTransfer(...)` starts transfer after party creation and outside the `MatchManager` lock.
- All four members transfer through the existing `RoomTransferService.StartPartyDungeonTransfer(...)` path.
- Manual `C_SceneMove` Start remains as a debug fallback and still rejects invalid/non-leader requests.

Future Redis extension should replace the queue storage, not the room transfer path.

