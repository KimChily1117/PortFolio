# Next Milestones

This file is the milestone index. Detailed milestone notes are split under [docs/milestones](milestones/README.md) so each direction can be reviewed independently.

## Current Stable Baseline

Completed and available:

- TCP login/session flow.
- UDP movement sync v1 with Proto UDP hello/move, non-zero sequence validation, and explicit TCP compatibility mode.
- RoomTransfer with `C_SceneReady` and `PendingRoomId` based target-room entry.
- MatchManager with `MatchTicket`, `IMatchQueueStore`, and `InMemoryMatchQueueStore`.
- MatchCondition v1 RequiredEquipment.
- TestEquipmentSetupTool.
- DummyClient `create-players`, `fill-visible`, and `fill-dummy`.
- DummyClient optional TCP movement, attack, follow-enemy, collision, and reward observation.
- Multi GameRoom foundation with explicit `RoomType`, room metrics, and multi Bakal room support.
- RoomSnapshot DTOs and cached room snapshots.
- Local read-only Monitoring API on `http://127.0.0.1:8090`.
- Monitoring endpoints for health, server status, rooms, online players, matching queue, and recent events.
- Static WebLauncher dashboard for rooms, online players, matching queue, recent events, reject reason counts, and game launch commands.
- Combat Authority v2 for basic attacks: active cast, duplicate-hit prevention, range, facing, line/depth, cooldown, action-lock, and combat reject counters.
- MatchHistory persistence with bounded async writer, durable match/member records, transfer timestamps, dungeon entry status, and warning-only DB write failure handling.
- Meteor ground-impact AABB client stabilization.
- Reward/drop toast and server reward logs.
- Disconnect / cleanup log polish.
- Town Channel v1 with automatic channel assignment and capacity reservation.
- Town AOI v1 with distance-based spawn/move/despawn filtering.
- Authoritative MyRoom/public Town state: private `(0,0)` spawn, `IsInPublicTownArea` AOI isolation, validated portal transition, and dungeon-clear return to private MyRoom.
- Manual Town Channel Movement v2 with approved C_ChannelMove/S_ChannelMove protocol, server-side capacity reservation, Unity ESC channel move UI, same-scene re-entry polish, stale-movement reset, and position preservation.
- Client Visibility / LOD v1 for remote players, validated at 50-client Town load with no visible FPS regression.
- Dungeon Dummy Movement Bounds v1 for bounded dummy movement in Bakal tests.
- Tilemap Movement Bounds v1: Unity-exported Town/Bakal bounds shared by DummyClient, server Town movement clamp, and local MyPlayer Town lobby clamp.
- Monster AI / bounded combat simulation for Bakal enemy patrol/meteor behavior; chase/melee targeting is currently de-emphasized after manual feel testing.
- Bakal root/combat-anchor alignment using the prefab-derived `(+/-1.0,-1.1280002)` Base/Shadow world offset.

## Completed Milestones

- [Combat Authority v2](milestones/combat-authority-v2.md)
- [MatchHistory / DungeonRun Persistence](milestones/match-history-dungeon-run.md)
- [Monitoring API Polish](milestones/monitoring-api-polish.md)
- [Dungeon Dummy / Tilemap Movement Bounds](milestones/dungeon-dummy-movement-bounds.md)
- [Unity Channel UI/UX + Bakal Loading Screen](milestones/unity-channel-loading-ux.md)

## Candidate Milestones

No candidate is required before portfolio demo. Optional roadmap if development resumes:

1. Per-area named bounds v2 for authored gameplay areas such as MyRoom, Lobby, Bakal combat lane, and boss patrol zone.
2. Optional original-channel affinity: the current dungeon-clear return is functional and private, but it does not restore the exact pre-dungeon Town channel.
3. Monster attack authority v2: server-side enemy attack windows and player damage validation after patrol/bounds feel is stable.
4. AOI Spatial Hash / Grid to reduce Town AOI lookup cost from per-move O(N) scanning if larger Town channel sizes are needed.
5. [Demo UX Polish](milestones/demo-ux-polish.md)
6. [Separate ASP.NET Core Web API](milestones/aspnet-web-api.md)
7. [Redis MatchQueueStore](milestones/redis-match-queue-store.md)
## Selection Guide

Choose based on the story to emphasize next:

- Observability and demo control: [Monitoring API Polish](milestones/monitoring-api-polish.md)
- Durable backend data: [MatchHistory / DungeonRun Persistence](milestones/match-history-dungeon-run.md)
- Backend API portfolio: [Separate ASP.NET Core Web API](milestones/aspnet-web-api.md)
- Distributed queue/storage: [Redis MatchQueueStore](milestones/redis-match-queue-store.md)
- Game-server authority: [Combat Authority v2](milestones/combat-authority-v2.md)
- Presentation quality: [Demo UX Polish](milestones/demo-ux-polish.md)

## Detailed Docs

- [Development Notes](dev-notes.md)
- [Monitoring API](monitoring-api.md)
- [DummyClient Regression and Demo Guide](dummyclient-regression.md)
- [Room Transfer Flow](room-transfer-flow.md)
- [Matchmaking Flow](matchmaking-flow.md)
- [Web API Roadmap](web-api-roadmap.md)
- [Current Status and Test Checklist](current-status-and-test-checklist.md)

## Global Non-Goals

- Do not replace GameServer TCP/UDP flow with HTTP.
- Do not expose live `ClientSession`, UDP tokens, or mutable `GameRoom` dictionaries through APIs.
- Do not store live session, room, movement, or combat state in Redis.
- Do not add UDP skills before TCP skill validation and cooldown policy are stable.
- Do not remove the explicit TCP movement compatibility mode yet, and do not describe it as automatic UDP failover.
- Do not remove `C_EnterGame` pending-transfer fallback until `C_SceneReady` remains stable across repeated tests.














