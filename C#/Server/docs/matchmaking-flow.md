# Matchmaking Flow

This document records the completed automatic 4-player matchmaking flow, MatchCondition v1, TestEquipmentSetupTool, DummyClient fill-visible demo, and remaining Redis / regression-test design notes.

Redis is still not implemented. DummyClient now supports create-players, fill-visible, and fill-dummy regression scenarios, including optional TCP movement, attack, follow-enemy, collision, and reward observation.

## Completed State

- UDP Movement v1 is complete for client-to-server movement ingress.
- Proto UDP `C_UdpHello`, `S_UdpHello`, and `C_UdpMove(PositionInfo, sequence)` are complete; normal Unity login enables UDP movement.
- Valid UDP movement is adapted into `GameRoom.HandleMove` and relevant `S_Move` replication remains on TCP.
- TCP `C_Move` is an explicit `UseUdpMovement=false` compatibility mode, not an automatic fallback when UDP registration is pending.
- String UDP fallback has been removed.
- Jump coordinate sync and OtherPlayer jump exit fixes are complete.
- RoomTransfer v1 and v1.5 are complete.
- MatchManager v1.6 in-memory 4-player queue is complete.
- MatchManager v1.7 automatic 4-player matching entry is complete.
- MatchManager v1.8 queue store separation is complete: `MatchTicket`, `IMatchQueueStore`, and `InMemoryMatchQueueStore`.
- PartyPopUp button text is `Matching...`.
- PartyPopUp button is disabled during normal matching.
- Four matched players automatically transfer to Bakal through `RoomTransferService.StartPartyDungeonTransfer(...)`.
- `S_EnterParty` is sent before `S_SceneMove`.
- `C_SceneReady` places all four players into the same Bakal DungeonRoom.
- Four-player UDP movement sync in dungeon has been verified.
- Dungeon clear returns all four players through RoomTransfer to authoritative private MyRoom `(0,0)` / `Public=false` before Town spawn fanout.
- The old manual `C_SceneMove` path remains as a debug fallback.
- MatchCondition v1 RequiredEquipment is complete.
- TestEquipmentSetupTool is complete.
- DummyClient create-players, fill-visible, and fill-dummy scenarios are complete.
- Dummy 3 + Unity visible player 1 fill-visible demo is verified.
- Match reject feedback uses `S_CreateRoom.ResponseCode=0` and Unity Toast.

## Current MatchManager v1.8 Flow

```text
Client A/B/C/D login
 -> each client enters Town
 -> each client enters portal or sends C_CreateRoom
 -> MatchManager.RequestMatch
 -> MatchTicket creation
 -> InMemoryMatchQueueStore
 -> S_CreateRoom
 -> PartyPopUp shows Matching...
 -> Start/Ready button is disabled
 -> four valid sessions create one MatchParty
 -> S_EnterParty.PartyMembers = [A, B, C, D]
 -> AutoStartPartyTransfer outside MatchManager lock
 -> RoomTransferService.StartPartyDungeonTransfer(members, RoomType.Bakal)
 -> S_SceneMove
 -> Unity BakalScene LoadSceneAsync
 -> C_SceneReady
 -> server validates PendingRoomId / TransferId / RoomType / SceneType
 -> all four players enter the same Bakal DungeonRoom
 -> dungeon UDP movement sync continues
```

## Current Queue Responsibilities

Current queue-related fields in `MatchManager`:

- `RequiredPartySize`: currently 4.
- `MaxPartySize`: currently 4.
- `_lock`: protects party map and party state.
- `_queueStore`: `IMatchQueueStore`, currently backed by `InMemoryMatchQueueStore`.
- `_partiesByPlayerId`: maps player id to the current `MatchParty`.
- `_partyId`: in-memory party id generator.

Current queue-related methods:

- `RequestMatch(...)`: validates session, creates a `MatchTicket`, prevents duplicate waiting/party entry, enqueues, sends `S_CreateRoom`, attempts to create a party, and triggers auto transfer outside the lock.
- `TryCreateMatchedParty(...)`: dequeues ticket batches, resolves live sessions, skips stale tickets, re-enqueues valid underfilled tickets, and creates a party when four valid sessions are available.
- `TryResolveTicket(...)`: resolves `SessionManager.Instance.Find(ticket.SessionId)` and verifies live session state.
- `CreateTicket(...)`: creates a Redis-ready pure data `MatchTicket`.
- `CreateParty(...)`: builds `MatchParty`, assigns leader/member state, registers `_partiesByPlayerId`, and sends `S_EnterParty`.
- `AutoStartPartyTransfer(...)`: validates a completed party and starts RoomTransfer.
- `RemovePartyMap(...)`: clears `_partiesByPlayerId` after transfer start or invalidation.

