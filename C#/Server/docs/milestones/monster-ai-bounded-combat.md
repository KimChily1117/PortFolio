# Monster AI / Bounded Combat Simulation

## Goal

Add a small, server-owned Monster AI loop for Bakal so dungeon combat tests can observe a moving enemy inside the same bounded area used by DummyClient dungeon movement.

## Implemented - 2026-07-08

Server:

- `Server/Server/Game/Object/Enemy.cs`
  - Bakal-only AI movement in the existing `Enemy.Update()` path.
  - Finds the nearest living player from the room snapshot.
  - Moves toward the target at a low fixed cadence (`500ms`) and low speed (`1.0`).
  - Stops near `0.9` root-distance while melee hit authority uses combat-anchor validation.
  - Clamps enemy root position inside Bakal bounds `X[-8,8]`, `Y[-4.5,4.5]`.
  - Broadcasts existing `S_Move` packets for the enemy object id.
  - Logs `[ENEMY_AI] Move...` with room, enemy, target, position, distance, step, and bounds.

Unity client:

- `Assets/Scripts/03.Player/EnemyPlayer.cs`
  - Added `ApplyRemoteMove(PositionInfo)`.
  - Smoothly moves Bakal enemy visuals toward server `S_Move` positions.
- `Assets/Scripts/04.Network/Packet/PacketHandler.cs`
  - `S_MoveHandler` now routes movement packets to `OtherPlayer` or `EnemyPlayer`.

## Non-Goals

- Do not change Protocol.
- Do not add client-owned monster AI.
- Do not change Protocol for monster attack damage/hit authority.
- Do not change player movement authority.
- Do not implement channel switching in this milestone.

## Validation

Build check:

```text
dotnet build "E:\task\Server\Server\Server\Server.csproj" -o "E:\task\Server\tmp\build-server-ai"
```

Expected result:

```text
Build succeeds with existing netcoreapp3.1 warnings only.
```

Runtime check after restarting the server so the new build is loaded:

```text
dotnet run --project "E:\task\Server\Server\DummyClient\DummyClient.csproj" -- --scenario fill-dummy --clients 4 --partySize 4 --expectedRooms 1 --host 127.0.0.1 --port 8080 --prefix PD_Dummy --delayMs 100 --sceneReadyDelayMs 100 --holdAfterDungeonSec 12 --timeoutSec 35 --enableMovement true --movementPattern patrol --patrolMode mixed --movementRadius 20 --movementSpeed 2 --gameplayDurationSec 8 --verbose false
```

Expected evidence:

```text
- Server logs contain [ENEMY_AI] Move.
- /api/rooms shows Bakal enemy position changing but staying inside X[-8,8], Y[-4.5,4.5].
- Unity Bakal enemy visually follows S_Move after entering dungeon.
- Dummy clients still reach EnteredDungeon and GameplayCompleted.
```

## Follow-Up

1. Monster attack authority: server-side attack windows and player damage validation.
2. Per-dungeon named bounds shared by DummyClient and Enemy AI instead of duplicated constants.
3. AI states beyond chase/idle: patrol, telegraph, attack, recover.

## Aggro / Nearest / Patrol + Melee Attack - 2026-07-08

Implemented server-side encounter behavior:

```text
Target policy:
1. Aggro target: the last player who successfully damaged Bakal, valid for 8 seconds.
2. Nearest target: nearest living player in the same room when aggro is missing/expired/dead.
3. Bounded patrol: random movement inside Bakal bounds when no valid target exists.
```

Melee attack:

```text
- SkillId=5 is reserved as Bakal melee attack visual hook through existing S_Skill.
- No Protocol change.
- Server applies damage immediately only when target combat anchors are within melee range and depth line.
- Combat-anchor melee check: horizontal <= 1.15, vertical <= 0.45.
- Raw damage: 35, reduced by Player.TotalDefence.
- Cooldown: 2500ms.
- Existing SkillId=4 meteor broadcast remains active on its original timer.
```

Expected logs:

