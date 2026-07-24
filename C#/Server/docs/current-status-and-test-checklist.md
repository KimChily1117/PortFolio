# Project Dawn Current Status and Test Checklist

Last updated: 2026-07-20

## Completed Work

### Milestone 1 - Combat Authority v2

Implemented:

- Server cooldown validation for basic attack SkillIds `2`, `3`, and `4`.
- Server action/cast lock validation before accepting a new `C_Skill` (`ActionLockMs=50` for basic attacks).
- Structured reject logs for `CooldownActive` and `ActionLocked`.
- In-memory `CombatRejectMetrics` counters for skill/hit reject reasons.
- Regression validation with DummyClient fast attack and normal attack intervals.

Current policy:

- TCP skill path remains authoritative for basic attacks.
- UDP skill transport is still intentionally out of scope.
- Basic attacks keep `CooldownMs=1200` and `ActiveWindowMs=1200`, but use `ActionLockMs=50` so the server lock does not block the Unity 0.6s combo input window.
- Movement is not forcibly blocked by action lock yet; the milestone only rejects overlapping skill casts.

### Milestone 2 - MatchHistory Persistence

Implemented:

- `MatchHistory` and `MatchHistoryMember` EF models.
- Manual migration SQL for the local GameDB schema.
- Fire-and-forget `MatchHistoryPersistence` worker with bounded queue size `1024`.
- Non-blocking match lifecycle capture from match creation, transfer start, dungeon entry, and failure paths.
- DB write failures are warning-only and do not block matching, transfer, or dungeon entry.

Persisted data is snapshot-only:

- Party id, queue key, target room type/id, transfer id, timestamps, result status, failure reason.
- Member player id/name.
- No `ClientSession`, live `Player`, live `GameRoom`, UDP endpoint, or UDP token fields.

### Milestone 3 - Monitoring API Polish

Implemented:

- Bounded thread-safe recent event buffer with `MaxEvents=300`.
- Read-only `GET /api/events/recent` endpoint.
- Event types: `MatchAccepted`, `MatchRejected`, `PartyMatched`, `TransferStarted`, `DungeonEntered`, `RoomCreated`, `RoomRemoved`, `SkillCastRejected`.
- `rejectReasonCounts` summary sourced from `CombatRejectMetrics`.
- WebLauncher cards for recent events and reject reason summary.
- Graceful WebLauncher degradation if the new event endpoint is unavailable.

The Monitoring API remains local, read-only, and snapshot-based.

## Latest Verification - 2026-07-20 UDP / MyRoom / Bakal Anchor

Current implementation:

- Normal Unity login enables `UseUdpMovement=true`.
- Unity sends `C_UdpMove` with a monotonically increasing non-zero `sequence` only after successful `S_UdpHello`.
- TCP `C_Move` remains available only when `UseUdpMovement=false` is selected explicitly; there is no automatic TCP resend when UDP is not registered.
- Initial Town login and dungeon-clear return apply authoritative MyRoom spawn `(0,0)` with `IsInPublicTownArea=false` before `EnterRoom`.
- Town AOI excludes a pair when either observer or subject is private, preventing remote MyRoom spawn/move fanout.
- The Seria portal UDP teleport is accepted as the private-to-public transition only for a walkable Town destination with `x >= 18`; normal speed validation is bypassed only for that transition.
- Bakal `PositionInfo` is the prefab root. Navigation/combat uses the Base/Shadow world anchor derived from local `(1.25,-1.4100002) * 0.8 = (1.0,-1.1280002)`.

Four-client MyRoom/UDP check:

1. Launch four standalone clients with separate logs.
2. Enter the same Town channel but remain in MyRoom.
3. Confirm each server self-spawn is `Pos=(0.00,0.00)` and `Public=False`.
4. Confirm initial `S_Spawn` lists do not contain the other private players.
5. Move one client through the Seria portal.
6. Confirm `[TOWN_FLOW][UDP_PUBLIC_TRANSITION]` and `[TOWN_FLOW][SERVER_PUBLIC_APPLIED]` appear once.
7. Confirm there is no repeating `[UDP] Move dropped. Reason=Speed` sequence for the portal distance.
8. Move the remaining clients into the public Lobby and confirm AOI-based spawn/move synchronization.

Dungeon-clear return check:

1. Enter Bakal with four clients and clear the boss.
2. Confirm all clients receive `S_DungeonClear` and Town `S_SceneMove`.
3. Confirm `[TOWN_SPAWN] MyRoom spawn assigned. Reason=ReturnToTown ... Pos=(0.00,0.00), Public=False` for every member.
4. Confirm each client sees only itself in MyRoom and does not show party members at stale Bakal or Lobby coordinates.
5. Enter the public Lobby again and confirm normal AOI visibility resumes.

Bakal anchor check:

