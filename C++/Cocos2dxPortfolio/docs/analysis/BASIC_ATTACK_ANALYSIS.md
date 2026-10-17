# Basic attacks: source evidence and host presentation

This note records a read-only review of the eight shipped battle rigs and the
current `GameScene::ActionMotion` / `SpinePreview` implementation. The action
player makes attacks readable by waiting for preparation, approach, the complete
attack clip and return before advancing the presentation queue. Original rig
motion and recovered event times are distinguished below from local staging
choices. This is not a claim that the original game's complete S1/S2 logic or
effect routing has been recovered.

## Evidence and measurement

Immutable inputs are under `.deps/epicseven-assets/raw/model`, `raw/skillpt` and
`raw/effect`. Hero playback JSON is under
`Resources/reference/skills/assets/model/{achates,araminta,clarisa,dominiel}`;
enemy playback JSON is under `Resources/reference/epicseven/spine/model`.
The measurements below are the largest timeline key/event time in each decoded
animation, rounded to four decimals. They are clip lengths, not original complete
turn durations or inferred damage timings. A zero-length animation can exist as
a setup/reset pose and must not be mistaken for visible motion.

| Actor / model | Attack animation lengths, seconds | `attacked` | `run` / `run_start` |
|---|---|---:|---|
| Achates / `achates` | `skill1` .6333; `skill2` 3.5333; `skill3_1` .6000; `skill3_2` 3.9333 | .5666 | .6666 / .2666 |
| Aramintha / `araminta` | `skill1` 2.1333; `skill2` 1.8333; `skill3` 3.8333 | .6666 | .8000 / .2666 |
| Clarissa / `clarisa` | `skill1` 1.4333; `skill2` 3.4000; `skill3` 4.2666 | .5666 | .8000 / .2666 |
| Dominiel / `dominiel` | `skill1` 2.0666; `skill2` 2.5000; `skill3` 2.9000 | .6333 | .8000 / .3333 |
| Ashmage / `ashmage` | `skill1` 2.1333; `skill2` .8000; `skill3` 3.2000; `skill4` .8000 | .7667 | absent / absent |
| Guardian / `chaos_guardian_m1` | `skill1` 2.1333; no `skill2` | .7000 | absent / absent |
| Ogre / `ogre_king` | `skill1` 2.7000; `skill2` 2.9667; `skill3` 2.8667 | .9000 | absent / absent |
| Guardian / `chaos_guardian_m2` | `skill1` 2.1333; no `skill2` | .7000 | absent / absent |

All eight rigs have zero-length `attacked_damage`; use `attacked` for a visible
reaction. The four enemy rigs have no jump-forward/back or run clips. Additional
movement/idle lengths are:

| Model | `jump_forward` / `jump_back` | `b_idle` / `idle` | `b_idle_ready` |
|---|---|---|---:|
| achates | .7000 / .7666 | 1.0000 / 2.0000 | 1.3666 |
| araminta | 0 / 0 | 1.6666 / 2.0000 | .8333 |
| clarisa | .7000 / .7333 | 1.5000 / 1.8333 | .7666 |
| dominiel | 0 / 0 | 1.6000 / 1.6000 | .8333 |
| ashmage | absent / absent | 2.3333 / 2.3333 | 0 |
| chaos_guardian_m1 | absent / absent | absent / 2.0000 | absent |
| ogre_king | absent / absent | 2.0000 / 2.0000 | 0 |
| chaos_guardian_m2 | absent / absent | absent / 2.0000 | absent |

## Verified events and sequence timing

Times in the next table are relative to the named attack animation, before any
host preparation or approach delay.

| Model | Original animation events |
|---|---|
| Achates S1 | `hit` at .2666 s |
| Achates S2 | `fire` at 1.1000 s |
| Clarissa S1 | `hit` at .5000 s |
| Clarissa S2 | `hit` at .7666, 1.1666, 1.4666, 1.6666, 1.8666, 2.1000, 2.4333 s |
| Aramintha, Dominiel S1/S2; all four enemies' attacks | No `hit` / `fire` events found |

