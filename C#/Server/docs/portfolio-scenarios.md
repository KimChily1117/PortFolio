# Project Dawn Portfolio Scenarios

This document records engineering scenarios, failures, tradeoffs, and follow-up designs that can later be turned into portfolio writeups.

## Scenario: Town Load Scalability

### Context

Project Dawn initially had one persistent Town room. Dungeon content was already split into party instances, but Town was a shared room where all logged-in players entered.

A new DummyClient scenario, `town-load`, was added to test MORPG-style Town load without entering dungeon matchmaking.

The scenario performs the real TCP flow:

```text
C_Login / C_CreatePlayer / C_EnterGame
-> enter Town
-> stay in Town for holdInTownSec
-> optionally send TCP C_Move patrol packets
```

It intentionally does not send `C_CreateRoom`, `C_SceneReady`, attack, or collision packets.

### Test Result

Small smoke test:

```text
Clients: 20
Result: stable enough for local confirmation
```

Scale test:

```text
Clients: 200-300
Unity Editor: dropped to about 0.9 FPS
Server: repeated slow room updates
```

Observed server logs:

```text
[ROOM][SLOW_UPDATE] RoomId=1, RoomType=Town, UpdateMs=300-700+, PlayerCount=201, EnemyCount=0
```

### What This Proved

The first assumption was that the 200-player failure might be mostly a Unity rendering problem. The slow server update logs showed that this was only part of the issue.

The actual bottleneck has two sides:

```text
Server side:
- One Town room held 201 players.
- Room update and movement broadcast work scaled poorly.
- Every moving player could become relevant to every other player.

Client side:
- Unity Editor attempted to receive, update, animate, and render too many remote players.
- Even if rendering were optimized, unnecessary server traffic would still remain.
```

The result is a useful portfolio point: the test did not just fail; it identified the next architectural boundary.

### Why Online Games Use Channels

A channel is a duplicated copy of the same social/map space. It prevents one Town room from holding all users.

Example target structure:

```text
Town Channel 1: up to 50 players
Town Channel 2: up to 50 players
Town Channel 3: up to 50 players
Town Channel 4: up to 50 players
Dungeon rooms: party-based instances
```

For Project Dawn, channels should be represented as multiple Town `GameRoom` instances, not as a separate protocol layer at first.

Initial policy:

```text
TownChannelMaxPlayers = 50
C_EnterGame -> RoomManager.FindOrCreateBestTownRoom()
```

This reduces the worst case from one 200-player room to four 50-player rooms.

### What AOI Means

AOI means Area Of Interest. It is the set of nearby objects that a player actually needs to know about.

Without AOI:

```text
201 players move
-> each movement may be sent to 200 other players
-> about 40,200 movement recipient checks/candidates
```

With AOI:

```text
201 players move
-> each movement is sent only to nearby players, for example 10-40 recipients
-> traffic and client update work drop sharply
```

Recommended first AOI policy:

```text
TownAoiRadius = 12
TownAoiMaxRecipients = 40
TownAoiRefreshMs = 500
```

AOI and channels solve different problems:

```text
Channels: reduce total players per Town room.
AOI: reduce what each player receives inside that room.
Client visibility: reduce what Unity renders and updates.
```

### Does AOI Alone Solve It?

Probably not by itself.

If all 200 players are packed into a small area, an AOI radius of 12 could still include almost everyone. In that case AOI gives little benefit.

That is why the plan needs three pieces:

```text
1. Town channeling to cap total room population.
2. AOI filtering to reduce per-player movement recipients.
3. Client visibility/LOD to reduce Unity update/render work.
```

DummyClient also needs wider Town movement bounds so load clients spread out instead of clustering around the same spawn area.

### Unity UI/UX Implication

A full channel system eventually needs UI:

```text
- Current channel display near the minimap or top bar.
- Channel selection popup.
- Channel full/busy indicators.
- Manual channel switch command.
- Optional auto-assignment on login.
```

