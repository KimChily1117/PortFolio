# Dev Tools

## TestEquipmentSetupTool

`TestEquipmentSetupTool` prepares deterministic test/demo equipment before running the server and clients. It is not the monster drop system and does not execute `Enemy.OnDead` or random reward selection. It writes `ItemDb` rows and `Equipped` flags so later matchmaking equipment checks can use server-owned inventory state.

Recommended timing:

1. Stop the server or run before login.
2. Run with `--dryRun true` first.
3. Run without `--dryRun` only after confirming target players and template ids.
4. Start the server and login clients so `ClientSession.HandleEnterGame` loads the updated `ItemDb` rows into `MyPlayer.Inven`.

Project:

```text
Server/TestEquipmentSetupTool/TestEquipmentSetupTool.csproj
```

The tool references the existing `Server` project to reuse `AppDbContext`, `ItemDb`, `PlayerDb`, `ItemData`, `WeaponData`, and `ArmorData`. It loads `ItemData.json` through the existing `ItemLoader` data type. `AppDbContext` currently uses its built-in localdb `GameDB` connection string.

Supported options:

```text
--prefix <prefix>
--count <count>
--startIndex <index>
--playerNames <name1,name2>
--weaponTemplateId <id>
--armorTemplateId <id>
--equip <true|false>
--overwriteExistingEquip <true|false>
--createMissingPlayer <true|false>
--dryRun <true|false>
--dataPath <path-to-Assets/Resources/Data>
```

Target selection uses either `--playerNames` or `--prefix/--count`, not both. Prefix mode generates names like `PD_Dummy_0001`, `PD_Dummy_0002`, starting from `--startIndex`.

Current sample data ids:

```text
Weapon: 1
Armor: 100 or 101
```

Dry run example:

```powershell
dotnet run --project E:\task\Server\Server\TestEquipmentSetupTool\TestEquipmentSetupTool.csproj -- --playerNames Player_0054 --weaponTemplateId 1 --armorTemplateId 100 --equip true --overwriteExistingEquip true --dryRun true
```

Apply example:

```powershell
dotnet run --project E:\task\Server\Server\TestEquipmentSetupTool\TestEquipmentSetupTool.csproj -- --playerNames Player_0054 --weaponTemplateId 1 --armorTemplateId 100 --equip true --overwriteExistingEquip true
```

Dummy prefix example:

```powershell
dotnet run --project E:\task\Server\Server\TestEquipmentSetupTool\TestEquipmentSetupTool.csproj -- --prefix PD_Dummy --count 3 --weaponTemplateId 1 --armorTemplateId 100 --equip true --overwriteExistingEquip true --dryRun true
```

Behavior:

- Validates that `weaponTemplateId` is `ItemType.Weapon`.
- Validates that `armorTemplateId` is `ItemType.Armor`.
- Reuses an existing matching `ItemDb` when present.
- Creates a new `ItemDb` in a free slot when absent.
- Uses slots `0..19`, matching current inventory policy.
- If `--equip true`, sets the target weapon and armor to `Equipped=true`.
- If `--overwriteExistingEquip true`, unequips an existing equipped weapon and an equipped armor with the same `ArmorType`.
- If `--dryRun true`, prints planned changes and skips `SaveChanges`.

`--createMissingPlayer true` is reserved for a later version. In v1, missing players are reported and the tool fails without creating accounts or players.

MatchCondition connection:

After this tool prepares equipment before login, a later `MatchProfileProvider` can check:

```csharp
HasEquippedWeapon = player.Inven.Items.Values.Any(i => i.Equipped && i.ItemType == ItemType.Weapon);
HasEquippedArmor = player.Inven.Items.Values.Any(i => i.Equipped && i.ItemType == ItemType.Armor);
```

Do not use this tool while target players are already connected unless a runtime refresh/GM command is added later. Updating DB rows alone will not refresh an existing `ClientSession.MyPlayer.Inven`.

## DummyClient

`DummyClient` is a TCP-only background/filler client for matchmaking demos and regression tests. It now supports hybrid visible-client matching, pure dummy multi-room tests, optional movement/attack/collision gameplay, and reward observation. Full current scenario guide: [DummyClient Regression and Demo Guide](dummyclient-regression.md).

Project:

```text
Server/DummyClient/DummyClient.csproj
```

References:

- `ServerCore` project for `PacketSession` receive framing.
- Existing generated `Server/Packet/Protocol.cs` as a linked compile item.
- Does not reference the `Server` project.
- Does not call `MatchManager`, `RoomTransferService`, DB, or server handlers directly.

