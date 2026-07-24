# Project Dawn Development Notes

This note is for continuing the Unity client and C# server work from another PC or a fresh Codex session.

## Paths

- Unity client: `E:/task/C#/Project_Dawn`
- Server: `E:/task/Server`

## Current Direction

- Movement sync uses UDP for frequent position/state updates.
- Login, account/player loading, and skill/basic attack events stay on TCP.
- Normal Unity login initialization enables `UseUdpMovement=true`. The TCP `C_Move` path remains an explicit compatibility/test mode, not an automatic UDP failure fallback.
- Server-to-client movement replication remains the reliable TCP `S_Move` path after validated UDP ingress is adapted into `GameRoom.HandleMove`.
- `Protocol.proto` and generated `Protocol.cs` should not be changed unless explicitly planned.
- Server DB schema should not be changed for the current movement/skill work.

## Completed Movement Work

- Server issues a UDP token after successful TCP login.
- Unity receives the token through `S_Login.UdpToken`.
- Unity sends UDP hello automatically.
- Server maps the UDP endpoint to the existing `ClientSession`.
- Unity can send UDP movement.
- Server converts UDP movement into the existing `C_Move -> GameRoom.HandleMove -> S_Move Broadcast` flow.
- UDP movement payload currently includes position, state, move direction, and a `uint32 sequence` for replay/duplicate rejection.
- Existing TCP movement compatibility mode remains available through `NetworkManager.UseUdpMovement=false`.
- UDP movement send rate is limited client-side, and the server also applies a per-session UDP Move rate limit.
- Movement coordinates now come from the target player transform, not the helper object's transform.
- Proto UDP movement uses `[ushort packetId][protobuf payload]`.
- `S_UdpHello` now returns registration success/failure to Unity.
- UDP movement v1 verification is complete, including invalid token rejection, movement sync, Run/Moving correction, non-zero monotonic sequence transmission, and the explicit TCP compatibility mode.
- String UDP fallback has been removed; Proto UDP remains for hello/move, TCP movement compatibility remains through `UseUdpMovement=false`, and UDP registration/movement now has first-pass server hardening for one-time tokens, endpoint binding, sequence checks, rate limiting, speed validation, and movement bounds.
- UDP registration failure does not silently resend movement over TCP. `TrySendMove` returns false until registration succeeds.
- Detailed notes: [UDP Movement Sync](udp-movement-sync.md)

## Completed Room Transfer Work

- RoomTransfer v1 verified direct 1-player Town to Bakal/Dungeon entry.
- RoomTransfer v1.5 restores the existing PartyPopUp UI and Start button before transfer.
- MatchManager v1.6 now supports a 4-player in-memory party queue before RoomTransfer.
- MatchManager v1.7 completes automatic 4-player matching entry.
- MatchManager v1.8 separates the in-memory queue behind `MatchTicket`, `IMatchQueueStore`, and `InMemoryMatchQueueStore` without changing the external v1.7 flow.
- PartyPopUp now shows `Matching...` and disables the previous Start/Ready button during normal matching.
- When four players are matched, the server sends `S_EnterParty` and automatically starts `RoomTransferService.StartPartyDungeonTransfer(...)`.
- The old `C_SceneMove` manual Start path remains only as a debug fallback.
- Current v1.7 flow:
  1. Town portal sends `C_CreateRoom`.
  2. `MatchManager` enqueues the player in the in-memory 4-player queue.
  3. Server sends `S_CreateRoom`.
  4. Unity opens PartyPopUp.
  5. PartyPopUp button text is `Matching...` and the button is disabled.
  6. When four valid sessions are available, `MatchManager` creates one `MatchParty`.
  7. Server sends `S_EnterParty.PartyMembers` to all four members.
  8. Server automatically starts `RoomTransferService.StartPartyDungeonTransfer(members, RoomType.Bakal)`.
  9. Server sends `S_SceneMove` with target room / transfer data.
  10. Unity loads BakalScene.
  11. Unity sends `C_SceneReady`.
  12. Server validates target room / transfer / scene data.
  13. Server enters all four players into the same Bakal room and reuses `S_EnterGame / S_Spawn`.
- `C_EnterGame` pending-transfer fallback remains temporarily for stabilization, but the normal RoomTransfer path uses `C_SceneReady`.
- Dungeon clear return now transfers players back to a Town room and applies `TownSpawnService.ApplyMyRoomSpawn(..., ReturnToTown)` before `EnterRoom`, so every member returns at authoritative MyRoom position `(0,0)` with `IsInPublicTownArea=false`.
- Detailed notes: [Room Transfer Flow](room-transfer-flow.md)
- Matchmaking extension notes: [Matchmaking Flow](matchmaking-flow.md)
- Current monitoring/API notes: [Monitoring API](monitoring-api.md) and [Web API Roadmap](web-api-roadmap.md)

## Completed Authoritative MyRoom / Public Town Sync - 2026-07-20