However, the first implementation does not need a full UI. The recommended order is:

```text
Phase 1: automatic channel assignment only
- No Protocol change if the client can treat the assigned Town room like the normal Town.
- WebLauncher/API shows multiple Town rooms for verification.
- Good enough to validate server load reduction.

Phase 2: lightweight Unity channel indicator
- Show current channel number.
- Still no manual switching.

Phase 3: channel selection UI
- Open channel list.
- Request channel switch.
- Server validates capacity and moves the session.
```

### Recommended Development Plan

```text
Milestone: Town Scalability v1

Task 1. Town Channel v1
- Add Town channel capacity policy.
- Auto-assign C_EnterGame to the least-loaded Town room.
- Keep dungeon/party rooms unchanged.
- Verify 200 DummyClients split into roughly four Town rooms.

Task 2. DummyClient movement bounds
- Add movement bounds option for town-load.
- Spread 200 clients across a wider Town area.
- Re-test slow room update logs.

Task 3. Town AOI v1
- Filter Town S_Move recipients by distance and max recipient cap.
- Keep Bakal/dungeon broadcast behavior unchanged for now.
- Protocol unchanged.

Task 4. Client Visibility v1
- Hide or low-update remote players outside a local radius/cap.
- Disable Animator/SpriteRenderer for hidden remote players.
- Compare Editor and build FPS.

Task 5. Channel UX v1
- Add current channel display.
- Add manual channel selection only after backend channel movement is stable.
```

### Portfolio Angle

This scenario can be presented as an engineering discovery loop:

```text
1. Built real TCP DummyClient load generation.
2. Verified 20-player Town smoke test.
3. Attempted 200-300 player Town load.
4. Found both Unity FPS collapse and server slow room update logs.
5. Identified missing production concepts: channeling, AOI, client visibility/LOD.
6. Designed a staged solution without changing gameplay protocol prematurely.
```

Strong talking point:

```text
The failure was not treated as just a performance bug. It revealed where the architecture needed interest management and population partitioning, which are standard online game server concepts.
```
## Scenario Update: Town Channel v1 Implementation

### Decision

The first implementation should not start with a full Unity channel selection UI. It starts with automatic server-side assignment.

Reason:

```text
The immediate production risk is one Town room reaching 200+ players.
Automatic channel assignment breaks that bottleneck without introducing new UI state or manual channel switching rules.
```

### Implemented v1 Scope

```text
TownChannelMaxPlayers = 50
C_EnterGame -> RoomManager.FindOrCreateTownChannel()
```

Behavior:

```text
- If an existing Town room has fewer than 50 players, assign the player to the least populated Town room.
- If all Town rooms are full, create a new Town room.
- Dungeon rooms and party transfer rooms remain unchanged.
- Protocol remains unchanged.
```

### Deferred UI/UX

Unity channel UI is still needed later, but it is intentionally phased:

```text
Phase 1: automatic assignment only
Phase 2: show current channel indicator
Phase 3: channel list and manual channel switch
```

This keeps the first scalability step focused on validating whether server-side room partitioning reduces slow Town updates.

### Validation Target

Run:

```powershell
dotnet run --project "E:\task\Server\Server\DummyClient\DummyClient.csproj" -- --scenario town-load --clients 200 --host 127.0.0.1 --port 8080 --prefix PD_TownLoad --delayMs 10 --holdInTownSec 120 --enableMovement true --movementPattern patrol --patrolMode mixed --movementIntervalMs 200 --movementRadius 1.5 --movementSpeed 0.35 --timeoutSec 180 --verbose false
```

Expected:

```text
/api/rooms shows multiple Town rooms.
Each Town room should stay at or below about 50 players.
Server [ROOM][SLOW_UPDATE] logs should be materially lower than the single 201-player Town room case.
```


### Town Channel v1 Validation - 2026-07-07

After the first reservation-based implementation, a 55-client town-load test exposed a race: when channel 1 reached capacity, multiple concurrent login requests could create several underfilled Town rooms. The root cause was that `FindOrCreateTownChannel()` released `RoomManager` lock before creating the next Town room, so several requests observed "no available channel" at the same time.

