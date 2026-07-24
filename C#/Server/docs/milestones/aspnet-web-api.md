# Separate ASP.NET Core Web API

## Goal

Add a separate read-only ASP.NET Core Web API project for DB-backed player/equipment/history endpoints.

## Current Boundary

The current in-process Monitoring API already exposes live snapshot visibility through `HttpListener`.

A separate Web API should add value by reading durable DB data, not by duplicating the existing Monitoring API.

## First Scope

Candidate endpoints:

```http
GET /health
GET /api/status
GET /api/players/{playerName}
GET /api/players/{playerName}/equipment
```

If MatchHistory / DungeonRun persistence exists first, add:

```http
GET /api/matches/recent
GET /api/players/{playerName}/matches
GET /api/dungeon-runs/recent
GET /api/dungeon-runs/{runId}
```

## Implementation Notes

Recommended constraints:

- separate project from GameServer
- read-only first version
- DB-backed endpoints only
- no live `ClientSession` access
- no direct `GameRoom` access
- no Protocol changes
- no Unity changes
- separate config/connection string plan

## Acceptance Criteria

- API project starts independently.
- Health/status endpoint works.
- Player lookup reads from DB without requiring the player to be online.
- Equipment endpoint matches MatchCondition expectations.
- GameServer TCP/UDP flow remains unchanged.

## Risks

- Duplicating current Monitoring API without adding DB/history value.
- Connection string/config drift from GameServer.
- Accidentally reaching into live GameServer state from a separate process.

## Related Docs

- [Web API Roadmap](../web-api-roadmap.md)
- [Monitoring API](../monitoring-api.md)
