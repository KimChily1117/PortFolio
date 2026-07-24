# Project Dawn Web Launcher MVP

Static HTML/CSS/JS monitoring page for the Project Dawn GameServer Monitoring API.

## Run

1. Start the GameServer.
2. Open `index.html` in a browser.

If your browser blocks `file://` fetches or you prefer a local static server:

```powershell
cd E:\task\Server\WebLauncher
python -m http.server 5500
```

Open:

```text
http://127.0.0.1:5500
```

## Game Start

The Game Start card shows the configured game name, executable path, launch mode, and launch command.

Browsers cannot directly launch a local `.exe` for security reasons. The default Start Game behavior uses copy command mode:

1. Click `Start Game` or `Copy Command`.
2. The launcher copies the configured command.
3. Paste it into PowerShell or the Windows Run dialog.

The command and executable path are configured in `launcher-config.js`:

```javascript
window.ProjectDawnLauncherConfig = {
  gameName: "Project Dawn",
  gameExecutablePath: "E:\\task\\C#\\Project_Dawn\\Builds\\Win64\\Project_Dawn1\\Project_Dawn1.exe",
  gameStartCommand: "start \"\" /D \"E:\\task\\C#\\Project_Dawn\\Builds\\Win64\\Project_Dawn1\" \"E:\\task\\C#\\Project_Dawn\\Builds\\Win64\\Project_Dawn1\\Project_Dawn1.exe\" -screen-width 800 -screen-height 600 -screen-fullscreen 0 -testClientId=build01",
  gameClients: [
    {
      index: 1,
      label: "Client 1",
      executablePath: "E:\\task\\C#\\Project_Dawn\\Builds\\Win64\\Project_Dawn1\\Project_Dawn1.exe",
      startCommand: "start \"\" /D \"E:\\task\\C#\\Project_Dawn\\Builds\\Win64\\Project_Dawn1\" \"E:\\task\\C#\\Project_Dawn\\Builds\\Win64\\Project_Dawn1\\Project_Dawn1.exe\" -screen-width 800 -screen-height 600 -screen-fullscreen 0 -testClientId=build01",
      protocolUrl: "projectdawn://launch?client=1"
    }
  ],
  customProtocolUrl: "projectdawn://launch",
  customProtocolEnabled: true
};
```

The default config includes Client 1 through Client 4. Their paths match `Assets/Editor/MultiplayerTestBuild.cs`, which builds to `Builds/Win64/Project_Dawn{index}/Project_Dawn{index}.exe` and starts clients with `-screen-width 800 -screen-height 600 -screen-fullscreen 0 -testClientId=buildNN`.

Update `gameExecutablePath`, `gameStartCommand`, and `gameClients` if the Unity project directory or project name changes.

## Optional Windows Custom Protocol

The optional custom protocol mode lets the browser request `projectdawn://launch`. Windows must be configured manually first.

Setup flow:

1. Build Project Dawn for Windows from Unity.
2. Update the executable path in `launcher-config.js`.
3. Update the `GAME_DIR` and `GAME_EXE` format in `tools/launch-projectdawn.bat` if the Unity project directory or project name changes.
4. Run the WebLauncher and verify manual launch with `Copy Command`.
5. Copy `tools/register-projectdawn-protocol.reg.template` to a `.reg` file.
6. Edit the `.reg` file so the command path points to your local `launch-projectdawn.bat`.
7. Keep registry path separators escaped as `\\`, for example `E:\\task\\Server\\WebLauncher\\tools\\launch-projectdawn.bat`.
8. Run the `.reg` file to register the protocol.
9. Verify `customProtocolEnabled: true` in `launcher-config.js`.
10. Click `Start Game` or `Client 1` through `Client 4` and accept the browser security prompt if shown.

To remove the protocol registration, copy `tools/unregister-projectdawn-protocol.reg.template` to a `.reg` file and run it.

The templates are intentionally stored as `.template` files so they are not executed accidentally.


## Online Players and Matching Queue

The WebLauncher also shows read-only Monitoring API v2 data:

- Online Players: connected sessions with player name, room, HP, position, state, and transfer state.
- Matching Queue: waiting queue count, queue key, party size, waiting player names, MMR, and equipment readiness.

If these endpoints are unavailable because an older server is running, the cards show `Unavailable` while the existing server status and room cards continue to refresh.

PowerShell checks:

```powershell
Invoke-RestMethod http://127.0.0.1:8090/api/players/online | ConvertTo-Json -Depth 10
Invoke-RestMethod http://127.0.0.1:8090/api/matching/queue | ConvertTo-Json -Depth 10
Invoke-RestMethod http://127.0.0.1:8090/api/server/status | ConvertTo-Json -Depth 6
```

### Matching Queue Waiting Test

Run three dummy clients and leave one external slot open so the queue remains visible:

```powershell
dotnet run --project "E:\task\Server\Server\DummyClient\DummyClient.csproj" -- --scenario fill-visible --clients 3 --expectedExternal 1 --host 127.0.0.1 --port 8080 --prefix PD_Dummy --delayMs 100 --sceneReadyDelayMs 100 --holdAfterDungeonSec 120 --verbose true
```

Expected:

- `/api/matching/queue` shows `totalWaitingPlayers=3`.
- The queue item shows `waitingCount=3` and `partySize=4`.
- The WebLauncher Matching Queue card shows `PD_Dummy_0001` through `PD_Dummy_0003`.

### Full Match Online Players Test

Run eight dummy clients to create two full dungeon rooms:

```powershell
dotnet run --project "E:\task\Server\Server\DummyClient\DummyClient.csproj" -- --scenario fill-dummy --clients 8 --partySize 4 --expectedRooms 2 --host 127.0.0.1 --port 8080 --prefix PD_Dummy --delayMs 100 --sceneReadyDelayMs 100 --holdAfterDungeonSec 180 --enableMovement true --enableAttack false --enableCollision false --movementPattern patrol --patrolMode mixed --patrolRadius 1.5 --patrolIntervalMs 500 --patrolSpeed 0.35 --verbose true
```

Expected:

- `/api/players/online` shows `count=8`.
- `/api/matching/queue` shows `totalWaitingPlayers=0`.
- Room cards still show two Bakal rooms with four players each.
- The WebLauncher Online Players card shows all eight dummy players.


## Recent Events and Reject Reasons

The WebLauncher now also shows Monitoring API v3 visibility:

- Recent Events: latest bounded server events from `/api/events/recent`.
- Reject Reasons: combat reject counters from `rejectReasonCounts`.

The card is optional. If `/api/events/recent` is unavailable because an older server is running, Recent Events and Reject Reasons show `Unavailable` while server status, rooms, online players, and matching queue cards continue to work.

PowerShell check:

```powershell
Invoke-RestMethod http://127.0.0.1:8090/api/events/recent | ConvertTo-Json -Depth 12
```

Expected during demos:

- `fill-visible` creates `MatchAccepted` events.
- `fill-dummy --clients 8 --partySize 4` creates `PartyMatched`, `TransferStarted`, `DungeonEntered`, and `RoomCreated` events.
- Fast attack tests create `SkillCastRejected` events and update `rejectReasonCounts`.

Full checklist: [Current Status and Test Checklist](../docs/current-status-and-test-checklist.md)
## Monitoring API

The page calls:

```text
http://127.0.0.1:8090/api/health
http://127.0.0.1:8090/api/server/status
http://127.0.0.1:8090/api/rooms
http://127.0.0.1:8090/api/events/recent
```

The GameServer must be running and the Monitoring API must be listening on `127.0.0.1:8090`.

## Multi-room Test

Run dummy-only room isolation test:

```powershell
dotnet run --project "E:\task\Server\Server\DummyClient\DummyClient.csproj" -- --scenario fill-dummy --clients 8 --partySize 4 --expectedRooms 2 --host 127.0.0.1 --port 8080 --prefix PD_Dummy --delayMs 100 --sceneReadyDelayMs 100 --holdAfterDungeonSec 180 --enableMovement true --enableAttack false --enableCollision false --movementPattern patrol --patrolMode mixed --patrolRadius 1.5 --patrolIntervalMs 500 --patrolSpeed 0.35 --verbose true
```

Expected dashboard state:

- Server badge: Online
- RoomId 1: Town
- RoomId 2: Bakal, Players 4, Boss `Bakal_2Phase`
- RoomId 3: Bakal, Players 4, Boss `Bakal_2Phase`

## Notes

- No React, Vue, Next.js, Node.js, npm, Electron, or ASP.NET MVC is used.
- The page is static and uses browser `fetch`.
- The browser never attempts to directly open `file:///...exe`.
- The Monitoring API returns cached RoomSnapshot data; it does not expose GameRoom dictionaries directly.


