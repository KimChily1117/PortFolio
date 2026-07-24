# Unity Channel UI/UX + Bakal Loading Screen

## Goal

Improve the user-facing transition flow around Town channels, matching, and dungeon scene entry. This milestone prioritizes demo polish over deeper server-scale optimization because Town Channel + AOI + Client LOD are currently sufficient for the local 50-client validation target.

## Background

Initial pre-milestone state:

- Town channels are assigned automatically by the server.
- WebLauncher can be used for channel/room observation.
- Unity client does not yet expose channel status or channel movement UI.
- `S_SceneMove` currently calls `LoadSceneAsync(...)`, then sends `C_SceneReady` after scene load completes.
- There is no user-facing loading screen during match transfer into Bakal.

## Scope

### Task 1. Bakal Scene Loading Screen v1

Add a lightweight Unity loading overlay for scene transfer:

- Show loading screen when `S_SceneMove` is received for Bakal/dungeon transfer.
- Route scene transfer through the existing Unity `LoadingScene` before loading Bakal.
- Use DOTween fade-in/fade-out transition for the loading overlay.
- Lock player input while loading is active.
- Keep existing `LoadSceneAsync -> C_SceneReady` protocol flow.
- Hide loading only after dungeon entry is confirmed and local player spawn is ready.
- If server disconnects while loading, let the disconnect notice replace/cover the loading state.

Do not change Protocol.

### Task 2. Channel Status UI v1

Expose current channel awareness inside Unity without manual switching first:

- Show current Town channel/room id in a small HUD area or simple overlay.
- Show population/congestion if it can be read without exposing live server objects.
- Prefer existing Monitoring API read-only snapshot if used for display.
- Gracefully degrade if Monitoring API is unavailable.

### Task 3. Channel Selection / Movement v2

Original design checklist before implementation:

- Channel list with player counts and full/busy state.
- Channel move button.
- Loading/input lock during channel movement.
- Capacity validation on server.
- Dungeon-clear return policy is now implemented separately: the current server selects an available Town room and re-enters every player as a private MyRoom occupant.

This required the later approved `C_ChannelMove/S_ChannelMove` request flow. The contract and implementation are now complete; the list above records the original design gate.

## Non-Goals

- Do not replace GameServer TCP/UDP flow with HTTP.
- Do not expose `ClientSession`, UDP tokens, or mutable room dictionaries.
- Do not change `Protocol.proto` unless a channel-switch contract is explicitly approved.
- Do not remove `C_SceneReady` or the pending-transfer fallback.
- Do not implement Monster AI in this milestone.

## Acceptance Criteria

- [x] Bakal scene transition shows a loading screen before/while the scene loads.
- [x] Player input is locked during loading.
- [x] `C_SceneReady` is still sent only after Unity scene load completes.
- [x] Loading screen hides after dungeon entry/local player spawn is ready.
- [x] Server disconnect during loading does not leave the client stuck on loading.
- [x] Protocol files remain unchanged for loading flow. Channel movement later changed Protocol after explicit approval.
- [x] Channel UI plan was documented before manual channel movement implementation.

## Validation

Manual Unity validation:

1. Start server.
2. Start Unity client and enter Town.
3. Enter Bakal matching flow.
4. Confirm loading overlay appears during scene transition.
5. Confirm controls do not move/attack during loading.
6. Confirm loading disappears after Bakal scene entry and local player spawn.
7. Stop server during loading and confirm disconnect notice appears instead of a stuck loading screen.

DummyClient regression:

- `fill-dummy --clients 4 --partySize 4` still reaches `EnteredDungeon: 4/4`.
- Existing `C_SceneReady` transfer flow remains stable.

## Follow-Up

After this milestone:

1. Dungeon Dummy Movement Bounds.
2. Monster AI / bounded combat simulation.
3. AOI Spatial Hash / Grid only if larger Town channel sizes become necessary.
## Implementation Notes - 2026-07-08

Implemented Task 1 Bakal Scene Loading Screen v1 in the Unity client path `E:\task\C#\Project_Dawn`:

- `Assets/Scripts/02.UI/SceneLoadingOverlay.cs`
  - Code-created full-screen loading overlay.
  - Uses ScreenSpaceOverlay sorting order `9000` so disconnect notice can cover it at `10000`.
