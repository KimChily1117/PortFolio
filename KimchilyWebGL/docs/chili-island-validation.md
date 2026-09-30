# Chili Island verification

## Current delivery — mobile layout and portal relay, 2026-09-30

Source commit: `2672fce9c`. The [current status and work log](current-status-and-work-log.md) collects the full project history, version table, execution paths, and remaining checks. Documentation updates do not count as a new runtime test run.

The portal now admits participants into a second cooperative area in the same scene. Players cross the physical bridge and charge star, moon, sun, and leaf pads in order for 1.5 seconds each. The initial contributors must be distinct up to the round's required player count; subsequent multiplayer turns alternate. Solo demonstration remains supported. Both admission and relay outcomes are authored in server TS, with client TS handling the HUD and visuals. No game-specific C# runtime rule or teleport exception was added.

Mobile layouts at widths up to 600 CSS pixels or heights up to 600 CSS pixels reserve a separate toolbar row above the Unity Canvas. This avoids mixing CSS button positions with Unity's height-based Canvas scaling. Browser checks covered 390×844 portrait, 844×390 landscape, and the final 896×520 layout. In each mobile layout, the toolbar occupied y=0..72 and the Canvas began at y=72, keeping Unity chat outside the exit-button region. Portrait/landscape chat panels were opened, and the temporary viewport override was reset. The 1280×720 desktop layout retains its overlay toolbar.

| Current check | Result |
|---|---|
| Server Release checks | 57 passed, 0 failed; build warnings/errors 0 |
| Added relay checks | 7 passed within the server total; includes three participants |
| Authored client TS strict type check | Passed |
| Unity common WebGL runtime | Built successfully; final CSS-only breakpoint adjustment copied from template to served build |
| Isolated Creator generation, world build, publish | Successful |
| Published WebGL walkthrough with final server | Solo first mission → portal → physical bridge → all four relay pads → ALL CLEAR |
| Final browser error console | No error entries; expanded in-game chat showed connected status |

The added checks cover closed-portal rejection, bypassing the entrance, height/grounding requirements, distinct contributors, ordered pads, late joining, expired position recovery, shared completion, and replay cleanup. Evidence: `KimchilyServer/Artifacts/checks/20260930-020458`.

The browser walkthrough exposed an existing facing-angle boundary problem: at north-facing rest, position updates were rejected and the last accepted yaw remained near 360 degrees. Turning away restored updates. Unity's Euler conversion can retain a small negative angle near north ([Unity reference source](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Runtime/Export/Math/Quaternion.cs)). The common server accepts the narrow -0.006-degree boundary tolerance and the equivalent full-turn endpoint, then canonicalizes the stored/broadcast heading to [0,360). One additional regression check verifies repeated 360-degree samples and small negative values keep refreshing while larger negative, greater-than-360, and non-finite angles remain rejected. Movement distance checks and TS game rules are unchanged by this transport fix.

After the final server fix, a fresh browser session completed both missions using ordinary keyboard movement. The north-facing sun-pad charge succeeded without turning away. Completion activated the second garden's celebration and replay button. Final 896×520 expanded-chat verification retained ALL CLEAR and connected status, with the exit button ending at y=58 and the Canvas beginning at y=72. Screenshots: `Artifacts/portal-relay/portal-crossing.png`, `relay-complete.png`, and `mobile-final-chat.png`. These browser viewport checks are not physical-phone tests.

**User-reported device check:** on 2026-09-30, the user reported simultaneous operation on up to three device types ("동시 3종까진 확인"). Model names, OS/browser versions, and detailed scenario coverage were not recorded. This report applies to the preceding delivery; the new second-stage mission still needs the user's physical-device walkthrough. It is separate from the automated three-participant relay test above.

### Current published revision

`webgl-20260930T014824479Z-2d905a55`

- Rule JS SHA-256: `0e7ac1ee9620519d7b9b55696fb7a03527260096a7f0c57a706cc7c784a1d8e4`.
- World manifest SHA-256: `cd2a2ec343f50c3006cd7b721d4fa83003adb328564bf3bc6b0eaeadfbd50b7d`.
- [Current QR and entry page](http://192.168.0.4:8788/w/chili-island/webgl-20260930T014824479Z-2d905a55).
- Final mobile screenshot: `Artifacts/portal-relay/mobile-final-chat.png`.
- Source changes belong to Task and are mirrored to the Git management folder. The user's MyWorld and generated chat-font serialization are excluded.

## Previous delivery — TypeScript game rules and presentation, 2026-09-29

That delivery introduced server-side `PortalRules.ts` for occupancy, round state, hold time, completion, and reset. Client-side `PortalGarden.ts` authored the HUD, button actions, pad visuals, and portal animation. C# hosted the generic script VM, transport, state versions, JSON boundaries, and TMP renderer. The former C# `PortalPuzzle` and game-specific HUD/client were removed.

- TypeScript SDK 0.3.0; Networking SDK 0.4.0.
- Approved rule ID: `chili-portal-ts-v1`.
- Rule JavaScript SHA-256: `5acf8acbf0153a0b5f7672984c27498826c31c527e91131cdb6f61ce9fb04290`.
- [Architecture and portfolio explanation in Korean](portfolio-typescript-multiplayer.md).

| Check | Result at that delivery |
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

### Previous published revision — 2026-09-29

`webgl-20260929T120442251Z-263c8bfc`

- World manifest SHA-256: `6ddc31f2e0bed96379f192665dfce631603d72649169c5428c444644e957cda5`.
- [Previous QR and entry page](http://192.168.0.4:8788/w/chili-island/webgl-20260929T120442251Z-263c8bfc).
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
