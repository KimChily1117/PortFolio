# Combat test gather hotkey

Date: 2026-07-28

## Usage

1. Stop and restart the Debug C# server after rebuilding it.
2. Start two clients with `Binaries/Client_FireballV7_CombatTest.exe` or the current `Binaries/Client.exe`.
3. Wait until both players have entered Room 0.
4. Focus either game viewport and press `F8` once.
5. Every connected player in that Room is moved into distinct adjacent Walkable Cells around the requesting player.

The players intentionally do not occupy the exact same Cell. Exact overlap makes target picking and melee attack testing unreliable. Adjacent Cell centers keep both players in the same combat space and within close-range attack distance.

## Authority and safety

- The client sends the existing `C_TEST_MSG` with command `combat-gather-v1`; no protobuf or packet ID was added.
- The server derives the requester from `ClientSession.Player` and never accepts an ObjectId from the client.
- The command executes in the Room JobQueue and rechecks Session, Player, Room, disconnection, and Room membership.
- The requester's current Walkable NavGrid Cell is the anchor.
- A bounded deterministic four-direction search selects distinct nearby Walkable or Slow Cells. Blocked and out-of-bounds Cells are excluded.
- Existing movement states are cancelled only after every destination has been validated.
- Server position, TileX/TileZ, legacy Tilemap occupancy, and Object state are updated together.
- Each moved player receives a new unique ServerMoveId and an `ARRIVED` `S_MovementSnapshot`; local and remote clients therefore snap to the same authoritative result.
- Rooms with fewer than two players are rejected without moving the connected player.

## Build policy

Debug server builds enable this command automatically.

Release server builds reject it unless the process is started with:

```powershell
$env:D3D_ENABLE_TEST_COMMANDS='1'
```

Do not enable this environment variable in a production deployment.

## Verification

- DirectX client Debug x64 sequential Rebuild: success.
- C# server Debug alternate-output build: success.
- C# server Release build: success.
- Full C# NavGrid, movement, pathfinding, combat, and gather regression: 432 assertions passed.
- Gather-specific checks cover two-player success, distinct adjacent Walkable Cells, authoritative Y preservation, Idle reset, two final Snapshots, unique ServerMoveIds, and one-player rejection.