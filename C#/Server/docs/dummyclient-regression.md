# DummyClient Regression and Demo Guide

This document summarizes current DummyClient scenarios for Project Dawn regression tests and demos.

## Purpose

DummyClient is a real TCP client simulator. It connects to the GameServer port, uses normal packet framing, logs in, enters rooms, requests matchmaking, handles scene transfer, and can optionally send movement, skill, and collision packets.

It does not call `MatchManager`, `RoomTransferService`, DB code, or server handlers directly.

Project:

```text
Server/DummyClient/DummyClient.csproj
```

## Current Scenarios

Supported scenarios:

```text
create-players
town-load
fill-visible
fill-dummy
```

### create-players

Creates deterministic dummy accounts/characters through the normal TCP login/create/enter-game flow.

```powershell
dotnet run --project "E:\task\Server\Server\DummyClient\DummyClient.csproj" -- --scenario create-players --clients 4 --host 127.0.0.1 --port 8080 --prefix PD_Dummy --delayMs 100
```

Use this before `fill-visible` or `fill-dummy` when the dummy accounts do not exist yet.
### town-load

Keeps many dummy clients in the Town room without requesting matchmaking. This is for server-side MORPG-style Town load checks: login, enter Town, optional TCP movement, and movement broadcast observation. It does not measure Unity rendering FPS. All clients initially enter authoritative private MyRoom at `(0,0)`; a movement-enabled Town load clamps its first patrol position into the public Town map and thereby enters public AOI.

Small smoke run:

```powershell
dotnet run --project "E:\task\Server\Server\DummyClient\DummyClient.csproj" -- --scenario town-load --clients 20 --host 127.0.0.1 --port 8080 --prefix PD_TownLoad --delayMs 20 --holdInTownSec 30 --enableMovement true --movementPattern patrol --patrolMode mixed --movementIntervalMs 200 --movementRadius 1.5 --movementSpeed 0.35 --verbose false
```

Larger load run:

```powershell
dotnet run --project "E:\task\Server\Server\DummyClient\DummyClient.csproj" -- --scenario town-load --clients 200 --host 127.0.0.1 --port 8080 --prefix PD_TownLoad --delayMs 10 --holdInTownSec 120 --enableMovement true --movementPattern patrol --patrolMode mixed --movementIntervalMs 200 --movementRadius 1.5 --movementSpeed 0.35 --timeoutSec 180 --verbose false
```

Rules:

- `town-load` never sends `C_CreateRoom`.
- If no gameplay flags are set, it defaults to movement-only mixed patrol.
- Attack and collision flags are ignored for Town load; there are no Town combat targets in this scenario.
- `TargetRoomId` is tracked as Town room `1` for movement isolation diagnostics.
- DummyClient uses TCP `C_Move` by design. UDP movement remains the Unity runtime path.
- With movement enabled and Town MovementBounds loaded, `CaptureGameplayOrigin` clamps the private `(0,0)` origin to a valid public Town span; the first move satisfies the server's private-to-public position rule.
- With movement disabled, dummy clients remain private and are intentionally excluded from one another's Town AOI.

Verify:

- DummyClient summary shows `EnteredTown: N/N`, `MatchRequested: 0/N`, `GameplayCompleted: N/N`, and `Result: SUCCESS`.
- `TownLoadMetrics` reports `ConnectToTownMs`, `LoginToTownMs`, `TownHoldActualMs`, `AllConnectedInMs`, `AllEnteredTownInMs`, `MoveSentPerSec`, `ReceivedMovePerSec`, and failed client count.
- `/api/players/online` shows the loaded dummy players.
- `/api/rooms` shows Town player count growth.
- WebLauncher Online Players and Rooms cards update while clients are connected.
- For a public movement load, server logs show public transition/application rather than repeated bounds or speed drops.

### fill-visible

Fills part of a 4-player party with dummy clients and waits for one or more visible Unity clients.

