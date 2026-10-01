# SQL Server local recovery

## Run on another Windows desktop

Pull `main` and run the commands below from the repository root. The server source and startup scripts are portable; the local `GameDB`, Windows registry setting and SQL Server instance repairs are specific to each PC and are not copied by Git.

Prerequisites: a .NET SDK, the x64 `Microsoft.NETCore.App` 3.1 runtime (the current projects target `netcoreapp3.1`), and SQL Server Express LocalDB. Check the .NET installation with `dotnet --list-sdks` and `dotnet --list-runtimes`. The Unity project records editor version **2021.3.11f1**.

For the first run on a PC without an existing game database:

```powershell
git pull --ff-only origin main
SqlLocalDB info MSSQLLocalDB > $null 2>&1
if ($LASTEXITCODE -ne 0) {
    SqlLocalDB create MSSQLLocalDB
    if ($LASTEXITCODE -ne 0) { throw 'Could not create the LocalDB instance.' }
}
SqlLocalDB start MSSQLLocalDB
if ($LASTEXITCODE -ne 0) { throw 'LocalDB failed to start; check the SQL Server error log.' }
& '.\C#\Server\tools\Start-LocalServer.ps1' -InitializeDatabase -CheckDatabase
```

After `[DB] Ready` and exit code 0, start the game server:

```powershell
& '.\C#\Server\tools\Start-LocalServer.ps1'
```

Open `C#/Project_Dawn` in Unity and play `Assets/Scenes/Title(Login).unity`. The client defaults to `127.0.0.1`, TCP 8080 and UDP 8081, so run the game server on the same desktop for this local test. The Unity client path still needs manual validation; the completed checks below used TCP DummyClient.

If `GameDB` already exists, use `-CheckDatabase` without `-InitializeDatabase` first. Existing partial schemas need a reviewed migration. The sector-size workaround below is only for the corresponding startup failure; it is not a required setup step on every PC.

## Disk-sector startup failure

The local NVMe disk reports a 65,536-byte physical sector. SQL LocalDB fails during master recovery with misaligned log I/O and ntdll faults. SQL Server supports at most 4,096-byte sectors. Microsoft documents the same failure and a stornvme registry workaround:

https://learn.microsoft.com/en-us/troubleshoot/sql/database-engine/database-file-operations/troubleshoot-os-4kb-disk-sector-size

`tools/Repair-SqlServerSectorSize.ps1` diagnoses by default. With `-Apply`, it requires administrator rights, confirms an NVMe system disk and the volume sector size with fsutil, backs up the existing registry key, then sets only `ForcedPhysicalSectorSizeInBytes` to the documented `* 4095` value. Existing custom overrides are left for manual review. It does not reboot Windows, delete a LocalDB instance, or remove database files.

Run from the repository root in an administrator PowerShell:

```powershell
& '.\C#\Server\tools\Repair-SqlServerSectorSize.ps1' -Apply
```

Save open work and reboot Windows after an `Applied` result. The script records the diagnosis, backup and result under `%LOCALAPPDATA%\Temp\project-dawn-fix-20261001\sql-repair`. Because this change adds a previously absent value, rollback is to remove that value in administrator PowerShell and reboot:

```powershell
Remove-ItemProperty -LiteralPath 'HKLM:\SYSTEM\CurrentControlSet\Services\stornvme\Parameters\Device' -Name 'ForcedPhysicalSectorSizeInBytes'
```

## Application configuration

Server config resolution: `PROJECT_DAWN_CONFIG_PATH`, then working-directory `config.json`, then `config.json` beside the executable. `dataPath` is resolved relative to the selected config file. The checked-in `Server/Server/config.example.json` points at this repository's Unity game data.

All default `AppDbContext` instances use `PROJECT_DAWN_DB_CONNECTION_STRING` when set, otherwise `config.json`'s `connectionString`, otherwise the local SQL Server `GameDB` default. Explicit constructor connections still take precedence; the existing match-history-specific override remains available. Credentials belong in local configuration or environment variables, never in source.

After reboot, verify LocalDB and build/run from the repository root:

```powershell
SqlLocalDB start MSSQLLocalDB
$env:PROJECT_DAWN_CONFIG_PATH = (Resolve-Path '.\C#\Server\Server\Server\config.example.json').Path
dotnet run --project '.\C#\Server\Server\Server\Server.csproj' -- --check-database
```

