# Chili Island verification — 2026-09-29

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
