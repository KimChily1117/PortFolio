# Galaxy S8+ USB deployment

## Device and toolchain checked on 2026-10-15

The connected device reports `SM-G955N` (Galaxy S8+), Android 9 / API 28,
and `arm64-v8a,armeabi-v7a,armeabi`. The project builds `arm64-v8a` and has
minimum API 24, so the device meets those requirements. `/data` had approximately
78.2 GiB available. USB authorization is already granted.

The installed package is `com.portfolio.asteridle`, version code 2 / version 0.2.0.
Installation uses `adb install -r`; the script never uninstalls or clears app data.
The package identity stays the same. Its visible label is now `에픽세븐 로컬 전투`.

The local toolchain consists of Gradle 6.7.1, Android Gradle Plugin 4.2.2,
SDK platform 33, build tools 30.0.3, NDK 21.0.6113669, and CMake 3.18.1.
`JAVA_HOME` initially pointed to JDK 16; the script selects the installed
Microsoft JDK 11 instead. Gradle 6.7.1 cannot run on Java 16
([official compatibility table](https://docs.gradle.org/current/userguide/compatibility.html)).

## Reproducible commands

From the repository root, preparation only (no compilation or installation):

```powershell
./tools/build-android.ps1 -PrepareOnly -BuildRoot D:\CodexBuilds\Cocos2dxPortfolio\android
```

After source and resources are finalized, build, validate, update-install, and launch:

```powershell
./tools/build-android.ps1 -BuildRoot D:\CodexBuilds\Cocos2dxPortfolio\android -Install
```

To install an already completed APK without rebuilding:

```powershell
./tools/build-android.ps1 -BuildRoot D:\CodexBuilds\Cocos2dxPortfolio\android -SkipBuild -Install
```

If several authorized devices are attached, pass `-Device <serial>` explicitly.
The script checks the target API, ABI, free device storage, APK package identity,
and APK native ABI. It does not remove an existing app to bypass signature or
installation failures. Successful execution writes `.deps/android-last-build.json`
with the APK path, byte count, SHA-256, toolchain and install result.

## Large asset handling

At the preparation checkpoint, `Resources` contained 14,953 files totaling
3.12 GiB; imports were still in progress, so this is not the final package size.
Animation JSON and atlas files remain compressed inside the APK. Ogg, WebP and
MP3 files use `noCompress`, since they are already compressed and native audio
may need direct asset file access. No gallery assets are omitted to reduce size.

`-BuildRoot` moves Gradle outputs and native CMake staging to a chosen directory
without moving source files. D: had approximately 141 GiB free, versus about
15.7 GiB on E:. The selected APK output is:

`D:\CodexBuilds\Cocos2dxPortfolio\android\app\outputs\apk\debug\app-debug.apk`

The script requires space for three uncompressed asset copies plus 2 GiB before
building. This is a conservative staging check, not a prediction of APK size.
Final packaging and device results are recorded below. The build retains all
35,599 imported resource files.

## Preparation validation

`-PrepareOnly` succeeded and recognized the authorized S8+.
Gradle `help -PasterBuildRoot=D:/CodexBuilds/Cocos2dxPortfolio/android` succeeded,
confirming the custom build-directory and CMake staging configuration is accepted
by the pinned plugin. This command does not compile or package the application.
Legacy SDK XML namespace warnings were emitted; no configuration error occurred.

During the first native build, AGP 4.2 rejected upstream WebP's CMake `OBJECT`
targets because its model reader expects one output per library. Android now
uses `cmake/AndroidWebp.cmake` to build ordinary static archives from the same
pinned WebP 1.6.0 decoder, encoder, DSP, utility, SharpYUV and demux source files.
Its arm64 flags follow upstream `Android.mk`; Windows keeps upstream CMake.
The build script uses a single-use Gradle daemon so a failed model parse cannot
leave persistent file handles blocking the next configuration attempt.

## Final packaging validation

The final asset library contains **35,599 files / 3,711,227,382 source bytes**,
including the complete imported sound library. Initial packaging exposed a
Gradle 6.7 worker-queue bottleneck: the stock `compressDebugAssets` task submits
one worker lease for every asset, and a thread dump showed global lease/cache
lock contention instead of compression work.

`proj.android/asset-compression.gradle` retains the original task input/output
tracking, `PackagingUtils` no-compress predicate, compression level and official
`Zipflinger` writer. It processes the same one-entry archives sequentially.
Previously generated archives are accepted only if entry name, count, method,
size and source CRC agree; obsolete output archives are removed within the
normalized task output directory. It does not alter security settings or omit
resources. The replacement processed all assets and the build completed in
1 minute 31 seconds.

`tools/verify-android-apk.py` compares the full APK asset set against Resources,
checks each original file's size and CRC, tests every ZIP entry's decompression
CRC, and verifies that streamed Ogg/WebP/MP3 files remain stored. All checks
passed on the complete APK; APK Signature Scheme v2 verification passed.
The subsequent build also includes the corrected Skip label anchor, paused
presentation input handling, audio recovery and Android save migration.

The first USB transfer completed at 26.8 MB/s in 90.8 seconds. Google package
verification returned ALLOW. Samsung Device Security then scanned the full
large APK, extending its verification window while consuming CPU; this was
normal ongoing verification, not an app crash or a consent dialog. No security
verification was disabled.

## Device verification before the attack-motion update

The installed APK was verified against the host APK by SHA-256:
`386e357ff90237a356e9fe99919b5b7d18d3c194cb630749628a9ef01dc3d3da`
(2,571,290,196 bytes). This is the earlier device QA checkpoint; the newer
attack-motion build has a separate final receipt.

The original version-1 profile was retained byte-for-byte in `.v1.bak`.
Android backup publication uses `renameat2(RENAME_NOREPLACE)` because the
previous hard-link publication could not complete on this device. Migration
preserved stage 8, best-cleared 7 and 14 victories, added the fourth level-1
party member, and settled pending gold exactly once: 2,750 + 546 = 3,296.
The actual stage 2-3 AUTO battle then reached VICTORY, awarded 275 gold, and
saved gold 3,571 / stage 9 / best-cleared 8 / victories 15. No profile values
were manually edited for testing.

Screenshots show the lobby, four-member party, AUTO OFF target-selection wait,
Dominiel S3 and victory. A six-second manual wait retained Dominiel HP 420/420
and combat readiness 100%; selecting S3 then played the enlarged character,
darkened battlefield and snowflake effect. The AUTO battle log recorded
Aramintha BGHATCH at 1.7 seconds, CUTIN, camera and STOP cues. Battle memory
at one sample was 259,382 KiB PSS; this is a sample, not a peak-memory claim.

Android's OpenSL/AudioTrack backend decoded and played Ogg music, effects and
Korean voices. AudioFlinger showed active tracks for this app at 24 kHz and
48 kHz. This verifies backend activity, not an assertion of listening quality.
The sound library displayed all 29,003 entries and returned after Home/resume.

The crash log contained no native fatal signal or Java fatal exception during
the observed run. Startup did emit one `OpenGL 0x0501` at Cocos
`ProgramGL::getActiveAttributes`; no shader compile/link failure accompanied
the observed S3 playback. Earlier app removals were logged as `remove task`,
not native crashes. Raw logcat, AudioFlinger and installer/home screenshots
are retained locally under the task's `work/android-device-evidence`, outside
the portfolio deliverables. Final structured evidence is
`captures/android/device-validation.json`.

## Final v6 deployment and attack-motion verification

The final APK is 2,571,368,020 bytes. Its SHA-256 is
`511c76a1bcaa30cc2f67ec06979e8076a5c6e260ad6a693614fcc6bbd6d5906c`.
The installed base.apk and exported APK have this same hash. The incremental
build completed in 29 seconds with unchanged asset tasks; USB transfer took
87.499 seconds, installation succeeded without clearing application data,
and launch completed in 3,534 ms. Full validation passed for all 35,599 assets
and 35,608 ZIP entries, including exact asset set, original size/CRC, archive
CRC and stored streaming media. The serial-free receipt and results are
`captures/android/build-receipt.json`, `apk-validation.json` and
`device-validation.json`.

The new ordinary-attack implementation was exercised on the preceding v5
checkpoint: ACTION start/impact/complete logs reached 31 completions and 31
impacts in the sampled run. Video frames show Clarissa approaching and
attacking, enemy guard preparation and an ogre approaching its target.
`action-auto.mp4` is 25.02 seconds of H.264, 1480 x 720, approximately 24.90 fps
and 6,560,398 bytes. It contains no audio track. Skill cutscenes were temporarily
disabled for this ordinary-motion capture. An in-flight ogre attack remained
pixel-identical across a four-second menu pause (`action-paused-before.png`
and `action-paused-after.png`, SHA-256
`cf959dca45df2ef61c277cd64bc440af6476a8af5ae3ff4894d6c4d8a6aaaecb`).
The subsequent v6 installation replaced that process, so resuming that same
v5 battle is not claimed. v6 retains the ordinary-action implementation and
adds the final S3-fallback effect-sound cleanup and QA flag.

On final v6, the 29,003-entry sound library opened and returned after Home
with a 339 ms resume. Individual library-track playback was not verified:
the captured library active-playback count remained zero. The earlier battle
backend/AudioFlinger evidence establishes audio activity, not listening quality.
No native/Java fatal crash was observed; the startup GL warning documented
above remains a known observation rather than a claim of zero GL errors.

The final device screen was visually checked in the lobby. Skill cutscenes
and sound were restored to ON through the battle menu (`settings-final.png`),
then the normal Return to Lobby button was used (`lobby-final.png`). The saved
profile remained gold 3,571, stage 9, best-cleared 8, victories 15 and four
level-1 heroes after the v6 update, resume and final return. No pending-gold
credit was repeated. The phone was left at the lobby, with next stage 2-4.
