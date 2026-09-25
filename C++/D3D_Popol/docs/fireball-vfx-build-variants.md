# Fireball VFX build variants

Date: 2026-07-28

## Executables

All executables must remain in `Binaries` so their existing relative paths to
`../Resources` and the compiled shaders continue to resolve.

| Variant | Executable | Description | SHA-256 |
|---|---|---|---|
| Classic | `Binaries/Client_FireballClassic.exe` | Previous fireball/trail/impact art, with the particle-count loader fix | `B6C09BD260F0D1C6F4BB2DBA9461C6D5C26C91C795FE2EDED6649510C5A1344B` |
| Fireball V2 | `Binaries/Client_FireballV2.exe` | New core, flame trail, short impact burst, core pulse/roll, trail-width flicker | `83F82F82E29BBDE8F391E35C660AFA68AF42815F0983C629F5B1A00F0A4B1264` |
| Fireball V3 | `Binaries/Client_FireballV3.exe` | V2 palette, 30% larger core/trail, pooled welding sparks, layered hit explosion | `62CC4A883946C12980346F7E795714A705D1232F8CF7EA5BED0B3C76AD001810` |
| Fireball V4 | `Binaries/Client_FireballV4.exe` | V3 visuals plus pooled rolling flame puffs on body/trail and a 3x hit explosion | `73EC3EB445D587657165618A9EAF69679CE6B3B04D09F2D54AF7B77898596532` |
| Fireball V5 | `Binaries/Client_FireballV5.exe` | Camera-facing alpha billboard body and a trail exactly 4x thicker than V4 | `FA3AFFBAE75D2C1AF6AFEEBFCCE2E9ED015FBFF24D92C525D6755564C799B04E` |
| Fireball V6 | `Binaries/Client_FireballV6.exe` | 3D Sphere body with the V5 thick trail and longer world-space billboard flame afterimages | `BA6AC69B537EA451CD5EF2FF548E87CFF021D2E172A597685FDD4880DA5B6E82` |
| Fireball V7 | `Binaries/Client_FireballV7.exe` | 3D Sphere body with one distance-sampled camera-facing world ribbon; no repeating flight-flame particle systems | `7B0DA04F0611F37BEE0CA567F0209D3FA2E5337931585F1CCA57BBB1FA73C11B` |
| Fireball V7 Combat Test | `Binaries/Client_FireballV7_CombatTest.exe` | V7 visuals plus the server-authoritative F8 Room gather test hotkey | `F9D0329AEB28D2CD044C4CC14DA9057C7BEDC7008B811786770359D129169A47` |
| Fireball V8 Trail Only 150% | `Binaries/Client_FireballV8_TrailOnly150.exe` | Sphere head removed; one world-space ribbon enlarged to 150% while retaining F8 combat gather | `888F5EB3D01D991526D0B54B624F1E3B8C28DD4C0BD537238562D76A19C7814C` |

`Binaries/Client.exe` currently matches Fireball V8 Trail Only 150% and is byte-identical to `Binaries/Client_FireballV8_TrailOnly150.exe`.
The original texture and particle files were not overwritten, so the Classic
binary can still load its original resources.

## V2 design

- Core: white-hot center, yellow plasma, orange flame curls, red outer rim.
- Trail: horizontal white/yellow core strands with orange flame ribbons and
  sparse red embers.
- Impact: compact radial white/yellow flash with orange flame petals.
- Core scale: 0.34 world units with a 7% sinusoidal pulse.
- Core roll: 3.5 radians per second without an extra object or draw call.
- Trail width: 0.22 world units with a +/-0.025 sinusoidal flicker.
- Impact: 12 billboard particles, 0.42 second lifetime, pool size 8.

## Assets

Source alpha PNGs and runtime DDS files:

- `Resources/Textures/Annie/Particles/V2/fireball-core-v2.png`
- `Resources/Textures/Annie/Particles/V2/fireball-core-v2.dds`
- `Resources/Textures/Annie/Particles/V2/fireball-trail-v2.png`
- `Resources/Textures/Annie/Particles/V2/fireball-trail-v2.dds`
- `Resources/Textures/Annie/Particles/V2/fireball-impact-v2.png`
- `Resources/Textures/Annie/Particles/V2/fireball-impact-v2.dds`
- `Resources/Particles/Annie_QSpellImpact_V2.fx`

Runtime DDS properties:

- Core: 512 x 512, DXT5, 10 mip levels.
- Trail: 1024 x 256, DXT5, 11 mip levels.
- Impact: 512 x 512, DXT5, 10 mip levels.
- Each DDS is approximately 350 KB.

## Image-generation prompt set

Built-in image generation was used. Each source was generated on a flat green
chroma background, converted to alpha locally, cropped, resized, and converted
to a mipmapped DXT5 DDS.

1. Fireball core: centered fantasy MOBA fireball sprite, white-hot center,
   yellow plasma, orange curls, thin red rim, compact circular silhouette.
2. Fire trail: one horizontal left-to-right flame streak, fading tail, bright
   rounded head, white/yellow strands, orange ribbons, sparse red embers.
3. Fire impact: one compact radial burst, bright central flash, short orange
   flame petals and sparks, designed for a sub-half-second hit effect.

All prompts prohibited text, watermarks, shadows, scene elements, and green in
the actual subject.

## V3 design

- V2 core and palette are retained; core scale increases from 0.34 to 0.442 world units (exactly 1.3x).
- Trail width increases from 0.22 to 0.286 and maximum length from 1.50 to 1.95 (exactly 1.3x).
- Welding sparks use a 16-instance `ParticleSystem` pool. One three-sprite emitter is requested every 0.075 seconds; delayed frames emit at most once instead of replaying missed bursts.
- Each spark emitter lasts 0.30 seconds. Normal steady state is about four active emitters, twelve billboard sprites, and four particle draw calls per projectile.
- Hit keeps the compact V2 impact and layers a new three-sprite, 0.52-second expanding explosion from an eight-instance pool.
- Projectile and trail GameObjects continue to be reused; no per-frame projectile or trail object construction was introduced.
- Per-`ParticleSystem::Play` position/rotation debug logging was removed so the 75 ms spark cadence does not flood the debugger output.

V3 assets:

- `Resources/Textures/Annie/Particles/V3/fireball-sparks-v3.png`
- `Resources/Textures/Annie/Particles/V3/fireball-sparks-v3.dds`
- `Resources/Textures/Annie/Particles/V3/fireball-explosion-v3.png`
- `Resources/Textures/Annie/Particles/V3/fireball-explosion-v3.dds`
- `Resources/Particles/Annie_QSpellTrailSparks_V3.fx`
- `Resources/Particles/Annie_QSpellExplosion_V3.fx`

Runtime DDS properties:

- Welding sparks: 256 x 256, DXT5, 9 mip levels, 87,536 bytes.
- Explosion: 512 x 512, DXT5, 10 mip levels, 349,680 bytes.

### V3 image-generation prompt set

Built-in image generation was used on flat green chroma, followed by the same local alpha cleanup and mipmapped DXT5 conversion used for V2.

1. Welding sparks: one compact real-time DX11 trail-particle sprite with a tiny white-hot center, 10-16 thin needle sparks, yellow/orange streaks, red ember tips, V2 fireball palette, no smoke, text, environment, or shadow.
2. Explosion: one compact radial magical-fire explosion with a white-hot spherical center, thick yellow/orange flame lobes, short red shockwave petals, embers, heavier than the V2 impact, no smoke, text, environment, or shadow.

## V4 design

- The V3 explosion artwork is reused as a short-lived flame lobe so the projectile body and trail resemble a rolling fireball rather than a flat streak.
- One two-sprite flame emitter plays immediately at spawn and every 0.075 seconds while moving. It shrinks from 0.50-0.62 to 0.20-0.30 over 0.22 seconds.
- Welding sparks remain layered in, but are emitted only every second flame emission (0.15 seconds) to cap particle draw-call growth.
- Both flame and spark systems use the existing `ParticleManager` pools (16 systems each); missed-frame emissions are not replayed in a burst.
- The V4 hit explosion keeps the same 0.52-second lifetime and three sprites, while every start/end scale is exactly 3x the V3 value: 1.65-2.25 at start and 3.45-4.35 at the end.
- `Resources/Particles/Annie_QSpellFlightFlame_V4.fx` stores the flight flame settings.
- `Resources/Particles/Annie_QSpellExplosion_V4.fx` stores the enlarged impact settings.

## V5 design