Fix applied:
- Town channel selection, creation, and reservation are now handled atomically inside `RoomManager.FindOrCreateTownChannel()`.
- `GameRoom.EnterRoom()` releases the temporary Town channel reservation only after the player has been added to `_players`, and outside the `GameRoom` lock to avoid lock inversion.

Validation command:
```powershell
dotnet run --project "E:\task\Server\Server\DummyClient\DummyClient.csproj" -- --scenario town-load --clients 55 --host 127.0.0.1 --port 8080 --prefix PD_TownChannel2 --delayMs 5 --holdInTownSec 35 --enableMovement true --movementPattern patrol --patrolMode mixed --movementIntervalMs 500 --movementRadius 1.5 --movementSpeed 0.35 --timeoutSec 180 --verbose false
```

Validation result:
- DummyClient: `Result: SUCCESS`, `GameplayStarted: 55/55`, `GameplayCompleted: 55/55`, `FailedClients: 0`.
- Monitoring API `/api/rooms`: `RoomId=1` had 50 players, `RoomId=2` had 5 players.
- Evidence files:
  - `tmp/town-channel/town-channel-55-final3.log`
  - `tmp/town-channel/rooms-final3.json`
  - `tmp/town-channel/server-final3.log`

This confirms Channel v1 reduces the previous single-room 200-player pressure, but it does not replace AOI. Room 1 still broadcasts within a 50-player channel, so AOI/client visibility culling remains the next scalability step.

### Town AOI v1 Validation - 2026-07-07

Town AOI v1 was added after channeling because channeling caps the room size but does not reduce all-to-all movement broadcasts inside a channel.

Implementation summary:
- Applies only to `RoomType.Town`; dungeon/combat rooms keep existing full broadcast behavior.
- Server keeps a per-observer visible player set in `GameRoom`.
- Initial `S_Spawn` includes only players inside AOI.
- Movement updates send `S_Move` only to observers that can currently see the mover.
- Distance transitions use `S_Spawn` when entering AOI and `S_Despawn` when leaving AOI.
- AOI uses hysteresis: enter radius 15, exit radius 17, reducing boundary flicker.
- No Protocol changes.

Validation command:
```powershell
dotnet run --project "E:\task\Server\Server\DummyClient\DummyClient.csproj" -- --scenario town-load --clients 50 --host 127.0.0.1 --port 8080 --prefix PD_AOI --delayMs 5 --holdInTownSec 45 --enableMovement true --movementPattern patrol --patrolMode mixed --movementIntervalMs 300 --movementRadius 30 --movementSpeed 2.0 --timeoutSec 180 --verbose false
```

Validation result:
- DummyClient: `Result: SUCCESS`, `GameplayStarted: 50/50`, `GameplayCompleted: 50/50`, `FailedClients: 0`.
- MoveSent: 7,285.
- ReceivedMove: 125,161.
- Without AOI, the same all-to-all movement pattern would be approximately `7,285 * 49 = 356,965` received movement messages.
- AOI reduced observed movement fanout to about 35% of the all-to-all baseline in this spread-out patrol test.
- Server max room update during this run: 81.59 ms at 50 players, far below the previous 200-player single-room 300~700 ms spikes but still a signal that further optimization may be useful.

Evidence files:
- `tmp/town-aoi/town-aoi-50.log`
- `tmp/town-aoi/server-aoi.log`
- `tmp/town-aoi/rooms-aoi-50.json`

Known limitations:
- This is still O(N) per mover within a Town channel. It reduces network fanout, but not yet lookup cost.
- A grid/spatial hash should be the next step if Town channel size grows beyond 50 or update cost remains high.
- Unity client rendering still needs visibility/LOD validation with the real editor.
### Confirmed Follow-up Roadmap - 2026-07-08

The next work should proceed in this order:

