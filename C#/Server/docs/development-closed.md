# Development Closed Snapshot

Date: 2026-07-20

This document records the feature work that is closed enough to treat as the current Project Dawn portfolio baseline. Items here are not necessarily production-final, but they have a working implementation, validation path, and milestone notes.

## Closed Backend / Server Work

- UDP Movement Ingress v1
  - Normal Unity gameplay sends Proto `C_UdpMove(PositionInfo, sequence)` after authenticated UDP hello registration.
  - Server binds the UDP endpoint to the TCP-authenticated `ClientSession` and validates sequence, rate, speed, room bounds, and endpoint ownership.
  - Valid UDP ingress reuses `GameRoom.HandleMove` and reliable TCP `S_Move` replication.
  - TCP `C_Move` remains an explicit `UseUdpMovement=false` compatibility mode; UDP failure does not trigger automatic TCP resend.

- Combat Authority v2
  - Server validates basic attack cast timing, cooldown, action lock, duplicate hit, range, facing, and line/depth.
  - Combat reject reasons are logged and counted in memory.
  - Protocol unchanged.

- MatchHistory Persistence
  - Match and party member records persist to DB through a bounded async writer.
  - Transfer started and dungeon entered timestamps are captured.
  - DB write failure is warning-only and does not block matching/transfer flow.

- Monitoring API Polish
  - Local read-only Monitoring API exposes health, server status, rooms, players, matching queue, and recent events.
  - Bounded event buffer prevents unbounded memory growth.
  - WebLauncher observes rooms, queue, events, and reject reason counts.

- Town Scalability v1
  - Town Channel v1 automatically distributes players across channel rooms.
  - Town AOI v1 reduces spawn/move/despawn fanout by distance.
  - MyRoom players use `IsInPublicTownArea=false` and are excluded from Town AOI until a validated Seria portal transition enters the public Lobby.
  - Server slow-update logs exposed the original 200-player single-room pressure and guided the channel/AOI work.

- Tilemap Movement Bounds v1
  - Unity MovementBounds exporter writes authored walkable spans into `Assets/Resources/Data/MovementBounds.json`.
  - Server loads MovementBounds and uses nearest-span clamp for Town movement after players are inside public lobby bounds.
  - DummyClient loads the same MovementBounds file and clamps patrol/follow/strafe movement inside exported spans.
  - DummyClient reverses patrol direction after bounds clamp so load-test characters do not keep staring into a wall.
  - Town fallback bounds match exported lobby range: `x=18..33`, `y=-4..0`.

## Closed Unity / Client Work

- Loading / Scene Transition UX
  - Matching transfer shows loading flow before Bakal entry.
  - Loading text communicates that matching completed and the dungeon transition is in progress.
  - Input is locked during loading.

- Channel UX / Manual Movement v2
  - ESC opens the channel overlay.
  - Channel list uses Monitoring API data and handles refresh without duplicating rows.
  - Manual channel movement uses the approved C_ChannelMove/S_ChannelMove protocol.
  - Server validates target channel, capacity, stale-room state, and preserves lobby position on success.
  - Same-scene channel re-entry preserves MyRoom/Lobby state and resets stale movement.

- Town MyRoom -> Lobby Flow
  - Server assigns the local player authoritative MyRoom position `(0,0)` / `Public=false` before Town entry.
  - Other private players are not sent through initial spawn or movement AOI; Unity hiding is retained only as a defensive fallback.
  - The Seria portal sends a UDP teleport to a walkable public Town position, accepted once without normal speed-distance rejection.
  - Lobby is the public Town area for other players, dummies, matching, and demo observation.

- Client Visibility / LOD v1
  - Remote players are locally culled or simplified by distance.
  - User validated 50/100-client Town tests without visible FPS regression after AOI/LOD work.

- Local Town Movement Bounds
  - Unity loads `Resources/Data/MovementBounds.json` at runtime.
  - MyPlayer walk/run/jump movement is clamped locally while in public Town Lobby.
  - MyRoom is exempt because it is currently outside the public Town tilemap bounds.

