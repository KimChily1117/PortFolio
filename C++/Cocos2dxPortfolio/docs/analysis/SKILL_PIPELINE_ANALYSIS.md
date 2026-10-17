# Epic Seven skill presentation: source evidence and current limits

This implementation plays a composite of the installed game's actor skeleton,
CFX Spine layers, particle emitters, camera tracks and animated WebP cutin. It is
**not a verified complete reproduction of every original S3**. Four checked
legacy sequence/actor pairs are selectable. Camera enum semantics and custom
particle extensions remain partial. The distinction matters:
427 available animated cutins do not constitute 427 complete skill sequences.

## Local evidence

The immutable extraction is `.deps/epicseven-assets/raw`. The source dependency
graph is `.deps/epicseven-assets/cutscene-analysis/dependency-graph.json`.
There are 264 plaintext `skillpt/*.seqani` action trees. These are older source
sequences, not a complete current hero S3 database. The catalog includes every
sequence in `sourceCoverage`, with its SHA-256 and mapping status; the companion
`skill-presentation-coverage.json` additionally retains every raw action tree.
Unmapped entries are not presented as playable skills.

| Skill | Actor source | Sequence source | Main effect chain | Cutin cue | Compiled end |
|---|---|---|---|---|---|
| Achates `c1017.s3` | `model/achates.scsp` | `skillpt/c_acha_skill_03.seqani` | `effect/achates_skill_03.cfx` → Spine + three particle groups | model event at 0.600 s | 6.420 s |
| Aramintha `c1048.s3` | `model/araminta.scsp` | `skillpt/c_araminta_skill_03.seqani` | `effect/arameenta_sk3.cfx` → back/middle/front Spine layers; `camera/arameenta_cm_sk3.scsp` | model event at 0.8333 s | 5.706 s |
| Clarissa `c1028.s3` | `model/clarisa.scsp` | `skillpt/c_clarisa_skill_03.seqani` | three CFX composites, including original `clarisa_sk3_eff_*` layers | explicit source CUTIN at 1.980 s | 4.9999 s |
| Dominiel `c1037.s3` | `model/dominiel.scsp` | `skillpt/c_dominiel_skill_03.seqani` | `effect/nixid_eff_sk3_self.cfx` + `effect/nixid_eff_sk3.cfx`; `camera/nixid_cm_sk3.scsp` | none recovered; available WebP is not inserted at a guessed time | 4.600 s |

Clarissa is partial: a source CFX requests `animation2` from
`effect/clarisa_sk2_eff_f.scsp`, but that installed skeleton contains only
`animation`. The missing layer is recorded and skipped, never replaced with a
different animation. Achates' source FIRE cue is at 2.980 s. Skills without a
source FIRE cue leave gameplay hit timing to the host; no hit marker is invented.
Dominiel's current `skill3` duration is 2.900 s; its 1.980/2.145 s animation-relative
cues fit the clip. All requested effect animations resolve. Its source lacks both
CUTIN and FIRE, so the compiled presentation uses neither an invented cutin cue
nor an invented source hit marker. The four-entry default party catalog includes
her checked skeleton, effects and camera; the existing particle extension limits
also apply to her five particle groups.

Evidence of stale data prevents blindly importing all sequence names: Kise's
sequence schedules an animation-relative action at 5.450 s in `skill3_2`, whose
current installed clip is approximately 1.1333 s. Cecilia's sequence references
`skill3`, absent from the current model. Birgitta's modern model timeline has a
`skill3` ready-effect removal, but does not provide the missing CFX phase times.
Modern effect filenames alone are not sufficient evidence of choreography.

A bounded local table inspection is recorded in
`.deps/epicseven-assets/cutscene-analysis/skill-table-feasibility.json`.
`db/skill.db` (490 bytes) and `db/skillset.db` are plaintext YUNADBSET dataset
selection manifests. They contain no animation phase timing. `skill_player.db`,
`skillact.db` and `skillset_player.db` remain encoded, with no recovered table
schema, decryption keys or verified rows. They are not reported as decoded.
Current presentation timing comes from checked SEQANI plus SCSP data; damage
coefficients, cooldowns and modern choreography cannot be claimed recovered from
these opaque tables. No dynamic game-process hooks were used for this inspection.

## Source compiler

`tools/import-epicseven-skills.py` stages `Resources/reference/skills`, preserving
original decoded JSON next to the Cocos compatibility copy and raw trees inside
each compiled timeline. CFX source fields, delays, z values, scale, rotation,
animation names and particle fields remain in the output. A 4 GiB free-space
reserve is enforced before new texture/skeleton conversions.

Observed/implemented schedule rules:

* ROOT and SPAWN run children in parallel; SEQ accumulates blocking durations.
* ANI duration and event markers come from the actual installed skeleton.
* ANI_POS uses its source millisecond delay relative to its parent animation.
* ANI_SCOPE resolves named from/to markers; its interval supplies MOVE duration
  when MOVE has no explicit time. Missing markers or beyond-clip cues are errors.
* ONEVENT expands at matching named model markers. An explicit CUTIN suppresses
  automatic model cutin markers to prevent duplicate playback.
* DELAY, asynchronous/without actions, FIRE, STOP/RMCHILD and IDLE produce stable
  ordered cues. CAM is nonblocking unless block is explicit.
* EFFECT loads actual CFX dependencies. SHAKE loads a camera SCSP if available.
  Unknown action types produce explicit UNSUPPORTED records.