```powershell
dotnet run --project "E:\task\Server\Server\DummyClient\DummyClient.csproj" -- --scenario fill-visible --clients 3 --expectedExternal 1 --host 127.0.0.1 --port 8080 --prefix PD_Dummy --delayMs 100 --sceneReadyDelayMs 100 --holdAfterDungeonSec 120 --verbose true
```

Rules:

- `clients + expectedExternal == 4`
- `clients` must be 1..3
- four dummy clients are rejected in this scenario because they can match before Unity joins

### fill-dummy

Creates complete dummy-only parties and is the current multi-room regression path.

Eight clients create two 4-player parties and two Bakal rooms:

```powershell
dotnet run --project "E:\task\Server\Server\DummyClient\DummyClient.csproj" -- --scenario fill-dummy --clients 8 --partySize 4 --expectedRooms 2 --host 127.0.0.1 --port 8080 --prefix PD_Dummy --delayMs 100 --sceneReadyDelayMs 100 --holdAfterDungeonSec 180 --enableMovement true --enableAttack false --enableCollision false --movementPattern patrol --patrolMode mixed --patrolRadius 1.5 --patrolIntervalMs 500 --patrolSpeed 0.35 --verbose true
```

Rules:

- `clients >= partySize`
- `clients % partySize == 0`
- default `expectedRooms = clients / partySize`
- if no gameplay flags are set, `fill-dummy` defaults to movement-only mixed patrol

## Match Readiness

MatchCondition v1 requires dummy players to have equipped weapon and armor.

For players whose names start with `PD_Dummy`, the server login/create flow now ensures starter equipment in DB:

- preferred weapon template id: `1`
- preferred armor template id: `100`
- fallback to the first valid loaded weapon/armor template if preferred ids are unavailable
- existing starter items are reused
- missing starter items are created in empty inventory slots
- unequipped starter items are marked `Equipped=true`

Rerun `create-players` before `fill-dummy` when adding more dummy accounts, then start a fresh match run so the corrected equipment is loaded into each session inventory.

Manual equipment setup remains available:

```powershell
dotnet run --project "E:\task\Server\Server\TestEquipmentSetupTool\TestEquipmentSetupTool.csproj" -- --prefix PD_Dummy --count 8 --weaponTemplateId 1 --armorTemplateId 100 --equip true --overwriteExistingEquip true --dryRun false --dataPath "E:\task\C#\Project_Dawn\Assets\Resources\Data"
```

## Gameplay Options

Movement options:

```text
--enableMovement true|false
--movementTransport tcp
--movementIntervalMs 200
--movementRadius 1.5
--movementPattern patrol|follow-enemy
--patrolMode horizontal|vertical|box|diagonal|mixed
--movementSpeed 1.0
--patrolRadius 1.5
--patrolIntervalMs 500
--patrolSpeed 0.35
```

Attack options:

```text
--enableAttack true|false
--attackIntervalMs 1500
--skillIds 2,3,4
--attackOnlyInRange true|false
--attackRange 1.5
```

Follow-enemy options:

```text
--movementPattern follow-enemy
--targetEnemy nearest|first
--followStopDistance 1.2
--followArrivalEpsilon 0.05
--sendIdleOnArrival true
--followInRangeMode idle|strafe
--strafeDistance 0.45
--strafeSwitchMs 1200
--enemyRefreshIntervalMs 500
```

Collision/reward options:

```text
--enableCollision true|false
--collisionDelayMs 200
--collisionOnlyInRange true|false
--maxCollisionsPerTarget 0
--stopAfterReward false
```

Gameplay duration:

```text
--gameplayDurationSec 60
--gameplayStartDelayMs 1000
```

Only TCP movement is supported by DummyClient. UDP movement remains a Unity/runtime path.

## Recommended Regression Runs


### Town Load

Use this to load only the Town room without dungeon matching:

```powershell
dotnet run --project "E:\task\Server\Server\DummyClient\DummyClient.csproj" -- --scenario town-load --clients 200 --host 127.0.0.1 --port 8080 --prefix PD_TownLoad --delayMs 10 --holdInTownSec 120 --enableMovement true --movementPattern patrol --patrolMode mixed --movementIntervalMs 200 --movementRadius 1.5 --movementSpeed 0.35 --timeoutSec 180 --verbose false
```