- Initial Town login calls `TownSpawnService.ApplyMyRoomSpawn` before `GameRoom.EnterRoom`.
- MyRoom state is server-owned through `Player.IsInPublicTownArea=false` and position `(0,0)`.
- Town AOI returns false when either observer or subject is private, so other players are not included in initial `S_Spawn`, movement fanout, or public visibility while either side is in MyRoom.
- Unity still applies its MyRoom remote-visibility rule as a defensive presentation fallback, but server AOI filtering is the primary isolation mechanism.
- `TownScene` teleports the local player to the public Lobby when the Seria portal is used and sends that position through UDP.
- `TownSpawnService.IsPublicLobbyPosition` requires `x >= 18` and a walkable Town tilemap position.
- UDP validation recognizes this private-to-public portal transition and bypasses only the normal speed-distance limit for that one accepted teleport.
- `GameRoom.HandleMove` then sets `IsInPublicTownArea=true` and normal Town AOI/bounds behavior resumes.
- Dungeon return uses the same authoritative MyRoom reset, preventing stale Bakal or public Town coordinates from being spawned into another party member's private room.

## Current Monitoring API / WebLauncher Direction

Project Dawn now includes a local read-only Monitoring API inside the GameServer process at `http://127.0.0.1:8090`.

Implemented endpoints:

- `GET /api/health`
- `GET /api/server/status`
- `GET /api/rooms`
- `GET /api/rooms/{roomId}`
- `GET /api/players/online`
- `GET /api/matching/queue`

The API uses snapshot DTOs and must not expose UDP tokens, live `ClientSession` references, mutable `GameRoom` dictionaries, or write/admin gameplay operations. WebLauncher consumes these endpoints for room, online-player, and matching-queue dashboard cards.

Detailed notes:

- [Monitoring API](monitoring-api.md)
- [Web API Roadmap](web-api-roadmap.md)
- [WebLauncher README](../WebLauncher/README.md)

## Completed Login Test Work

- Unity login UniqueId can be overridden for multi-client tests on one PC.
- Priority:
  1. `-uniqueIdOverride=value`
  2. `-testClientId=value`, producing `SystemInfo.deviceUniqueIdentifier + "_" + value`
  3. default `SystemInfo.deviceUniqueIdentifier`
- This is used to let Editor and Build log in as different accounts/players during local tests.

## Completed Basic Attack / Skill Work

- Basic attacks still use TCP `C_Skill` / `S_Skill`.
- Basic attack 1/2/3 currently sends `SkillId` 2/3/4.
- `MyPlayer.OnClickAtkBtn()` and desktop `X` key input now share `TrySendBasicAttack()`.
- Basic attack is blocked while jumping or dashing because DashAttack/JumpAttack animations do not exist yet.
- Dash blocking includes the same frame where a double-tap dash is about to be recognized.
- `OtherPlayer.UseSkill(4)` now sets `PositionInfo.State = PlayerState.Atk`, matching SkillId 2/3 behavior.

## Completed Reward / Drop UI Polish

- Server reward flow remains `Enemy.OnDead -> DbTransaction.RewardPlayer -> ItemDb INSERT -> S_AddItem`.
- `DbTransaction.RewardPlayer` now logs successful item rewards with player, template id, count, and item db id.
- Unity `S_AddItemHandler` keeps the existing `GameManager.Inven.Add(item)` path.
- Unity now shows a toast when `S_AddItem` is received.
- Toast text uses item data name when available, otherwise it falls back to template id and count.
- If the inventory UI is currently open, `S_AddItemHandler` refreshes it after applying the new item.
- No `Protocol.proto`, reward table, drop chance, or inventory prefab change is required for this polish step.

## Completed C_Collision Minimal Defense v2.1

- `C_CollisionHandler` now rejects invalid session, invalid packet, null player, transfer state, and null room before queueing collision work.
- `GameRoom.HandleCollision` now accepts enemy hits only when the target object id exists in the current room enemy map.
- Collision targeting a non-enemy, non-self object is ignored instead of falling through into damage.
- Existing meteor/environment player damage is preserved for self-target collision packets.
- Player environmental damage is clamped to zero after defence.
- `GameObject.OnDamaged` now ignores already-dead objects and non-positive damage.
- `Enemy` now has death/reward guards so `OnDead`, `S_Die`, and `RewardPlayer` are not repeatedly triggered by duplicate collisions.
- Range validation, cooldown, action lock, per-skill hit tracking, DummyClient `C_Collision`, and reward simulation remain future work.
## Completed DummyClient v2.0 Dungeon Gameplay Basic

- DummyClient can optionally send gameplay packets after entering Bakal DungeonRoom.
- `--enableMovement true` sends TCP `C_Move` patrol movement.
- `--enableAttack true` sends TCP `C_Skill` using configurable skill ids, default `2,3,4`.
- Gameplay starts after dungeon entry and an optional `gameplayStartDelayMs`.
- Movement uses the dungeon entry position as origin and keeps a small patrol radius.
- Summary output now includes gameplay started/completed, move count, skill count, last skill id, and last position.
- Defaults keep existing v1 fill-visible behavior; movement and attack are opt-in.
- UDP movement, `C_Collision`, hit simulation, reward simulation, Protocol changes, Server runtime changes, and Unity changes are still out of scope for v2.0.

## Completed Disconnect / Cleanup Log Polish

- `ClientSession.OnDisconnected` now leaves only the player's current room instead of trying Town and Bakal unconditionally.
- Disconnect during an active RoomTransfer no longer forces an extra leave from the session path; the transfer flow owns that transitional leave.
- `GameRoom.LeaveRoom` player/enemy not-found logs are now structured as skipped leave logs with room id and object id.
- UDP movement during the expected RoomTransfer gap where `player.Room == null` is ignored without noisy reject logs.
- UDP movement with `Room == null` outside transfer still logs as a real rejected state.