1. Compare Unity enemy root and Base/Shadow world positions.
2. Confirm the delta is approximately `(+/-1.0,-1.128)` according to facing.
3. Confirm server `[ENEMY_AI] Patrol` logs include matching `Root`, `CombatAnchor`, `TargetRoot`, `TargetAnchor`, and `Dir`.
4. Evaluate walkable bounds and hit range with CombatAnchor values, not the sprite body center.


## Latest Verification - 2026-07-06 Attack Sync and WebLauncher

Code change:

- Basic attack server timing is now split into `CooldownMs=1200`, `ActiveWindowMs=1200`, and `ActionLockMs=50`.
- `ActiveWindowMs` remains the hit-validation window for animation hit-frame candidates.
- `ActionLockMs=50` is the recast guard, kept shorter than the Unity 0.6s combo input window.
- Protocol files were not changed.

Verification run:

- Build: `dotnet build Server\Server\Server.csproj` passed with existing netcoreapp3.1 EOL warnings only.
- 600ms combo-paced dummy attack: `PD_Dummy_0021` through `PD_Dummy_0024`, 4 clients, `--attackIntervalMs 600`, `--skillIds 2,3,4` completed successfully with `Active cast recorded=36`, `ActionLocked=0`, `CooldownActive=0` in server logs.
- 200ms fast dummy attack: `PD_Dummy`, 4 clients, `--attackIntervalMs 200`, `--skillIds 2,3,4` completed successfully and produced server rejects: `ActionLocked=40`, `CooldownActive=24`.
- Event API snapshot: `tmp\attack-sync-test\api-events-recent.json` shows `rejectReasonCounts.ActionLocked=40` and `rejectReasonCounts.CooldownActive=24`.
- WebLauncher static server verified at `http://127.0.0.1:5500` with HTTP 200; Recent Events and Reject Reasons cards consume `/api/events/recent`.

Live local URLs after this run:

- Game server TCP: `127.0.0.1:8080`
- Monitoring API: `http://127.0.0.1:8090`
- WebLauncher: `http://127.0.0.1:5500`


## Latest Verification - 2026-07-07 Town Load DummyClient

Implemented:

- New DummyClient scenario `town-load`.
- Town-only hold duration option `--holdInTownSec`.
- Town movement loop that sends TCP `C_Move` without `C_CreateRoom`, dungeon transfer, attack, or collision.
- Summary checks for `EnteredTown`, `MatchRequested: 0/N`, movement completion, room isolation diagnostics, and `TownLoadMetrics` latency/throughput output.

Purpose:

- This measures server-side Town connection and movement broadcast behavior for MORPG-style load.
- It does not measure Unity rendering, animation, or client-side object pooling performance.

## Start Server

Use the built server output so `config.json` is found from the expected runtime directory:

```powershell
cd "E:\task\Server\Server\Server\bin\Debug\netcoreapp3.1"
dotnet Server.dll
```

Expected server logs:

```text
TCP Listening : 0.0.0.0:8080
UDP Listening : 0.0.0.0:8081
[MONITOR_API] Started. Url=http://127.0.0.1:8090
```

## Open WebLauncher

Either open the static file directly:

```text
E:\task\Server\WebLauncher\index.html
```

Or run a local static server:

```powershell
cd "E:\task\Server\WebLauncher"
python -m http.server 5500
```

Open:

```text
http://127.0.0.1:5500
```

Expected WebLauncher state:

- Server badge is `Online`.
- Status cards show room/player counts.
- Rooms card shows at least Room 1 / Town.
- Online Players and Matching Queue cards are `Ready`.
- Recent Events card is `Ready`.
- Reject Reasons card is `Ready`.

## Launch Unity Client

Configured build paths currently exist under:

```text
E:\task\C#\Project_Dawn\Builds\Win64\Project_Dawn1
E:\task\C#\Project_Dawn\Builds\Win64\Project_Dawn2
E:\task\C#\Project_Dawn\Builds\Win64\Project_Dawn3
E:\task\C#\Project_Dawn\Builds\Win64\Project_Dawn4
```

Client 1 manual launch command:

```powershell
start "" /D "E:\task\C#\Project_Dawn\Builds\Win64\Project_Dawn1" "E:\task\C#\Project_Dawn\Builds\Win64\Project_Dawn1\Project_Dawn1.exe" -screen-width 800 -screen-height 600 -screen-fullscreen 0 -testClientId=build01
```

Expected client/server observations:

- Client reaches login/character flow without TCP connection errors.
- Server logs a new session and login.
- `/api/players/online` shows the client after entering the game.
- WebLauncher Online Players card shows the client name and current room.

## API Smoke Test

Run after the server is started:

```powershell
Invoke-RestMethod http://127.0.0.1:8090/api/health
Invoke-RestMethod http://127.0.0.1:8090/api/server/status | ConvertTo-Json -Depth 6
Invoke-RestMethod http://127.0.0.1:8090/api/rooms | ConvertTo-Json -Depth 10
Invoke-RestMethod http://127.0.0.1:8090/api/players/online | ConvertTo-Json -Depth 10
Invoke-RestMethod http://127.0.0.1:8090/api/matching/queue | ConvertTo-Json -Depth 10
Invoke-RestMethod http://127.0.0.1:8090/api/events/recent | ConvertTo-Json -Depth 12
```

