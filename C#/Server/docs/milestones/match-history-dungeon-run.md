# MatchHistory / DungeonRun Persistence

## Goal

Persist match and dungeon run outcomes to DB for durable history, debugging, and later API endpoints.

## Why This Matters

Current Monitoring API snapshots are live/read-only process state. They are useful for demos, but disappear after server restart.

Persistence would provide:

- recent match history
- dungeon run history
- player match history
- debugging evidence after a run ends
- backend portfolio value beyond live game traffic

## First Scope

Persist minimal match result data:

- match id or party id
- queue key
- member player ids/names
- target room type
- target room id
- transfer id
- created time
- transfer started time
- dungeon entered time if available
- result status
- failure reason if applicable

Keep persisted models free of:

- live `ClientSession`
- live `Player`
- live `GameRoom`
- UDP endpoint/token state

## Implementation Notes

Recommended approach:

- decide DB schema/migration first
- capture events at MatchManager / RoomTransfer boundaries
- avoid blocking hot paths with synchronous heavy work
- keep history writes small and explicit
- expose only read-only endpoints later

Possible data concepts:

```text
MatchHistory
MatchHistoryMember
DungeonRun
DungeonRunMember
```

## Acceptance Criteria

- A successful 4-player match creates one durable history record.
- A `fill-dummy --clients 8 --partySize 4` run creates two match/run records.
- Member names and target room ids are persisted accurately.
- Server still runs if history write fails, with clear warning logs.
- No live session/room objects are stored in DB models.

## Risks

- DB schema changes require deliberate migration handling.
- Writes inside match/transfer flow can add latency if done carelessly.
- Failure/result policy must be clear before expanding fields.

## Related Docs

- [Matchmaking Flow](../matchmaking-flow.md)
- [Room Transfer Flow](../room-transfer-flow.md)
- [Monitoring API](../monitoring-api.md)

## Completion Status

Status: Completed on 2026-07-06.

Implemented and verified:

- EF models: `MatchHistory` and `MatchHistoryMember`.
- Manual migration SQL for local GameDB.
- `MatchHistoryPersistence` worker using a bounded queue of `1024` events.
- Match lifecycle capture from party creation, transfer start, dungeon entry, and failure paths.
- Write failures are warning-only and do not block matching, transfer, or dungeon entry.
- Hot-path comparison against no-op persistence showed no visible delay in request-to-entry timing.
- Protocol files were not changed.

Validation notes:

- `fill-dummy --clients 4 --partySize 4` creates one durable match record and four member records.
- `fill-dummy --clients 8 --partySize 4 --expectedRooms 2` creates two records with different target room ids.
- DB outage tests confirmed the game flow continues and persistence failures are logged as warnings.
- Stored models contain only primitive/string/timestamp snapshot data, not live sessions, players, rooms, UDP endpoints, or tokens.