```text
[ENEMY_AI] AggroChanged. ... Reason=Damaged
[ENEMY_AI] Move. ... TargetPolicy=Aggro|Nearest
[ENEMY_AI] MeleeAttack. ... RootDist=..., EnemyCombatAnchor=..., TargetCombatAnchor=..., HorizontalDelta=..., VerticalDelta=..., RawDamage=35, Defence=..., Damage=...
[ENEMY_AI] Patrol. ... Reason implied by no target
```

Unity hook:

```text
EnemyPlayer.UseSkill(5) triggers Animator trigger "MeleeAttack".
The trigger can be wired to a real Bakal melee sprite animation later.
```
## Anchor-Based Melee Fix - 2026-07-08

Issue observed during Unity playtest:

```text
Bakal aggro switched correctly after the real player hit the boss, but the player could receive melee damage while the boss appeared to be hitting empty space.
```

Root cause:

```text
Player -> enemy hit validation already used combat anchors / combat line depth, but enemy -> player melee used raw PosInfo root distance.
For Bakal, root position is offset from the visible foot/shadow contact point, so raw-distance melee could look visually desynced.
```

Fix:

```text
- Bakal melee now validates Player.CombatAnchorX/Y against Enemy.CombatAnchorX/Y.
- Horizontal melee range reduced to 1.15.
- Vertical/depth tolerance set to 0.45.
- Melee logs now include RootDist, EnemyCombatAnchor, TargetCombatAnchor, HorizontalDelta, VerticalDelta, Range, and LineHalfHeight.
- No Protocol change.
```
## Client Remote Enemy Movement Fix - 2026-07-08

Issue observed after anchor-based server melee validation:

```text
Bakal could still look visually desynced in Unity. The likely cause was client-side remote enemy movement being applied twice: BaseCharacter.ProcWalkPlayer/ProcRunPlayer translated the enemy locally, then EnemyPlayer.ApplyRemoteRenderPosition moved it again toward the server S_Move target.
```

Fix:

```text
- EnemyPlayer now overrides ProcIdlePlayer / ProcWalkPlayer / ProcRunPlayer / ProcAtk.
- Remote enemy movement no longer uses BaseCharacter local Translate logic.
- Server S_Move remains the source of truth for enemy position.
- Client applies facing/flip from server MoveDir first, then smooths transform toward the remote target.
- Server still validates melee damage with combat-anchor range/depth checks.
```

Validation notes:

```text
- In Unity, confirm Bakal no longer drifts or flips independently while chasing.
- During [ENEMY_AI] MeleeAttack, compare HorizontalDelta/VerticalDelta with the visible contact point.
- If visual range still feels long after this client fix, tune BakalMeleeRange downward from 1.15 to 1.0.
```
## Patrol-Only Boss AI Adjustment - 2026-07-08

Decision after Unity playtest:

```text
Target-chasing Bakal looked visually awkward and made melee/contact readability worse. The boss AI is now patrol-first instead of pursuit-first.
```

Behavior:

```text
- Bakal no longer chases aggro or nearest players.
- Bakal keeps random bounded patrol inside X[-8,8], Y[-4.5,4.5].
- Aggro is still recorded for observability, but it does not drive movement pursuit.
- If an aggro target is already inside combat-anchor melee range, Bakal can attack it with TargetPolicy=AggroInRange.
- Otherwise, if any nearby player is already inside combat-anchor melee range, Bakal can attack with TargetPolicy=PatrolContact.
- Existing meteor skill timer remains unchanged.
- No Protocol change.
```

Expected logs:

```text
[ENEMY_AI] Patrol. ...
[ENEMY_AI] MeleeAttack. ... TargetPolicy=AggroInRange|PatrolContact ... HorizontalDelta=..., VerticalDelta=...
```
## Patrol Bounds / Collision HP Sync Tuning - 2026-07-08

Playtest feedback:

```text
Bakal patrol area was still too wide, and client-side HP did not visibly update after boss melee damage.
```

Changes:

```text
- Bakal patrol bounds reduced by half: X[-4,4], Y[-2.25,2.25].
- Bakal melee raw damage reduced from 35 to 25. With player defence 20, visible damage is 5.
- Server now explicitly fills S_Collision.Playerinfo.ObjectId with the target object id before broadcasting.
- Server logs [ENEMY_AI] S_Collision broadcast with target id/object id/damage.
- Unity S_CollisionHandler now falls back from Playerinfo.ObjectId to S_Collision.PlayerId and logs [CLIENT][S_COLLISION].
```