## Closed Monster / Dungeon Work

- Bakal Bounded Combat v1
  - Bakal patrol/meteor flow remains available and uses exported MovementBounds where present.
  - Direct chase targeting was intentionally removed because it looked visually awkward with the current coordinate/anchor setup.
  - Bakal root position is separated from the Base/Shadow combat anchor; the final world offset is `(+/-1.0,-1.1280002)` from prefab local `(1.25,-1.4100002)` at scale `0.8`.
  - Patrol diagnostics expose both root and combat-anchor target coordinates.
  - General player/enemy collision and HP decrease path has been verified.
  - Melee/boss attack authority remains a future authority milestone, not closed as production logic.

- Dummy Dungeon Bounds
  - DummyClient dungeon movement can use exported Bakal tilemap bounds instead of only CLI rectangles.
  - This keeps dungeon patrol tests inside authored movement areas.

- Dungeon Clear MyRoom Return
  - `StartReturnToTownTransfer` reuses the pending transfer / `C_SceneReady` path.
  - Town pending entry applies `ApplyMyRoomSpawn` before `EnterRoom`, resetting every member to `(0,0)` / `Public=false`.
  - Returned party members cannot appear at stale Bakal/Lobby coordinates inside another member's MyRoom.

## Known Deferred Work

- Per-area Named Bounds v2
  - Current MovementBounds is room-level.
  - Future data should distinguish MyRoom, TownLobby, Bakal patrol, combat lanes, boss skill zones, and spawn zones.

- Monster Attack Authority
  - Server-side enemy attack windows, player damage validation, and client animation sync are still future work.

- AOI Spatial Hash / Grid
  - Current AOI is sufficient for validated 50/100 Town cases, but larger channel sizes should move from O(N) checks to grid/spatial hash lookup.

- ASP.NET Core Web API
  - Monitoring remains local read-only inside GameServer. A separate portfolio-grade API remains a candidate milestone.

- Redis MatchQueueStore
  - Still intentionally deferred. Redis must not store live sessions, rooms, movement, or combat state.

## Validation Commands

Server build:

```powershell
dotnet build Server\Server\Server.csproj
```

DummyClient build:

```powershell
dotnet build Server\DummyClient\DummyClient.csproj
```

Town tilemap load check:

```powershell
dotnet run --project "E:\task\Server\Server\DummyClient\DummyClient.csproj" -- --scenario town-load --clients 20 --host 127.0.0.1 --port 8080 --prefix PD_TownDummy --delayMs 50 --holdInTownSec 60 --enableMovement true --movementPattern patrol --patrolMode mixed --movementRadius 20 --movementSpeed 2 --movementIntervalMs 300 --verbose true
```

## Portfolio Story

The strongest current technical story is:

1. A naive MORPG-style Town load caused Unity FPS collapse and server slow updates around 200 visible clients.
2. The project added Channel v1 to split room pressure.
3. AOI v1 reduced network fanout inside each channel.
4. Client LOD reduced Unity rendering/animation cost for remote players.
5. Tilemap Movement Bounds made spawn and movement respect authored map space instead of arbitrary rectangles.
6. Unity channel/loading UX made the result presentable as a live demo.
7. Monitoring API/WebLauncher made these behaviors observable during demo and regression runs.
## Final Demo Readiness - 2026-07-09

Status: portfolio-demo ready.

Closed for demo:
- Combat Authority v2
- MatchHistory Persistence
- Monitoring API/WebLauncher
- Town Channel + AOI + Client LOD
- Unity MyRoom -> Lobby flow
- Authoritative MyRoom spawn/isolation and dungeon-clear MyRoom return
- Proto UDP movement with sequence/rate/speed/bounds validation
- Manual Town channel selection and same-position re-entry
- Bakal loading screen / input lock / C_SceneReady transfer flow
- Tilemap MovementBounds shared by Unity, server, and DummyClient
- Bakal patrol/meteor bounded simulation
- Bakal root/combat-anchor alignment

Use `docs/portfolio-scenarios.md` as the primary source for the final presentation story and live demo order.