- `Assets/Scripts/00.Manager/InputManager.cs`
  - Added `SetInputLocked(bool)` and `IsInputLocked`.
  - `OnUpdate()` returns while locked.
- `Assets/Scripts/04.Network/Packet/PacketHandler.cs`
  - `S_SceneMoveHandler` shows loading and locks input for Bakal scene moves.
  - Existing `LoadSceneAsync -> C_SceneReady` flow is preserved.
  - `S_EnterGameHandler` hides loading and unlocks input after local player entry.
- `Assets/Scripts/04.Network/ClientDisconnectNotifier.cs`
  - Hides loading and keeps input locked before showing disconnect/quit notice.

Protocol files were not changed.

Validation still required in Unity Editor:

```text
- Trigger Bakal matching from Town.
- Confirm Unity briefly loads `LoadingScene` before `Bakal`.
- Confirm loading appears before/while Bakal scene loads.
- Confirm controls are locked during loading.
- Confirm C_SceneReady still sends after scene load.
- Confirm loading disappears after local player spawns in Bakal.
- Stop server during loading and confirm disconnect notice appears instead of a stuck loading screen.
```
## Channel Status UI v1 Implementation - 2026-07-08

Implemented a read-only Unity channel selection overlay:

- `Assets/Scripts/02.UI/ChannelSelectionOverlay.cs`
  - Opens with `Esc`.
  - Fetches `GET http://127.0.0.1:8090/api/rooms`.
  - Shows Town channel rows with room id and population.
  - Detects current channel by matching `GameManager.MyName` against room player snapshots.
  - Gracefully displays an error row if Monitoring API is unavailable.
  - Channel move buttons are intentionally disabled until the channel-switch contract is designed.
- `Assets/Scripts/03.Player/MyPlayer.cs`
  - `Esc` toggles `ChannelSelectionOverlay` from the existing UI key handling path.

Current v1 scope is observation only. Manual channel movement remains v2 because it needs an approved server/client request contract and capacity validation.

Validation checklist:

```text
- Start server and Monitoring API.
- Enter Town with Unity client.
- Press Esc.
- Confirm channel popup opens.
- Confirm Town channels show RoomId and player count.
- Confirm the current channel row is marked as current when the player appears in /api/rooms.
- Stop Monitoring API/server and press refresh; popup should show a graceful API failure message.
- Confirm move buttons are disabled and do not mutate server state.
```


## Channel Movement v2 Deferred - 2026-07-08

At this 2026-07-08 checkpoint, channel status/list UI was read-only and manual switching was deferred. The later sections in this document record the approved `C_ChannelMove/S_ChannelMove` implementation, capacity validation, and same-scene re-entry behavior.

## Town MyRoom to Lobby Flow - 2026-07-09

Goal: keep the first Town experience private for the local player, then reveal the shared Town/lobby after the player uses the existing Seria portal trigger.

Implementation direction:
- Reuse the existing `TownMapState.SERIAROOM` as the local MyRoom state.
- Reuse the existing `TownMapState.DUNGEONENTRANCE` as the public lobby / dungeon entrance state.
- Keep Protocol unchanged.
- Server calls `TownSpawnService.ApplyMyRoomSpawn` before initial Town `EnterRoom`, setting authoritative position `(0,0)` and `Player.IsInPublicTownArea=false`.
- Town AOI excludes a player pair whenever either observer or subject is private. Remote players are therefore omitted from initial `S_Spawn` and movement fanout instead of merely being spawned and hidden.
- Unity still hides any remote object while `TownMapState.SERIAROOM` as a defensive presentation fallback for stale/already-created objects.
- Existing `SeriaPortal` trigger acts as MyRoom -> Lobby transition. Unity teleports the local player to `LobbySpawn` and sends the new position through UDP.
- The server accepts the first private-to-public teleport only when `x >= 18` and the destination is walkable in the exported Town map, then changes `IsInPublicTownArea` to `true`.
- Existing `BakalPortal` trigger is accepted only after the local player is in the lobby state.

Files:
- Server: `Server/Server/Game/Room/TownSpawnService.cs`
- Server: `Server/Server/Session/ClientSession_Login.cs`
- Server: `Server/Server/Game/Room/RoomTransferService.cs`
- Unity: `Assets/Scripts/01.Scene/TownScene.cs`
- Unity: `Assets/Scripts/00.Manager/ObjectManager.cs`
- Unity: `Assets/Scripts/03.Player/MyPlayer.cs`

