# Combat Animation and VFX Polish

## Scope

This change addresses:

- repeated movement clicks restarting locomotion animation
- skill animations remaining in a loop after the action ended
- Garen E movement and spaced multi-hit timing
- projectile movement, trail, and impact presentation
- in-game world-space floating damage numbers

It does not use ImGui for damage display.

## Animation state

`ModelAnimator::SetAnimation` now preserves the current playback position when
the requested animation and loop mode are already active.

Authoritative move request/accept processing no longer inserts an IDLE
animation between consecutive movement commands. Action completion selects RUN
when an authoritative movement snapshot is active and IDLE otherwise.

Remote Garen and Annie controllers process action timeouts before returning from
their authoritative-snapshot update path. This prevents one-shot skill
animations from remaining active indefinitely.

## Garen E

- Garen E no longer forces the local movement state to IDLE.
- Its visual effect follows the moving caster.
- The server broadcasts the cast animation once.
- Six damage ticks are scheduled at 400 ms intervals, from 0.4 through 2.4
  seconds.
- Each tick emits its own `S_Damage`, producing separated hit feedback and
  floating numbers.

## Projectiles

Client projectiles now track the current target position, move without
overshooting, use a short capped trail, and wait for the authoritative hit
packet before removal. Local and remote projectile behavior share the same
script.

The server projectile uses the Room movement delta time rather than a
hard-coded frame duration and performs continuous distance-based arrival.

An impact particle is emitted when `S_ProjectileHit` arrives.

## World-space floating damage

`S_Damage` creates an in-game world-space billboard above the damaged
GameObject. It is not an ImGui overlay.

Each decimal digit is a small `ParticleQuad`. A procedural seven-segment pixel
shader renders the digit from a material parameter. The billboard:

- follows the target's world position
- rises and fades for approximately 0.95 seconds
- uses alternating horizontal lanes so rapid multi-hits remain readable
- performs a short scale pop on spawn
- depth-tests against the 3D scene without writing depth

This V1 intentionally avoids a font-atlas dependency. A later presentation pass
can replace the seven-segment shader with SDF font glyphs while keeping the
same world-space controller and `S_Damage` integration.

## Authority and safety

The server skill handler resolves the caster from `ClientSession.Player`.
An optional packet caster ID must match the session player, and ownership is
revalidated inside the Room JobQueue.

Projectile damage and Garen E tick damage remain server-authoritative.

## Verification

- DirectX client Debug x64 target: build succeeded
- new HLSL billboard/digit pass: FXC succeeded
- server Release: build succeeded
- server NavGrid/authoritative movement regression: 399 assertions passed

The full Visual Studio solution still has an existing shared-intermediate
configuration issue: EngineCore, AssimpTool, and Client share
`Intermediate/Debug`. This can make AssimpTool link Client object files.
Building the Client target with a valid `SolutionDir` succeeds.

## Manual runtime checks

1. Repeatedly right-click while moving. RUN should not restart from frame zero.
2. Cast Annie/Garen skills locally and remotely. One-shot animations should
   return to RUN or IDLE.
3. Cast Garen E while issuing movement commands. The caster and E effect should
   move together.
4. Observe six distinct E damage numbers instead of one simultaneous burst.
5. Fire Annie's projectile at a moving target. It should track the target,
   retain a short trail, and show an impact effect.
6. Confirm damage digits appear above characters in the 3D world and are
   occluded by scene depth.

## Visibility and allocation follow-up

The initial billboard implementation read the legacy global `W` matrix while
`MeshRenderer` supplies per-object transforms through the instanced
`input.world` matrix. This placed digits at a stale position. `VS_Billboard`
now uses `input.world` for position and scale, and the world-text pass uses
`CullMode = NONE`.

Digit materials are cached by value (0 through 9). Trail rendering also uses
the shared immutable Trail material. This prevents transient combat visuals
from adding permanent unique keys to `InstancingManager::_buffers`.

InstancingManager also prunes cached buffers whose InstanceID is no longer present in the scene. This bounds cleanup cost even when another transient effect uses a unique render group.

## Annie W damage-over-time

- Total damage: 30
- Tick damage: 3
- Tick count: 10
- Tick interval: 200 ms
- Tick window: 0.0 through 1.8 seconds
- Cast animation/effect broadcast: once
- Damage packet and world-space number: once per affected target per tick
## 2026-07-28 combat pipeline re-audit

### Root causes confirmed