Expected:

- All requests return JSON.
- No endpoint returns UDP token, UDP endpoint, `ClientSession`, or `GameRoom` object references.
- `/api/events/recent` includes `count`, `maxEvents`, `rejectReasonCounts`, and `events`.

## DummyClient Test Checklist


### 0. Town Load

Small smoke run:

```powershell
dotnet run --project "E:\task\Server\Server\DummyClient\DummyClient.csproj" -- --scenario town-load --clients 20 --host 127.0.0.1 --port 8080 --prefix PD_TownLoad --delayMs 20 --holdInTownSec 30 --enableMovement true --movementPattern patrol --patrolMode mixed --movementIntervalMs 200 --movementRadius 1.5 --movementSpeed 0.35 --verbose false
```

Scale run after the smoke run passes:

```powershell
dotnet run --project "E:\task\Server\Server\DummyClient\DummyClient.csproj" -- --scenario town-load --clients 200 --host 127.0.0.1 --port 8080 --prefix PD_TownLoad --delayMs 10 --holdInTownSec 120 --enableMovement true --movementPattern patrol --patrolMode mixed --movementIntervalMs 200 --movementRadius 1.5 --movementSpeed 0.35 --timeoutSec 180 --verbose false
```

Check:

- DummyClient summary shows `EnteredTown: N/N`.
- DummyClient summary shows `MatchRequested: 0/N`.
- DummyClient summary shows `GameplayCompleted: N/N` and `Result: SUCCESS`.
- `TownLoadMetrics` shows connect/login/Town timings and movement send/receive rates.
- `/api/rooms` shows Town player count while clients are held.
- `/api/players/online` shows the loaded clients.
- WebLauncher Rooms and Online Players cards refresh during the hold window.

### 1. Queue Visibility

```powershell
dotnet run --project "E:\task\Server\Server\DummyClient\DummyClient.csproj" -- --scenario fill-visible --clients 3 --expectedExternal 1 --host 127.0.0.1 --port 8080 --prefix PD_Dummy --delayMs 100 --sceneReadyDelayMs 100 --holdAfterDungeonSec 60 --verbose false
```

Check:

- `/api/matching/queue` shows `totalWaitingPlayers=3`.
- `/api/events/recent` shows three `MatchAccepted` events.
- WebLauncher Matching Queue card shows `PD_Dummy_0001` through `PD_Dummy_0003`.

### 2. Multi-Room Match Flow

```powershell
dotnet run --project "E:\task\Server\Server\DummyClient\DummyClient.csproj" -- --scenario fill-dummy --clients 8 --partySize 4 --expectedRooms 2 --host 127.0.0.1 --port 8080 --prefix PD_Dummy --delayMs 100 --sceneReadyDelayMs 100 --holdAfterDungeonSec 15 --timeoutSec 40 --enableMovement true --movementPattern patrol --patrolMode mixed --verbose false
```

Check:

- DummyClient summary shows `PartyMatched: 8/8`, `SceneReadySent: 8/8`, `EnteredDungeon: 8/8`.
- `/api/rooms` shows one Town room and two Bakal rooms.
- `/api/events/recent` shows two `PartyMatched`, two `TransferStarted`, and `DungeonEntered` events for both parties.
- WebLauncher Recent Events card shows the party/transfer/dungeon timeline.

### 3. Match Rejection Visibility

Use a non-`PD_Dummy` prefix so dummy auto-equipment does not run:

```powershell
dotnet run --project "E:\task\Server\Server\DummyClient\DummyClient.csproj" -- --scenario fill-visible --clients 1 --expectedExternal 3 --host 127.0.0.1 --port 8080 --prefix NoGear --startIndex 1 --delayMs 100 --sceneReadyDelayMs 100 --holdAfterDungeonSec 1 --timeoutSec 8 --verbose false
```

Check:

- DummyClient receives `S_CreateRoom ResponseCode=0`.
- `/api/events/recent` shows `MatchRejected` with `reason=MissingRequiredEquipment`.

### 4. Combat Reject Visibility and Buffer Cap

```powershell
dotnet run --project "E:\task\Server\Server\DummyClient\DummyClient.csproj" -- --scenario fill-dummy --clients 4 --partySize 4 --expectedRooms 1 --host 127.0.0.1 --port 8080 --prefix PD_Dummy --delayMs 100 --sceneReadyDelayMs 100 --holdAfterDungeonSec 8 --timeoutSec 25 --enableMovement true --movementPattern follow-enemy --targetEnemy nearest --enableAttack true --attackIntervalMs 200 --skillIds 2,3,4 --gameplayDurationSec 4 --verbose false
```

Check:

- DummyClient summary shows `SkillSent` greater than zero.
- Server logs contain `[SKILL] Cast rejected. Reason=CooldownActive` and/or `Reason=ActionLocked`.
- `/api/events/recent` shows `SkillCastRejected` events.
- `/api/events/recent.rejectReasonCounts` includes `ActionLocked` and/or `CooldownActive`.

For cap verification, run a longer or 8-client variant:

```powershell
dotnet run --project "E:\task\Server\Server\DummyClient\DummyClient.csproj" -- --scenario fill-dummy --clients 8 --partySize 4 --expectedRooms 2 --host 127.0.0.1 --port 8080 --prefix PD_Dummy --delayMs 100 --sceneReadyDelayMs 100 --holdAfterDungeonSec 12 --timeoutSec 35 --enableMovement true --movementPattern follow-enemy --targetEnemy nearest --enableAttack true --attackIntervalMs 200 --skillIds 2,3,4 --gameplayDurationSec 8 --verbose false
```

Expected:

- `/api/events/recent.count` never exceeds `/api/events/recent.maxEvents`.
- Current cap is `300`.




## Unity Channel Selection UI Check

Run after server and Unity client are in Town:

1. Press `Esc`.
2. Wait for the channel list to refresh from `/api/rooms`.

Expected:

- A `채널 선택` popup opens.
- Town channels are listed as `Town CH N  Room X`.
- Player count appears as `N/50` with congestion text.
- The current room row is marked `현재` when the local player is visible in `/api/rooms`.
- `새로고침` reloads the list.
- `닫기` closes the popup.
- Move buttons are enabled for available non-current Town channels and show `이동`, `이동 중`, `접속 중`, or `가득 참` depending on state.
- If Monitoring API is unavailable, the popup shows a graceful failure message instead of throwing.
## Bakal Loading Screen Check

Run after server and Unity client are running:

1. Enter Town with the Unity client.
2. Trigger Bakal matching from the dungeon portal.
3. Wait until party transfer starts and `S_SceneMove` is received.

Expected:

- Loading overlay appears with `매칭이 완료되어 던전으로 이동합니다`.
- Unity scene flow goes `Town -> LoadingScene -> Bakal`.
- Arrow/X/C inputs do not move, attack, or jump while loading is visible.
- Client log contains `[SCENE][LOADING] Shown`.
- Client log still contains `Scene loaded. Sending C_SceneReady` after Bakal scene load.
- Loading overlay disappears after `S_EnterGameHandler` in Bakal.
- Client log contains `[SCENE][LOADING] Hidden after S_EnterGame.`
- If the server is stopped during loading, disconnect notice appears and the client exits instead of staying stuck.
## Client Visibility / LOD Check

Run after Unity client enters Town and the server is running:

```powershell
dotnet run --project "E:\task\Server\Server\DummyClient\DummyClient.csproj" -- --scenario town-load --clients 50 --host 127.0.0.1 --port 8080 --prefix PD_LOD50 --delayMs 10 --holdInTownSec 180 --enableMovement true --movementPattern patrol --patrolMode mixed --movementIntervalMs 300 --movementRadius 30 --movementSpeed 2.0 --timeoutSec 240 --verbose false
```

Expected:

- In public Lobby, near remote players keep normal animation and name labels.
- In public Lobby, mid-range remote players remain visible but name labels are hidden.
- In public Lobby, far remote players are hidden locally while their root object still receives network position updates.
- In private MyRoom, other private players are not sent by server AOI; client `SetActive(false)` is only a defensive fallback for stale objects.
- No Unity Console errors from `RemotePlayerLod`, `OtherPlayer`, `Animator`, `Canvas`, or `Renderer`.
- FPS should be equal or better than the pre-LOD 50/100-client run.
## Tilemap Movement Bounds Check

Requires `Assets/Resources/Data/MovementBounds.json` to contain both `Town` and `Bakal` maps exported from Unity Tilemaps.

Town dummy validation:

```powershell
dotnet run --project "E:\task\Server\Server\DummyClient\DummyClient.csproj" -- --scenario town-load --clients 20 --host 127.0.0.1 --port 8080 --prefix PD_TownDummy --delayMs 50 --holdInTownSec 60 --enableMovement true --movementPattern patrol --patrolMode mixed --movementRadius 20 --movementSpeed 2 --movementIntervalMs 300 --verbose true
```

Expected:

- Server log contains `[MOVEMENT_BOUNDS] Map loaded. RoomType=Town`.
- Dummy log contains `Town load started ... Bounds=Tilemap[...]`.
- If a dummy hits the edge, `[DUMMY][BOUNDS]` shows a clamped position and then movement turns back inward.
- Server log may contain `[MOVE][BOUNDS_CLAMP] ... AcceptedDir=...` when a movement request crosses the Town walkable edge.
- Unity client in Town Lobby cannot walk outside the exported Town walkable spans.
- Unity client in MyRoom is not snapped to the Town lobby bounds.
## Durable MatchHistory Checks