Supported scenarios:

```text
create-players
fill-visible
fill-dummy
```

Supported options:

```text
--scenario create-players|fill-visible
--clients 3
--expectedExternal 1
--host 127.0.0.1
--port 8080
--prefix PD_Dummy
--delayMs 100
--sceneReadyDelayMs 100
--timeoutSec 120
--holdAfterDungeonSec 120
--startIndex 1
--verbose false
```

### create-players

Creates deterministic dummy accounts/characters through the normal TCP login/create/enter-game flow. It does not send `C_CreateRoom`.

```powershell
dotnet run --project E:\task\Server\Server\DummyClient\DummyClient.csproj -- --scenario create-players --clients 3 --host 127.0.0.1 --port 8080 --prefix PD_Dummy --delayMs 100
```

Expected summary:

```text
Scenario: create-players
Connected: 3/3
LoggedIn: 3/3
PlayerReady: 3/3
EnteredTown: 3/3
MatchRequested: 0/3
Result: SUCCESS
```

### fill-visible

Runs 1-3 dummy filler clients. Each dummy logs in, enters Town, sends `C_CreateRoom`, waits for visible Unity clients to complete a 4-player party, handles `S_SceneMove`, sends `C_SceneReady`, enters dungeon, and holds the connection.

```powershell
dotnet run --project E:\task\Server\Server\DummyClient\DummyClient.csproj -- --scenario fill-visible --clients 3 --expectedExternal 1 --host 127.0.0.1 --port 8080 --prefix PD_Dummy --delayMs 100 --sceneReadyDelayMs 100 --holdAfterDungeonSec 120
```

`fill-visible` requires `clients + expectedExternal == 4`. It rejects `--clients >= 4` because four dummies can match before Unity joins.

Expected party summary:

```text
Party 1:
RoomId=2
External/Visible Members=Player_0054
Dummy Members=PD_Dummy_0001,PD_Dummy_0002,PD_Dummy_0003
Result=SUCCESS
```

### Required Equipment Setup

Before `fill-visible`, dummy players must satisfy MatchCondition v1 equipment requirements. Run this before server/client login:

```powershell
dotnet run --project E:\task\Server\Server\TestEquipmentSetupTool\TestEquipmentSetupTool.csproj -- --prefix PD_Dummy --count 3 --weaponTemplateId 1 --armorTemplateId 100 --equip true --overwriteExistingEquip true --dryRun false --dataPath "E:\task\C#\Project_Dawn\Assets\Resources\Data"
```

Visible Unity players also need equipment:

```powershell
dotnet run --project E:\task\Server\Server\TestEquipmentSetupTool\TestEquipmentSetupTool.csproj -- --playerNames Player_0054 --weaponTemplateId 1 --armorTemplateId 100 --equip true --overwriteExistingEquip true --dryRun false --dataPath "E:\task\C#\Project_Dawn\Assets\Resources\Data"
```

### v1 Packet Handling

Handled server packets:

- `S_Connected`
- `S_Login`
- `S_CreatePlayer`
- `S_EnterGame`
- `S_CreateRoom`
- `S_EnterParty`
- `S_SceneMove`
- `S_Spawn`

Ignored server packets:

- `S_Move`
- `S_Skill`
- `S_ItemList`
- `S_AddItem`
- `S_EquipItem`
- `S_UdpHello`
- other movement/combat/despawn packets

DummyClient does not implement UDP movement and does not bypass real TCP packet framing. Current TCP gameplay options and regression commands are maintained in [DummyClient Regression and Demo Guide](dummyclient-regression.md).

If connection to `127.0.0.1` fails, check the server log bind IP and pass it through `--host`.


### Current Regression Guide

Use the dedicated regression guide for the latest commands and option matrix:

- create-players`r
- ill-visible`r
- ill-dummy`r
- patrol and mixed movement
- follow-enemy movement
- TCP skill and collision simulation
- reward observation
- multi-room isolation checks
- Monitoring API verification

See [DummyClient Regression and Demo Guide](dummyclient-regression.md).

### Verified fill-visible Demo

The current hybrid demo flow has been verified with three TCP dummy filler clients and one visible Unity client.

Verified clients:

- Dummy: `PD_Dummy_0001`
- Dummy: `PD_Dummy_0002`
- Dummy: `PD_Dummy_0003`
- Visible Unity player: `Player_0090`

Verified result:

```text
PD_Dummy_0001 -> HasWeapon=True, HasArmor=True, QueueKey=Bakal:Bakal:L0:M1000
PD_Dummy_0002 -> HasWeapon=True, HasArmor=True, QueueKey=Bakal:Bakal:L0:M1000
PD_Dummy_0003 -> HasWeapon=True, HasArmor=True, QueueKey=Bakal:Bakal:L0:M1000
QueueCount=3/4
Player_0090 -> HasWeapon=True, HasArmor=True, QueueKey=Bakal:Bakal:L0:M1000
Party matched = PD_Dummy_0001, PD_Dummy_0002, PD_Dummy_0003, Player_0090
TargetRoomId=3
TransferId=2
All four players entered the same Bakal DungeonRoom
```

The dungeon-side flow continued after room entry. Skill and collision logs were observed after transfer, confirming that the client remained in the dungeon gameplay path.

Recommended demo sequence:

1. Run the server.
2. Run `DummyClient` with `--scenario create-players` if the dummy accounts/characters do not exist yet.
3. Stop the server.
4. Run `TestEquipmentSetupTool` for `PD_Dummy_0001` through `PD_Dummy_0003`.
5. Run `TestEquipmentSetupTool` for the visible Unity player.
6. Restart the server.
7. Run `DummyClient` with `--scenario fill-visible --clients 3 --expectedExternal 1`.
8. Login the visible Unity client and enter the Bakal portal.
9. Confirm the party contains three dummy players and one visible Unity player.
10. Confirm all four players enter the same Bakal DungeonRoom.

### C_Collision / Hit Validation v2.1 Notes

Server-side collision handling has minimal defensive checks before future DummyClient collision simulation.

Current behavior:

- Invalid session, invalid packet, null player, transfer state, and null room are rejected in `C_CollisionHandler`.
- Enemy damage requires the target object id to exist in the current room enemy dictionary.
- Already-dead enemy targets are ignored.
- Non-enemy, non-self targets are ignored.
- Self-target collision is preserved for current meteor/environment player damage.
- Enemy death and reward are guarded so duplicate collisions do not repeatedly call `RewardPlayer`.

Still not implemented:

- Distance/range validation.
- Skill cooldown validation.
- Action lock validation.
- Per-skill duplicate hit tracking.
- DummyClient `C_Collision` simulation.
- Server-authoritative meteor pattern validation.
### DummyClient v2.0 Dungeon Gameplay Basic

DummyClient v2.0 can optionally send basic gameplay packets after entering the Bakal DungeonRoom.

Scope:

- TCP `C_Move` movement only.
- TCP `C_Skill` attack only.
- No UDP movement in v2.0.
- No `C_Collision`, hit simulation, or reward simulation in v2.0.
- No server, Unity, Protocol, RoomTransfer, or MatchManager change is required.

New options:

```text
--enableMovement true|false
--movementTransport tcp
--movementIntervalMs 200
--movementRadius 1.5
--movementPattern patrol
--movementSpeed 1.0
--enableAttack true|false
--attackIntervalMs 1500
--skillIds 2,3,4
--gameplayDurationSec 60
--gameplayStartDelayMs 1000
```

Defaults keep the old v1 behavior:

```text
enableMovement=false
enableAttack=false
movementTransport=tcp
movementPattern=patrol
skillIds=2,3,4
```

Gameplay-enabled fill-visible example:

```powershell
dotnet run --project E:\task\Server\Server\DummyClient\DummyClient.csproj -- --scenario fill-visible --clients 3 --expectedExternal 1 --host 127.0.0.1 --port 8080 --prefix PD_Dummy --delayMs 100 --sceneReadyDelayMs 100 --holdAfterDungeonSec 120 --enableMovement true --movementTransport tcp --movementIntervalMs 200 --movementRadius 1.5 --movementPattern patrol --movementSpeed 1.0 --enableAttack true --attackIntervalMs 1500 --skillIds 2,3,4 --gameplayDurationSec 60 --gameplayStartDelayMs 1000
```

Movement behavior:

- Starts after dungeon `S_EnterGame` / entered-dungeon state.
- Waits `gameplayStartDelayMs` before sending gameplay packets.
- Uses the dungeon entry `PositionInfo` as origin.
- Sends small left/right patrol `C_Move` packets every `movementIntervalMs`.
- Sends one final idle `C_Move` after gameplay completes.

Attack behavior:

- Sends `C_Skill` every `attackIntervalMs`.
- Cycles through `skillIds`, default `2,3,4`.
- Unity visible clients should receive `S_Skill` and play dummy `OtherPlayer` attack animations.

Expected summary fields when gameplay is enabled:

```text
MovementEnabled: True
AttackEnabled: True
GameplayStarted: 3/3
GameplayCompleted: 3/3
MoveSent: ...
SkillSent: ...
PD_Dummy_0001: EnteredDungeon=True, GameplayStarted=True, MoveSent=..., SkillSent=..., LastSkillId=..., LastPosition=(...,...)
```

Verification points:

- Existing fill-visible command without gameplay options must still behave as v1.
- With `--enableMovement true`, server should receive TCP `C_Move` and broadcast `S_Move`.
- With `--enableAttack true`, server should receive `C_Skill` and broadcast `S_Skill`.
- Unity should show dummy movement and `Attack1/2/3` animations.

### Nameplate Demo Notes

Unity nameplate display has been verified in both Town and BakalScene.

Current behavior:

- Dummy names use the `PD_Dummy` prefix.
- Dummy labels can be displayed as `[DUMMY] PD_Dummy_0001`.
- Visible Unity player labels can be displayed as `[USER] Player_0090`.
- Nameplates are created as World Space UI under the player object.

BakalScene note:

The Bakal tilemaps use the `Tile` sorting layer. Character sprites use the `Charecter` sorting layer. World Space Canvas labels must explicitly override sorting and render on a layer/order above the tilemap. Otherwise the label can exist in the hierarchy but appear hidden behind the tilemap.

Current nameplate sorting policy:

```text
Canvas RenderMode = WorldSpace
Canvas Override Sorting = true
Sorting Layer = Charecter
Order in Layer = 100
```

When changing the nameplate prefab or runtime label creation, verify:

- Sorting Layer
- Order in Layer
- Canvas scale
- Camera visibility
- Tilemap render order
- Whether the label is regenerated after `S_SceneMove` and `C_SceneReady`




### DummyClient v2.2 Follow-Enemy Movement

DummyClient v2.2 adds an enemy-aware movement pattern for dungeon gameplay demos. It still uses TCP `C_Move` only and does not send `C_Collision`.

Scope:

- DummyClient-only change.
- Uses enemies received through `S_Spawn.Objects`.
- Identifies enemies from the high bits of `ObjectInfo.ObjectId`, matching the server object id policy.
- Chooses a target enemy and moves toward its `PositionInfo` with simple straight-line movement.
- Stops when the dummy reaches `followStopDistance`.
- Keeps existing `patrol` movement unchanged.
- Keeps attack as TCP `C_Skill`; optional range gating can skip attack when the target is outside `attackRange`.
- No UDP movement, collision simulation, hit/reward simulation, enemy movement, pathfinding, Protocol change, Server change, or Unity change.

Additional options:

```text
--movementPattern patrol|follow-enemy
--targetEnemy nearest|first
--followStopDistance 1.2
--attackOnlyInRange false
--attackRange 1.5
--enemyRefreshIntervalMs 500
```

Defaults preserve existing behavior:

```text
movementPattern=patrol
targetEnemy=nearest
attackOnlyInRange=false
```

Follow-enemy example:

```powershell
dotnet run --project E:\task\Server\Server\DummyClient\DummyClient.csproj -- --scenario fill-visible --clients 3 --expectedExternal 1 --host 127.0.0.1 --port 8080 --prefix PD_Dummy --delayMs 100 --sceneReadyDelayMs 100 --holdAfterDungeonSec 120 --enableMovement true --movementTransport tcp --movementIntervalMs 200 --movementRadius 1.5 --movementPattern follow-enemy --movementSpeed 1.0 --targetEnemy nearest --followStopDistance 1.2 --enableAttack true --attackIntervalMs 1500 --skillIds 2,3,4 --gameplayDurationSec 60 --gameplayStartDelayMs 1000
```

Expected summary additions:

```text
MovementPattern: follow-enemy
TargetEnemyMode: nearest
EnemyTracked: 3/3
FollowReached: 3/3
PD_Dummy_0001: ... EnemyTracked=True, TargetEnemyId=..., LastTarget=(...,...), LastDistance=..., FollowReached=True
```

If no enemy appears in `S_Spawn.Objects`, DummyClient logs that follow-enemy movement is skipped. It does not silently fall back to patrol, so spawn/object-sync issues are visible during testing.

### DummyClient v2.3 C_Collision / Reward Simulation

DummyClient v2.3 can optionally send TCP `C_Collision` after a TCP `C_Skill` to exercise server hit/death/reward flow.

Scope:

- DummyClient-only change.
- Uses the enemy `ObjectInfo` tracked from `S_Spawn.Objects` in v2.2.
- Sends `C_Skill` first, then schedules one `C_Collision` for the same target after `collisionDelayMs`.
- Does not send standalone collision without a preceding skill in the dummy attack loop.
- Observes reward through `S_AddItem` count and last item template/count fields.
- Tracks `S_Die` / `S_Despawn` to avoid repeatedly targeting known dead/despawned enemies.
- Still no UDP movement, server runtime change, Protocol change, Unity change, enemy movement, pathfinding, or meteor math change.

Additional options:

```text
--enableCollision true|false
--collisionDelayMs 200
--collisionOnlyInRange true|false
--maxCollisionsPerTarget 0
--stopAfterReward false
```

Defaults preserve existing behavior:

```text
enableCollision=false
collisionOnlyInRange=true
maxCollisionsPerTarget=0
stopAfterReward=false
```

Collision-enabled example:

```powershell
dotnet run --project E:\task\Server\Server\DummyClient\DummyClient.csproj -- --scenario fill-visible --clients 3 --expectedExternal 1 --host 127.0.0.1 --port 8080 --prefix PD_Dummy --delayMs 100 --sceneReadyDelayMs 100 --holdAfterDungeonSec 120 --enableMovement true --movementTransport tcp --movementIntervalMs 200 --movementRadius 1.5 --movementPattern follow-enemy --movementSpeed 1.0 --targetEnemy nearest --followStopDistance 1.2 --enableAttack true --attackOnlyInRange true --attackRange 1.5 --attackIntervalMs 1500 --skillIds 2,3,4 --enableCollision true --collisionDelayMs 200 --collisionOnlyInRange true --gameplayDurationSec 60 --gameplayStartDelayMs 1000
```

Expected summary additions:

```text
CollisionEnabled: True
CollisionSent: ...
CollisionSkippedNoTarget: ...
CollisionSkippedOutOfRange: ...
CollisionSkippedDeadTarget: ...
CollisionSkippedLimit: ...
AddItemReceived: ...
RewardObserved: .../...
PD_Dummy_0001: ... CollisionSent=..., AddItemReceived=..., LastRewardTemplateId=..., LastRewardCount=..., LastCollisionTargetId=...
```

Recommended verification:

1. Run fill-visible with `follow-enemy`, `enableAttack=true`, `attackOnlyInRange=true`, and `enableCollision=true`.
2. Confirm the server logs `C_Collision received` and enemy damage/death logs.
3. Confirm `[REWARD]` server logs when an enemy dies.
4. Confirm DummyClient summary increments `CollisionSent` and, if the dummy receives reward, `AddItemReceived` / `RewardObserved`.
5. Re-run with `--enableCollision false` to confirm v2.2 movement/attack behavior remains unchanged.

### DummyClient follow-enemy diagnostics

When `--movementPattern patrol` moves correctly but `--movementPattern follow-enemy` does not, the TCP movement pipeline is known-good. Use `--verbose true` with follow-enemy to inspect DummyClient-side target selection and distance calculation.

Diagnostic logs added:

```text
[SPAWN] ObjId=..., Name=..., DetectedType=..., Pos=(x,y), HasPos=True
[SPAWN] Enemy stored. ObjId=..., Pos=(x,y)
[SPAWN] Objects inspected=..., EnemyStoredNew=..., EnemyCount=...
[FOLLOW] Origin captured. Player=..., Origin=(x,y), Current=(x,y), PlayerInfoObjectId=...
[FOLLOW] EnemyTracked=False, EnemyCount=0, Reason=NoEnemy
[FOLLOW] Player=..., TargetId=..., Current=(x,y), Target=(x,y), Dist=..., Stop=..., ShouldMove=True, Next=(x,y)
[FOLLOW] Player=..., TargetId=..., Current=(x,y), Target=(x,y), Dist=..., Stop=..., ShouldMove=False, Reason=WithinStopDistance
[MOVE] Player=..., Sent C_Move Pos=(x,y), State=..., MoveDir=..., MoveSent=...
```

Summary fields added:

```text
EnemyCount
FollowShouldMove
FollowIdle
LastTarget
LastDistance
LastPosition
MoveSent
```

Diagnosis guide:

- `EnemyCount=0`: S_Spawn enemy detection issue.
- `EnemyCount>0` and `LastTarget=(0,0)`: enemy PosInfo/default-position issue.
- `LastDistance <= followStopDistance`: DummyClient believes it is already close enough, so it sends idle movement.
- `FollowShouldMove>0` and `MoveSent=0`: follow movement send bug.
- `MoveSent>0` but no Unity movement: re-check client movement pipeline, though patrol success makes this unlikely.