Validation checklist:
- Enter Town with one Unity client. It should start in the SeriaRoom/MyRoom area.
- Start `town-load` dummy clients. They should not be visible while the local player remains in MyRoom.
- Move into the Seria portal. The local player should teleport/sync to the lobby and remote players should become visible.
- Bakal portal should be ignored in MyRoom and accepted in lobby.
- Server logs should show `[TOWN_SPAWN] MyRoom spawn assigned... Public=False` on Town entry/return.
- Initial `S_Spawn` in MyRoom should not contain other private players.
- Server should show `[TOWN_FLOW][UDP_PUBLIC_TRANSITION]` followed by `[TOWN_FLOW][SERVER_PUBLIC_APPLIED]` when the portal teleport is accepted.
- Unity logs should show `[CLIENT][TELEPORT_SYNC]` and `[TOWN_FLOW] MyRoom -> Lobby`.

## Channel Movement v2 Implementation - 2026-07-09

Implemented manual Town channel movement after the protocol contract was approved.

Protocol changes:
- `C_CHANNEL_MOVE = 35`
- `S_CHANNEL_MOVE = 36`
- `C_ChannelMove.targetRoomId`
- `S_ChannelMove.success/currentRoomId/targetRoomId/reason`

Server changes:
- `Server/Server/Game/Room/RoomManager.cs`
  - Added `TryReserveTownChannel(...)` so manual moves use the same bounded reservation policy as automatic Town channel assignment.
- `Server/Server/Packet/PacketHandler.cs`
  - Added `C_ChannelMoveHandler`.
  - Allows movement only from Town to another Town room.
  - Rejects invalid, missing, full, non-Town, same-channel, and stale-room requests with structured `S_ChannelMove.reason`.
  - Uses `LeaveRoom(..., sendLeaveToSelf:false)` and then target `EnterRoom(...)`, preserving TCP game-server flow and AOI spawn/despawn behavior.

Unity changes:
- `Assets/Scripts/02.UI/ChannelSelectionOverlay.cs`
  - Existing `Esc` overlay now sends `C_ChannelMove` for available Town channels.
  - Shows `이동`, `이동 중`, `접속 중`, or `가득 참` button state.
  - Displays success/failure status returned by S_ChannelMove.
  - On success, clears previous-channel remote objects before target-channel S_EnterGame/S_Spawn rehydrates the scene.
  - Continues to gracefully handle Monitoring API failure for the channel list.
- `Assets/Scripts/04.Network/Packet/PacketHandler.cs`
  - Added `S_ChannelMoveHandler` and routes the result back to `ChannelSelectionOverlay`.

Generated packet files updated:
- `Server/Server/Packet/Protocol.cs`
- `Server/Server/Packet/ServerPacketManager.cs`
- `E:/task/C#/Project_Dawn/Assets/Scripts/04.Network/Packet/Protocol.cs`
- `E:/task/C#/Project_Dawn/Assets/Scripts/04.Network/Packet/ClientPacketManager.cs`

Validation performed:
- `dotnet build Server/PacketGenerator/PacketGenerator.csproj`: pass.
- `protoc.exe --proto_path=. --csharp_out=. Protocol.proto`: pass, unused timestamp import warning only.
- PacketGenerator run for new packet managers: pass.
- `dotnet build Server/Server/Server.csproj`: pass, existing netcoreapp3.1 EOL warnings only.
- `dotnet build Server/DummyClient/DummyClient.csproj`: pass, existing netcoreapp3.1 EOL warnings only.
- `dotnet build E:/task/C#/Project_Dawn/Assembly-CSharp.csproj`: blocked by pre-existing DOTween firstpass reference errors, not by channel movement code.

Manual Unity validation checklist:
1. Start server and Monitoring API.
2. Create at least two Town channels. Easiest path: run Town load over the 50-player channel cap, then keep clients connected.
3. Enter Town with Unity client.
4. Press `Esc` to open Channel Selection.
5. Confirm current channel row shows `접속 중`.
6. Click `이동` on a different non-full Town channel.
7. Confirm UI shows channel movement status.
8. Confirm server logs show `[TOWN_CHANNEL] Move requested` and `[TOWN_CHANNEL] Move completed`.
9. Confirm Unity receives `S_ChannelMove Success=True`.
10. Confirm local player remains in Town, updates to the target room, and nearby players are re-spawned by AOI.
11. Try moving to the current channel or a full/invalid channel and confirm `S_ChannelMove Success=False` with a reason.