## MatchTicket Design

`MatchTicket` is a pure data queue item. It is explicit and serializable enough for future Redis queue storage, and it does not contain a `ClientSession`.

Candidate fields:

- `TicketId`: stable unique id for queue/ticket references.
- `Mode`: queue mode, initially `Bakal` or dungeon mode.
- `PlayerId`: DB/player id.
- `PlayerName`: display name for `S_EnterParty`.
- `SessionId`: current TCP session id for local process lookup.
- `EnqueuedAt`: original queue time for FIFO ordering.
- `ExpiresAt`: stale ticket cleanup target.
- `State`: waiting, matched, cancelled, expired.
- `PartyId`: assigned when matched.
- `ClientBuildId` or version field later if protocol/client compatibility matters.

`MatchTicket` should not own `ClientSession` directly if Redis storage is introduced. The server process should resolve `SessionId` or `PlayerId` back to a live local session before sending packets or starting transfer.

## MatchParty vs MatchTicket

- `MatchTicket`: one player's matchmaking request.
- `MatchParty`: a matched group of tickets/sessions ready for transfer.

`MatchTicket` should represent queue membership and expiry. `MatchParty` should represent the result of matching: ordered members, leader, party id, created time, invalidation state, and transfer-start state.

Redis can store ticket and party membership metadata, but the live `MatchParty` used for `RoomTransferService` still needs live `ClientSession` references in the server process.

## MatchQueueStore Extraction

Current extraction state:

- `_waitingQueue` and `MatchEntry` have been replaced by `IMatchQueueStore` and `MatchTicket`.
- The current store implementation is `InMemoryMatchQueueStore`.
- Keep `MatchManager` responsible for party creation, packet sends, and RoomTransfer start.

Candidate interface shape:

```text
Enqueue(ticket)
ContainsPlayer(playerId)
TryDequeueBatch(requiredCount)
Requeue(tickets)
RemovePlayer(playerId)
ExpireStaleTickets(now)
```

Current implementation:

- `InMemoryMatchQueueStore`
- Keep the same FIFO behavior.
- Keep all packet sends and transfer calls in `MatchManager`.
- Regression test the current 4-player automatic flow before adding Redis.

## Redis Store Direction

Candidate Redis structures:

- Sorted Set: `match:queue:{mode}`
  - member: `ticketId`
  - score: enqueue timestamp
- Hash: `match:ticket:{ticketId}`
  - ticket metadata such as player id, name, session id, state, mode, enqueue time, expiry
- Set: `match:party:{partyId}:members`
  - matched ticket ids or player ids
- TTL:
  - expire stale ticket hashes
  - support cleanup of abandoned waiting tickets

Redis should own:

- Match ticket metadata.
- Queue ordering.
- Stale ticket cleanup.
- Cross-process queue visibility if multiple server instances are introduced later.

Redis should not own:

- Real-time position.
- UDP movement state.
- GameRoom object state.
- In-room enemy/player lifecycle.
- Live `ClientSession` objects.
- Actual `RoomTransferService` execution.

## Dummy Simulator vs TCP DummyClient

Redis dummy simulator:

- Tests queue algorithms without real TCP clients.
- Useful for validating Redis sorted set order, stale ticket cleanup, and batch creation.
- Faster to run and easier to scale.
- Does not prove real login, packet serialization, scene move, or `C_SceneReady` behavior.

TCP DummyClient:

- Connects to the real server TCP port.
- Sends the same packets as Unity for login, enter game, `C_CreateRoom`, and `C_SceneReady`.
- Receives `S_CreateRoom`, `S_EnterParty`, and `S_SceneMove`.
- Exercises real packet framing, `ClientSession`, `MatchManager`, `RoomTransferService`, and Room enter/spawn paths.
- Stronger for portfolio/demo value because it proves the real server flow under simulated client load.

Recommended first DummyClient:

- Minimal console TCP client.
- Unique login identity per instance.
- Optional UDP hello later, but first version can focus on TCP matchmaking and scene-ready.
- Auto behavior:
  1. connect
  2. login
  3. enter Town
  4. send `C_CreateRoom`
  5. wait for `S_EnterParty`
  6. wait for `S_SceneMove`
  7. send `C_SceneReady` with the received room/transfer fields
  8. report room entry/spawn packets

16-client scenario:

```text
Start server
Start 16 TCP dummy clients
Each client logs in with a unique identity
Each client sends C_CreateRoom
Server creates 4 parties of 4
Each party receives S_EnterParty
Each party receives S_SceneMove with its own room/transfer id
Each client sends C_SceneReady
Server enters all 16 clients into 4 Bakal rooms
```

## Redis Before DummyClient

Do not add Redis first unless there is a stable seam around queue storage. Redis should be a storage replacement for ticket/queue operations, not a rewrite of party transfer.

Recommended prerequisites:

- Keep MatchManager v1.7 regression green with four real clients.
- Keep `MatchTicket` and `InMemoryMatchQueueStore` stable under regression tests.
- Confirm automatic transfer still works with the in-memory store.
- Add logs around ticket id, party id, queue mode, and stale cleanup.

## Risky Changes

- Calling RoomTransfer while holding the MatchManager lock.
- Moving `RoomTransferService` responsibility into Redis.
- Storing live `ClientSession` in Redis.
- Replacing GameRoom state with Redis structures.
- Removing the manual `C_SceneMove` fallback before repeated stabilization.
- Changing `Protocol.proto` for queue storage before the current flow is stable.
- Building DummyClient against shortcuts that bypass actual TCP packet framing.
- Adding Redis and DummyClient in the same change.

## Recommended Development Order

1. Regression test MatchManager v1.8 with four real clients.
2. Confirm the external v1.7 automatic matching flow is unchanged after queue store extraction.
3. Add TCP DummyClient.
4. Use 16 dummy clients to verify four automatic parties.
5. Add `RedisMatchQueueStore`.
6. Add Redis dummy simulator.
7. Run Redis-backed 16-client matching tests.

## Do Not Do Yet

- Do not install Redis packages.
- Do not implement `RedisMatchQueueStore`.
- Do not create a DummyClient project.
- Do not modify `Protocol.proto`.
- Do not modify generated `Protocol.cs`.
- Do not modify Unity code.
- Do not modify server RoomTransfer logic.
- Do not modify UDP movement.
- Do not modify `C_SceneReady`.
- Do not work on SkillId 402 or Action Lock in this phase.

## MatchCondition v1 Required Equipment

MatchCondition v1 gates Bakal matchmaking before a ticket enters the queue. The client does not send equipment condition fields. The server builds a `MatchProfile` from the live `ClientSession.MyPlayer.Inven.Items` state.

Required equipment:

- At least one equipped `ItemType.Weapon`.
- At least one equipped `ItemType.Armor`.

Flow:

```text
C_CreateRoom
MatchManager.RequestMatch
MatchProfileProvider.TryBuild(session, Bakal, Bakal)
Check Equipped weapon/armor from server inventory
Reject before enqueue if missing required equipment
Create MatchTicket from MatchProfile if valid
Enqueue by MatchQueueKey
Continue existing S_CreateRoom / S_EnterParty / auto RoomTransfer flow
```

Reject log example:

```text
[MATCH] Request rejected. Reason=MissingRequiredEquipment, PlayerId=..., PlayerName=..., HasWeapon=False, HasArmor=True
```

Ticket log example:

```text
[MATCH] Ticket created. Player=..., PlayerId=..., Level=1, LevelBucket=0, Mmr=1000, MmrBucket=1000, HasWeapon=True, HasArmor=True, QueueKey=Bakal:Bakal:L0:M1000
```

`RequiredEquipment` is not part of the queue key in v1. It is an enqueue validation. `MatchQueueKey` is still introduced so later Level/MMR/GearScore buckets can map naturally to in-memory or Redis queue keys.

`TestEquipmentSetupTool` can prepare equipment before server/client login:

```powershell
dotnet run --project E:\task\Server\Server\TestEquipmentSetupTool\TestEquipmentSetupTool.csproj -- --playerNames Player_0054,Player_0009,Player_0084,Player_0001 --weaponTemplateId 1 --armorTemplateId 100 --equip true --overwriteExistingEquip true
```

No `Protocol.proto`, Unity UI, DummyClient, Redis, RoomTransfer, UDP, or `C_SceneReady` changes are required for MatchCondition v1.

## Verified MatchCondition + DummyClient fill-visible Flow

The current end-to-end demo has been verified with MatchManager v1.8, MatchCondition v1, TestEquipmentSetupTool, DummyClient v1, and Unity nameplates.

Verified setup:

- Dummy filler clients:
  - `PD_Dummy_0001`
  - `PD_Dummy_0002`
  - `PD_Dummy_0003`
- Visible Unity player:
  - `Player_0090`
- Match queue key:
  - `Bakal:Bakal:L0:M1000`