```text
1. Unity client real-world validation
2. Client Visibility / LOD
3. AOI Spatial Hash / Grid
4. Unity Channel UI/UX
5. Dungeon Dummy Movement Bounds
```

#### 1. Unity Client Real-World Validation

This is a validation stage, not primarily a development stage.

Checklist:

```text
- Confirm the WebLauncher Town Channel card is visible and refreshes while clients connect.
- Run Unity with AOI enabled at 20, 50, and 100 dummy clients.
- Measure Unity FPS in the Editor and, if possible, in a standalone build.
- Confirm nearby dummy players are visible.
- Confirm distant dummy players despawn or stop being shown when they leave AOI.
- Confirm dummy players reappear when they move back into AOI.
```

This stage separates server packet fanout improvement from actual Unity rendering cost.

#### 2. Client Visibility / LOD

Server AOI reduces packets, but Unity still needs client-side rendering and update control.

Planned direction:

```text
- Nearby remote players: full Animator and normal visuals.
- Mid-range remote players: reduced update frequency or simplified display.
- Far/outside-AOI remote players: hidden, despawned, or kept inactive.
- Nameplates, effects, and nonessential UI should update less frequently or be disabled outside close range.
```

Portfolio framing:

```text
Server AOI solved network relevance. Client LOD solves rendering and per-object update cost.
```

#### 3. AOI Spatial Hash / Grid

Town AOI v1 currently reduces outgoing recipients, but the lookup is still a per-move scan over players in the same Town channel.

Current state:

```text
Town channel size: capped at 50
AOI lookup: O(N) distance checks per mover inside that channel
```

Future v2:

```text
- Divide Town space into grid cells.
- Track which players are in each cell.
- On movement, inspect only nearby cells instead of every player in the room.
- Keep the existing AOI enter/exit radius and spawn/despawn semantics.
```

Portfolio framing:

```text
AOI v1 reduced fanout. AOI v2 reduces lookup cost.
```

#### 4. Unity Channel UI/UX

Channels are currently automatic and visible through WebLauncher, not through the Unity client.

Needed UX:

```text
- Channel list.
- Channel population and congestion display.
- Channel move button.
- Input lock or loading state during channel transfer.
- Loading screen during matchmaking -> Bakal scene transition.
- Loading screen state should cover scene load, `C_SceneReady`, and final dungeon entry confirmation.
```

Historical return-policy question:

```text
At that time the open question was whether to restore the exact pre-dungeon channel
or choose another available Town channel.
```

Current implemented choice:

```text
Return through the pending Town transfer flow to the server's available Town room.
Before Town EnterRoom, reset every member to authoritative private MyRoom:
Pos=(0,0), State=Idle, MoveDir=Right, IsInPublicTownArea=false.
Exact original-channel affinity is optional future work.
```


Scene-change loading policy:

```text
- Show loading screen after match/transfer starts and before entering Bakal.
- Lock player input while Unity loads the dungeon scene.
- Send C_SceneReady only after client scene load completes.
- Hide loading screen after dungeon entry confirmation and local player spawn are ready.
- If transfer fails or server disconnects, replace loading with an error/disconnect notice instead of leaving the user stuck.
```
Optional future refinement:

```text
Store the pre-dungeon Town channel and prefer it on return if exact social-channel affinity becomes a product requirement.
The current private MyRoom return is already functionally correct without this affinity.
```

#### 5. Dungeon Dummy Movement Bounds

Dummy clients currently move in dungeon tests without strict map-area bounds. That makes combat and monster-AI tests noisy because clients can patrol outside intended gameplay space.

Needed work:

```text
- Define per-dungeon movement bounds.
- Keep dummy patrol/follow movement inside those bounds.
- Reuse the bounded movement setup for monster AI and combat regression tests.
- Make dungeon test results reproducible by preventing invalid dummy positions.
```

Portfolio framing:

```text
After Town scalability, the next reliability step is making dungeon simulation deterministic enough for combat and AI testing.
```
### Client Visibility / LOD v1 Implementation - 2026-07-08