## Important Client Files

- `Assets/Scripts/00.Manager/GameManager.cs`
  - Login UniqueId override helper.
  - Currently forces `Screen.SetResolution(800, 600, false)` for test builds.
- `Assets/Scripts/04.Network/Packet/PacketHandler.cs`
  - Login packet creation and server packet handlers.
- `Assets/Scripts/04.Network/NetworkManager.cs`
  - TCP send, UDP hello, UDP move send, UDP movement toggle.
- `Assets/Scripts/03.Player/MyPlayer.cs`
  - Local movement input, UDP/TCP movement branch, basic attack input, jump/dash state.
- `Assets/Scripts/03.Player/OtherPlayer.cs`
  - Remote movement/animation state and skill animation playback.
- `Assets/Scripts/03.Player/BaseCharacter.cs`
  - Shared state, direction, position, animation processing.
- `Assets/Scripts/04.Network/UdpHelloTestClient.cs`
  - Legacy manual UDP test component. Runtime UDP registration/movement is now owned by `NetworkManager`.
- `Assets/Scripts/00.Manager/ObjectManager.cs`
  - Creates local/remote gameplay objects from server object info.

## Important Server Files

- `Server/Server/Program.cs`
  - Server startup and UDP listener wiring.
- `Server/Server/Udp/UdpGamePacketHandler.cs`
  - Proto UDP hello/move handling, endpoint binding, sequence/rate checks, and movement validation.
- `Server/ServerCore/UdpListener.cs`
  - UDP receive loop and low-level send/receive socket ownership.
- `Server/Server/Packet/PacketHandler.cs`
  - TCP packet handlers including `C_Move` and `C_Skill`.
- `Server/Server/Game/Room/GameRoom_Contents.cs`
  - `HandleMove`, `HandleJump`, `HandleSkill`.
- `Server/Server/Session/ClientSession_Login.cs`
  - Login flow and player/account setup.
- `Server/Server/Game/Room/RoomTransferService.cs`
  - RoomTransfer v1/v1.5 source leave, scene move, scene-ready validation, pending target room enter.
- `Server/Server/Game/Match/MatchManager.cs`
  - MatchManager v1.8 ticket-based queue orchestration, PartyPopUp notifications, automatic Bakal party transfer, and debug fallback manual Start path.
- `Server/Server/Game/Match/InMemoryMatchQueueStore.cs`
  - Redis-ready in-memory queue store implementation for match tickets.
- `Server/Server/Game/Match/MatchTicket.cs`
  - Pure data matchmaking ticket. It does not store `ClientSession`, live `Player`, or `GameRoom` references.

## Current UI Findings

- Runtime UI root is `@UI_Root`, created in `UIManager`.
- CanvasScaler is already `ScaleWithScreenSize` with reference resolution `1920x1080`.
- The 800x600 clipping issue is mostly from UI prefabs using fixed 1920x1080 offsets and center anchors.
- High-risk prefabs:
  - `Assets/Resources/Prefabs/UI/Scene/Dynamic_Joystick.prefab`
  - `Assets/Resources/Prefabs/UI/Scene/HUD.prefab`
  - `Assets/Resources/Prefabs/UI/PopUp/UI_StatInfo.prefab`
  - `Assets/Resources/Prefabs/UI/Scene/charInfo.prefab`
- For manual Unity Editor fixes, start with joystick attack/jump buttons and HUD inventory/stat buttons.


## Documentation Index

Current high-level docs:

- [Monitoring API](monitoring-api.md) - implemented local read-only HTTP endpoints and snapshot boundaries.
- [DummyClient Regression and Demo Guide](dummyclient-regression.md) - current DummyClient scenarios and regression commands.
- [Room Transfer Flow](room-transfer-flow.md) - Town to Bakal transfer and scene-ready flow.
- [Matchmaking Flow](matchmaking-flow.md) - MatchManager, queue, MatchCondition, and matching flow.
- [Dev Tools](dev-tools.md) - TestEquipmentSetupTool and tool-specific notes.
- [UDP Movement Sync](udp-movement-sync.md) - Proto UDP movement and explicit TCP compatibility mode.
- [Next Milestones](next-milestones.md) - milestone index.
- [Milestones](milestones/README.md) - file-based milestone details.
- [Web API Roadmap](web-api-roadmap.md) - current Monitoring API boundary and future separate Web API direction.
## Things Not To Do Yet

- Do not implement UDP skills until TCP skill flow, cooldown policy, and hit validation are intentionally designed.
- Do not remove the explicit TCP movement compatibility mode yet, and do not describe it as automatic UDP failover.
- Do not change `Protocol.proto` or generated `Protocol.cs` without an explicit migration plan.
- Do not remove the `C_EnterGame` pending-transfer fallback until repeated `C_SceneReady` tests confirm it is no longer used in normal RoomTransfer.
- Do not add Redis before deciding whether the next milestone needs queue scale, observability, or portfolio backend coverage.
- Do not store real-time room state, UDP movement, live `ClientSession`, or GameRoom object state in Redis.
- Do not add DashAttack or JumpAttack SkillIds until animation/controller policy is decided.
- Do not add server cooldown, validation, hit detection, or damage logic until the skill milestone is selected.
- Do not do large animation controller refactors while debugging packet/state flow.