- Annie W selected victims through the legacy Tilemap occupancy list. That list is not the authoritative combat position and could yield no victim after server path movement. W now queries current authoritative `ObjectInfo.Position` in the XZ plane.
- The client consumed only one IOCP completion per render frame. It now drains at most 64 ready completions so closely spaced `S_Damage` packets are handled in the arrival frame without an unbounded render-thread loop.
- Floating damage digits now inherit the damaged object's world layer. Their billboard is larger, uses thicker segments, and disables depth testing so character geometry cannot hide the feedback.
- Transient projectile render groups were deleting cached instancing buffers after removal. The cache is retained so repeated shots reuse GPU buffers.
- Impact particles uploaded the same instance buffer once during update and again during render. The update-side upload was removed; render performs the single GPU upload. Particle manager loops also avoid copying the particle pools.

### Authoritative Annie W contract

- 10 ticks
- 200 ms interval
- 3 damage per tick
- 30 total damage when the same valid enemy remains in range
- range is XZ distance from the caster's current server position
- one `S_Damage` is broadcast per target per tick

`NavGridTests/CombatPhaseTests.cs` runs the real room queue and JobTimer and verifies one cast result, exactly ten `S_Damage` packets, the `100 -> 70` HP sequence, and ally exclusion.

### Runtime verification

Run the newly built `Binaries/Client.exe` with the newly built server. In a Debug client, each received hit writes:

```text
[Combat] S_Damage target=<id> damage=<value> remainHp=<value>
```

Annie W should produce ten log entries containing `damage=3`, spaced at approximately 200 ms, and ten separate world-space `3` billboards. Basic attacks, projectiles, Garen skills, and any other server action that emits `S_Damage` use the same display path.

### Verification results

- Server Debug: success
- Server Release: success
- Client/Engine Debug: success
- C# Phase 1-5 plus combat tests: 415 assertions
- Annie W integration: 10 x 3 damage packets confirmed
- C++ NavGrid: 279 assertions
- C++/C# NavGrid result: byte-identical

Visual smoothness and GPU frame time still require an interactive two-client run; automated tests establish packet count, HP sequence, build output, and data compatibility but cannot assert what appears in the DirectX swap chain.
## 2026-07-28 floating damage placement follow-up

The transform coordinates were correct; the visibility defect was render ordering. Pass 14 damage digits previously participated in the normal MeshRenderer stage, before terrain and animated character rendering. Later world draws could overwrite the digits, making fragments appear embedded in the Rift.

The renderer now splits pass 14 into a final world-overlay stage after mesh, model, animation, and particle renderers. Digits are also positioned at `target world position + (0, 3.5, 0)` during `Start`, before their first render, and the billboard basis now comes directly from `VInv` camera world axes.

Annie W responsiveness keeps the authority boundary:

- client input immediately starts animation, sound, and W particle feedback;
- the first real damage tick is resolved immediately in the current server Room job;
- ticks 2 through 10 remain server-authoritative at 200 ms intervals;
- the client never predicts or commits HP, so rejection and range differences cannot create fake or duplicate damage.

A future client-predicted damage-number system requires a cast/event ID in `S_Damage` so predicted numbers can be matched, replaced, or cancelled. It should not be added using timing-only suppression.
## 2026-07-28 projectile pooling and vertical multi-hit follow-up

Projectile visuals now use a scene-scoped client pool with a maximum retained
size of 64. A pooled entry retains its `GameObject`, sphere mesh, shared
material, `ProjectileScript`, and trail object. Projectile hit removes the
visuals from the active scene and resets their runtime state, but does not
destroy and rebuild those resources. Changing scenes clears the old scene's
pool. Impact particles continue to use the existing particle pool.

Floating damage no longer spreads repeated hits horizontally. Rapid hits on
the same target receive one of five vertical stack slots, spaced by 0.42 world
units, and every number then rises another 1.35 world units during its
lifetime. The anchor remains the victim's current world position plus 3.5 on
Y, so moving targets keep the damage feedback above their model.

Server-authoritative multi-hit timing is:

- Annie W: first hit immediately, then 9 hits at 200 ms; 3 damage per hit,
  30 total.
- Garen E: first hit immediately, then 5 hits at 250 ms; 47 damage per hit,
  282 total.
- Garen E resolves each tick around the caster's current authoritative
  position, so movement during the spin remains supported.

The client still predicts only animation, sound, particles, and markers.
Actual HP changes and floating damage numbers are created from server
`S_Damage` packets. Client-side damage commitment was deliberately not added,
because it would duplicate or display rejected hits without a protocol-level
damage event identifier.

Verification:

- EngineCore/Client Debug: build and final link succeeded.
- Server Debug and Release: build succeeded.
- C# Navigation, movement, and combat regression: 419 assertions passed.
- Combat integration: Annie W `10 x 3`; Garen E `6 x 47`.
- C++ NavGrid regression: 279 assertions passed.
- Existing warnings remain: shared C++ intermediate directory, legacy shader
  warnings, missing DirectXTex PDBs, and unsupported `netcoreapp3.1`.
## 2026-07-28 burst timing and projectile render-state diagnosis

The server previously scheduled every future Annie W and Garen E tick at cast
time. `JobTimer.Flush` executes every expired entry in one loop, so a delayed
Room update could send all overdue damage packets in one burst. Both skills
now use chained scheduling: the immediate tick runs first and schedules only
the next tick. A delayed update can therefore execute at most one overdue tick
and the following tick is scheduled from the actual execution time.

A regression test stalls the Garen E Room for 1.3 seconds and verifies that the
next Flush produces only one additional damage packet, not all five remaining
packets. Total damage and tick counts remain Annie W `10 x 3` and Garen E
`6 x 47`.

The confirmed client render defect was D3D11 pipeline-state leakage from world
floating damage pass 14. That pass disables depth and enables alpha blending,
and Effects11 does not restore previous state automatically. InstancingManager
now resets Blend, DepthStencil, and Rasterizer state after the overlay stage so
the next camera/frame does not render normal world geometry with overlay
states. This removes the most likely full-scene overdraw source associated
with projectile impact and floating damage.

Projectile pooling remains enabled. Duplicate projectile IDs now return the
old visual to the pool before replacement. Debug builds log each spawn and hit
with `active` and `pooled` counts. A normal single projectile sequence should
show `active=1` on spawn and `active=0 pooled=1` on hit. Counts that continue
to grow identify a server packet/lifetime problem rather than a GPU-state
problem.

Verification:

- EngineCore and Client Debug build/link: success.
- Server Release build: success.
- Server regression: 420 assertions passed, including delayed-Flush burst test.
- C++ NavGrid regression: 279 assertions passed.
- Server Debug source compilation completed, but its running `Server.exe` and
  `Server.dll` (PID 32556) prevented replacing the Debug output files. Restart
  the Debug server before runtime verification so it loads the new chained
  tick implementation.
## 2026-07-28 status: multi-hit fixed, projectile A/B diagnosis

Multi-hit scheduling is now considered FIXED. Annie W and Garen E use chained
server scheduling, preserve their authoritative totals, and pass the delayed
Room Flush regression.

Projectile pooling and pipeline-state restoration did not remove the reported
runtime FPS drop. Static inspection found two remaining isolated render inputs:

- a separate trail GameObject using mesh pass 13;
- `fireball.png`, a 2048 x 2048, 5.5 MB WIC texture with no mip chain.

The current `Binaries/Client.exe` is an A/B diagnostic build with only the
separate trail disabled. Projectile body, pooling, tracking, hit packet,
impact particle, and floating damage remain enabled. The prior trail-enabled
binary is preserved as `Binaries/Client_TrailOn_AB.exe`.

Compare the same Annie projectile cast in both binaries. If only the trail-on
binary drops, replace pass 13 with a cheaper pooled/batched trail. If both drop
the same way, the next change is to replace or preprocess the unmipped 2048
projectile texture and then profile the impact particle separately.

## 2026-07-28 collision FPS-drop root cause and fix

Runtime A/B showed the same FPS drop with projectile Trail enabled and disabled,
and the drop began exactly when the projectile collided and floating damage was
created. Binary inspection then found a concrete loader defect in the impact
particle path.

Several legacy `.fx` particle assets were saved from an ABI-aligned C++ struct.
After the three one-byte bool flags, those files contain one padding byte before
the uint32 particle count. The current field-by-field `ParticleSystem::LoadData`
did not consume this byte. Consequently `Annie_QSpellSmoke.fx` was interpreted
as 76,800 particles even though the authored count is 300. `ProjectileImpact`
has a pool size of eight, so the client held state for roughly 614,400 particles
and collision called `Init()` across 76,800 entries before updating them every
active frame.

`ParticleSystem::LoadData` now supports both formats without changing assets:

- normal packed assets continue to read their count directly;
- an impossible direct count is checked for the legacy padding layout;
- the recovered count must be in the explicit range 1..4096;
- invalid or allocation-sized counts no longer reach vector allocation.

Existing asset verification selected these values:

- `Annie_QSpell.fx`: 43,264 raw -> 169 aligned
- `Annie_QSpellSmoke.fx`: 76,800 raw -> 300 aligned
- `Annie_WSkill.fx`: 51,200 raw -> 200 aligned
- `Firework.fx`: 90,880 raw -> 355 aligned
- `annie_wspell_New.fx`: packed 200 retained
- `Test1.fx`: packed 305 retained

The impact particle pool therefore drops from about 614,400 particle records to
2,400. The projectile Trail has been restored in the final `Binaries/Client.exe`.
A fixed Trail-off comparison binary remains at
`Binaries/Client_ParticleCountFix_TrailOff_AB.exe`.

Verification:

- Full sequential Debug solution rebuild: success, zero errors.
- Final output: `Binaries/Client.exe` (Trail on, particle count fix on).
- Known build-system issue: all C++ projects share `Intermediate/Debug`; parallel
  incremental builds can contaminate `.obj`/`.tlog` state. A sequential Rebuild
  was required to produce a reliable final link.

## 2026-07-28 Fireball V2 build

A versioned Classic/V2 fireball comparison, generated textures, DDS runtime assets, impact budget, hashes, and run instructions are recorded in docs/fireball-vfx-build-variants.md. The current Binaries/Client.exe uses V2 while Binaries/Client_FireballClassic.exe preserves the previous art path.


## 2026-07-28 Fireball V3 build

Fireball V3 preserves the V2 palette while increasing the projectile core, trail width, and maximum trail length by 30%. A low-count pooled welding-spark emitter runs at 75 ms intervals without catch-up bursts, and collision layers a pooled expanding explosion over the V2 impact. Classic, V2, and V3 executables remain available; the current `Binaries/Client.exe` matches V3. Full details and hashes are in `docs/fireball-vfx-build-variants.md`.

## 2026-07-28 Fireball V4 build

V4 adds pooled explosion-texture flame lobes to both the projectile head and its moving trail, retains lower-frequency welding sparks, and triples the V3 hit-explosion scale. The current `Binaries/Client.exe` matches the separately preserved `Binaries/Client_FireballV4.exe`; Classic through V3 remain unchanged.

## 2026-07-28 Fireball V5 build

V5 replaces the projectile Sphere with a camera-facing, alpha-blended Quad using dedicated shader pass 15, eliminating the visible transparent rectangle without increasing projectile object or draw-call count. The mesh trail width and flicker are exactly 4x V4. `Binaries/Client_FireballV5.exe` is preserved and the current `Binaries/Client.exe` matches it.

## 2026-07-28 Fireball V6 build

V6 restores the projectile body to the pooled 3D Sphere/pass 12 while retaining the four-times-thicker connected trail. Only the world-space flame and spark afterimages remain billboarded; the flame lifetime increases to 0.32 seconds for a clearer fading trail. `Binaries/Client_FireballV6.exe` is preserved and the current `Binaries/Client.exe` matches it.

## 2026-07-28 Fireball V7 dynamic ribbon build

V7 keeps the projectile as a pooled 3D Sphere and replaces the visually mismatched Cube plus billboard-puff trail with one connected camera-facing ribbon in world space. The ribbon is distance sampled, tapered, and updated through one fixed-size dynamic vertex buffer, so it follows 3D curvature without per-frame object or buffer construction. Repeating `ProjectileFlightFlame` particle systems were removed; only lower-frequency pooled welding sparks remain. The V4 three-times-sized collision explosion is retained.

`Binaries/Client_FireballV7.exe` is preserved, and the current `Binaries/Client.exe` is byte-identical to it. Classic through V6 hashes remain unchanged. Full sequential Debug x64 rebuild and Effect shader compilation succeeded. No server or protocol files changed.

## 2026-07-28 Fireball V8 trail-only 150% build

V8 removes the visible Sphere head entirely and leaves the pooled projectile GameObject as an invisible movement/collision anchor. The connected camera-facing world ribbon is now the whole fireball silhouette: maximum length, head width, and tail width are each exactly 150% of V7. Sampling spacing remains constant and capacity grows to 32 points, so the longer trail keeps its 3D curvature. Sphere-only pulse and rotation work is removed, while welding sparks, the enlarged hit explosion, and the F8 combat gather hotkey remain intact.

`Binaries/Client_FireballV8_TrailOnly150.exe` is preserved and byte-identical to the current `Binaries/Client.exe` (`888F5EB3D01D991526D0B54B624F1E3B8C28DD4C0BD537238562D76A19C7814C`). Full sequential Debug x64 rebuild succeeded with zero errors. No server or protocol files changed.