The server checks all five application tables before opening TCP/UDP listeners. On failure it exits with code 1. For a **new, empty demo database**, `--initialize-database --check-database` uses EF's `EnsureCreated` to create the current model, including match history. It never drops an existing database. This demo initialization is not an EF migration workflow: existing partial schemas require a reviewed migration, and databases created with `EnsureCreated` must not be passed to `Migrate` without first establishing a migration baseline.

Then run the server normally and repeat the four-client `town-load` scenario in `dummyclient-regression.md`, followed by a restart/relogin to verify persisted accounts, characters and equipment. Use `--prefix PD_Dummy_Recovery` to enable the existing dummy-equipment setup. External networking and Unity validation are separate follow-up checks.

## Regression tests

```powershell
dotnet run --project '.\C#\Server\Server\ServerRegressionTests\ServerRegressionTests.csproj'
```

These tests use real TCP sockets and an intentionally unavailable SQL endpoint. They verify config/environment precedence, a failed login response followed by another login on the same connection, cleanup after a receive exception, cleanup when the disconnect callback itself fails, and rejection of an invalid packet size. They do not substitute for a successful login against a running SQL Server.

## Verification on 2026-10-01

- Server and regression-test build succeeded; all five TCP/config regression cases passed.
- The real LocalDB startup check reproduced the engine-start failure and exited with code 1 before opening the game listener.
- Administrator fsutil confirmed `PhysicalBytesPerSectorForAtomicity=4096` and `PhysicalBytesPerSectorForPerformance=65536` on C:.
- The repair script backed up the existing registry key, applied `REG_MULTI_SZ` value `* 4095`, and read it back successfully. `sql-repair/repair-result.json` reports `Applied` and `rebootRequired=true`.
- At this point, reboot and real-DB integration checks were pending; the post-reboot results below supersede that state.

The build still targets .NET Core 3.1. A supported-runtime migration is a separate change from this LocalDB recovery.

## Post-reboot recovery and integration results

After the Windows reboot, the disk reported 4,096-byte physical sectors. LocalDB proceeded past the previous disk fault, exposing incomplete initialization: model/msdb/tempdb paths still pointed at non-existent SQL build directories under `D:\dbs\sh`, and the instance owner's Windows login was not correctly provisioned.

The existing LocalDB system data/log files were copied to a backup before repair. The instance was started temporarily with master-only recovery (`-T3608`, single-user mode), following Microsoft's system-database recovery procedure. The system file catalog paths were corrected to the existing LocalDB instance files. Only the existing instance owner, `DESKTOP-JFO0QHI\Yeop`, was provisioned with LocalDB administrator membership. The original `MSSQLLocalDB` instance now starts normally. No existing instance or user database was deleted. The temporary `ProjectDawn` instance used to verify the engine was stopped after testing.

Reference: https://learn.microsoft.com/en-us/sql/relational-databases/databases/move-system-databases

The repaired instance contained only system databases. A new `GameDB` was initialized with the current model, and the startup check passed.

| Check | Result |
| --- | --- |
| First four-client town run | Login, player readiness and town entry: 4/4; failed clients: 0 |
| Town movement | Sent 152, received 438 |
| Game server and LocalDB restart | Completed; the same four player IDs were present afterward |
| Four-client relogin | Login and town entry: 4/4; no character creation requests |
| Account/character/equipment persistence | Four accounts, four characters and eight equipped items; rows unchanged by relogin |
| Four-player dungeon match | Party matched and dungeon entered: 4/4 in room 2 |
| Match-history persistence | One entered match with all four members persisted |
| Restarted-server DB/receive errors | Zero matching error entries |

These are local TCP DummyClient checks. External Internet connectivity and the Unity UDP/visual path still require their own tests.

Logs, snapshots, system-file backups and the machine-specific repair SQL are in `%LOCALAPPDATA%\Temp\project-dawn-fix-20261001\post-reboot`.

## Start the repaired local server

From the repository root:

```powershell
& '.\C#\Server\tools\Start-LocalServer.ps1'
```

The helper builds current sources under `%LOCALAPPDATA%\ProjectDawn\local-server`, selects the repository's example config with a valid game-data path and runs the server in the foreground. Use Ctrl+C to stop it. It checks port 8080 to prevent starting a second listener, and does not change firewall or router settings.

For a DB-only health check, add `-CheckDatabase`. For a custom SQL Server config, add `-ConfigPath 'C:\path\config.json'`. `-InitializeDatabase` is reserved for a new, empty demo database as described above.