Verify:

- Town receives all dummy clients.
- `MatchRequested` remains zero.
- TCP movement is sent from Town clients.
- Movement-enabled clients transition from private MyRoom into the public Town bounds before AOI fanout is measured.
- `/api/rooms`, `/api/players/online`, and WebLauncher reflect the load while clients are held in Town.

### Queue Visibility

Use three dummy clients and leave one visible slot open:

```powershell
dotnet run --project "E:\task\Server\Server\DummyClient\DummyClient.csproj" -- --scenario fill-visible --clients 3 --expectedExternal 1 --host 127.0.0.1 --port 8080 --prefix PD_Dummy --delayMs 100 --sceneReadyDelayMs 100 --holdAfterDungeonSec 120 --verbose true
```

Verify:

- `/api/matching/queue` shows three waiting players
- WebLauncher Matching Queue card shows the same players
- Unity visible client can complete the party and enter Bakal

### Multi-Room Isolation

Use eight dummy clients:

```powershell
dotnet run --project "E:\task\Server\Server\DummyClient\DummyClient.csproj" -- --scenario fill-dummy --clients 8 --partySize 4 --expectedRooms 2 --host 127.0.0.1 --port 8080 --prefix PD_Dummy --delayMs 100 --sceneReadyDelayMs 100 --holdAfterDungeonSec 180 --enableMovement true --enableAttack false --enableCollision false --movementPattern patrol --patrolMode mixed --patrolRadius 1.5 --patrolIntervalMs 500 --patrolSpeed 0.35 --verbose true
```

Verify:

- two Bakal rooms are created
- each Bakal room has four players
- movement broadcasts stay within the correct room
- DummyClient summary reports expected room grouping
- `/api/players/online` shows eight players
- `/api/rooms` shows separate Bakal room snapshots

### Follow Enemy and Collision

Use follow-enemy movement with attack and collision:

```powershell
dotnet run --project "E:\task\Server\Server\DummyClient\DummyClient.csproj" -- --scenario fill-visible --clients 3 --expectedExternal 1 --host 127.0.0.1 --port 8080 --prefix PD_Dummy --delayMs 100 --sceneReadyDelayMs 100 --holdAfterDungeonSec 120 --enableMovement true --movementTransport tcp --movementIntervalMs 200 --movementPattern follow-enemy --movementSpeed 1.0 --targetEnemy nearest --followStopDistance 1.2 --enableAttack true --attackOnlyInRange true --attackRange 1.5 --attackIntervalMs 1500 --skillIds 2,3,4 --enableCollision true --collisionDelayMs 200 --collisionOnlyInRange true --gameplayDurationSec 60 --gameplayStartDelayMs 1000 --verbose true
```

Verify:

- DummyClient tracks enemies from `S_Spawn`
- follow movement sends TCP `C_Move`
- attack sends TCP `C_Skill`
- collision sends TCP `C_Collision` after skill
- server logs accepted or rejected hit reasons
- reward is observed through `S_AddItem` when a dummy receives a drop

## Useful Server Logs

Room and transfer:

```text
[ROOM_MANAGER] Room created. RoomType=Bakal
[TRANSFER] Party transfer start. TargetRoomId=...
[MOVE] S_Move broadcast. RoomId=..., Recipients=...
```

Matching:

```text
[MATCH] Ticket created. Player=..., QueueKey=...
[MATCH] Party matched...
[MATCH] Auto start party transfer...
```

Combat:

```text
[SKILL] Active cast recorded...
[HIT] RangeCheck...
[HIT] Enemy damaged...
[HIT] Collision ignored. Reason=...
[REWARD] ...
```

## Current Non-Goals

- DummyClient does not implement UDP movement.
- DummyClient does not emulate Unity animation events.
- DummyClient does not replace visible Unity validation.
- DummyClient does not bypass real TCP packet framing.
- DummyClient does not mutate DB state directly.