- The projectile body replaces its pooled Sphere mesh with the existing Quad mesh; it does not add a GameObject or draw call.
- New shader pass 15 uses `VS_Billboard`, alpha blending, no culling, alpha clip at 0.02, depth testing enabled, and depth writes disabled. The fireball always faces the camera and fully transparent texture pixels do not draw a rectangular background.
- Pass 15 renders in the existing overlay stage and the engine restores blend/depth/rasterizer state afterward, preventing pipeline-state leakage.
- The base trail width increases exactly 4x from V4 `0.286` to `1.144`. Width flicker also increases 4x from `0.0325` to `0.13`.
- Trail length, flame/spark emission cadence, particle pool sizes, and hit-explosion size remain identical to V4, isolating the requested silhouette changes.

## V6 design

- The projectile body returns to the pooled 3D Sphere mesh and shader pass 12. The body itself is no longer billboarded.
- The V5 four-times-thicker mesh trail remains attached to the projectile for a solid connected silhouette.
- Billboard rendering is retained only for flame/spark trail particles placed at successive world-space projectile positions, so the trail remains visible from the final 3D camera view.
- A versioned `Annie_QSpellFlightFlame_V6.fx` extends flame-afterimage lifetime from 0.22 to 0.32 seconds and shrinks it to 0.12-0.22 at the tail end.
- Emission remains one two-sprite flame system per 0.075 seconds and sparks per 0.15 seconds; pool sizes and no-catch-up behavior are unchanged.
- Shader pass 15 remains compiled for the preserved V5 executable but is not used by the V6 projectile body.

## V7 design

- The projectile head remains a real pooled 3D Sphere and grows from `0.442` to `0.60` world units, preserving the requested non-billboard body.
- The Cube mesh trail and repeated explosion-texture flame puffs are replaced by one camera-facing world-space ribbon attached directly to the Sphere head.
- The ribbon uses 20 fixed points, a fixed 40-vertex dynamic buffer, 114 static indices, and shader pass 16. It therefore costs one trail draw call per active projectile and creates no per-frame GameObjects or GPU buffers.
- Samples are inserted every `0.12` world units rather than every frame or timer tick. This keeps the ribbon continuous through frame-rate changes and preserves curved 3D projectile motion.
- Maximum ribbon length is `2.4` world units. Width tapers from `0.52` at the head to `0.03` at the tail, with only a 6% head-weighted flicker.
- Pass 16 uses the existing V2 trail DDS with camera-facing vertices, no culling, depth testing on, depth writes off, and additive alpha blending. Transparent tail vertices are clipped.
- Welding sparks remain pooled but emit at the lower `0.15` second cadence. `ProjectileFlightFlame` is no longer registered or played, removing its 16 pooled particle systems and repeated collision-adjacent particle work.
- The V4 three-times-sized hit explosion remains unchanged.
- The dynamic mesh API creates a D3D11 dynamic vertex buffer once and updates it with `D3D11_MAP_WRITE_DISCARD`; the index buffer remains immutable.

## V8 Trail Only 150% design

- The pooled projectile GameObject remains only as an invisible movement, target-tracking, lifetime, and collision anchor. Its Sphere `MeshRenderer` is removed, so the fireball has no separate spherical head or head draw call.
- The V7 world-space camera-facing ribbon is the complete projectile silhouette and remains one dynamic-mesh draw call per active projectile.
- Ribbon maximum length increases exactly 150%, from `2.4` to `3.6` world units.
- Head width increases exactly 150%, from `0.52` to `0.78`; tail width increases from `0.03` to `0.045`.
- Distance sampling remains `0.12` world units. Fixed capacity grows from 20 to 32 points (64 dynamic vertices, 186 static indices), preventing the longer trail from losing curvature resolution.
- Sphere-only pulse, scale, and rotation updates are removed. Pooled sparks and the V4 three-times-sized impact explosion remain unchanged.
- The F8 server-authoritative combat gather test hotkey remains included; no server or protocol files changed for V8.
## Build verification

- Debug x64 full sequential solution Rebuild: success, zero errors.
- The repository's projects share `Intermediate/Debug`; incremental or parallel
  builds can produce unrelated missing-symbol link failures. Use `/m:1` with
  `Rebuild` until each project receives a unique intermediate directory.
- No server or protocol change is required for these VFX variants.

## Runtime comparison

Run the same Annie projectile collision with Classic, V2, V3, V4, V5, V6, and V7. Compare:

- projectile readability against bright and dark terrain;
- trail direction and width;
- impact scale and duration;
- FPS immediately before and during impact;
- repeated casts and pool reuse.

If the visual needs adjustment, preserve the existing versioned executable and
create the next numbered texture/executable variant instead of overwriting the existing
comparison builds.