`skillpt/def_heal_02.seqani` listens for `fire` and invokes
`achatest_skill_02` CFX. Its effect name corroborates the Achates S2 event/effect
association, but the current encoded skill-routing database was not decoded to
prove every actor-to-template assignment. Likewise, `def_range_attack.seqani`
contains a `hit` listener with a 180 ms SHOT action; this does not establish that
the template belongs to every ranged actor, or justify automatically adding
180 ms to their damage times.

The named Aramintha, Clarissa and Dominiel S1/S2 SEQANI files contain no explicit
FIRE or SHOT node. Relevant source schedules are:

- `c_clarisa_skill_01.seqani`: `jump_forward` → `skill1` → `jump_back` → IDLE.
  The approach jump has `jump` at .3333 and `landing` at .7000; its ANI_SCOPE
  attaches camera/MOVE actions to that interval. With current clip lengths the
  attack starts at .7000, its hit occurs at 1.2000, and the complete three clips
  take 2.8666 seconds. A SHAKE is attached .530 s into the attack. These values
  exclude the host's extra preparation time.
- `c_clarisa_skill_02.seqani`: the same approach/return clips surround `skill2`.
  Total clip span is 4.8333 seconds; seven hit markers occur at absolute sequence
  times 1.4666, 1.8666, 2.1666, 2.3666, 2.5666, 2.8000 and 3.1333 seconds.
  Its separate shake/camera tracks are retained source evidence, not additional
  damage events.
- `c_araminta_skill_01.seqani`: `skill{idx}` begins at zero;
  `arameenta_sk1.cfx` begins after a 462 ms DELAY. S2 and its `_02_02` variant
  start `arameenta_sk2.cfx` after 990 ms. These are verified effect-start times;
  treating either as a damage timestamp would be an inference.
- `c_dominiel_skill_01.seqani`: after 300 ms, `skill{idx}` and
  `nixid_eff_sk1.cfx` start together. The attack clip ends at 2.3666 seconds on
  that clock. CFX particles have further 700, 1350 and 1380 ms delays relative
  to effect start. S2 starts its clip and `nixid_eff_sk2.cfx` at zero. Neither
  sequence proves the gameplay impact time.
- No actor-specific SEQANI or content reference for Ashmage, the two Chaos
  Guardians or Ogre King was found among the 264 extracted sequences. Their
  hit timings remain unverified.

## Root motion and movement

For **all eight S1/S2 rigs**, the skeleton root has no translation keys or only
zero x/y keys. The attack clips do not translate the whole actor across the
battlefield. Clarissa's source sequences explicitly supply MOVE separately.

Large motion on a weapon, effect socket or hit marker is not actor locomotion:
Achates S1 `hit` reaches x=-351.82; Clarissa S1 `hit` reaches x=469.70 and its
weapon chain reaches y=-639.53; Ashmage S2 `efx_position` reaches x=-528.64.
Ogre King's `body_center` does have substantial local body sway: S1 x ranges
-152.96..83.36, S2 -122.68..0. This sway should remain in the rig and should not
be interpreted as recovered approach/return motion. Values are skeleton units,
before actor scale and facing transforms.

## Available S1 effect candidates

These files exist in the raw extraction. Presence does not mean that the current
basic-attack player stages or renders them, or that original runtime routing is
known.

| CFX | Available components / evidence |
|---|---|
| `achates_skill_01.cfx` | Two Spine layers plus a particle layer delayed 267 ms; associated staff-particle CFX also exists |
| `arameenta_sk1.cfx` | One Spine layer; explicitly named by Aramintha S1 SEQANI |
| `clarisa_skill_01.cfx` | Two Spine layers; their atlas files are aliases to `clarisa_sk1eff.atlas`, which references `clarisa_sk1eff.sct` |
| `nixid_eff_sk1.cfx` | Two Spine layers plus three particle layers; explicitly named by Dominiel S1 SEQANI |
| `skeff_ashmage_sk1_{fire,ice,dark,light,nature}.cfx` | Actor effect and corresponding `target` variants; names indicate element-specific candidates, not verified runtime selection |
| `skeff_chaos_guardian_sk1.cfx` | Seventeen Spine layers, including eye, flare, ray/electricity, flash and dust components |
| `ogre_warrior_skeff_sk1_{self,target}.cfx` | Exists, but correspondence from `ogre_warrior` effects to the `ogre_king` model is unproven; automatic matching is not recommended |