Expected evidence:

```text
Server:
[ENEMY_AI] S_Collision broadcast. ... ObjectId=..., Damage=5

Unity:
[CLIENT][S_COLLISION] PlayerId=..., ObjectId=..., Damage=5
Take Damage : 5
```
## Boss Basic Attack Cooldown Tuning - 2026-07-08

Playtest feedback:

```text
Bakal patrol-contact melee needed a clearer basic attack cooldown so repeated contact does not feel like constant damage.
```

Change:

```text
- Bakal melee cooldown increased from 1500ms to 2500ms.
- Cooldown remains server-authoritative through _lastMeleeAttackTime.
- [ENEMY_AI] MeleeAttack logs include CooldownMs=2500.
- No Protocol change.
```
## Boss Action Lock During Skills - 2026-07-08

Playtest feedback:

```text
Bakal should not keep patrolling while meteor or melee attack animations are playing.
```

Changes:

```text
- Enemy.Update now evaluates the meteor timer before the patrol movement tick.
- Bakal movement loop skips patrol while _actionLockedUntilTime is active.
- Melee attack applies a 900ms movement lock.
- Meteor applies a 10000ms movement lock, matching the current Unity MeteorPattern duration.
- Meteor start broadcasts S_Move with PlayerState.Skill before S_Skill(4), so the client can stop movement interpolation during the skill.
- No Protocol change.
```

Expected logs:

```text
[ENEMY_AI] Action locked. Reason=Meteor, ... LockMs=10000
[ENEMY_AI] MeleeAttack. ... CooldownMs=2500
```
## Bakal Animator Walk/Melee Hook - 2026-07-08

Unity setup:

```text
Animator parameters required on Bakal:
- isWalk: bool
- MeleeAttack: trigger
- SkillTrigger: trigger
- DeadTrigger: trigger
```

Client behavior:

```text
- EnemyPlayer sets isWalk=true while remote S_Move state is Moving or Run.
- EnemyPlayer sets isWalk=false while Idle, Atk, damaged, dead, meteor, or melee attack.
- SkillId=5 still triggers MeleeAttack.
- SkillId=4 still triggers SkillTrigger through MeteorPattern.
```

Expected checks:

```text
- Patrol movement plays Walk.
- Idle stops Walk.
- MeleeAttack stops Walk before the trigger fires.
- Meteor stops Walk before SkillTrigger fires.
```
## Bakal Root Scale Flip Attempt - 2026-07-08

Superseded playtest note:

```text
Root-scale flipping was tested as a quick way to keep Bakal's authored child offsets together, but later playtesting showed it can also mirror Base/Shadow and make the visual foot/contact anchor harder to reason about.
```

Current decision:

```text
- Do not flip the Bakal root for facing.
- Keep Base/Shadow stable because it is the foot/contact anchor used by client hit candidates.
- Flip only the real visual body/Animator transform; see the follow-up visual anchor recheck section below.
```
## Bakal Visual Anchor / Flip Recheck - 2026-07-08

Follow-up playtest feedback:

```text
Root-scale flipping made Bakal's visual alignment suspicious again. The HP bar was fixed, but the boss body, shadow/foot anchor, and hit/contact readability still needed a full recheck.
```

Findings:

```text
- Bakal prefab has Base/Shadow and Sprite as separate root children.
- BaseCharacter.Start() grabs the first child SpriteRenderer as _Sprite.
- On Bakal, Base/Shadow can be discovered before the real body Sprite, so hit flash / sprite bounds logs could point at the shadow instead of the boss body.
- Root-scale flipping also mirrors Base/Shadow. That can move the visual foot/contact anchor while the server still validates against a fixed combat anchor offset.
- Foot/shadow-based collision is still the intended model for this belt-scroll style combat. The problem was visual anchor selection and flip scope, not the use of foot anchors.
```

Client-side adjustment:

```text
- EnemyPlayer now resolves the real non-shadow SpriteRenderer after BaseCharacter.Start().
- Facing flip targets the Animator/Sprite transform, not the root transform.
- Base/Shadow localScale is forced back to Vector3.one so the foot/contact anchor remains stable.
- Spawn and combat anchor logs now use character._Sprite when available instead of GetComponentInChildren<SpriteRenderer>(), so logs report the body sprite bounds instead of shadow bounds.
- Added [CLIENT][ENEMY_VISUAL_ANCHOR] logs on spawn and facing changes to compare Root, Shadow, SpriteBounds, FlipTarget, RootScale, and FlipScale.
```

Expected Unity checks:

```text
- When Bakal turns left/right, Root and Base/Shadow should not jump.
- Only the visual body/Animator should mirror.
- [CLIENT][SPAWN_ANCHOR][ENEMY] and [CLIENT][ENEMY_VISUAL_ANCHOR] should show Sprite=Sprite, not Base/Shadow.
- Player hit candidates should still use Shadow/foot combat position.
- Server melee logs should continue to validate EnemyCombatAnchor/TargetCombatAnchor, not sprite center.
```
## Player Attack Candidate Hitbox / Authoritative Combo Fix - 2026-07-08

Playtest feedback:

```text
Combo count could increase while Bakal HP did not decrease. Left-facing attacks also looked visually misaligned because Bakal's sprite and shadow/contact anchor were not moving as one readable unit.
```

Changes:

```text
- Client basic attack candidate detection now uses a forward-facing Physics2D.OverlapBoxAll hitbox instead of the player's small Base/Shadow overlap collider.
- The client no longer blocks candidate sending on local Shadow-based LineMismatch / TargetBehind / OutOfRangeX checks. Those checks are now diagnostic only; the server remains authoritative.
- Combo UI is no longer incremented when C_Collision is merely sent. Combo increments only after server-confirmed S_Collision for an enemy target.
- Enemy S_Collision now includes authoritative StatInfo.Hp/MaxHp so the Bakal HP bar can sync to the server value.
- Bakal facing now flips at the root transform for visual consistency, and the server mirrors Bakal X combat/hit offsets when MoveDir is Left.
- No Protocol change.
```

Expected logs:

```text
Client candidate:
[CLIENT][ATTACK_HITBOX] ... Hits=...
[CLIENT][HIT_CANDIDATE_SEND] ...

Server accepted hit:
[HIT] Enemy damaged. ... EnemyHp=.../...

Client authoritative result:
[CLIENT][S_COLLISION] ...
[CLIENT][S_COLLISION_HP_SYNC] ... SyncedHp=.../...
```
## Bakal Server Anchor Offset Correction - Final 2026-07-20

The 2026-07-08 values `X=1.04, Y=-1.85` and the intermediate estimate `X=1.78, Y=-1.13` are superseded. They were derived from runtime visual observations and an incorrect Base/Shadow local X value.

The authoritative Unity prefab data is:

```text
enemy_Bakal root scale = 0.8
Base/Shadow local position = (1.25, -1.4100002)

world offset X = 1.25 * 0.8 = 1.0
world offset Y = -1.4100002 * 0.8 = -1.1280002
```

Current server implementation:

```text
BakalShadowLocalOffsetX = 1.25
BakalShadowLocalOffsetY = -1.4100002
BakalShadowWorldOffsetX = 1.0
BakalShadowWorldOffsetY = -1.1280002
```

- `PositionInfo` remains the prefab root coordinate sent in `S_Spawn` and `S_Move`.
- `HurtBoxOffset`, `CombatLineOffsetY`, and `CombatAnchorOffset` use the derived world offset.
- The X offset mirrors from the root according to `MoveDir.Left/Right`; Y does not mirror.
- Bakal patrol samples `MovementBounds` in CombatAnchor coordinates, converts the chosen anchor to `TargetRoot`, moves the root, and verifies the resulting anchor remains walkable.
- `[ENEMY_AI] Patrol` now logs `Root`, `CombatAnchor`, `TargetRoot`, `TargetAnchor`, and `Dir`.
- No Protocol change was required.

Validation:

- Compare Unity `Root` and `Base/Shadow`; their world delta should be approximately `(+/-1.0, -1.128)` according to facing.
- Compare the same values with server `Root` and `CombatAnchor` logs.
- Use CombatAnchor/TargetAnchor for collision and walkable-map analysis; do not compare a player position directly with the Bakal sprite body center.
## Enemy Remote Movement Smoothing - 2026-07-08

Playtest feedback:

```text
Remote players/dummies were acceptable, but Bakal enemy movement could still look like teleporting while patrol packets and Idle state updates arrived from the server.
```

Root cause:

```text
EnemyPlayer.ApplyRemoteRenderPosition snapped directly to the server target not only on large distance gaps, but also whenever PositionInfo.State was Idle. Bakal patrol can legitimately broadcast Idle after reaching a patrol target, so a delayed Idle packet could force a visible snap.
```

Fix:

```text
- EnemyPlayer no longer snaps just because the remote state is Idle.
- Enemy render position now smooths toward the latest server target with MoveTowards.
- Snap is reserved for large desync only: remoteSnapDistance=6.
- Close-enough arrival threshold is remoteArriveDistance=0.02.
- Added [CLIENT][ENEMY_MOVE_SNAP] log for the exceptional snap path.
- PositionInfo remains the latest server state; visual transform interpolation no longer rewrites PositionInfo.PosX/PosY each frame.
```

Expected check:

```text
- Bakal patrol should glide to server positions instead of snapping on Idle.
- [CLIENT][ENEMY_MOVE_SNAP] should not appear during normal patrol.
- If it appears, the log distance indicates a real large server/client desync.
```
## Temporary Bakal AI Disable For Collision Test - 2026-07-08

Purpose:

```text
Temporarily freeze Bakal while validating normal player -> enemy collision flow. This removes patrol, melee, and meteor movement/state changes from the test so hitbox/range/HP sync can be isolated.
```

Implementation:

```text
- `BakalAiEnabledForCollisionTest` was temporarily set to `false` for this historical isolation test. The current value is `true`.
- While set to false, Bakal room enemies return from `Update` after forcing `PosInfo.State=Idle`.
- Player attacks and enemy damage handling still work.
- The flag has been restored to true. A runtime/debug config is still preferable if the stationary-boss test is needed again.
```

Expected test condition:

```text
- No [ENEMY_AI] Patrol / MeleeAttack / Action locked. Reason=Meteor logs during the collision test.
- Bakal stays stationary at spawn position.
- Only player C_Skill / C_Collision / S_Collision hit flow should be evaluated.
```
## Bakal In-Place Turn Tween - 2026-07-08

Playtest feedback:

```text
Root-scale flip made Bakal face left/right correctly, but the visual pivot felt awkward because Base/Shadow moved sideways instantly during the flip.
```

Fix:

```text
- EnemyPlayer now tweens root scale X over 0.12s when facing changes.
- During the tween, the previous Base/Shadow world position is kept fixed by compensating root position.
- This makes Bakal look like it turns in place around the foot/contact anchor instead of sliding sideways during the flip.
- Server collision authority is unchanged; this is a client visual correction only.
```

Expected check:

```text
- When Bakal changes Left/Right, the feet/shadow should stay in place while the body turns.
- The turn should feel like a short pivot rather than an instant mirrored pop.
```
## Bakal Targeted Melee Disabled - 2026-07-08

Playtest feedback:

```text
Even without chase movement, PatrolContact melee made Bakal feel like it was tracking/chasing the player. The encounter reads better if Bakal does not target a player directly during the current collision/visual tuning pass.
```

Change:

```text
- BakalMeleeEnabled=false on the server.
- Bakal no longer runs FindBakalMeleeTarget / TryBakalMeleeAttack during its AI tick.
- Random bounded patrol and meteor skill remain available.
- Player -> Bakal hit/damage flow is unchanged.
- No Protocol change.
```

Expected check:

```text
- No [ENEMY_AI] MeleeAttack logs.
- No TargetPolicy=PatrolContact logs.
- Bakal still patrols randomly and can still be damaged by player attacks.
```
## Human-Only Boss Targeting / HP Sync - 2026-07-08

Playtest feedback:

```text
Bakal should identify and attack real client players only, not PD_Dummy load-test actors. Also verify that client HP updates when a real client player is hit by Bakal melee.
```

Changes:

```text
- Server Bakal target selection now excludes players whose name starts with PD_Dummy.
- Dummy hits no longer become Bakal aggro targets.
- Bakal melee S_Collision still sends Damage, and now also includes authoritative StatInfo.Hp/MaxHp after server-side OnDamaged.
- Unity S_CollisionHandler still plays local damage feedback through TakeDamage, then applies authoritative Hp/MaxHp if present.
- Unity HUD now renders HP using targetChar.HP / targetChar.MaxHP instead of hard-coded /100.
- No Protocol change.
```

Expected evidence:

```text
Server:
[ENEMY_AI] S_Collision broadcast. ... Target=Player_..., Damage=5, Hp=.../...
No Bakal melee S_Collision target should be PD_Dummy_*.

Unity:
[CLIENT][S_COLLISION] ... Damage=5
[CLIENT][S_COLLISION_HP_SYNC] ... SyncedHp=.../...
HUD HP bar decreases from server-synced HP.
```
## Player HUD HP Refresh on Boss Hit - 2026-07-08

Clarification:

```text
The HP sync issue was about the real client player's HUD HP bar, not the boss HP bar.
```

Changes:

```text
- S_Collision still calls TakeDamage for local hit feedback.
- If authoritative StatInfo.Hp/MaxHp is present, PacketHandler applies it to the damaged character.
- If the damaged character is GameManager.ObjectManager.MyPlayer, PacketHandler finds UI_HUD and calls RefreshHpBarImmediate.
- UI_HUD now auto-binds to GameManager.ObjectManager.MyPlayer if targetChar is missing.
- UI_HUD uses HP / MaxHP, not HP / 100.
```

Expected Unity logs:

```text
[CLIENT][S_COLLISION] ... Damage=5
[CLIENT][S_COLLISION_HP_SYNC] ... SyncedHp=.../...
```










## Tilemap Movement Bounds Export v1 - 2026-07-09

Problem:

```text
Bakal patrol and DummyClient dungeon movement still depend on hand-written rectangular bounds.
That keeps tests simple, but it does not match the real Tilemap walkable area and allows future AI/dummy movement to drift into visually invalid map space.
```

Implemented first pass:

```text
- Added a Unity Editor exporter at Assets/Editor/MovementBoundsExporter.cs.
- Menu: KIMCHILY_TOOL/Movement Bounds/Exporter.
- The exporter scans the selected walkable Tilemap and compresses occupied cells into world-space row spans.
- Default output path: Assets/Resources/Data/MovementBounds.json.
- Added server MovementBoundsProvider that loads MovementBounds.json from ConfigManager.Config.dataPath or known Unity data-path fallbacks.
- Bakal patrol now picks random patrol targets from exported walkable spans when available.
- Bakal next-step validation rejects non-walkable patrol steps and retargets instead of walking outside the exported area.
- If MovementBounds.json is missing or invalid, Bakal falls back to the previous hard-coded rectangle.
- No Protocol change.
```

Current scope:

```text
- Server runtime applies exported bounds to Bakal patrol first.
- DummyClient still uses its existing CLI rectangular dungeon bounds.
- Unity player-side movement clamp is not implemented yet.
- The exporter treats "tile exists" on the selected Tilemap as walkable. If the scene uses obstacle tiles instead, create/select a dedicated Walkable Tilemap before export.
```

Expected check:

```text
1. Open Bakal scene in Unity.
2. Run KIMCHILY_TOOL/Movement Bounds/Exporter.
3. Select the walkable Tilemap, Room Type=Bakal, Export.
4. Confirm Assets/Resources/Data/MovementBounds.json is generated.
5. Start server and enter Bakal.
6. Server should log:
   [MOVEMENT_BOUNDS] Loaded. Path=..., Maps=1
7. Bakal patrol logs should stay inside exported spans.
8. If the JSON is deleted, server should log fallback and Bakal should keep using the previous rectangle.
```

## Tilemap Export Diagnostics / Center-Based Span Fix - 2026-07-09