After a successful `fill-dummy --clients 8 --partySize 4` run, query the local DB with the temporary DB tool:

```powershell
dotnet run --project "E:\task\Server\tmp\DbTool\DbTool.csproj" -- recent 5
```

Expected:

- One 4-player party creates one `MatchHistory` row and four `MatchHistoryMember` rows.
- Eight dummy clients with party size four create two rows with different target room ids.
- Status should become `Entered` after dungeon entry.

## Manual Pass/Fail Sheet

- [ ] Server starts TCP 8080, UDP 8081, Monitoring API 8090.
- [ ] Unity client can connect and enter town.
- [ ] Unity client shows a disconnect notice and exits when the server is stopped.
- [ ] Town load smoke test succeeds with `MatchRequested: 0/N`.
- [ ] Town load scale test reaches the intended client count without server crash.
- [ ] WebLauncher shows Online status.
- [ ] API smoke test endpoints all return JSON.
- [ ] Queue visibility test shows waiting players.
- [ ] Multi-room test shows two Bakal rooms.
- [ ] Recent events show match and transfer timeline.
- [ ] Missing equipment rejection appears as `MatchRejected`.
- [ ] Combat reject counters appear in `rejectReasonCounts`.
- [ ] Recent event count remains at or below `300`.
- [ ] MatchHistory rows persist after successful match.
- [ ] Tilemap MovementBounds.json contains both Town and Bakal entries.
- [ ] Town dummy movement stays inside exported Town spans and turns back after clamp.
- [ ] Unity MyPlayer is blocked by Town lobby tilemap bounds but not snapped while in MyRoom.
- [ ] Normal Unity movement sends UDP `C_UdpMove` with increasing non-zero `sequence`.
- [ ] UDP registration-pending movement does not silently use TCP.
- [ ] Four private MyRoom clients do not receive or display one another.
- [ ] MyRoom -> Lobby teleport is accepted once without repeated UDP speed drops.
- [ ] Dungeon clear returns every party member to `(0,0)` / `Public=False` before Town spawn.
- [ ] Bakal root-to-combat-anchor delta matches `(+/-1.0,-1.128)`.
- [ ] Protocol files remain unchanged.

## Known Notes

- `holdAfterDungeonSec` should be greater than `gameplayDurationSec` when checking DummyClient `Result: SUCCESS`; otherwise summary timing can show `GameplayCompleted` slightly short even though dungeon entry succeeded.
- Monitoring API and WebLauncher are local read-only tools, not gameplay APIs.
- Do not expose UDP tokens, internal auth tokens, live session objects, or mutable room dictionaries through API responses.
















## Monster AI Bounded Combat Check

Requires server restart after the Monster AI build so the new `Enemy.Update()` code is loaded.

```text
dotnet run --project "E:\task\Server\Server\DummyClient\DummyClient.csproj" -- --scenario fill-dummy --clients 4 --partySize 4 --expectedRooms 1 --host 127.0.0.1 --port 8080 --prefix PD_Dummy --delayMs 100 --sceneReadyDelayMs 100 --holdAfterDungeonSec 12 --timeoutSec 35 --enableMovement true --movementPattern patrol --patrolMode mixed --movementRadius 20 --movementSpeed 2 --gameplayDurationSec 8 --verbose false
```

Expected:

```text
- Server log contains [ENEMY_AI] Move.
- Enemy position in /api/rooms changes over time and stays inside X[-8,8], Y[-4.5,4.5].
- Unity Bakal enemy visually moves from server S_Move.
- DummyClient still reports EnteredDungeon: 4/4 and GameplayCompleted: 4/4.
```

## Bakal Melee Attack Check

Requires server restart after build.

```text
- Enter Bakal with Unity and dummy fillers.
- Hit Bakal once with basic attack.
- Confirm server logs [ENEMY_AI] AggroChanged with your player name.
- Move near Bakal.
- Confirm [ENEMY_AI] MeleeAttack appears and S_Collision damage is received by Unity.
- Confirm SkillId=4 meteor still appears later.
- Confirm SkillId=5 logs in Unity as BAKAL MELEE ATTACK!!!!. If the Animator trigger is not wired yet, the log is enough.
```

## Latest Development - 2026-07-09 Manual Town Channel Movement

Implemented:

- Approved protocol contract: `C_ChannelMove(targetRoomId)` and `S_ChannelMove(success/currentRoomId/targetRoomId/reason)`.
- Server-side Town channel validation and capacity reservation through `RoomManager.TryReserveTownChannel(...)`.
- `C_ChannelMoveHandler` rejects non-Town, same-channel, invalid, missing, full, and stale-room requests.
- Unity `Esc` channel overlay now sends channel move requests for available Town channels.
- Unity handles S_ChannelMove and displays success/failure state.
- Unity clears previous-channel remote objects with RemoveRemoteObjects() before target-channel spawn packets repopulate nearby players.

