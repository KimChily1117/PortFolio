# Dungeon Dummy Movement Bounds

## Goal

Make dungeon load, combat, and future Monster AI tests deterministic by keeping DummyClient movement inside configured dungeon map bounds.

## Scope

- Add DummyClient dungeon movement bounds for match scenarios (`fill-visible`, `fill-dummy`).
- Keep Town load behavior unchanged so Town Channel/AOI/LOD scalability tests still use the existing patrol radius flow.
- Support CLI override for bounds so each dungeon can tune its playable area without code changes.
- Clamp patrol, follow-enemy, in-range strafe, and idle movement before sending `C_Move`.
- Expose a clamp counter in DummyClient logs so out-of-bounds corrections are visible during tests.

## Implemented - 2026-07-08

- `Server/DummyClient/DummyClientOptions.cs`
  - Added `--useDungeonMovementBounds true|false`.
  - Added `--dungeonBounds minX,minY,maxX,maxY` plus individual `--dungeonMinX`, `--dungeonMaxX`, `--dungeonMinY`, `--dungeonMaxY` overrides.
  - Default Bakal test bounds: `-8,-4.5,8,4.5`.
- `Server/DummyClient/DummyClient.cs`
  - Intersects patrol radius with dungeon bounds after dungeon entry.
  - Applies final movement clamp before every `C_Move` send.
  - Logs `DungeonBounds=...` at gameplay start and `DungeonBoundsClamp=...` at gameplay completion.

## Deferred

This milestone originally preceded Channel Movement v2. Manual channel switching, server capacity validation, and same-scene re-entry are now implemented. Dungeon-clear Town return is also implemented and always re-enters private MyRoom; only exact original-channel affinity remains optional follow-up work.

## Non-Goals

- Do not change Protocol.
- Do not change server movement authority.
- Do not change Town Channel/AOI behavior.
- Do not implement Monster AI in this milestone.
- Do not add Unity channel switching in this milestone.

## Validation

Build:

```text
dotnet build "E:\task\Server\Server\DummyClient\DummyClient.csproj"
```

Smoke test with a running server:

```text
dotnet run --project "E:\task\Server\Server\DummyClient\DummyClient.csproj" -- --scenario fill-dummy --clients 4 --partySize 4 --host 127.0.0.1 --port 8080 --prefix PD_Dummy --delayMs 100 --sceneReadyDelayMs 100 --holdAfterDungeonSec 5 --enableMovement true --movementPattern patrol --patrolMode mixed --movementRadius 20 --movementSpeed 2 --gameplayDurationSec 5 --verbose true
```

Expected evidence:

```text
- Gameplay started contains DungeonBounds=Enabled[-8.00,-4.50..8.00,4.50].
- Verbose movement positions stay within X [-8, 8], Y [-4.5, 4.5].
- Gameplay completed contains DungeonBoundsClamp=N.
- Result remains PASS when the server and DB are healthy.
```

Override example:

```text
--dungeonBounds -6,-3,6,3
```

Disable example for comparison:

```text
--useDungeonMovementBounds false
```

## Follow-Up

1. Monster AI / bounded combat simulation.
2. Per-dungeon named bounds if more dungeon scenes are added.
3. Channel Movement v2 after the client/server contract is approved.

## Tilemap Movement Bounds Integration - 2026-07-09

Problem:

```text
DummyClient dungeon movement was bounded only by CLI rectangles. After Unity exported MovementBounds.json from the Bakal patrol/walkable Tilemap, dummy clients should use the same authored movement area as server-side Bakal patrol.
```

Change:

```text
- Added DummyMovementBoundsProvider in DummyClient.
- DummyClient loads MovementBounds.json using the same JSON schema as the server.
- If a RoomType map exists, dungeon movement clamp uses the exported walkable spans first.
- If MovementBounds.json is missing or invalid, DummyClient falls back to the existing --dungeonBounds rectangle.
- Patrol, follow-enemy, and strafe all pass through the shared SendMove clamp path.
- No Protocol change.
```

Expected logs:

```text
[DUMMY][MOVEMENT_BOUNDS] Map loaded. RoomType=Bakal, SourceTilemap=EnemyPatolTileMap, CoordinateBasis=CombatAnchor, HasTileCount=25, SpanCount=6, Bounds=(-7.00,-1.73)..(8.00,3.27)
[DUMMY][BOUNDS] Name=..., RoomType=Bakal, Pos=(...), Bounds=Tilemap[...], ClampCount=...
```

Validation command:

```powershell
dotnet run --project "E:\task\Server\Server\DummyClient\DummyClient.csproj" -- --scenario fill-dummy --clients 4 --partySize 4 --host 127.0.0.1 --port 8080 --prefix PD_Dummy --delayMs 100 --sceneReadyDelayMs 100 --holdAfterDungeonSec 20 --enableMovement true --movementPattern patrol --patrolMode mixed --movementRadius 8 --movementSpeed 2 --gameplayDurationSec 20 --verbose true
```

Acceptance check:

```text
- DummyClient logs MovementBounds.json load once.
- Gameplay started log shows DungeonBounds=Tilemap[...].
- Any clamp logs use Bounds=Tilemap[...], not only the rectangle fallback.
- Dummy final LastPosition stays inside exported MovementBounds.json spans.
```

## Town Tilemap Movement Bounds - 2026-07-09

Problem:

```text
Town demo/load-test movement can still drift into invalid map space if dummy clients rely only on radius patrol around spawn. This caused demo-stage movement issues even after Bakal bounds were stabilized.
```

Change:

```text
- MovementBoundsExporter now preserves existing maps in MovementBounds.json and replaces only the same roomType entry.
- This allows exporting both Bakal and Town into one MovementBounds.json file.
- DummyClient now applies tilemap movement bounds during town-load as well as dungeon gameplay.
- Town-load uses RoomType.Town, so it will use the Town map entry if exported.
- Missing Town entry falls back to the existing rectangle/radius behavior.
- No Protocol change.
```

Unity export flow:

```text
1. Open Bakal scene, select Bakal walkable/patrol Tilemap, Room Type=Bakal, Export.
2. Open Town scene, select Town walkable Tilemap, Room Type=Town, Coordinate Basis=CombatAnchor, Export.
3. Confirm MovementBounds.json contains both maps: Bakal and Town.
```

Town validation command:

```powershell
dotnet run --project "E:\task\Server\Server\DummyClient\DummyClient.csproj" -- --scenario town-load --clients 20 --host 127.0.0.1 --port 8080 --prefix PD_Dummy --delayMs 50 --holdInTownSec 30 --enableMovement true --movementPattern patrol --patrolMode mixed --movementRadius 20 --movementSpeed 2 --verbose true
```

Expected logs:

```text
[DUMMY][MOVEMENT_BOUNDS] Map loaded. RoomType=Town, SourceTilemap=..., CoordinateBasis=CombatAnchor, HasTileCount=..., SpanCount=...
Town load started. ... Bounds=Tilemap[...]
[DUMMY][BOUNDS] ... RoomType=Town ... Bounds=Tilemap[...]  // only when clamp occurs
```

## Tilemap Bounds Enforcement for Spawn and Movement - 2026-07-09

Town/Bakal movement bounds are now treated as the shared authored source for demo movement instead of only a visual/editor guide.

Implemented scope:
- Server `MovementBoundsProvider` now supports nearest-span clamp in addition to random point sampling and walkable checks.
- Server `GameRoom.HandleMove` applies Town movement bounds after a player is already inside public lobby bounds. This prevents lobby players and dummies from walking outside `TownWalkableTileMap`, while preserving the current private MyRoom coordinate area because it is intentionally outside the public Town tilemap.
- Server private/public state is explicit through `Player.IsInPublicTownArea`. Initial login and dungeon return start at `(0,0)` / `false`; Town AOI excludes private players.
- The first MyRoom -> Lobby request is checked against the Town walkable map and `x >= 18`, then allowed to bypass only the UDP speed-distance check for the portal teleport.
- DummyClient town-load fallback bounds now match the exported Town tilemap rectangle (`x=18..33`, `y=-4..0`) and still prefers `MovementBounds.json` spans when available.
- Unity client added `MovementBoundsRuntime`, loading `Resources/Data/MovementBounds.json` for local movement checks.
- `MyPlayer` walk/run/jump movement now clamps locally against Town bounds while in public Town lobby, except while `TownScene` is in `SERIAROOM` state. Bakal player clamp is intentionally deferred until the Bakal exported bounds are rechecked.
- `OtherPlayer` movement is handled through server-authoritative `S_Move`; no separate client-side prediction is added for remote players.

Validation:
- `dotnet build Server\Server\Server.csproj` passed.
- `dotnet build Server\DummyClient\DummyClient.csproj` passed.

Expected logs during validation:
- Client loads maps: `[MOVEMENT_BOUNDS] Map loaded. Scene=Town ...`
- Local wall clamp: `[CLIENT][MOVE_BOUNDS_CLAMP] ...`
- Server final correction: `[MOVE][BOUNDS_CLAMP] RoomType=Town ...`
- Dummy bounds source: `Town load started ... Bounds=Tilemap[...]`

Notes:
- If MyRoom should also become bounded by tilemap data later, export it as a separate authored area or extend the data model with sub-areas. For now authoritative MyRoom remains exempt so `(0,0)` is not snapped into the public Town map.