Issue found during validation:

```text
The first MovementBounds export could load successfully but still produce suspicious data. The likely causes were selecting a non-walkable Tilemap, very low HasTile count, or using CellToWorld without confirming tile center/anchor semantics.
```

Changes:

```text
- MovementBoundsExporter now calculates spans from Tilemap.GetCellCenterWorld(cell) plus Grid.cellSize * 0.5.
- Exported JSON now includes coordinateBasis, defaulting to CombatAnchor.
- Exported JSON now includes hasTileCount for quick sanity checks.
- Exporter logs Grid cell size, Grid/Tilemap transform position/scale, TileAnchor, cellBounds, HasTileCount, first sample cells, CellToWorld, Center, WorldMin, and result min/max/span count.
- Server MovementBoundsProvider model now reads coordinateBasis and hasTileCount and logs them when a map loads.
```

Validation focus:

```text
- If HasTileCount is 0, 1, or unexpectedly tiny, the selected Tilemap is wrong or mostly empty.
- If CellToWorld and Center differ in a surprising way, inspect Grid/Tilemap transform and TileAnchor.
- The selected Tilemap should represent the character Shadow/CombatAnchor walkable floor, not wall/decor/collision art.
```

## Bakal FlipX Visual Policy - 2026-07-09

Playtest feedback:

```text
Root scale -1 flipping fixed facing direction, but it made hierarchy offsets and shadow/sprite alignment feel fragile.
For Bakal, facing should not move root or child transforms; it should only flip the rendered sprite state.
```

Change:

```text
- EnemyPlayer no longer uses DG.Tweening for facing scale tween.
- EnemyPlayer no longer multiplies root scale X by -1 for Left.
- EnemyPlayer now caches the main SpriteRenderer and Shadow SpriteRenderer.
- Left sets spriteRenderer.flipX=true and shadowRenderer.flipX=true.
- Right sets both flipX=false.
- Root scale is normalized back to positive X if an old negative scale is detected.
```

Expected check:

```text
- Bakal facing Left/Right should not shift root position.
- Shadow and sprite should flip together.
- Root scale should remain positive.
- [CLIENT][ENEMY_VISUAL_ANCHOR] logs should show SpriteFlipX/ShadowFlipX instead of FlipScale.
```

## Bakal Shadow Direction Scale Fix - 2026-07-09

Follow-up visual correction:

```text
The main Bakal sprite should continue using SpriteRenderer.flipX, but the shadow visual needs localScale.x sign flipping to align with the authored hierarchy.
```

Change:

```text
- EnemyPlayer keeps the main sprite direction as SpriteRenderer.flipX.
- EnemyPlayer keeps shadowRenderer.flipX=false.
- EnemyPlayer caches the shadow transform's default localScale.
- Left sets shadow localScale.x to negative abs(defaultX).
- Right sets shadow localScale.x to positive abs(defaultX).
- Root scale remains positive and is not used for facing.
```

Expected check:

```text
- Left: SpriteFlipX=True, ShadowFlipX=False, ShadowScale.x < 0.
- Right: SpriteFlipX=False, ShadowFlipX=False, ShadowScale.x > 0.
- Bakal root position should not jump when changing direction.
```

## Bakal Shadow Local Position Mirror - 2026-07-09

Follow-up visual correction:

```text
When the shadow localScale.x is mirrored, the shadow localPosition.x must be mirrored with the same sign so the authored offset stays aligned with the sprite direction.
```

Change:

```text
- EnemyPlayer now caches shadow default localPosition.
- Left sets shadow localScale.x < 0 and shadow localPosition.x < 0.
- Right sets shadow localScale.x > 0 and shadow localPosition.x > 0.
- Main sprite still uses SpriteRenderer.flipX.
- Root scale and root position are not used for facing.
```

Expected check:

```text
[CLIENT][ENEMY_VISUAL_ANCHOR] ... ShadowScale=(negative x), ShadowLocalPos=(negative x) when facing Left.
[CLIENT][ENEMY_VISUAL_ANCHOR] ... ShadowScale=(positive x), ShadowLocalPos=(positive x) when facing Right.
```