Build verification:

- `dotnet build Server\PacketGenerator\PacketGenerator.csproj`: pass.
- `protoc.exe --proto_path=. --csharp_out=. Protocol.proto`: pass with unused import warning only.
- PacketGenerator run: pass.
- `dotnet build Server\Server\Server.csproj`: pass with existing netcoreapp3.1 EOL warnings only.
- `dotnet build Server\DummyClient\DummyClient.csproj`: pass with existing netcoreapp3.1 EOL warnings only.
- Unity `Assembly-CSharp.csproj` command-line build remains blocked by existing DOTween firstpass reference errors; verify final compile in Unity Editor.

Manual channel movement checklist:

1. Start server.
2. Create at least two Town channels. Use Town load over 50 clients or multiple builds until `/api/rooms` shows two Town rooms.
3. Start Unity client and enter Town.
4. Press `Esc`.
5. Click `?대룞` on a different non-full Town channel.
6. Server should log `[TOWN_CHANNEL] Move requested` then `[TOWN_CHANNEL] Move completed`.
7. Unity should log `[CHANNEL_UI] S_ChannelMove. Success=True`.
8. The current row should update after refresh and nearby players should be re-spawned according to Town AOI.
9. Try current/full/invalid channel cases and confirm `Success=False` with reason text.



## Completed - 2026-07-09 Channel Move Town Re-entry Polish

Goal:

- Manual Town channel movement should behave like a same-scene soft re-entry, not like an uninitialized partial room swap.
- Hybrid policy preserves the player area state:
  - MyRoom -> channel move -> MyRoom, server AOI excludes private remotes and Unity hides any stale object defensively.
  - Lobby -> channel move -> Lobby, server-provided lobby spawn retained, remote players visible by AOI.

Validated/closed behavior:

- MyRoom channel movement preserves MyRoom camera/visibility policy.
- Lobby channel movement preserves Lobby camera/visibility policy.
- BakalPortal eligibility still depends on `TownMapState.DUNGEONENTRANCE`.
- Lobby channel movement preserves the pre-switch coordinate and normalizes local movement to idle.

Implemented hybrid channel re-entry:

- TownScene.BeginChannelReenter() captures current MyRoom/Lobby state on successful channel move.
- TownScene.CompleteChannelReenter(...) reapplies camera bounds, BGM, visibility, and MyRoom teleport only when preserving MyRoom state.
- Lobby channel movement keeps the server-provided lobby spawn from S_EnterGame.


- Lobby channel re-entry normalizes the local player to Idle at the current position to prevent stale movement after channel transfer.
## Bakal Transfer Movement Reset Check

Run after server restart:

1. In Town, hold a movement key so the client is moving.
2. While moving or immediately after movement, trigger Bakal matching/transfer.
3. Wait for `Town -> LoadingScene -> Bakal` and local `S_EnterGame`.

Expected:

- Server log contains `[TRANSFER] Dungeon spawn reset... State=Idle, MoveDir=Right`.
- Local player does not keep walking from the previous Town direction after Bakal loads.
- `S_EnterGame`/spawn state for the local player is idle, not stale `Moving`/`Run`.
## Town Channel Position Preserve Check

Run after server restart:

1. Enter Town Lobby with Unity.
2. Move to an obvious coordinate in the lobby.
3. Open channel UI with `Esc` and move to another Town channel.

Expected:

- Server log contains `[TOWN_CHANNEL] Preserved position for channel move...` with the pre-switch coordinate.
- After the target channel re-entry, the player remains around the same lobby coordinate instead of being assigned a new random lobby spawn.
- Player state is idle after re-entry and does not continue the previous movement direction.
## Town Channel Movement Test Cases

Run these after server restart with Monitoring API available.

Shared PowerShell helpers:

```powershell
# Check current room/channel snapshots.
Invoke-RestMethod http://127.0.0.1:8090/api/rooms | ConvertTo-Json -Depth 10

# Create enough Town load to force additional Town channels when channel capacity is 50.
dotnet run --project "E:\task\Server\Server\DummyClient\DummyClient.csproj" -- --scenario town-load --clients 55 --host 127.0.0.1 --port 8080 --prefix PD_CH_SETUP --delayMs 10 --holdInTownSec 180 --enableMovement true --movementPattern patrol --patrolMode mixed --movementIntervalMs 300 --movementRadius 3 --movementSpeed 0.5 --timeoutSec 240 --verbose false

# Fill one Town channel to capacity for full-channel rejection checks.
dotnet run --project "E:\task\Server\Server\DummyClient\DummyClient.csproj" -- --scenario town-load --clients 50 --host 127.0.0.1 --port 8080 --prefix PD_CH_FULL --delayMs 10 --holdInTownSec 180 --enableMovement true --movementPattern patrol --patrolMode mixed --movementIntervalMs 300 --movementRadius 3 --movementSpeed 0.5 --timeoutSec 240 --verbose false
```