Implemented in the Unity client:

```text
RemotePlayerLod
- Near <= 8 units: full renderer, name label, Animator speed 1.0.
- Mid <= 15 units: renderer visible, name label hidden, Animator speed 0.35, visual update throttled to 10Hz.
- Far > 15 units: renderer hidden, name label hidden, Animator disabled, root position still updated from network packets.
```

Integration points:

```text
ObjectManager
- Adds RemotePlayerLod only to OtherPlayer instances.
- MyPlayer and EnemyPlayer are unchanged.

OtherPlayer
- Keeps receiving S_Move and remote target positions.
- Skips expensive visual update when the LOD level is Far.
- Applies reduced visual update cadence when the LOD level is Mid.
```

Design note:

```text
Server AOI still owns network visibility and S_Spawn/S_Despawn.
Client LOD only controls local rendering, name labels, Animator cost, and visual update cadence.
It does not change Protocol or server authority.
```

Validation checklist:

```text
- Run 20/50/100 town-load again with Unity open.
- Confirm nearby players keep normal animation and name labels.
- Confirm mid-range players remain visible but labels disappear.
- Confirm far players disappear locally until server AOI despawns them or they return closer.
- Confirm no NullReferenceException from RemotePlayerLod, OtherPlayer, Animator, Canvas, or Renderer.
- Compare FPS against the pre-LOD 20/50/100 run.
```
### Direction Change - 2026-07-08

After Unity validation, 50-client Town load showed no visible FPS regression compared with walking alone. Because Town Channel + server AOI + Client LOD are currently good enough for the demo target, the next priority shifts from AOI Grid to user-facing flow polish.

New recommended order:

```text
1. Unity Channel UI/UX + Bakal scene loading screen
2. Dungeon Dummy Movement Bounds
3. Monster AI / bounded combat simulation
4. AOI Spatial Hash / Grid only if larger Town channel sizes become necessary
```

Reasoning:

```text
- AOI Grid is still useful architecture, but it optimizes lookup cost for a scale target that is not currently blocking the demo.
- Channel UI, loading screens, and input locks improve the user-facing experience immediately.
- Dungeon movement bounds are the right bridge into Monster AI because AI/combat tests need valid map areas.
```


### Dungeon Dummy Movement Bounds v1 - 2026-07-08

Problem:

```text
Dungeon dummy clients could patrol or follow targets without a strict gameplay-area boundary, which made future Monster AI and combat regression tests noisy.
```

Implemented direction:

```text
- Keep Town load movement unchanged for scalability testing.
- Clamp dungeon dummy movement after dungeon entry only.
- Expose CLI bounds so dungeon playable areas can be tuned without Protocol or server authority changes.
- Defer Channel Movement v2 until the request contract and UX policy are approved.
```

Portfolio framing:

```text
After Town scale was stabilized with Channel/AOI/LOD, the next step was making dungeon simulation reproducible enough for AI and combat validation.
```

### Monster AI / Bounded Combat Simulation v1 - 2026-07-08

Problem:

```text
Dungeon combat tests had player movement and server hit validation, but the Bakal enemy was mostly static except for skill timing. That limited AI/combat demo value and made future player-damage authority harder to stage.
```

Implemented direction:

```text
- Reuse the existing server room update loop.
- Keep AI server-owned and Bakal-only.
- Move the enemy toward the nearest living player at low cadence.
- Clamp enemy movement to the same bounded dungeon area used by DummyClient tests.
- Reuse existing S_Move instead of changing Protocol.
- Add Unity EnemyPlayer S_Move handling so the server-owned movement is visible in-client.
```

Portfolio framing:

```text
This extends the project from static dungeon validation into bounded, server-authoritative encounter simulation without introducing new protocol surface area.
```

### Tilemap Movement Bounds Export v1 - 2026-07-09

Problem:

```text
Hard-coded dungeon rectangles were enough for smoke tests, but they do not prove that AI and dummy movement respect the authored map.
This became visible while tuning Bakal patrol/collision because movement validity was separate from the Unity Tilemap layout.
```

Portfolio framing:

```text
Built an editor-to-server data pipeline: Unity Tilemap -> exported walkable span JSON -> server-side AI movement constraint.
This shows how authored level data can become deterministic server validation data without adding new network protocol messages.
```

Current state:

```text
- Unity Editor exporter is available from KIMCHILY_TOOL/Movement Bounds/Exporter.
- Server loads MovementBounds.json and applies it to Bakal patrol target selection / step validation.
- Missing JSON falls back to the previous rectangle so the demo does not break.
```

Next useful expansion:

```text
- Apply the same bounds file to DummyClient dungeon patrol/follow movement.
- Add Unity player-side client prediction clamp, then let the server remain final authority.
- Support multiple maps/scenes and a dedicated Walkable Tilemap naming convention.
```
## Final Portfolio Demo Scenario - 2026-07-09

This is the recommended presentation flow for the current Project Dawn baseline. Treat the project as feature-complete for portfolio/demo purposes unless a new milestone is explicitly selected later.

### Demo Goal

Show that Project Dawn is not only a Unity client, but a small online-game backend stack with server authority, instanced dungeon flow, monitoring, load testing, and map-authored movement constraints.

Core message:

```text
I started from a working multiplayer dungeon prototype, then hardened it through server authority, persistence, observability, load testing, channel/AOI optimization, and Unity-facing demo UX.
```

### Demo Flow

1. Start GameServer and WebLauncher

```text
- TCP 8080, UDP 8081, Monitoring API 8090 start.
- WebLauncher shows server status, rooms, online players, matching queue, recent events, and reject reason counts.
```

Talking point:

```text
The game server remains the source of gameplay truth. The Monitoring API is read-only and exposes snapshots, not live sessions or UDP tokens.
```

2. Enter Town and show MyRoom -> Lobby

```text
- Server spawns the Unity client at authoritative private MyRoom `(0,0)` with `Public=false`.
- Server Town AOI does not send other private players; Unity hiding remains only a defensive fallback.
- Enter the portal to send the validated UDP teleport into the public Lobby.
- Remote players appear according to Town AOI/visibility rules.
```

Talking point:

```text
The server separates private and public interest with `IsInPublicTownArea`, while Unity owns camera/BGM/portal presentation. The one-time MyRoom -> Lobby teleport is validated against the authored Town map.
```

3. Show Town channels and manual channel movement

```text
- Press Esc to open channel selection.
- Show channel population/congestion from the Monitoring API.
- Move to another non-full Town channel.
- The player remains in the same lobby coordinate, movement state resets to idle, and nearby objects are rehydrated by AOI/spawn packets.
```

Talking point:

```text
The 200-player Town test exposed the need for channels and AOI. The final design caps Town channels, filters interest by distance, and keeps manual channel movement server-validated.
```

4. Show Town load / AOI story

Use the existing evidence instead of always running 200 clients live:

```text
- 20 clients: smoke path.
- 50/100 clients: validated as stable in Unity after AOI/LOD.
- 200/300 clients: documented failure case that motivated channeling, AOI, and client LOD.
```

Talking point:

```text
This was an engineering discovery loop: the load test failed, server logs showed slow room updates, then the architecture moved to channel partitioning plus AOI plus client LOD.
```

5. Trigger matching and Bakal loading

```text
- Use the dungeon portal / party flow.
- Loading screen appears with input lock.
- Unity transitions Town -> LoadingScene -> Bakal.
- C_SceneReady is still sent only after the target scene load completes.
```

Talking point:

```text
Scene transfer uses the existing TCP room-transfer protocol and pending-transfer validation. The loading screen is UX polish, not a replacement for server flow.
```

6. Show dungeon / Bakal interaction

