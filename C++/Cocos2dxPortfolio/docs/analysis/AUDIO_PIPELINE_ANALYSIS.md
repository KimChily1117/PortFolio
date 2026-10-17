# Epic Seven audio import and playback

The installed game contains 26 FMOD banks under
`D:/Games/EpicSeven/data.unpacked/sound`. The existing verified extraction decoded
**28,984 samples from all 23 sample-bearing banks** to PCM WAV, preserving embedded
FSB5 sample names and metadata. The WAV archive is 9.223 GiB and remains unchanged
under `.deps/epicseven-assets/media/audio`.

| Category | Samples |
|---|---:|
| Korean voice banks | 23,141 |
| Effects, general and event samples | 4,400 |
| UI bank | 890 |
| Music banks | 553 |
| FMOD sample subtotal | 28,984 |
| Installed cinematic MP4 audio streams | 19 |
| Complete audio catalog | 29,003 |

These are bank/sample categories, not an assertion that every sample in a music
bank is music. Only the installed Korean voice banks are present. Other language
packs were not downloaded. Total decoded duration is approximately 2,317 minutes.
Separately, all 19 installed cinematic MP4s contain AAC soundtracks. Their existing
Korean/Japanese/English/Traditional Chinese path variants are extracted to PCM and
converted to Ogg without changing the videos. These streams retain their original
44.1 kHz stereo format and appear under the `cinema` library category; they are not
counted as additional FMOD banks. Per-video source hashes remain in the extraction
manifest. The FMOD samples all retain their original 24 kHz mono/stereo formats.

`master.bank`, `master.strings.bank` and `startup.bank` contain no embedded FSB5
sample headers. Their RIFF/FEV metadata is not a missing PCM sound. Their hashes,
headers and no-FSB5 findings are recorded in
`.deps/epicseven-assets/cutscene-analysis/audio-bank-metadata.json`.

## Decoder and encoder evidence

The earlier extraction used vgmstream r2117, SHA-256
`29df08c557ada8269c92a6abfe3886ad2f65849b0b26ba78a0819b363bbc5b85`.
Per-bank manifests retain source-bank SHA-256, stream count, original sample
names, codec, channel count, sample rate and frame count. All 23 decodable banks
have complete expected/extracted sample counts in `media/summary.json`.

`tools/import-epicseven-audio.py` uses the existing local FFmpeg 5.0.1 at
`C:/Program Files/MediaHuman/YouTube Downloader/ffmpeg/ffmpeg.exe`. Its SHA-256 is
`d1e23bc0ac839354491a8ca2c6e84e4f0132f7ab5e6f64837805e5dbe9083ffd`.
No new external executable was downloaded. The exact version/hash is also saved
in the import report. Encoding uses `libvorbis -q:a 3`, preserving source sample
rate, channel count and sample length. This is a lossy delivery derivative; the
original decoded WAV files remain available for future processing.

The full Ogg library is staged at `Resources/reference/audio/assets`, with a
portable catalog at `Resources/reference/audio/catalog.json`. Bank-relative
numeric file names retain unique IDs even when embedded sample names collide.
All 29,003 Ogg files occupy 1,018,732,926 bytes (971.5 MiB).
The importer first converts the 80 samples referenced by current bindings, then
all other tracks with four workers. It resumes existing Ogg outputs, writes new
files atomically, and enforces a 4 GiB free-space reserve. WAV files are not copied
into Resources or the Android package.

## Playback integration

`Classes/ReferenceAudio` uses the project's Cocos `AudioEngine`, whose Windows
backend explicitly has an Ogg decoder. The Android engine also has its Vorbis
decoder. The installed game's FMOD DLLs were not used as a replacement SDK; no
local licensed FMOD development headers/libraries were found in the project.

Only requested clips are opened. Initialization reads the small binding table;
the full catalog loads only when the library browser or a direct track request
needs it. Completed/stopped clips are uncached on a later scheduler tick after
all concurrent uses of that path finish. Music is a single looping instance.
Pause/resume, effects stop, music stop, persistent mute, and shutdown are explicit.
Diagnostic text reports backend state, active IDs, names and playback positions.

The public calls are:

```cpp
ReferenceAudio::initialize();
ReferenceAudio::playMusic("lobby"); // or "battle"
ReferenceAudio::playUi();          // "click", "back", "skill"
ReferenceAudio::playAttack("c1017");
ReferenceAudio::playSkill("c1017", 3);
ReferenceAudio::stopEffects();     // also used when skipping a presentation
ReferenceAudio::setPaused(true);
ReferenceAudio::setEnabled(false);
ReferenceAudio::shutdown();
```

`catalog()`, `playTrack(id)`, `stopTrack(id)` and `diagnostic()` provide inspection.
`AudioLibraryScene::create(onBack)` exposes every catalog entry with original
sample name, bank, duration, category filters, name/hero-ID/bank search, pagination
and play/stop. It does not decode all sounds into memory. Its text field detaches
IME on scene exit and row refresh is deferred out of UI callbacks.

## What the bindings prove

The following are exact embedded sample names, so their identities are verified:

| Host action | Bound sample |
|---|---|
| Lobby music | `bgm_lobby_base` |
| Battle music | `bgm_map01_battle` |
| UI click | `SE_UI2_LOBBY_BUTTON_NORMAL` |
| UI back | `SE_UI2_LOBBY_BUTTON_BACK` |
| Skill selection | `se_ui_skill_select` |
| Achates S3 first SFX phase | `se_chr_achates_skill_3_01_cutin_ready` |
| Aramintha S3 first SFX phase | `se_chr_araminta_skill_3_01` |
| Clarissa S3 first SFX phase | `se_chr_clarisa_skill_3_01` |
| Dominiel S3 first SFX phase | `se_chr_dominiel_skill_3_01` |

The four heroes' `voc_cNNNN_skill1_*`, `skill2_*`, `skill3_*` Korean samples are
also included in bindings. Voices cycle deterministically between available
matching names; source naming sometimes includes multiple numbered parts.

**Original FMOD event routing and timing have not been recovered.** A matching
sample name verifies identity, not its original trigger time, random selection,
paired voice fragments, phase offsets or mixer gain. S1/S2 start one matched SFX
phase and one voice. S3 plays all 17 canonical phase samples across the four
heroes, plus one voice at action start. Each audio-to-visual association is an
explicit reconstruction; its numeric offset comes from a retained SEQANI/SCSP
cue or CFX primitive delay, not an invented claim about FMOD scheduling.

| Hero | Phase suffixes in playback order | Cue offsets (seconds) |
|---|---|---|
| Achates | 01 ready, 02 swish, 03 spell | 0, 0.6, 1.33 |
| Aramintha | 01, 02, 03, 04 | 0, 0.8333, 0.86, 1.7 |
| Clarissa | 01, 02, 03, 04, 05 | 0, 1.122, 1.98, 2.013, 3 |
| Dominiel | 01, 02, 03, cut, 04 | 0, 0.9, 1.9, 1.98, 2.4 |

`bindings.json` retains each timeline hash, original sequence, event index/type/
parameters, CFX primitive index/source/delay where relevant, and the explicit
`originalFmodTimingVerified:false` flag. Dominiel's `cut` sound uses BGHATCH as a
reconstruction anchor; its source sequence has no CUTIN/FIRE marker. Numbered SFX
phases cannot by themselves prove the correct visual association. The selected
offsets are auditable staging choices requiring comparison with original gameplay.

The scheduler advances the same deterministic `SkillTimeline` clock used by the
presentation. Pausing freezes its elapsed time and active audio instances;
resuming continues both. Skip, completion, replacement S3, mute and shutdown
cancel queued phase clips through `stopEffects()`. No delayed callbacks survive
a skipped presentation. Music loops the delivered clip; original FMOD intro/loop
segmentation is not reconstructed. All clips remain individually auditionable.

## Windows streaming recovery

Desktop battle QA observed looping music disappear while effects continued and
no host music-stop call occurred. The bundled Windows backend queues only three
0.1-second buffers (`audio/win32/AudioMacros.h`). Its
`AudioPlayer::rotateBufferThread` refills only while `AL_PLAYING`, and
`AudioEngineImpl::update` interprets any ready `AL_STOPPED` source as finished,
even when looping. Buffer starvation therefore has no backend recovery path.
The observed symptom is consistent with this path; the exact OS scheduling cause
was not instrumented. Per-path cache eviction does not uncache active music.

`ReferenceAudio` now treats unexpected looping-music completion as recoverable.
It retains the requested context and last sampled playback position, restarts on
a later scheduler tick outside the engine callback, and seeks once the new player
reports PLAYING. Failed starts retry at most once per second. Pausing suspends
recovery. Explicit music stop, replacement, mute and shutdown clear recovery
intent. Diagnostics expose `musicRecoveries` and `musicRecoveryPending`; this is
underrun recovery with an approximate resume position, not a gapless guarantee.
The rebuilt 32-second automatic battle smoke completed all four presentations,
reached victory, and retained BGM in PLAYING state at 32.0001 seconds. This run
reported zero recoveries, so it verifies continued normal playback but does not
by itself exercise the unexpected-completion recovery branch.

## Reproduction and validation

From the project directory:

```powershell
python tools/import-epicseven-audio.py --priority-only
python tools/import-epicseven-audio.py --workers 4
python tools/test-epicseven-audio.py --priority-only
python tools/test-epicseven-audio.py
```

Use `--ffmpeg PATH` when the existing encoder is elsewhere. Import progress,
completion, actual output bytes and encoder/source provenance are recorded in
`.deps/epicseven-assets/cutscene-analysis/audio-import.json`.

The validator checks binding IDs against original sample names and checks each
Ogg identification packet, preserved channel/sample rate, end-of-stream page and
exact final granule/sample count against the WAV extraction. Reports are
`audio-validation-priority.json` and `audio-validation.json` in the same directory.
Full-library verification passed for all 29,003 files, with zero failures. The
validator also checks all 17 S3 phases: exact canonical sample coverage, retained
timeline hashes, source cue/CFX offsets, ordering and completion within the staged
presentation. The core timeline tests cover ordered dispatch, catch-up, stop and
reset; desktop/device playback remains a separate integration check.

The desktop build and Android packaging should consume only the Ogg Resources
tree. The unchanged 9.223 GiB WAV archive and original installed banks belong to
the local evidence/conversion workspace, not the APK. Actual device playback and
package-size checks are separate integration tests.