Note:

- DummyClient currently prepares Town load and Bakal matching scenarios, but it does not expose a `C_ChannelMove` command-line option.
- Actual channel move requests are sent from Unity channel UI, or from a dedicated packet/debug client if one is added later.

### TC-CH-001 Lobby channel move preserves position

PowerShell setup:

```powershell
# Start this in a separate PowerShell window and keep it running during the Unity test.
dotnet run --project "E:\task\Server\Server\DummyClient\DummyClient.csproj" -- --scenario town-load --clients 55 --host 127.0.0.1 --port 8080 --prefix PD_CH_LOBBY --delayMs 10 --holdInTownSec 180 --enableMovement true --movementPattern patrol --patrolMode mixed --movementIntervalMs 300 --movementRadius 3 --movementSpeed 0.5 --timeoutSec 240 --verbose false

# Confirm at least two Town rooms exist.
Invoke-RestMethod http://127.0.0.1:8090/api/rooms | ConvertTo-Json -Depth 10
```

Setup:

- Start the server.
- Create at least two Town channels with the PowerShell setup above.
- Enter Town Lobby with one Unity client.
- Move the local player to an obvious lobby coordinate away from the spawn point.

Steps:

1. Open the channel UI with `Esc`.
2. Select a different non-full Town channel.
3. Wait until the target channel re-entry completes.
4. Refresh `/api/rooms`.

PowerShell verification:

```powershell
Invoke-RestMethod http://127.0.0.1:8090/api/rooms | ConvertTo-Json -Depth 10
Invoke-RestMethod http://127.0.0.1:8090/api/players/online | ConvertTo-Json -Depth 10
```

Expected:

- Server logs `[TOWN_CHANNEL] Move requested`, `[TOWN_CHANNEL] Preserved position for channel move`, and `[TOWN_CHANNEL] Move completed`.
- Unity logs `[CHANNEL_UI] S_ChannelMove. Success=True`.
- Local player remains near the pre-switch lobby coordinate.
- Local player state is idle after re-entry.
- Remote players from the previous channel are cleared before target-channel spawn packets repopulate nearby players.

### TC-CH-002 MyRoom channel move preserves MyRoom state

PowerShell setup:

```powershell
# Start this in a separate PowerShell window and keep it running during the Unity test.
dotnet run --project "E:\task\Server\Server\DummyClient\DummyClient.csproj" -- --scenario town-load --clients 55 --host 127.0.0.1 --port 8080 --prefix PD_CH_MYROOM --delayMs 10 --holdInTownSec 180 --enableMovement true --movementPattern patrol --patrolMode mixed --movementIntervalMs 300 --movementRadius 3 --movementSpeed 0.5 --timeoutSec 240 --verbose false

# Confirm at least two Town rooms exist.
Invoke-RestMethod http://127.0.0.1:8090/api/rooms | ConvertTo-Json -Depth 10
```

Setup:

- Start the server.
- Create at least two Town channels with the PowerShell setup above.
- Enter Town with one Unity client.
- Move from Lobby into MyRoom.

Steps:

1. Open the channel UI with `Esc`.
2. Select a different non-full Town channel.
3. Wait until the target channel re-entry completes.

PowerShell verification:

```powershell
Invoke-RestMethod http://127.0.0.1:8090/api/rooms | ConvertTo-Json -Depth 10
Invoke-RestMethod http://127.0.0.1:8090/api/players/online | ConvertTo-Json -Depth 10
```

Expected:

- Channel move succeeds.
- Unity remains in MyRoom state after re-entry.
- MyRoom camera and visibility policy remain active.
- Other private players are absent from the MyRoom spawn/move stream; any stale client object remains hidden defensively.
- The player is not snapped to Town Lobby bounds.

### TC-CH-003 Current channel move is rejected

PowerShell setup:

```powershell
# Optional: inspect the Unity client's current Town room before testing current-channel rejection.
Invoke-RestMethod http://127.0.0.1:8090/api/rooms | ConvertTo-Json -Depth 10
Invoke-RestMethod http://127.0.0.1:8090/api/players/online | ConvertTo-Json -Depth 10
```

Setup:

- Enter Town with one Unity client.
- Open the channel UI after `/api/rooms` is available.

Steps:

1. Try to move to the currently selected Town channel, or force a `C_ChannelMove` request with the current room id.

PowerShell verification:

```powershell
Invoke-RestMethod http://127.0.0.1:8090/api/players/online | ConvertTo-Json -Depth 10
```

Expected:

- Server rejects the request with `AlreadyInChannel`.
- `S_ChannelMove.Success` is `False`.
- `currentRoomId` remains unchanged.
- Unity keeps the current channel row marked as current.
- No remote objects are cleared for the rejected request.

### TC-CH-004 Full target channel is rejected

PowerShell setup:

```powershell
# Start this in a separate PowerShell window and keep it running during the Unity test.
dotnet run --project "E:\task\Server\Server\DummyClient\DummyClient.csproj" -- --scenario town-load --clients 50 --host 127.0.0.1 --port 8080 --prefix PD_CH_FULL --delayMs 10 --holdInTownSec 180 --enableMovement true --movementPattern patrol --patrolMode mixed --movementIntervalMs 300 --movementRadius 3 --movementSpeed 0.5 --timeoutSec 240 --verbose false

# Confirm one Town channel is at or near capacity before attempting the move.
Invoke-RestMethod http://127.0.0.1:8090/api/rooms | ConvertTo-Json -Depth 10
```

Setup:

- Start the server.
- Fill a target Town channel to capacity with the PowerShell setup above.
- Enter Town with one Unity client in a different channel.

Steps:

1. Open the channel UI with `Esc`.
2. Attempt to move to the full Town channel.

PowerShell verification:

```powershell
Invoke-RestMethod http://127.0.0.1:8090/api/rooms | ConvertTo-Json -Depth 10
Invoke-RestMethod http://127.0.0.1:8090/api/players/online | ConvertTo-Json -Depth 10
```

Expected:

- Move button shows the full/unavailable state.
- If a request is still sent, server rejects it with `TargetFull` or target-unavailable reason.
- `S_ChannelMove.Success` is `False`.
- Local player stays in the original room.
- No position, area state, or remote-object state is reset.

### TC-CH-005 Invalid or stale target room is rejected

PowerShell setup:

```powershell
# Capture current valid room ids, then use a known invalid target id from a packet/debug client.
Invoke-RestMethod http://127.0.0.1:8090/api/rooms | ConvertTo-Json -Depth 10
```

Setup:

- Enter Town with one Unity client.
- Prepare an invalid, removed, or non-Town room id for `C_ChannelMove`.

Steps:

1. Send `C_ChannelMove` with the invalid/stale target room id from Unity debug tooling or a packet test client.

PowerShell verification:

```powershell
Invoke-RestMethod http://127.0.0.1:8090/api/players/online | ConvertTo-Json -Depth 10
Invoke-RestMethod http://127.0.0.1:8090/api/rooms | ConvertTo-Json -Depth 10
```

Expected:

- Server rejects the request with `TargetUnavailable`, `InvalidTarget`, `RoomChanged`, or equivalent reason.
- `S_ChannelMove.Success` is `False`.
- Reason text identifies invalid, missing, stale, or non-Town target.
- Local player stays in the original Town room.
- Server does not reserve target capacity for the failed request.

### TC-CH-006 Bakal channel move is rejected

PowerShell setup:

```powershell
# Use three dummy clients so one visible Unity client can complete a 4-player Bakal party.
dotnet run --project "E:\task\Server\Server\DummyClient\DummyClient.csproj" -- --scenario fill-visible --clients 3 --expectedExternal 1 --host 127.0.0.1 --port 8080 --prefix PD_Dummy --delayMs 100 --sceneReadyDelayMs 100 --holdAfterDungeonSec 120 --timeoutSec 180 --verbose false

# Confirm the visible Unity client and dummy clients enter Bakal.
Invoke-RestMethod http://127.0.0.1:8090/api/rooms | ConvertTo-Json -Depth 10
```

Setup:

- Enter Bakal through normal matching using the PowerShell setup above.

Steps:

1. Attempt to open channel UI or send `C_ChannelMove` while in Bakal.

PowerShell verification:

```powershell
Invoke-RestMethod http://127.0.0.1:8090/api/rooms | ConvertTo-Json -Depth 10
Invoke-RestMethod http://127.0.0.1:8090/api/players/online | ConvertTo-Json -Depth 10
```

Expected:

- Channel movement is unavailable outside Town.
- If a packet is sent, server rejects it with `NotInTown`.
- Player remains in the Bakal room.
- Dungeon movement/combat state is not reset by the rejected request.
## Remaining Development Candidates - 2026-07-09

Recommended next order:

1. Per-area named bounds v2
   - Split exported Tilemap bounds into named gameplay areas: MyRoom, Lobby, Bakal combat lane, Bakal patrol zone.
   - Use those named areas for spawn, portal eligibility, dummy patrol, enemy patrol, and local movement clamp.

2. Optional original-channel affinity
   - Dungeon-clear return is implemented: the current server selects an available Town room and always enters private MyRoom.
   - Store the pre-dungeon Town channel only if returning to that exact social channel becomes a product requirement.

3. Monster attack authority v2
   - Revisit enemy attack windows after patrol/bounds are stable.
   - Keep patrol/meteor behavior, then add server-authoritative enemy melee only if it feels readable in Unity.

4. Optional channel movement transition polish
   - Same-scene channel movement works now.
   - Add a short loading/input-lock overlay only if the visual swap still feels abrupt.

5. AOI Spatial Hash / Grid
   - Current AOI is acceptable for validated 50/100 client local tests.
   - Move to grid lookup only when larger Town channel targets are needed.