Known follow-up:
- Channel move currently uses immediate same-scene re-entry rather than a loading/input-lock transition. Add a short Town-channel loading overlay if the visual jump feels abrupt in client testing.
- Dungeon-clear return is implemented by `StartReturnToTownTransfer`. The current policy selects the server's available Town room and applies private MyRoom spawn on pending-room entry.


## Channel Movement Hybrid Town Re-entry Policy - 2026-07-09

Problem found after manual validation:
- Manual channel movement changes the server `GameRoom`, but Unity remains in the same `TownScene` instance.
- Because `TownScene.Initialize()` is not called again, scene-level state such as camera limits, BGM, trigger/matching flags, and MyRoom/Lobby placement policy can remain stale.
- `S_EnterGame` rehydrates the local player, but `TownScene.RegisterSpawnedCharacter(..., isMyPlayer:true)` currently treats every local spawn as initial Town entry and relies on `_myPlayerPlacedInMyRoom` to avoid repeated placement. That is not expressive enough for channel re-entry.

Hybrid policy:
- Preserve the local Town area state across manual channel movement.
- If the player moved channel while in `SERIAROOM`, complete re-entry back into MyRoom:
  - camera limit = MyRoom
  - server AOI excludes private remote players; Unity hides any stale remote object defensively
  - MyPlayer teleported/synced to `MyRoomSpawn`
  - Seria room BGM restored
- If the player moved channel while in `DUNGEONENTRANCE`, complete re-entry in Lobby:
  - camera limit = Lobby
  - remote players visible
  - keep the server-provided lobby spawn from `S_EnterGame`
  - Lobby/Bakal-ready BGM restored
- Reset transient Town flow state such as dungeon match requesting during re-entry.
- Do not reload the Town scene for channel movement; this remains a same-scene soft re-entry.

Implementation tasks:
- [x] Add `TownScene.BeginChannelReenter()` called when `S_ChannelMove.Success` arrives.
- [x] Capture the current `TownMapState` before the target channel `S_EnterGame/S_Spawn` is processed.
- [x] Add `CompleteChannelReenter(MyPlayer)` path inside `RegisterSpawnedCharacter` for the next local player spawn.
- [x] Keep MyRoom channel movement private and Lobby channel movement public.
- [x] Reapply camera limit/BGM/remote visibility after re-entry.
- [x] Keep Protocol unchanged beyond the already-approved channel movement contract.

Validation checklist:
1. Enter Town and stay in MyRoom. Move channel from the ESC overlay. The player should remain/return to MyRoom, and no other private players should be spawned or visible.
2. Enter Lobby through SeriaPortal. Move channel from the ESC overlay. The player should remain in Lobby, camera bounds should be Lobby bounds, and remote players should be visible according to AOI.
3. After Lobby channel movement, BakalPortal should still work without requiring a scene reload.
4. After MyRoom channel movement, BakalPortal should still be rejected until entering Lobby.
5. Server logs should still show `[TOWN_CHANNEL] Move requested/completed`.
6. Unity logs should show a channel re-entry start and completion with preserved state.

Implementation completed:
- `Assets/Scripts/01.Scene/TownScene.cs`
  - Added pending channel re-entry state.
  - Added `BeginChannelReenter()` and `CompleteChannelReenter(MyPlayer)`.
  - `RegisterSpawnedCharacter(..., isMyPlayer:true)` now distinguishes initial Town entry from channel re-entry.
- `Assets/Scripts/02.UI/ChannelSelectionOverlay.cs`
  - Calls `TownScene.Current?.BeginChannelReenter()` before removing previous-channel remote objects on `S_ChannelMove.Success`.

Build note:
- Server-side projects are unaffected by this Unity-only polish.
- Unity command-line `dotnet build Assembly-CSharp.csproj` is still blocked by the existing DOTween firstpass metadata/reference issue; validate in Unity Editor.


