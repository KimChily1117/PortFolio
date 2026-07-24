# Milestones

This folder keeps Project Dawn milestone candidates as separate files so another developer or AI assistant can inspect one direction at a time.

## Current Baseline

The current stable baseline includes:

- TCP login/session flow
- Proto UDP movement sync with sequence validation and explicit TCP compatibility mode
- RoomTransfer with `C_SceneReady`
- MatchManager with `MatchTicket`, `IMatchQueueStore`, and `InMemoryMatchQueueStore`
- MatchCondition v1 RequiredEquipment
- DummyClient `create-players`, `fill-visible`, and `fill-dummy`
- Multi GameRoom foundation and multi Bakal room support
- RoomSnapshot DTOs and cached snapshots
- Monitoring API on `http://127.0.0.1:8090`
- WebLauncher dashboard for room, online player, matching queue, recent event, and reject reason visibility
- Combat Authority v2 for basic attack cooldown/action-lock and validated hit flow
- MatchHistory persistence for durable match/member records
- Reward/drop toast and server reward logs
- Town Channel v1, Town AOI v1, and Client Visibility / LOD v1
- Authoritative private MyRoom spawn/AOI isolation, public Lobby transition, and dungeon-clear return to MyRoom
- Tilemap Movement Bounds v1 for Town/Bakal authored movement regions
- Unity Channel UI/UX + Bakal Loading Screen
- Manual Town Channel Movement v2 with same-scene re-entry and position preservation
- Bakal prefab root/combat-anchor alignment at `(+/-1.0,-1.1280002)`

## Completed Milestones

- [Combat Authority v2](combat-authority-v2.md)
- [MatchHistory / DungeonRun Persistence](match-history-dungeon-run.md)
- [Monitoring API Polish](monitoring-api-polish.md)
- [Dungeon Dummy / Tilemap Movement Bounds](dungeon-dummy-movement-bounds.md)
- [Unity Channel UI/UX + Bakal Loading Screen](unity-channel-loading-ux.md)

## Candidate Milestones

No candidate is required before portfolio demo. Optional roadmap if development resumes:

1. Per-area named bounds v2 for MyRoom/Lobby/dungeon combat sub-regions
2. Optional exact original-channel affinity after dungeon return
3. Monster attack authority v2
4. AOI Spatial Hash / Grid, if larger Town channel sizes become necessary
5. [Demo UX Polish](demo-ux-polish.md)
6. [Separate ASP.NET Core Web API](aspnet-web-api.md)
7. [Redis MatchQueueStore](redis-match-queue-store.md)

## Selection Guide

Choose based on the story to emphasize next:

- Observability and demo control: Monitoring API Polish
- Durable backend data: MatchHistory / DungeonRun Persistence
- Backend API portfolio: Separate ASP.NET Core Web API
- Distributed queue/storage: Redis MatchQueueStore
- Game-server authority: Combat Authority v2
- Presentation quality: Demo UX Polish
- User-facing scene transition polish: Unity Channel UI/UX + Bakal Loading Screen

## Global Non-Goals

- Do not replace GameServer TCP/UDP flow with HTTP.
- Do not expose live `ClientSession`, UDP tokens, or mutable `GameRoom` dictionaries through APIs.
- Do not store live session, room, movement, or combat state in Redis.
- Do not add UDP skills before TCP skill validation and cooldown policy are stable.
- Do not remove the explicit TCP movement compatibility mode yet, and do not describe it as automatic UDP failover.
- Do not remove `C_EnterGame` pending-transfer fallback until `C_SceneReady` remains stable across repeated tests.