## Candidate Next Milestones

These are options for the next milestone, not a finalized direction yet. See [Next Milestones](next-milestones.md) and [Milestones](milestones/README.md) for file-based milestone details.

- Milestone A: Monitoring API Polish - recent events, reject reason counts, and WebLauncher visibility.
- Milestone B: MatchHistory / DungeonRun Persistence - durable match and dungeon run records.
- Milestone C: Redis MatchQueueStore - Redis-backed ticket queue behind `IMatchQueueStore`.
- Milestone D: Combat Authority v2 - cooldown/action-lock/damage validation hardening.
- Milestone E: Demo UX Polish - presentation-focused UI and flow improvements.
- Milestone F: Separate ASP.NET Core Web API - DB/history read endpoints outside GameServer.

## Test Command Line Examples

Editor and build can use different test identities:

```text
-testClientId=editor
-testClientId=build01
```

Or exact override:

```text
-uniqueIdOverride=test_client_editor
-uniqueIdOverride=test_client_build01
```










## Completed DummyClient v2.2 Follow-Enemy Movement

- DummyClient can now use `--movementPattern follow-enemy` after entering Bakal DungeonRoom.
- Enemy `ObjectInfo` entries are collected from `S_Spawn.Objects` without changing Protocol, Server runtime, or Unity.
- Target selection supports `--targetEnemy nearest|first`.
- Movement uses simple straight-line TCP `C_Move` toward the target enemy and stops at `--followStopDistance`.
- Existing `patrol` movement remains the default and existing fill-visible behavior remains opt-in for gameplay packets.
- `--attackOnlyInRange true` can gate existing TCP `C_Skill` sends by target distance.
- `C_Collision`, reward simulation, UDP movement, enemy movement, pathfinding, and meteor math are still out of scope.

## Completed DummyClient v2.3 C_Collision / Reward Simulation

- DummyClient can now opt into `--enableCollision true` after entering Bakal DungeonRoom.
- Collision simulation is tied to the existing attack loop: TCP `C_Skill` is sent first, then TCP `C_Collision` is sent after `--collisionDelayMs` for the selected enemy target.
- `--collisionOnlyInRange true` keeps collision sends gated by `--attackRange`.
- `S_AddItem` is now observed for reward summary counts; DummyClient still does not implement a real inventory.
- `S_Die` and `S_Despawn` remove known enemy targets from DummyClient tracking so dead/despawned enemies are not selected again.
- Defaults keep v1/v2.0/v2.2 behavior unchanged because `enableCollision=false`.
- Server runtime, Unity, Protocol, UDP movement, enemy movement, pathfinding, and meteor math remain unchanged.

## Completed Server Hit Range Validation v2.3.1

- Enemy-target `C_Collision` now checks server-side distance before applying enemy damage.
- Validation uses `player.Info.PosInfo` and `enemy.Info.PosInfo`, matching the server state updated by TCP `C_Move` / `C_Jump`.
- Current allowed enemy hit range is `1.8f`, intended to match DummyClient `--attackRange 1.5` plus a small tolerance.
- Out-of-range enemy collision is ignored before `Enemy.OnDamaged`, so death/reward cannot be triggered by ObjectId alone.
- Self-target player environmental damage path is unchanged.
- Existing target validation, dead guard, reward guard, and reward/drop Toast flow are unchanged.

Expected logs:

```text
[HIT] Collision ignored. Reason=OutOfRange, Player=..., PlayerId=..., TargetId=..., Dist=..., Allowed=1.80, PlayerPos=(...), TargetPos=(...)
[HIT] Enemy damaged. Player=..., PlayerId=..., EnemyId=..., Dist=..., Damage=..., EnemyHp=...
```

## Completed Combat Hit Validation v1

- `C_Collision` is now treated as a hit candidate, not a confirmed client-side hit, for enemy targets.
- Server records a per-player runtime `SkillCastState` when valid TCP `C_Skill` is received.
- v1 accepted basic attack SkillIds are `2`, `3`, and `4`.
- Each active cast records `SkillId`, `CastSeq`, cast time, active-until time, and hit targets for that cast.
- Enemy-target `C_Collision` now validates:
  - active skill cast exists
  - skill id is valid
  - cast active window has not expired
  - target was not already hit by the same cast
  - target enemy exists in the current room and is alive
  - attacker/enemy distance is within the skill range
- Duplicate hits in the same cast are rejected with `Reason=DuplicateHit`.
- Expired or missing casts are rejected with `Reason=SkillCastExpired` or `Reason=NoActiveSkillCast`.
- Meteor/environment self-damage remains on the existing self-target path and does not require an active skill cast.
- Existing enemy death, reward, `S_AddItem`, and reward/drop Toast flow are unchanged.

Expected logs:

```text
[SKILL] Active cast recorded. Player=..., SkillId=2, CastSeq=..., Damage=50, Range=1.80, ActiveWindowMs=1200, ActiveUntil=...
[HIT] Collision ignored. Reason=NoActiveSkillCast, Player=..., TargetId=...
[HIT] Collision ignored. Reason=SkillCastExpired, Player=..., SkillId=..., CastSeq=..., TargetId=...
[HIT] Collision ignored. Reason=DuplicateHit, Player=..., SkillId=..., CastSeq=..., TargetId=...
[HIT] Collision ignored. Reason=OutOfRange, Player=..., SkillId=..., CastSeq=..., Dist=..., Allowed=...
[HIT] Enemy damaged. Player=..., SkillId=..., CastSeq=..., EnemyId=..., Dist=..., Damage=50, EnemyHp=...
```

