# Monitoring API Polish

## Goal

Make the current local Monitoring API more useful for repeated demos, debugging, and regression checks without changing gameplay flow.

## Current Foundation

Implemented now:

- `GET /api/health`
- `GET /api/server/status`
- `GET /api/rooms`
- `GET /api/rooms/{roomId}`
- `GET /api/players/online`
- `GET /api/matching/queue`
- WebLauncher cards for rooms, online players, and matching queue

Detailed current API docs: [Monitoring API](../monitoring-api.md)

## First Scope

Add read-only recent event visibility:

- match request accepted/rejected events
- match reject reason counts
- party matched events
- transfer started events
- dungeon entered events
- room created/removed events if useful for multi-room demos

Add WebLauncher display for recent events:

- small recent events list
- reject reason summary
- party/transfer timeline for demo verification

## Implementation Notes

Recommended model:

- add bounded in-memory event buffer
- store plain DTO/event records only
- keep event count capped
- expose through one read-only endpoint such as `GET /api/events/recent`
- keep event writes lightweight and non-blocking

Candidate endpoint:

```http
GET /api/events/recent
```

Possible response shape:

```json
{
  "snapshotUpdatedAtUtc": "...",
  "count": 20,
  "events": [
    {
      "utcTime": "...",
      "type": "PartyMatched",
      "message": "Party matched for Bakal",
      "queueKey": "Bakal:Bakal:L0:M1000",
      "partyId": 3,
      "roomId": 5
    }
  ]
}
```

## Acceptance Criteria

- Existing endpoints continue working.
- Monitoring API remains read-only.
- WebLauncher continues to work if the new event endpoint is unavailable.
- Event buffer cannot grow unbounded.
- Events do not expose UDP tokens, internal auth tokens, or live object references.
- `fill-visible` queue wait and `fill-dummy` multi-room runs can be inspected from API/WebLauncher.

## Risks

- Event logging can become noisy.
- Event buffer access needs thread-safe handling.
- Avoid turning API DTOs into direct references to live server objects.

## Good Verification Commands

```powershell
Invoke-RestMethod http://127.0.0.1:8090/api/server/status | ConvertTo-Json -Depth 6
Invoke-RestMethod http://127.0.0.1:8090/api/matching/queue | ConvertTo-Json -Depth 10
Invoke-RestMethod http://127.0.0.1:8090/api/rooms | ConvertTo-Json -Depth 10
```

## Completion Status

Status: Completed on 2026-07-06.

Implemented and verified:

- `GET /api/events/recent` read-only endpoint.
- Bounded recent event buffer with `MaxEvents=300`.
- Event types: `MatchAccepted`, `MatchRejected`, `PartyMatched`, `TransferStarted`, `DungeonEntered`, `RoomCreated`, `RoomRemoved`, `SkillCastRejected`.
- `rejectReasonCounts` summary sourced from `CombatRejectMetrics`.
- WebLauncher Recent Events card.
- WebLauncher Reject Reasons card.
- Graceful degradation when optional monitoring endpoints are unavailable.
- Protocol files were not changed.

Verification notes:

- Existing endpoints remained healthy: `/api/health`, `/api/server/status`, `/api/rooms`, `/api/players/online`, `/api/matching/queue`.
- `fill-visible --clients 3 --expectedExternal 1` shows waiting-player `MatchAccepted` events.
- `fill-dummy --clients 8 --partySize 4 --expectedRooms 2` shows party, transfer, dungeon entry, and room events.
- A non-`PD_Dummy` no-gear match request shows `MatchRejected` with `MissingRequiredEquipment`.
- Fast attack tests produce `SkillCastRejected` events and reject reason counters.
- Event count remains capped at `300`.

Current checklist: [Current Status and Test Checklist](../current-status-and-test-checklist.md)
