# Chili Island verification — 2026-09-29

## Current delivery — TypeScript game rules and presentation

The current demo uses server-side `PortalRules.ts` for occupancy, round state, hold time, completion, and reset. Client-side `PortalGarden.ts` authors the HUD, button actions, pad visuals, and portal animation. C# hosts the generic script VM, transport, state versions, JSON boundaries, and TMP renderer. The former C# `PortalPuzzle` and game-specific HUD/client were removed.

- TypeScript SDK 0.3.0; Networking SDK 0.4.0.
- Approved rule ID: `chili-portal-ts-v1`.
- Rule JavaScript SHA-256: `5acf8acbf0153a0b5f7672984c27498826c31c527e91131cdb6f61ce9fb04290`.
- [Architecture and portfolio explanation in Korean](portfolio-typescript-multiplayer.md).

| Check | Current result |
|---|---|
| C# server Release checks | 49 passed |
| Unity EditMode | 60 passed |
| Unity PlayMode | 121 passed |
| TS compiler | 20 passed |
| Server TS bundle compiler regression | 7 passed |
| TS facade | 24 passed |
| Actual Jint VM | 34 passed |
| TS strict typing / LanguageService | Passed |
| Common WebGL runtime build | Successful |
| Isolated Creator world build and publish | Successful; interactive Creator stayed open |

Server tests include two real WebSocket clients, late joining, stale positions, reset, hash/world mismatches, bounded JSON, script resource faults, and isolation from other rooms. A second counter game runs on the same C# host. A test also copies the actual portal TS source, changes `holdSeconds` from 3 to 5, compiles that source, and verifies the new five-second result without changing or rebuilding C# game logic.

The separate [server compiler regression suite](../KimchilyServer/tools/tests/compile-script.test.cjs) verifies that identical input, type-only edits, and LF/CRLF conversion reuse the same immutable JavaScript bundle without rewriting its original provenance or timestamp. Corrupted code, world approval, script ID, and provenance type are rejected. It runs with Node's `--test` runner and writes isolated fixtures under `KimchilyServer/Artifacts/compiler-checks`.

The published WebGL walkthrough verified nickname entry, a solo round, movement onto the star pad, the server TS countdown, and portal completion. A second browser participant joined afterward and received the completed portal and existing avatar. That participant pressed the TS-authored reset button; the first participant then showed the same two-player waiting state. The second browser's captured error console contained no error entries. Screenshots are saved as `Artifacts/typescript-game/portal-ts-clear.png`, `peer-ts-state.png`, and `peer-ts-reset.png` in the Task workspace.

Two-player simultaneous hold is covered by the server tests. The browser walkthrough verifies solo completion, two-client late joining, and reset propagation; it does not establish simultaneous play on two physical phones. Portrait layout was checked in the earlier art delivery below, while current HUD behavior is also covered by Unity tests. Physical multi-phone testing remains separate.

### Current published revision

`webgl-20260929T120442251Z-263c8bfc`

- World manifest SHA-256: `6ddc31f2e0bed96379f192665dfce631603d72649169c5428c444644e957cda5`.
- [Current QR and entry page](http://192.168.0.4:8788/w/chili-island/webgl-20260929T120442251Z-263c8bfc).
- Local publisher: port 8788; C# script host: port 8790. The LAN address can change and these links require the local services to be running.
- Release server test evidence: `KimchilyServer/Artifacts/checks/20260929-120522`.
- Unity XML reports: `KimchilyUnityRuntime/Artifacts/runtime-editmode.xml` and `runtime-playmode.xml`.
- Build identity and counts: `Artifacts/typescript-game/verification.json`.

## Historical record — original art delivery and C# preset

The remainder documents the earlier C# preset build. Its package versions, counts, and URL are historical, not the current TS delivery.

## Delivered content

- 14 original environment/prop prefabs, 5,540 triangles across unique generated meshes.
- Dedicated ChiliIsland scene; existing Unity-Chan player reused.
- OBJ/MTL files, two custom shaders, and generated art-only `.unitypackage` (about 1.5 MB).
- C# `chili-portal-v1` cooperative preset, Networking 0.3.0 HUD/client, TypeScript 0.2.0 Room facade.

## Checks

| Check | Result |
|---|---|
| C# server Release checks | 40 passed |
| Unity EditMode | 60 passed |
| Unity PlayMode, including HUD layout | 115 passed |
| TS compiler | 20 passed |
| TS facade | 20 passed |
| Actual Jint VM | 32 passed |
| TS strict typing / LanguageService | Passed |
| Art metadata | 83 unique valid GUIDs under Assets/Demos |
| Separate clone build script | Successful world build; interactive Creator remained open |

Browser verification used the published WebGL runtime and world, rather than an editor-only preview. The participant entered a nickname, connected to the C# server, started a one-player round, walked onto the star pad, held it for three seconds and opened the animated portal. A second browser participant then joined and received the completed state and the first avatar. Korean TMP chat was displayed. The status card was moved to the upper left on wide screens after an overlap with the speech bubble was observed.

The 390×844 browser viewport was inspected for portrait layout and the temporary viewport override was reset. Physical multi-phone play is still a separate device check. A two-player simultaneous hold is covered by server checks; the browser walkthrough above tested solo completion and two-client late joining, not simultaneous play on two physical phones. Inactive tabs can stop supplying fresh poses.

## Published revision

`webgl-20260929T112318821Z-78178e98`

- World manifest SHA-256: `5593786af00db3f87bca59e110c9815f310ecef9117faf8081f7753406ff8601`
- [QR and entry page](http://192.168.0.4:8788/w/chili-island/webgl-20260929T112318821Z-78178e98)
- Local publisher: port 8788; existing C# server: port 8790.

Logs, Unity XML reports, screenshots, previews, and exported package remain under the Task workspace's ignored `Artifacts` folders. The source is mirrored from Task to the Git management directory with a hash manifest and backups. The user's MyWorld scene is excluded from that mirror.