```text
- Bakal spawns in the dungeon room.
- Meteor behavior remains active.
- Bakal patrol uses server-owned movement constrained by exported Tilemap bounds.
- Player basic attacks are validated by the server: cooldown, action lock, active cast, range, facing, line/depth, duplicate hit.
```

Talking point:

```text
The client can animate and predict, but the server validates whether a hit is accepted. Rejected reasons are observable through logs and the event API.
```

7. Show MatchHistory and events

```text
- Matching creates durable MatchHistory and MatchHistoryMember rows.
- TransferStarted and DungeonEntered timestamps are recorded asynchronously.
- Recent match/transfer/combat events appear in the Monitoring API/WebLauncher.
```

Talking point:

```text
Persistence is deliberately snapshot-based and asynchronous. DB failure is warning-only and does not block matching or dungeon entry.
```

### Strongest Portfolio Angles

- Hybrid TCP/UDP transport with explicit reliability boundaries
  - Login, room/scene transfer, combat events, and `S_Move` replication stay on TCP.
  - Frequent client movement ingress uses authenticated Proto UDP with non-zero sequence, rate, speed, and bounds validation.
  - TCP `C_Move` is an explicit compatibility mode, not an automatic failure fallback.

- Server-authoritative private/public Town interest
  - Initial login and dungeon return apply private MyRoom state before spawn packets are built.
  - Town AOI prevents private players from entering each other's spawn/move stream.
  - The public transition is tied to a walkable Seria portal destination.

- Server-authoritative combat validation
  - Basic attacks are not trusted just because the client reports a hit.
  - Server checks timing, cooldown, active cast, range, facing, line/depth, duplicate hits.

- Load-test-driven architecture
  - 200-player Town test exposed both Unity and server bottlenecks.
  - The response was channels, AOI, and client LOD rather than a blind micro-optimization.

- Observability and operations
  - WebLauncher and Monitoring API make queue, rooms, events, and reject counters visible during demos.
  - Event buffer is bounded and read-only.

- Durable backend data
  - MatchHistory records party, members, room id, transfer id, timestamps, and result status.
  - Live sessions, rooms, UDP endpoints, and tokens are not persisted or exposed.

- Tooling pipeline
  - Unity Tilemap exports MovementBounds JSON.
  - Server, DummyClient, and Unity use the same authored movement data.
  - Bakal root navigation is converted to a prefab-derived Base/Shadow combat anchor `(+/-1.0,-1.1280002)` for map and hit checks.

- Demo UX polish
  - Loading screen, input lock, channel UI, MyRoom/Lobby flow, disconnect notice, and remote LOD make the system presentable.

### Known Deferred Work To Mention Honestly

These are not blockers for the current portfolio demo:

```text
- Monster attack authority v2: enemy melee windows/player damage validation can be expanded later.
- Per-area named bounds v2: current MovementBounds are room-level; named MyRoom/Lobby/Bakal sub-regions would be cleaner.
- Exact original Town-channel affinity after dungeon return: current return already selects a Town room and enters private MyRoom correctly.
- AOI grid/spatial hash: current AOI is enough for validated local demo scale, but larger channels should reduce lookup cost.
- Separate ASP.NET Core Web API and Redis MatchQueueStore remain roadmap items, not current scope.
```

### Recommended Live Demo Checklist

```text
1. Start Server.dll from bin/Debug/netcoreapp3.1.
2. Open WebLauncher.
3. Start one Unity client.
4. Enter Town MyRoom, then Lobby.
5. Press Esc and show channel list.
6. Move channel and confirm same-position re-entry.
7. Optionally run 20-50 town-load clients for visual density.
8. Trigger Bakal matching/loading.
9. Attack Bakal and compare Root/CombatAnchor logs with the visible Base/Shadow point.
10. Clear Bakal and confirm all clients return privately to MyRoom without seeing stale party coordinates.
11. Query recent events and, if needed, MatchHistory DB rows.
```

### Final Position

The current Project Dawn baseline is ready to treat as a portfolio demo build. Further work should be framed as optional roadmap, not required completion work.