## Completed Combat Anchor Alignment v1

- Enemy root `PosInfo` remains the network/spawn placement point.
- Enemy combat validation now uses a server-side hit center and hit radius instead of raw enemy root position.
- `Enemy` now has runtime-only combat anchor fields:
  - `HurtBoxOffsetX`
  - `HurtBoxOffsetY`
  - `HitRadius`
  - `HitCenterX/Y`
- Bakal template id `1` currently uses:
  - `HurtBoxOffsetX = 1.2f`
  - `HurtBoxOffsetY = -0.2f`
  - `HitRadius = 1.2f`
- Enemy hit range validation now checks distance from `player.Info.PosInfo` to `enemy.HitCenter`.
- Effective allowed range is `SkillRange + enemy.HitRadius`.
- Hit logs now include player position, enemy root, enemy hit center, enemy hit radius, skill range, distance, and allowed range.
- Unity prefab structure is unchanged. Long term, enemy prefab Root / Visual / Shadow / Collider / HurtBox should be aligned or exported as shared combat data.

Expected logs:

```text
[SPAWN] Enemy created. RootPos=(...), HitCenter=(...), HitRadius=...
[HIT] RangeCheck. Player=..., PlayerPos=(...), EnemyRoot=(...), EnemyHitCenter=(...), EnemyHitRadius=..., SkillRange=..., Dist=..., Allowed=...
[HIT] Collision ignored. Reason=OutOfRange, ... EnemyRoot=(...), EnemyHitCenter=(...), Dist=..., Allowed=...
```

## Completed Combat Direction and Line Validation v1

- Enemy-target C_Collision now validates the target as a server-side hit candidate using the player's facing direction and line alignment.
- The rule is front-relative, not right-only:
  - player left of enemy + facing right can hit
  - player right of enemy + facing left can hit
  - target behind the player is rejected with Reason=TargetBehind
  - target outside the vertical line tolerance is rejected with Reason=LineMismatch
- Player.LastFacingDir is tracked from the latest horizontal C_Move / C_Jump direction and reused when the player is idle.
- v1 skill specs for SkillIds 2, 3, and 4 now include LineHalfHeight = 0.8f.
- Enemy hit validation uses player position, player last facing direction, enemy hit center / hit radius, skill horizontal range, and vertical line tolerance.
- Existing active cast validation, duplicate hit prevention, target alive checks, combat anchor alignment, enemy death, reward, and self/environment damage exception remain unchanged.

Expected logs:

```text
[HIT] RangeCheck. Player=..., Facing=Left, ForwardDx=..., VerticalDelta=..., Dist=..., Allowed=...
[HIT] Collision ignored. Reason=TargetBehind, ...
[HIT] Collision ignored. Reason=LineMismatch, ...
[HIT] Collision ignored. Reason=OutOfRange, ...
```

## Completed Combat Line Anchor Fix v1

- Enemy hit center and combat/depth line are now separated on the server.
- `Enemy.HitCenterX/Y` remains the hurtbox center used for horizontal/front range checks.
- `Enemy.CombatLineY` is now used for same-line/depth validation instead of `HitCenterY`.
- `Enemy` now has runtime-only `CombatLineOffsetY`.
- Bakal template id `1` currently uses:
  - `HurtBoxOffsetX = 1.2f`
  - `HurtBoxOffsetY = -0.2f`
  - `CombatLineOffsetY = -1.65f`
  - `HitRadius = 1.2f`
- With Bakal root Y `-0.55`, server combat line becomes `-2.20`, matching the observed normal player attack lane.
- Basic attack SkillIds `2`, `3`, and `4` now use `LineHalfHeight = 0.4f`.
- Front validation now uses `FrontEpsilon = 0.05f` to avoid rejecting tiny floating point offsets such as `ForwardDx=-0.01` as behind.
- Hit logs now include `EnemyCombatLineY` so root, hurt center, and line anchor can be inspected separately.

Expected logs:

```text
[HIT] RangeCheck. PlayerPos=(...), EnemyRoot=(...), EnemyHitCenter=(...), EnemyCombatLineY=..., LineHalfHeight=..., VerticalDelta=..., Facing=..., ForwardDx=...
[HIT] Collision ignored. Reason=LineMismatch, ... EnemyCombatLineY=..., VerticalDelta=...
[HIT] Collision ignored. Reason=TargetBehind, ... ForwardDx=...
```

## Combat Ground Anchor Rediagnosis Notes

- Project Dawn combat should be treated as belt-scroll combat, not simple side-scroller combat.
- X-axis remains the left/right front/back axis.
- Y-axis is the ground-plane depth/lane axis.
- Visual sprite center, object root, hurt center, and ground/combat anchor must be separated.
- Current server fields are diagnostic and not final tuning values:
  - EnemyRoot is spawn/network root.
  - EnemyHitCenter is hurtbox-oriented.
  - EnemyCombatLineY is the current server line check value, but must be validated against Unity Shadow/Ground anchors before being treated as final.