- Required equipment:
  - `HasWeapon=True`
  - `HasArmor=True`

Verified flow:

```text
Start server
Start DummyClient fill-visible with 3 clients
PD_Dummy_0001 enters Town and sends C_CreateRoom
PD_Dummy_0002 enters Town and sends C_CreateRoom
PD_Dummy_0003 enters Town and sends C_CreateRoom
All three dummy tickets pass RequiredEquipment
QueueCount reaches 3/4
Visible Unity client Player_0090 logs in
Player_0090 enters the Bakal portal and sends C_CreateRoom
Player_0090 passes RequiredEquipment
MatchManager finds 4 tickets with QueueKey=Bakal:Bakal:L0:M1000
MatchParty is created with 3 dummy players and 1 visible Unity player
S_EnterParty is sent to all four sessions
AutoStartPartyTransfer starts RoomTransfer outside the MatchManager lock
S_SceneMove is sent to all four sessions
Unity loads BakalScene
DummyClient sends C_SceneReady after S_SceneMove
Visible Unity client sends C_SceneReady after BakalScene load
All four sessions enter the same Bakal DungeonRoom
```

Verified party:

```text
Party matched:
PD_Dummy_0001
PD_Dummy_0002
PD_Dummy_0003
Player_0090

TargetRoomId=3
TransferId=2
RoomType=Bakal
SceneType=SceneBakal
```

Post-transfer dungeon activity was also observed through skill and collision logs, confirming that the flow continued past room entry.

## Unity Nameplate Verification

Unity nameplates have been verified in Town and BakalScene for the fill-visible demo.

Purpose:

- Identify background dummy clients in the visible Unity scene.
- Distinguish dummy users from real Unity users without Protocol changes.

Current display policy:

```text
PD_Dummy_* -> [DUMMY] PD_Dummy_0001
Other names -> [USER] Player_0090
```

The label is based on the player name already carried by `ObjectInfo.Name`; no `Protocol.proto` change is required.

BakalScene rendering note:

- Bakal tilemaps render on Sorting Layer `Tile`.
- Character sprites render on Sorting Layer `Charecter`.
- Runtime World Space Canvas labels need explicit sorting override.

Current working policy:

```text
Canvas RenderMode = WorldSpace
Canvas Override Sorting = true
Sorting Layer = Charecter
Order in Layer = 100
```

If labels appear in Town but not in BakalScene, first check whether the label object exists under the player in the hierarchy. If it exists but is invisible, the likely cause is tilemap/sprite sorting, Canvas scale, or camera visibility rather than matchmaking or spawn packet data.

## Current Known Issues / Notes

- Redis is still not implemented.
- Redis packages are not installed.
- DummyClient does not implement UDP hello or UDP movement; it uses TCP gameplay packets.
- MatchCondition reject feedback currently reuses `S_CreateRoom.ResponseCode=0` and Unity Toast. There is still no dedicated `S_MatchRejected` packet.
- `RequiredEquipment` is an enqueue validation, not a queue-key bucket.
- Nameplate dummy/user detection is prefix-based (`PD_Dummy`) and not a formal protocol field.
- The existing manual `C_SceneMove` path remains as a debug fallback.

## Multi-Room / Monitoring API Status

Current completed regression and visibility work:

- DummyClient `fill-dummy` can create pure dummy parties without Unity.
- `--clients 8 --partySize 4 --expectedRooms 2` verifies two separate Bakal rooms.
- DummyClient observes `S_Move` and reports suspected cross-room movement only when room ids are known and different.
- RoomSnapshot DTOs expose room metadata, players, enemies, boss summary, and update metrics.
- Monitoring API exposes `/api/rooms`, `/api/players/online`, and `/api/matching/queue` for WebLauncher and local checks.

Detailed docs:

- [DummyClient Regression and Demo Guide](dummyclient-regression.md)
- [Monitoring API](monitoring-api.md)

## Web API Integration Candidate

The current in-process Monitoring API already exposes read-only matchmaking and demo visibility without changing the TCP/UDP gameplay path. A future separate ASP.NET Core Web API can add DB-backed player/equipment/history endpoints.

Useful API views for this flow:

- Player equipment inspection for MatchCondition troubleshooting.
- Current or recent match queue snapshot.
- Match reject events such as `MissingRequiredEquipment`.
- Party matched events with members and queue key.
- RoomTransfer events with target room id and transfer id.
- Dungeon run history after persistence is added.

Current API notes: [Monitoring API](monitoring-api.md). Future API roadmap: [Web API Roadmap](web-api-roadmap.md).