Channel re-entry movement fix:
- Lobby re-entry now normalizes MyPlayer to Idle at the current server-provided spawn position via TeleportAndSync(myPlayer.transform.position, facing).
- This prevents stale Moving/Right or prior local move state from carrying into the new channel after same-scene re-entry.
## Bakal Scene Transfer Movement Reset - 2026-07-09

Problem found during manual channel/scene validation:
- Town channel re-entry had a stale movement issue because same-scene rehydration could preserve `Moving` state.
- The same symptom could appear when moving into Bakal: the server reset dungeon spawn `PosX/PosY` to `(0,0)` but did not reset `PosInfo.State` or `PosInfo.MoveDir` before `EnterRoom` sent `S_EnterGame`.
- If the player was moving before scene transfer, Bakal could start with stale `Moving` direction and keep walking after scene load.

Implemented server-side fix:
- `Server/Server/Game/Room/RoomTransferService.cs`
  - Non-Town pending room entry now resets `State=Idle`, `MoveDir=Right`, and server facing before target room `EnterRoom`.
  - Added `[TRANSFER] Dungeon spawn reset... State=Idle, MoveDir=Right` log for validation.

Validation checklist:
1. Move continuously in Town, then trigger Bakal matching/transfer.
2. After Bakal `S_EnterGame`, local player should spawn at the dungeon entry position and remain idle.
3. Server log should contain `[TRANSFER] Dungeon spawn reset... State=Idle, MoveDir=Right` before Bakal room `EnterRoom`/spawn logs.
4. Unity should not continue the previous Town movement direction after Bakal loading completes.
## Channel Move Position Preserve Fix - 2026-07-09

Problem found during manual validation:
- Manual Town channel movement called `TownSpawnService.ApplyLobbySpawn(..., "ChannelMove")` on the server.
- That made channel switching behave like a fresh lobby spawn, so the player appeared at a different spawn position instead of staying at the position used before switching channels.

Implemented server-side policy change:
- `Server/Server/Packet/PacketHandler.cs`
  - Channel movement now preserves the player's current `PosInfo.PosX/PosY`.
  - It only normalizes `State=Idle` and `MoveDir` to the last horizontal facing direction before entering the target channel.
  - Added `[TOWN_CHANNEL] Preserved position for channel move...` validation log.

Expected UX:
- Lobby -> channel move -> same lobby coordinate in target channel, idle after re-entry.
- MyRoom -> channel move -> client-side MyRoom re-entry still applies; server AOI keeps private players out of each other's spawn/move stream.
## Completion Status - 2026-07-09

Status: Completed.

Closed scope:
- Bakal loading screen and input lock during scene transfer.
- Disconnect notice compatibility during loading.
- Read-only Town channel status UI via Monitoring API.
- Approved manual Town channel movement protocol and server/client handlers.
- Server-side channel capacity validation and reservation.
- Same-scene Town channel re-entry policy for MyRoom and Lobby.
- Stale movement reset for Town channel re-entry and Bakal scene entry.
- Channel movement now preserves the player's current lobby coordinate instead of assigning a new random spawn.
- Initial Town entry and dungeon-clear return now apply authoritative MyRoom position/state before `EnterRoom`.
- Private MyRoom isolation is enforced by server AOI, with client visibility handling retained only as a defensive fallback.

Remaining follow-up outside this milestone:
- Record and restore the exact pre-dungeon Town channel later if preserving social-channel affinity becomes a product requirement. The current return selects the server's available Town room and always enters private MyRoom.
- Optional short loading/input-lock overlay for channel movement if the same-scene transition still feels abrupt.

## Authoritative MyRoom Return Fix - 2026-07-20

The initial implementation treated MyRoom mostly as a local visibility mode. That allowed remote objects to be created with shared Town coordinates and relied on `SetActive(false)` until the local client reached the lobby. After dungeon clear, public/dungeon coordinates could also be reintroduced during Town re-entry.

The current split is:

```text
Initial login or dungeon return
 -> ApplyMyRoomSpawn: Pos=(0,0), Public=false
 -> Town EnterRoom
 -> private players excluded from Town AOI

Seria portal
 -> Unity TeleportAndSync(LobbySpawn)
 -> UDP C_UdpMove
 -> walkable public-position check
 -> one-time speed bypass
 -> Public=true
 -> normal Town AOI spawn/move/despawn
```

This keeps the Unity camera/BGM/portal state local while making position ownership and social visibility server-authoritative.