- Server line check logs now include PlayerCombatLineY, PlayerDepthBandHalfHeight, EnemyCombatLineY, EnemyDepthBandHalfHeight, AllowedDepth, and LineOverlap.
- Unity CombatSystem now emits [CLIENT][COMBAT_ANCHOR] and [CLIENT][ENEMY_ANCHOR] logs during hit candidate evaluation, including root, shadow, collider bounds, sprite bounds, and PositionInfo.
- Do not tune CombatLineOffsetY to a visible player position or Bakal visual body center. Use Shadow/Ground anchor data from Unity logs first.

## Completed Combat Shadow Anchor Alignment v2 - 2026-07-20

- Unity combat anchors are based on the visible Base/Shadow contact point:
  - Player combat anchor = Player Base/Shadow transform.
  - Enemy combat anchor = Enemy Base/Shadow transform.
- Confirmed Unity offsets:
  - Player root -> shadow world offset: `X=+0.05`, `Y=-0.15`.
  - Bakal prefab Base/Shadow local offset: `X=+1.25`, `Y=-1.4100002`.
  - Bakal root scale: `0.8`.
  - Final Bakal root -> shadow world offset: `X=+1.0`, `Y=-1.1280002`.
- Server validates enemy hits using combat anchors instead of root or visual center:
  - `Player.CombatAnchorX/Y = Player PosInfo + player shadow offset`.
  - `Enemy.CombatAnchorX/Y = Enemy PosInfo + enemy shadow offset`.
- Bakal template id 1 now uses the derived world offset for `HurtBoxOffset`, `CombatLineOffsetY`, and `CombatAnchorOffset`, with `HitRadius=1.2`.
- The X offset mirrors with `MoveDir.Left/Right` while Y remains fixed, matching the Unity shadow local-position mirror policy.
- Bakal patrol samples a target in CombatAnchor-based MovementBounds, converts it to a root target, moves the root, and validates the resulting combat anchor against the map.
- Patrol logs expose `Root`, `CombatAnchor`, `TargetRoot`, `TargetAnchor`, and `Dir` so visual/server coordinates can be compared directly.
- Server line/depth validation compares Player combat anchor Y against Enemy combat anchor Y; front/range validation compares their combat anchor X values.
- Unity tracks `LastHorizontalDir` separately so `MoveDir.None`, `Up`, and `Down` do not overwrite the last left/right facing direction for hit candidate generation.

## Completed DummyClient Movement Demo Patrol Modes

- DummyClient movement-only demo can now run with `--movementPattern patrol --patrolMode mixed`.
- `patrolMode=mixed` assigns different patrol behavior per dummy by client index:
  - index 0: horizontal patrol
  - index 1: vertical patrol
  - index 2: box patrol
- Additional aliases are supported for demo readability: `--patrolRadius`, `--patrolIntervalMs`, and `--patrolSpeed` map to existing movement radius, interval, and speed options.
- Movement-only demo should use `--enableAttack false --enableCollision false` so no `C_Skill`, `C_Collision`, `[SKILL]`, or `[HIT]` logs are expected.

Example:

```powershell
dotnet run --project "E:\task\Server\Server\DummyClient\DummyClient.csproj" -- --scenario fill-visible --clients 3 --expectedExternal 1 --host 127.0.0.1 --port 8080 --prefix PD_Dummy --delayMs 100 --sceneReadyDelayMs 100 --holdAfterDungeonSec 180 --enableMovement true --enableAttack false --enableCollision false --movementPattern patrol --patrolMode mixed --verbose true
```

## Milestone C - Attack HitFrame Integration

- Basic attack timing is now split between attack start and animation hit frame.
- Attack input sends TCP `C_Skill` first and records the current basic attack skill/combo locally.
- `CombatSystem` no longer scans hit candidates from `Atk` state plus input timing.
- Hit candidate scanning and TCP `C_Collision` sends now happen only through the Animation Event entrypoint:
  - `MyPlayer.AnimEvent_BasicAttackHitFrame()`
- The existing client filters remain in `CombatSystem`:
  - Shadow/combat-anchor based positions
  - local line/depth check
  - facing/front check
  - horizontal range check
  - local duplicate suppression per attack sequence
- Server-side active skill validation, duplicate hit validation, range/line/facing validation, death, and reward flow remain unchanged.
- If the hit-frame Animation Event is missing, basic attack still sends `C_Skill`, but no `C_Collision` hit candidate is sent.

Animation clip setup:

- Add an Animation Event at the actual hit frame of each basic attack clip.
- Function name: `AnimEvent_BasicAttackHitFrame`
- Use the same function for `Attack1`, `Attack2`, and `Attack3` clips.
- Keep the event argument empty.

Expected logs:

```text
[CLIENT][ATTACK_START] SkillId=2, ComboIndex=1, Facing=..., Pos=(...)
[CLIENT][ATTACK_HIT_FRAME] SkillId=2, ComboIndex=1, State=Atk, Facing=..., Pos=(...)
[CLIENT][HIT_CANDIDATE_SEND] Target=..., TargetId=..., Facing=..., Dx=..., Dy=...
[CLIENT][HIT_CANDIDATE_SKIP] Reason=DuplicateLocal, ...
```

Test notes:

- Before Animation Events are added, pressing attack should send `C_Skill` but should not emit `[CLIENT][HIT_CANDIDATE_SEND]`.
- After Animation Events are added, candidate send/skip logs should appear only at the event frame.
- If the same event is accidentally fired twice during one attack, duplicate target candidates are skipped locally.

## Milestone C - Animation Event Proxy

- Unity Animation Events look for the function on the GameObject that owns the `Animator`.
- The player prefab has a root object such as `male_ghostknight` and child objects such as `Sprite` and `Base/Shadow`.
- `MyPlayer` lives on the root object, while the `Animator` may live on the `Sprite` child.
- `AnimationEventProxy` was added so Animation Events on the Animator object can forward to the parent `MyPlayer`.
- `AnimationEventProxy.AnimEvent_BasicAttackHitFrame()` calls parent `MyPlayer.AnimEvent_BasicAttackHitFrame()`.
- `MyPlayer.Start()` now finds the active child/root `Animator` and automatically adds `AnimationEventProxy` to the Animator GameObject when missing.
- This is runtime-only support; no prefab asset is modified by code.

Animation Event setup in Unity Editor:

1. Select the runtime object or prefab root, for example `male_ghostknight`.
2. Confirm which child has the `Animator`, usually `Sprite`.
3. Open `Window > Animation > Animation`.
4. Select the Animator GameObject, not `Base/Shadow`.
5. Open the `Attack1` animation clip.
6. Move to the actual hit frame.
7. Add an Animation Event.
8. Set Function to `AnimEvent_BasicAttackHitFrame`.
9. Leave arguments empty.
10. Repeat for `Attack2` and `Attack3`.

Expected client logs after event setup:

```text
[CLIENT][ATTACK_START] SkillId=2, ...
[CLIENT][ANIM_EVENT_PROXY] Forward=AnimEvent_BasicAttackHitFrame, GameObject=Sprite, Player=male_ghostknight
[CLIENT][ATTACK_HIT_FRAME] SkillId=2, ...
[CLIENT][HIT_CANDIDATE_SEND] Target=Bakal_2Phase, ...
```

If the Animation Event is not connected, only `[CLIENT][ATTACK_START]` is expected and no hit candidate is sent.

Runtime owner mapping note:

- Player prefabs may be shared while the runtime component is decided by `ObjectManager`:
  - local visible player gets `MyPlayer`
  - remote players/dummies get `OtherPlayer`
  - enemies get `EnemyPlayer`
- `BaseCharacter.Start()` now attaches `AnimationEventProxy` to the Animator GameObject for every runtime character owner.
- `AnimationEventProxy` resolves its owner with `GetComponentInParent<BaseCharacter>()` at runtime.
- If the owner is `MyPlayer`, the event is forwarded to `MyPlayer.AnimEvent_BasicAttackHitFrame()`.
- If the owner is `OtherPlayer` or another non-MyPlayer character, the event is ignored with `[CLIENT][ANIM_EVENT_PROXY_IGNORED]`; remote attack animation playback should not send local `C_Collision` candidates.

## Meteor Collision Bug Fix - Ground Impact AABB

- Meteor collision was changed for Project Dawn's belt-scroll coordinate model.
- Meteor root now represents the fixed ground impact center from the `Meteor_Area` transform.
- Falling visual motion is handled by visual child local Y movement instead of moving the root transform.
- `OnTriggerEnter2D` no longer sends Meteor damage candidates.
- Meteor damage is checked once at impact time with AABB against the local MyPlayer combat anchor:
  - primary anchor: `Base/Shadow` transform
  - fallback: player root transform, with log marker
- Meteor v1 AABB defaults:
  - half width: `1.1`
  - half depth: `0.4`
- The existing self-target `C_Collision` packet shape is preserved for server environmental damage handling.
- This is still client-side hit candidate stabilization; a server-authoritative AreaHazard model remains a future milestone.

## Multi GameRoom Safety Foundation

- `GameRoom` now carries explicit room metadata:
  - `RoomType`
  - `CreatedAtUtc`
  - `LastUpdatedAtUtc`
  - `State`
- `RoomManager.Add(RoomType)` assigns `RoomType` directly instead of relying on `RoomId` inference.
- Room logs now use the explicit `GameRoom.RoomType`; `RoomId == 1 ? Town : Bakal` style inference should be avoided.
- `RoomManager.UpdateRooms()` now copies a snapshot of the room dictionary under lock, then calls `room.Update()` outside the manager lock.
- `RoomManager.Find(RoomType)` and `Remove(RoomType)` are kept for legacy compatibility, but non-Town usage logs a warning because multi dungeon rooms must be addressed by `RoomId`.
- `GameRoom` exposes read-only helper/metric fields for future cleanup and RoomSnapshot API work:
  - `PlayerCount`
  - `EnemyCount`
  - `IsEmpty`
  - `IsDungeonRoom`
  - `UpdateCount`
  - `LastUpdateMs`
  - `MaxUpdateMs`
- Slow room updates log `[ROOM][SLOW_UPDATE]` when a room update exceeds the current threshold.
- Latest dungeon transfer remains `PendingRoomId` based, which is the correct lookup model for multiple Bakal rooms.

## Multi GameRoom Isolation Test - DummyClient fill-dummy

