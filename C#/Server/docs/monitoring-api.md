# Monitoring API

This document records the current local read-only Monitoring API implemented inside the Project Dawn GameServer.

## Purpose

The Monitoring API gives the WebLauncher and local developer tools visibility into the running server without changing the TCP/UDP gameplay protocol.

It is intended for local demo dashboards, room and dungeon visibility, online player/session inspection, matching queue inspection, quick health checks, and regression test verification.

It is not a gameplay API and does not mutate server state.

## Implementation

Current implementation:

- Host: `Server/Server/Monitoring/MonitoringApiHost.cs`
- Transport: `HttpListener`
- Default URL: `http://127.0.0.1:8090`
- JSON serializer: `System.Text.Json`
- CORS: permissive for local WebLauncher development
- Methods: `GET` and `OPTIONS`
- Target framework: still `netcoreapp3.1`

The API starts from `Server/Server/Program.cs` alongside the normal TCP and UDP listeners.

Existing ports:

```text
TCP gameplay: 8080
UDP movement: 8081
Monitoring API: 8090
```

## Endpoints

### GET /api/health

Returns basic process/API health.

```powershell
Invoke-RestMethod http://127.0.0.1:8090/api/health
```

### GET /api/server/status

Returns server summary: online flag, uptime, room counts, player/enemy totals, online player count, matching queue counts, room update metrics, server name, and API version.

```powershell
Invoke-RestMethod http://127.0.0.1:8090/api/server/status | ConvertTo-Json -Depth 6
```

### GET /api/rooms

Returns cached `RoomSnapshot` data for all rooms, including room metadata, counts, boss summary, player/enemy positions, and room update metrics.

```powershell
Invoke-RestMethod http://127.0.0.1:8090/api/rooms | ConvertTo-Json -Depth 10
```

### GET /api/rooms/{roomId}

Returns one cached `RoomSnapshot`.

```powershell
Invoke-RestMethod http://127.0.0.1:8090/api/rooms/2 | ConvertTo-Json -Depth 10
```

### GET /api/players/online

Returns current online player/session visibility: session id, object id, player db id, name, current room, transfer state, pending room/transfer ids, HP, position, and current state.

```powershell
Invoke-RestMethod http://127.0.0.1:8090/api/players/online | ConvertTo-Json -Depth 10
```

### GET /api/matching/queue

Returns current in-memory matching queue visibility: queue key, dungeon/room type, waiting count, party size, waiting player names, level/MMR, equipment readiness, and waiting seconds.

```powershell
Invoke-RestMethod http://127.0.0.1:8090/api/matching/queue | ConvertTo-Json -Depth 10
```


### GET /api/events/recent

Returns a bounded recent-event snapshot for local demo/debug visibility.

The buffer is in-memory, thread-safe, capped at `300` events, and stores snapshot DTOs only. It does not query MatchHistory DB and does not expose live server objects.

Current event types:

- `MatchAccepted`
- `MatchRejected`
- `PartyMatched`
- `TransferStarted`
- `DungeonEntered`
- `RoomCreated`
- `RoomRemoved`
- `SkillCastRejected`

Response shape:

```json
{
  "snapshotUpdatedAtUtc": "...",
  "count": 42,
  "maxEvents": 300,
  "rejectReasonCounts": {
    "ActionLocked": 48,
    "CooldownActive": 12
  },
  "events": [
    {
      "type": "DungeonEntered",
      "partyId": 3,
      "roomId": 4,
      "transferId": 2,
      "occurredAtUtc": "...",
      "detail": "PartyId=3, RoomId=4, TransferId=2"
    }
  ]
}
```

```powershell
Invoke-RestMethod http://127.0.0.1:8090/api/events/recent | ConvertTo-Json -Depth 12
```
## Snapshot Model

The API uses snapshots instead of live object references.

- Room snapshots: `GameRoom.CreateSnapshot()`, `RoomManager.GetRoomSnapshots()`, `RoomManager.GetRoomSnapshot(roomId)`
- Online player snapshots: `SessionManager.CreateOnlinePlayersSnapshot()`, `OnlinePlayerSnapshot.FromSession(...)`
- Matching queue snapshots: `MatchManager.CreateQueueSnapshot()`, `IMatchQueueStore.CreateSnapshot(...)`

This keeps WebLauncher and API code away from mutable room/session dictionaries.

## WebLauncher Integration

WebLauncher reads the Monitoring API from static browser JavaScript.

Current dashboard cards:

- server status
- rooms
- online players
- matching queue
- recent events
- reject reason summary
- game launch/copy command controls

WebLauncher notes: [WebLauncher README](../WebLauncher/README.md)

## Verification Commands

Basic API verification:

```powershell
Invoke-RestMethod http://127.0.0.1:8090/api/health
Invoke-RestMethod http://127.0.0.1:8090/api/server/status | ConvertTo-Json -Depth 6
Invoke-RestMethod http://127.0.0.1:8090/api/rooms | ConvertTo-Json -Depth 10
Invoke-RestMethod http://127.0.0.1:8090/api/players/online | ConvertTo-Json -Depth 10
Invoke-RestMethod http://127.0.0.1:8090/api/matching/queue | ConvertTo-Json -Depth 10
```

Queue visibility test:

```powershell
dotnet run --project "E:\task\Server\Server\DummyClient\DummyClient.csproj" -- --scenario fill-visible --clients 3 --expectedExternal 1 --host 127.0.0.1 --port 8080 --prefix PD_Dummy --delayMs 100 --sceneReadyDelayMs 100 --holdAfterDungeonSec 120 --verbose true
```

Expected:

- `/api/matching/queue` shows `totalWaitingPlayers=3`
- WebLauncher Matching Queue card shows `PD_Dummy_0001` through `PD_Dummy_0003`

Multi-room visibility test:

```powershell
dotnet run --project "E:\task\Server\Server\DummyClient\DummyClient.csproj" -- --scenario fill-dummy --clients 8 --partySize 4 --expectedRooms 2 --host 127.0.0.1 --port 8080 --prefix PD_Dummy --delayMs 100 --sceneReadyDelayMs 100 --holdAfterDungeonSec 180 --enableMovement true --enableAttack false --enableCollision false --movementPattern patrol --patrolMode mixed --patrolRadius 1.5 --patrolIntervalMs 500 --patrolSpeed 0.35 --verbose true
```

Expected:

- `/api/players/online` shows `count=8`
- `/api/matching/queue` shows `totalWaitingPlayers=0`
- `/api/rooms` shows one Town room and two Bakal rooms
- WebLauncher room cards show separate dungeon rooms

## Security and Scope Notes

The current API is for localhost development monitoring.

Do not expose UDP tokens, internal auth/session tokens, mutable live collections, admin write operations, or raw DB connection details.

Before exposing beyond localhost, add an explicit auth and deployment plan.

## Full Client/Server Checklist

See [Current Status and Test Checklist](current-status-and-test-checklist.md) for server startup, Unity client launch, WebLauncher checks, DummyClient scenarios, MatchHistory DB checks, and pass/fail items.