Checked hero and Ashmage/Guardian candidates have their referenced Spine/particle
sources and texture/atlas chains available. Conversion/render validation for this
new basic-attack effect set was outside this read-only investigation. Chaos
Guardian's stationary rig and ray/electricity effect names do not prove a melee
attack. The host's melee classification is a reconstruction choice.

## Current host implementation

The following describes the inspected `Classes/GameScene.cpp` and
`Classes/SpinePreview.cpp`, not original-game behavior:

- `ActionMotion` serializes preparation → approach → full attack clip → return.
  The actor is manually sampled through `seekAnimation`, and the battle update
  returns while an action is active. The requested `skillN` falls back to
  `skill1` when its duration is zero/missing, which covers both Guardians.
  A minimum .5-second attack span and .8-second missing-rig fallback are local
  choices. HUD HP and floating damage are applied at the presentation impact,
  rather than immediately when the already-resolved battle event is queued.
- Clarissa is classified melee. Achates, Aramintha, Dominiel and the first enemy
  (Ashmage) are ranged; other enemy indices are melee. Supportive actions never
  use melee approach. These classifications, enemy preparation .3 s versus hero
  .16 s, enemy approach/return .34 s, ranged .12/.18 s, 125-unit melee spacing,
  24-unit ranged lean, smoothstep interpolation and hop arcs are host staging.
  Clarissa's .7/.7333 approach/return clip lengths have source evidence, but the
  host path geometry and additional .16 s preparation are not recovered values.
- Achates S1/S2 use the verified .2666/1.1 clip-relative event times. Clarissa
  S1 uses .5; **S2 applies the grouped gameplay result once at the first .7666
  marker**, while playing the full multi-hit animation. It does not yet dispatch
  seven separate original damage results. Other actors use **55% of attack clip
  duration**, explicitly an estimated host impact anchor. Windup and approach
  are added to all these times; this is not identical to raw SEQANI time zero.
- Target rings, enemy warning arrows, ranged projectile strokes/white heads,
  impact rings/rays/slashes and supportive plus signs are procedural DrawNode
  visuals. The projectile occupies at most the final .3 s before impact.
  They are readability aids, not extracted CFX or original projectile timings.
  The current basic-attack path does not play the candidate CFX table above.
- On damage, `attacked` is sampled, the victim is pushed back and flashed red,
  and a defeated victim fades. Enemy hits use a larger push (32 versus 20 units)
  and stronger .22-second stage shake (7 versus 4 units). Flash duration .2 s,
  shake waveform, fade/knockback curves and impact graphics are host choices.
  The default impact sound also uses the helper's generic attack binding, not a
  recovered per-enemy hit-sound table.
- `SpinePreview::draw` now multiplies the original batch color by node displayed
  color and opacity, including premultiplied-alpha RGB handling. It recomputes
  from the saved base color every draw, so a paused pose does not accumulate
  repeated tint multiplication. This makes GameScene's flash/fade visible in
  the custom renderer; it does not recover an original Spine two-color shader.
- Finishing restores home positions, idle playback, white tint, full opacity
  and stage origin, then advances the queued presentations. These are local
  lifecycle rules intended to prevent one action obscuring the next.

## Remaining limits

Source hit/fire events should take precedence over estimated anchors when more
actors are added. Unknown enemy timing, original camera/easing constants, current
skill database routing, actual projectile travel, precise CFX attachment and
Clarissa's per-hit gameplay distribution remain unresolved. No files besides this
analysis document were changed for this documentation task; no build or device
operation was performed.