The raw vocabulary includes SOUND, SHADER, SHOT, PPEFFECT and CALL beyond these
four chosen sequences. These do not have a verified runtime implementation.
BY_TRANSFORM is retained but not rendered. BGSTAGE can stage a CFX when its source
resolves; its original stage semantics are unverified. Full raw source preservation
is separate from runtime support.

## Cocos player and ownership

`SkillPresentation::create(id, callbacks, error)` returns a Node covering the
visible viewport, with a touch blocker, Skip button and Escape handler. The host
adds it above the battle and hides the duplicated active actor. `onComplete(bool)`
fires once on normal finish or skip; the host removes the node on the next frame.
Scene teardown suppresses gameplay callbacks. `setPaused` pauses the presentation
and all children. A deterministic `SkillTimeline` clock catches up every crossed
cue in stable source order, and stop prevents subsequent cues.

Before its first update the battle calls
`bindBattleStage(stage, background, opponents, actorHome, targetHome)`. The player
retains these nodes without reparenting them. Camera translation/scale/rotation
and camera-track translation are mirrored onto the real battlefield; BGSHOW
changes the bound background visibility, and LAYOUT away offsets move the bound
opponents. A local offset proxy keeps those layout actions within the player's
pause/cleanup scope. Original position, rotation, scale and visibility are restored
before completion callbacks, on skip, and during scene teardown, then all retained
references are released. Time-zero cues are deferred until this binding can occur.
An unbound roster preview retains the standalone presentation behavior.

Named finite-duration layers are retained safely until STOP or cleanup. Each CFX
primitive has its own camera sibling layer so negative and positive source z can
straddle the actor. Cutins remain in a separate screen-space overlay. Every child,
particle emitter, action and callback is cleaned up with the presentation.

The three original `effect/bg_hatch_1/2/3.png` textures and
`system/program/sprite_hatch.{vert,frag}` are staged. The Cocos shader changes only
matrix/texture uniform names and adds their declarations; its three-texture RGB /
alpha blend expression is preserved. Source colors, rotate, scale, fade and speed
fields drive it. Original shader source is retained alongside the adapter.

## Explicit rendering limits

The following are adapter assumptions, **not recovered original engine behavior**:

* Battle actor/target anchors come from the host binding; unbound previews use
  28%/72% of viewport width and 25% height. Source x/y offsets are interpreted as
  fractions of the viewport. Active actor scale comes from animated idle bounds.
  Bound opponent actors and the stage follow the presentation's camera.
* CAM focus 20 selects target, 41 resets; other focus values select actor. Camera
  position/zoom use linear interpolation. Source `log`, `vel`, `clipping`, distance
  and other focus/location meanings are preserved but not faithfully decoded.
* MOVE locations 1/3/4 select target, others home; style 2 uses a Cocos jump with
  height derived from distance. LAYOUT self/away offsets and reset are applied to
  actor/opponents. BGSHOW uses its boolean `show`, defaulting to hidden when omitted;
  the original numeric coordinate/layout semantics remain unverified.
* Camera SCSP translation uses linear/stepped keyframes and actor unit scale.
  Source camera Bezier curves, rotation, scale and power/timeScale semantics have
  not been validated. There is no random substitute when camera data is missing.
* Hatch speed is interpreted as UV cycles/second, textures repeat and cover the
  viewport. The original host's UV speed conversion/geometry were not recovered.
* Standard Cocos particles consume standard plist fields, source emission rate,
  delay and blend parameters. Custom colorBlends, burning/repetition behaviors
  differ; original data is preserved and the catalog reports these limits.
* Cutins use their decoded WebP frame durations. They are screen overlays; whether
  an original cutin pauses the surrounding world is not verified here.

Thus the player demonstrates real multi-layer source animation with source cue
times, while its warnings remain visible in the catalog. Pixel-identical original
camera movement, every particle extension and all modern S3 phase tables still
require further runtime evidence.

## Reproduction and checks

From the project folder, with the packaged conversion dependencies available:

```powershell
python tools/import-epicseven-skills.py --self-test
python tools/test-epicseven-skills.py
python tools/import-epicseven-skills.py
```

The Python tests check sequential/parallel timing, animation-relative positions,
marker scopes, explicit cutin deduplication, ONEVENT, nonblocking delay, stale clip
rejection and unsupported reporting. `tests/core/SkillTimelineTests.cpp` checks
same-time source order, large-frame catch-up, exact finish boundaries, negative /
nonfinite delta, skip and reset. Link it with `Classes/core/SkillTimeline.cpp`.

Each staged `.cocos.json`/atlas pair is checked by the real bundled Spine runtime.
Results are recorded in
`.deps/epicseven-assets/cutscene-analysis/skill-skeleton-validation.json`.
Effect clips may intentionally have no attachment at time zero, so validation
must sample their animation interval before deciding that a clip is empty.

Verification on 2026-10-15: all 21 staged model/portrait/effect JSON-atlas pairs
passed the bundled runtime's skin/animation/finite-world-vertex sampling. The six
Python compiler tests passed, the standalone timeline clock test passed, and the
parent project's seven CTest targets passed. Passing structural/pose tests does
not validate the unverified camera and particle semantics listed above.

Converter provenance and licenses remain in
`.deps/epicseven-assets/tools/spine-conversion/README.md` and its provenance files.
Original game assets remain local analysis resources; this work does not transfer
their ownership or establish redistribution rights.