- DummyClient now supports `--scenario fill-dummy` for pure dummy multi-room tests.
- `fill-dummy` fills matchmaking without a Unity visible client:
  - `--clients 4 --partySize 4` creates one 4-dummy party.
  - `--clients 8 --partySize 4` creates two 4-dummy parties and should create two Bakal rooms.
- The default `fill-dummy` behavior is movement-only patrol if gameplay flags are omitted:
  - movement enabled
  - attack disabled
  - collision disabled
  - patrol/mixed movement
- `S_SceneMove` target room id is tracked per dummy and used for summary grouping.
- DummyClient observes `S_Move` packets and reports suspected cross-room movement only when both receiver and mover room ids are known and different.
- Room isolation should be verified with both DummyClient summary and server logs:
  - `[ROOM_MANAGER] Room created. RoomType=Bakal`
  - `[TRANSFER] Party transfer start. TargetRoomId=...`
  - `[MOVE] S_Move broadcast. RoomId=..., Recipients=...`

Example:

```powershell
dotnet run --project "E:\task\Server\Server\DummyClient\DummyClient.csproj" -- --scenario fill-dummy --clients 8 --partySize 4 --expectedRooms 2 --host 127.0.0.1 --port 8080 --prefix PD_Dummy --delayMs 100 --sceneReadyDelayMs 100 --holdAfterDungeonSec 180 --enableMovement true --enableAttack false --enableCollision false --movementPattern patrol --patrolMode mixed --patrolRadius 1.5 --patrolIntervalMs 500 --patrolSpeed 0.35 --verbose true
```

## Dummy Create-Players Required Equipment

- `create-players` now relies on the server login/create flow to keep `PD_Dummy` players match-ready.
- For players whose names start with `PD_Dummy`, the server ensures starter equipment in DB:
  - preferred weapon template: `1`
  - preferred armor template: `100`
- If the preferred ids are not valid for the loaded `ItemData.json`, the server falls back to the first loaded template with the required `ItemType` and logs the fallback. The setup is idempotent:
  - existing starter items are reused
  - missing starter items are created in empty inventory slots
  - existing unequipped starter items are marked `Equipped=true`
- Matching validation is unchanged. `MissingRequiredEquipment` is still rejected when the in-memory inventory lacks an equipped weapon or armor.
- Re-run `create-players` before `fill-dummy` when adding more dummy accounts, then start a fresh `fill-dummy` run so the corrected equipment is loaded into each dummy session inventory.


## RoomSnapshot Foundation

- Added read-only `RoomSnapshot` DTOs so API/Web Launcher code does not need to read `GameRoom` dictionaries directly.
- `GameRoom.CreateSnapshot()` copies room metadata, players, enemies, boss summary, and update metrics into DTO objects.
- `RoomManager.GetRoomSnapshots()` copies the room list under the manager lock, then creates room snapshots outside that lock.
- `RoomManager.GetRoomSnapshot(roomId)` provides single-room read-only lookup by `RoomId`.
- `RoomManager.PrintRoomSnapshotSummary()` is available for manual console diagnostics and does not run automatically.
- Snapshot fields include `RoomId`, `RoomType`, `State`, counts, player/enemy positions, boss HP, and update metrics.
- Monitoring API v1/v2 now exposes this through `GET /api/rooms`, `GET /api/players/online`, and `GET /api/matching/queue` for WebLauncher.


## Monitoring API v2

- Added read-only `GET /api/players/online` for current online player/session visibility.
- Added read-only `GET /api/matching/queue` for in-memory matching queue visibility.
- `/api/server/status` now includes `onlinePlayers`, `matchingWaitingPlayers`, and `matchingQueueCount` while preserving existing fields.
- WebLauncher now displays Online Players and Matching Queue cards alongside existing room monitoring and Game Start UI.
- The API remains localhost development monitoring only and returns snapshot DTOs; it does not expose UDP tokens, internal auth tokens, live collections, or mutable gameplay objects.
- Matching/gameplay flow, TCP/UDP protocol, Unity, DummyClient, DB schema, and target framework are unchanged.

## Monitoring API v1

- GameServer now starts a local read-only monitoring API at `http://127.0.0.1:8090`.
- The implementation uses `HttpListener` instead of Kestrel to keep the existing `netcoreapp3.1` target and avoid ASP.NET framework/package changes.
- The API does not read `GameRoom` dictionaries directly.
- `RoomManager.UpdateRooms()` refreshes a cached `RoomSnapshot` list after room updates in the game update flow.
- `RoomManager.GetRoomSnapshots()` and `GetRoomSnapshot(roomId)` return cloned cached snapshots for API use.
- Supported endpoints:
  - `GET /api/health`
  - `GET /api/server/status`
  - `GET /api/rooms`
  - `GET /api/rooms/{roomId}`
- CORS headers are permissive for local Web Launcher development.
- Existing TCP `8080` and UDP `8081` ports are unchanged.

PowerShell checks:

```powershell
Invoke-RestMethod http://127.0.0.1:8090/api/health
Invoke-RestMethod http://127.0.0.1:8090/api/server/status | ConvertTo-Json -Depth 6
Invoke-RestMethod http://127.0.0.1:8090/api/rooms | ConvertTo-Json -Depth 10
Invoke-RestMethod http://127.0.0.1:8090/api/rooms/2 | ConvertTo-Json -Depth 10
```





