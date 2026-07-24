# Monitoring API and Web API Roadmap

This document separates the API work that is already implemented from the future ASP.NET Core Web API direction.

## Current Implemented State

Project Dawn now includes a local read-only Monitoring API inside the GameServer process.

Implemented:

- API host: `Server/Server/Monitoring/MonitoringApiHost.cs`
- Transport: `HttpListener`
- Default URL: `http://127.0.0.1:8090`
- Target framework remains `netcoreapp3.1`
- No ASP.NET Core/Kestrel dependency has been added.
- Existing TCP `8080` and UDP `8081` gameplay ports are unchanged.
- WebLauncher consumes this API from static browser JavaScript.

Supported endpoints:

```http
GET /api/health
GET /api/server/status
GET /api/rooms
GET /api/rooms/{roomId}
GET /api/players/online
GET /api/matching/queue
```

The API is intentionally read-only. It exposes snapshots and summary DTOs, not live mutable collections.

Detailed current implementation notes: [Monitoring API](monitoring-api.md)

## Current Snapshot Boundaries

The Monitoring API can expose:

- server health and uptime
- room counts and room update metrics
- cached room snapshots
- player/enemy positions from room snapshots
- online player/session visibility
- transfer state summaries
- current in-memory matching queue visibility

The Monitoring API must not expose:

- UDP login tokens
- internal auth/session tokens
- mutable `GameRoom` dictionaries
- live `ClientSession` references
- mutable inventory objects
- write/admin gameplay operations

## GameServer Ownership

GameServer still owns:

- TCP session lifecycle
- UDP movement registration and movement sync
- login/session runtime state
- GameRoom player/enemy object lifecycle
- RoomTransfer and `C_SceneReady` validation
- MatchManager queue, party creation, and dungeon transfer
- real-time packet send/broadcast paths

The Monitoring API reads snapshots around those systems. It does not replace TCP/UDP gameplay flow.

## Why Keep a Future ASP.NET Core Web API Roadmap

The current Monitoring API is enough for local demo visibility and WebLauncher dashboards. A separate ASP.NET Core Web API can still be useful later for broader backend portfolio coverage.

A future separate Web API could provide:

- DB-backed player/account lookup
- DB-backed equipment inspection
- persisted match history endpoints
- persisted dungeon run history endpoints
- admin metrics and event history
- Redis-backed read-model visibility after Redis is introduced
- cleaner production-style API hosting, configuration, and middleware

That future API should remain read-oriented at first and should not bypass GameServer ownership of live gameplay state.

## Future Web API v1 Candidate

Candidate endpoints:

```http
GET /health
GET /api/status
GET /api/players/{playerName}
GET /api/players/{playerName}/equipment
GET /api/matches/recent
GET /api/dungeon-runs/recent
```

First scope:

- separate ASP.NET Core Web API project
- read-only DB-backed player/equipment endpoints
- no live `ClientSession` access
- no direct GameRoom mutation
- no Protocol changes
- no Unity changes

## Future Web API v2 Candidate

Persist match and dungeon history, then expose it through read-only endpoints.

Candidate data concepts:

- `MatchHistory`
- `DungeonRun`
- party members
- queue key
- target room type/id
- transfer id
- created/started/completed timestamps
- result and failure reason

Candidate endpoints:

```http
GET /api/matches/recent
GET /api/players/{playerName}/matches
GET /api/dungeon-runs/recent
GET /api/dungeon-runs/{runId}
```

## Future Redis Visibility

After a Redis-backed `IMatchQueueStore` exists, a future API can expose read-only Redis queue state.

Candidate endpoints:

```http
GET /api/admin/redis/status
GET /api/admin/redis/match-queues
GET /api/admin/redis/match-tickets/{ticketId}
```

Redis should still not store live `ClientSession`, GameRoom object state, or UDP movement state.

## Recommended Order From Current State

1. Keep the current Monitoring API stable.
2. Use WebLauncher and DummyClient tests to verify room, online player, and queue snapshots.
3. Add MatchHistory / DungeonRun persistence if durable demo history is needed.
4. Add DB-backed read endpoints in a separate Web API only after the history/player inspection scope is selected.
5. Add Redis-backed queue storage only after the in-memory queue and snapshot API remain stable.

## Current Non-Goals

- Do not replace the GameServer TCP/UDP flow with HTTP.
- Do not add write endpoints for live gameplay state.
- Do not expose internal session/token data.
- Do not add ASP.NET Core just to duplicate the current Monitoring API.
- Do not store real-time room state, UDP movement, or live sessions in Redis.
