using Google.Protobuf.Protocol;
using Server.Game.Object;
using Server.Game.Map;
using Server.Monitoring;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Server.Game.Room
{
    public partial class GameRoom : JobSerializer
    {
        private static readonly bool DebugDummyMoveBroadcastLog = true;
        private static readonly Dictionary<int, int> DebugMoveLogCounts = new Dictionary<int, int>();
        private const float FrontEpsilon = 0.05f;
        private static readonly Dictionary<int, SkillCombatSpec> SkillSpecs = new Dictionary<int, SkillCombatSpec>
        {
            { 2, new SkillCombatSpec(2, 50, 1.8f, 1200, 50, 0.4f, 1200) },
            { 3, new SkillCombatSpec(3, 50, 1.8f, 1200, 50, 0.4f, 1200) },
            { 4, new SkillCombatSpec(4, 50, 1.8f, 1200, 50, 0.4f, 1200) },
        };

        private sealed class SkillCombatSpec
        {
            public SkillCombatSpec(int skillId, int damage, float range, int activeWindowMs, int actionLockMs, float lineHalfHeight, int cooldownMs)
            {
                SkillId = skillId;
                Damage = damage;
                Range = range;
                ActiveWindowMs = activeWindowMs;
                ActionLockMs = actionLockMs;
                LineHalfHeight = lineHalfHeight;
                CooldownMs = cooldownMs;
            }

            public int SkillId { get; }
            public int Damage { get; }
            public float Range { get; }
            public int ActiveWindowMs { get; }
            public int ActionLockMs { get; }
            public float LineHalfHeight { get; }
            public int CooldownMs { get; }
        }

        public void HandleMove(Player player, C_Move movePacket)
        {
            if (player == null || movePacket?.PosInfo == null)
                return;

            PositionInfo requested = movePacket.PosInfo;
            bool enteredPublicTownArea = false;
            if (RoomType == RoomType.Town && player.IsInPublicTownArea == false &&
                TownSpawnService.IsPublicLobbyPosition(requested.PosX, requested.PosY))
            {
                player.IsInPublicTownArea = true;
                enteredPublicTownArea = true;
                Console.WriteLine($"[TOWN_FLOW][SERVER_PUBLIC_ENTER] RoomId={RoomId}, Player={player.Info?.Name}, PlayerId={player.Id}, Pos=({requested.PosX:0.00},{requested.PosY:0.00})");
            }

            float acceptedX = requested.PosX;
            float acceptedY = requested.PosY;
            MoveDir acceptedMoveDir = requested.MoveDir;
            ApplyMovementBounds(player, requested.PosX, requested.PosY, requested.State, requested.MoveDir, out acceptedX, out acceptedY, out acceptedMoveDir);

            ObjectInfo info = player.Info;
            info.PosInfo = new PositionInfo
            {
                PosX = acceptedX,
                PosY = acceptedY,
                State = requested.State,
                MoveDir = acceptedMoveDir
            };
            player.UpdateFacing(acceptedMoveDir);

            if (enteredPublicTownArea)
                Console.WriteLine($"[TOWN_FLOW][SERVER_PUBLIC_APPLIED] RoomId={RoomId}, Player={player.Info?.Name}, PlayerId={player.Id}, Pos=({acceptedX:0.00},{acceptedY:0.00}), Public={player.IsInPublicTownArea}");

            S_Move resMovePacket = new S_Move
            {
                PlayerId = player.Info.ObjectId,
                PosInfo = new PositionInfo
                {
                    PosX = acceptedX,
                    PosY = acceptedY,
                    State = requested.State,
                    MoveDir = acceptedMoveDir
                }
            };

            DebugLogDummyMoveBroadcast(player, resMovePacket);
            BroadcastMoveWithAoi(player, resMovePacket);
        }

        private void ApplyMovementBounds(Player player, float requestedX, float requestedY, PlayerState state, MoveDir moveDir, out float acceptedX, out float acceptedY, out MoveDir acceptedMoveDir)
        {
            acceptedX = requestedX;
            acceptedY = requestedY;
            acceptedMoveDir = moveDir;

            if (player?.Info?.PosInfo == null)
                return;

            if (MovementBoundsProvider.TryGet(RoomType, out MovementBoundsMap map) == false)
                return;

            bool currentWalkable = map.IsWalkable(player.Info.PosInfo.PosX, player.Info.PosInfo.PosY);
            bool requestedWalkable = map.IsWalkable(requestedX, requestedY);

            // MyRoom is intentionally outside the public Town tilemap. Start enforcing Town only after the player is already in the lobby bounds.
            if ((RoomType == RoomType.Town && currentWalkable == false) || requestedWalkable)
                return;

            if (map.TryClamp(requestedX, requestedY, out float clampedX, out float clampedY) == false)
                return;

            acceptedX = clampedX;
            acceptedY = clampedY;
            acceptedMoveDir = ResolveBoundsBounceDirection(requestedX, requestedY, acceptedX, acceptedY, moveDir);
            Console.WriteLine($"[MOVE][BOUNDS_CLAMP] RoomId={RoomId}, RoomType={RoomType}, Player={player.Info?.Name}, PlayerId={player.Id}, Requested=({requestedX:0.00},{requestedY:0.00}), Accepted=({acceptedX:0.00},{acceptedY:0.00}), State={state}, Dir={moveDir}, AcceptedDir={acceptedMoveDir}");
        }

        private static MoveDir ResolveBoundsBounceDirection(float requestedX, float requestedY, float acceptedX, float acceptedY, MoveDir fallback)
        {
            float dx = requestedX - acceptedX;
            float dy = requestedY - acceptedY;

            if (Math.Abs(dx) >= Math.Abs(dy) && Math.Abs(dx) > 0.001f)
                return dx > 0f ? MoveDir.Left : MoveDir.Right;

            if (Math.Abs(dy) > 0.001f)
                return dy > 0f ? MoveDir.Down : MoveDir.Up;

            return fallback;
        }

        private void DebugLogDummyMoveBroadcast(Player mover, S_Move movePacket)
        {
            if (DebugDummyMoveBroadcastLog == false || mover?.Info == null || movePacket?.PosInfo == null)
                return;

            string moverName = mover.Info.Name ?? string.Empty;
            if (moverName.StartsWith("PD_Dummy", StringComparison.Ordinal) == false)
                return;

            int count = 0;
            DebugMoveLogCounts.TryGetValue(mover.Id, out count);
            count++;
            DebugMoveLogCounts[mover.Id] = count;

            if (count > 8 && count % 20 != 0)
                return;

            string recipients = string.Join(",", _players.Values.Select(p => p.Info?.Name ?? $"ObjectId={p.Id}"));
            PositionInfo pos = movePacket.PosInfo;
            Console.WriteLine($"[MOVE] C_Move received. RoomId={RoomId}, Player={moverName}, ObjectId={mover.Id}, Pos=({pos.PosX:0.00},{pos.PosY:0.00}), State={pos.State}, Dir={pos.MoveDir}, LogCount={count}");
            Console.WriteLine($"[MOVE] S_Move broadcast. RoomId={RoomId}, Mover={moverName}, Recipients={recipients}, Pos=({pos.PosX:0.00},{pos.PosY:0.00}), State={pos.State}");
        }
        public void HandleJump(Player player, C_Jump jumpPacket)
        {
            if (player == null)
                return;


            // TODO : 검증

            // 일단 서버에서 좌표 이동
            ObjectInfo info = player.Info;
            info.PosInfo = jumpPacket.PosInfo;
            info.PosInfo.MoveDir = jumpPacket.PosInfo.MoveDir;
            player.UpdateFacing(jumpPacket.PosInfo.MoveDir);

            // 다른 플레이어한테도 알려준다
            S_Jump resJumpPacket = new S_Jump();
            resJumpPacket.PlayerId = player.Info.ObjectId;
            resJumpPacket.PosInfo = jumpPacket.PosInfo;
            resJumpPacket.PosInfo.MoveDir = jumpPacket.PosInfo.MoveDir;


            Broadcast(resJumpPacket);

        }


        public void HandleMoveScene(Player player, C_SceneMove scenePacket)
        {
            if (player == null)
                return;

            S_SceneMove s_Scene_Move = new S_SceneMove();
            s_Scene_Move.Playerinfo = player.Info;

            if (scenePacket.Playerinfo.IsMaster)
            {
                Broadcast(s_Scene_Move);
            }
        }


        /// <summary>
        /// </summary>
        /// <param name="player"> 시전자의 정보 -> ?? </param>
        /// <param name="collisionPacket"> 피폭자의 정보를 넣을려고 </param>
        public void HandleCollision(Player player, C_Collision collisionPacket)
        {
            if (player == null || collisionPacket == null || collisionPacket.Playerinfo == null)
                return;

            int targetObjectId = collisionPacket.Playerinfo.ObjectId;
            if (targetObjectId <= 0)
            {
                Console.WriteLine($"[HIT] Collision ignored. Reason=InvalidTargetId, AttackerId={player.Id}, TargetId={targetObjectId}");
                return;
            }

            Enemy enemy = null;
            if (_enemys.TryGetValue(targetObjectId, out enemy))
            {
                if (enemy == null || enemy.IsDead)
                {
                    Console.WriteLine($"[HIT] Enemy collision ignored. Reason=TargetDeadOrNull, AttackerId={player.Id}, TargetId={targetObjectId}");
                    return;
                }

                SkillCastState cast = player.SkillCastState;
                DateTime nowUtc = DateTime.UtcNow;

                int requestedSkillId = collisionPacket.Playerinfo.SkillInfo != null && collisionPacket.Playerinfo.SkillInfo.SkillId > 0
                    ? collisionPacket.Playerinfo.SkillInfo.SkillId
                    : cast.SkillId;

                if (cast == null || cast.HasActiveCast == false)
                {
                    RecordRejectedHit("NoActiveSkillCast", $", Player={player.Info?.Name}, PlayerId={player.Id}, RequestedSkillId={requestedSkillId}, TargetId={targetObjectId}");
                    return;
                }

                if (TryGetSkillSpec(requestedSkillId, out SkillCombatSpec spec) == false)
                {
                    RecordRejectedHit("InvalidSkill", $", Player={player.Info?.Name}, PlayerId={player.Id}, SkillId={requestedSkillId}, TargetId={targetObjectId}");
                    return;
                }

                if (cast.TryGetActiveCast(requestedSkillId, nowUtc, out SkillCastState.ActiveCast activeCast) == false)
                {
                    RecordRejectedHit("SkillCastExpired", $", Player={player.Info?.Name}, PlayerId={player.Id}, SkillId={requestedSkillId}, TargetId={targetObjectId}");
                    return;
                }

                if (activeCast.HitTargets.Contains(targetObjectId))
                {
                    RecordRejectedHit("DuplicateHit", $", Player={player.Info?.Name}, PlayerId={player.Id}, SkillId={activeCast.SkillId}, CastSeq={activeCast.CastSeq}, TargetId={targetObjectId}");
                    return;
                }

                bool hitInRange = IsEnemyHitValid(player, enemy, spec, out float distance, out float allowedRange, out float forwardDx, out float verticalDelta, out string invalidReason);
                PositionInfo playerPos = player.Info?.PosInfo;
                PositionInfo enemyPos = enemy.Info?.PosInfo;
                bool lineOverlap = verticalDelta <= spec.LineHalfHeight;
                Console.WriteLine($"[HIT][LINE_CHECK] Player={player.Info?.Name}, PlayerId={player.Id}, SkillId={activeCast.SkillId}, CastSeq={activeCast.CastSeq}, TargetId={targetObjectId}, PlayerPos=({playerPos?.PosX ?? 0f:0.00},{playerPos?.PosY ?? 0f:0.00}), PlayerCombatAnchor=({player.CombatAnchorX:0.00},{player.CombatAnchorY:0.00}), PlayerCombatLineY={player.CombatAnchorY:0.00}, PlayerDepthBandHalfHeight=0.00, EnemyRoot=({enemyPos?.PosX ?? 0f:0.00},{enemyPos?.PosY ?? 0f:0.00}), EnemyHitCenter=({enemy.HitCenterX:0.00},{enemy.HitCenterY:0.00}), EnemyCombatAnchor=({enemy.CombatAnchorX:0.00},{enemy.CombatAnchorY:0.00}), EnemyCombatLineY={enemy.CombatAnchorY:0.00}, EnemyDepthBandHalfHeight={spec.LineHalfHeight:0.00}, AllowedDepth={spec.LineHalfHeight:0.00}, LineOverlap={lineOverlap}, EnemyHitRadius={enemy.HitRadius:0.00}, SkillRange={spec.Range:0.00}, Facing={player.LastFacingDir}, ForwardDx={forwardDx:0.00}, VerticalDelta={verticalDelta:0.00}, Dist={distance:0.00}, Allowed={allowedRange:0.00}, ReasonCandidate={invalidReason}");
                if (hitInRange == false)
                {
                    RecordRejectedHit(invalidReason, $", Player={player.Info?.Name}, PlayerId={player.Id}, SkillId={activeCast.SkillId}, CastSeq={activeCast.CastSeq}, TargetId={targetObjectId}, PlayerPos=({playerPos?.PosX ?? 0f:0.00},{playerPos?.PosY ?? 0f:0.00}), PlayerCombatAnchor=({player.CombatAnchorX:0.00},{player.CombatAnchorY:0.00}), PlayerCombatLineY={player.CombatAnchorY:0.00}, PlayerDepthBandHalfHeight=0.00, EnemyRoot=({enemyPos?.PosX ?? 0f:0.00},{enemyPos?.PosY ?? 0f:0.00}), EnemyHitCenter=({enemy.HitCenterX:0.00},{enemy.HitCenterY:0.00}), EnemyCombatAnchor=({enemy.CombatAnchorX:0.00},{enemy.CombatAnchorY:0.00}), EnemyCombatLineY={enemy.CombatAnchorY:0.00}, EnemyDepthBandHalfHeight={spec.LineHalfHeight:0.00}, AllowedDepth={spec.LineHalfHeight:0.00}, EnemyHitRadius={enemy.HitRadius:0.00}, SkillRange={spec.Range:0.00}, Facing={player.LastFacingDir}, ForwardDx={forwardDx:0.00}, VerticalDelta={verticalDelta:0.00}, Dist={distance:0.00}, Allowed={allowedRange:0.00}");
                    return;
                }

                activeCast.HitTargets.Add(targetObjectId);

                S_Collision enemyCollision = new S_Collision();
                enemyCollision.Playerinfo = collisionPacket.Playerinfo.Clone();
                enemyCollision.Playerinfo.ObjectId = targetObjectId;
                enemyCollision.Playerinfo.Damage = spec.Damage;
                enemyCollision.PlayerId = targetObjectId;

                enemy.OnDamaged(spec.Damage, player);
                if (enemyCollision.Playerinfo.StatInfo == null)
                    enemyCollision.Playerinfo.StatInfo = new StatInfo();
                enemyCollision.Playerinfo.StatInfo.Hp = Math.Max(0, (int)enemy.HP);
                enemyCollision.Playerinfo.StatInfo.MaxHp = Math.Max(1, (int)enemy.MaxHP);
                enemy.RegisterAggro(player, "Damaged");
                Console.WriteLine($"[HIT] Enemy damaged. Player={player.Info?.Name}, PlayerId={player.Id}, SkillId={activeCast.SkillId}, CastSeq={activeCast.CastSeq}, EnemyId={targetObjectId}, Dist={distance:0.00}, Allowed={allowedRange:0.00}, Damage={spec.Damage}, EnemyHp={enemy.HP:0.00}/{enemy.MaxHP:0.00}");
                Broadcast(enemyCollision);
                return;
            }

            if (targetObjectId != player.Id)
            {
                Console.WriteLine($"[HIT] Collision ignored. Reason=TargetNotInRoomEnemies, AttackerId={player.Id}, TargetId={targetObjectId}");
                return;
            }

            int damage = Math.Max(0, (int)collisionPacket.Playerinfo.Damage - player.TotalDefence);
            if (player.IsDead)
            {
                Console.WriteLine($"[HIT] Player collision ignored. Reason=PlayerDead, PlayerId={player.Id}");
                return;
            }

            Console.WriteLine($"[HIT] Player environmental damage. PlayerId={player.Id}, RawDamage={collisionPacket.Playerinfo.Damage}, Defence={player.TotalDefence}, Damage={damage}");

            S_Collision playerCollision = new S_Collision();
            playerCollision.PlayerId = targetObjectId;
            playerCollision.Playerinfo = collisionPacket.Playerinfo;
            playerCollision.Playerinfo.Damage = damage;

            player.OnDamaged(damage, player);
            Broadcast(playerCollision);
        }

        private static void RecordRejectedHit(string reason, string details)
        {
            CombatRejectMetrics.Increment(reason);
            Console.WriteLine($"[HIT] Collision ignored. Reason={reason}{details}");
        }

        private static void RecordRejectedSkillCast(string reason, string details)
        {
            CombatRejectMetrics.Increment(reason);
            Console.WriteLine($"[SKILL] Cast rejected. Reason={reason}{details}");
        }
        private static bool TryGetSkillSpec(int skillId, out SkillCombatSpec spec)
        {
            return SkillSpecs.TryGetValue(skillId, out spec);
        }

        private static bool IsEnemyHitValid(Player player, Enemy enemy, SkillCombatSpec spec, out float distance, out float allowedRange, out float forwardDx, out float verticalDelta, out string invalidReason)
        {
            distance = float.MaxValue;
            allowedRange = 0f;
            forwardDx = 0f;
            verticalDelta = float.MaxValue;
            invalidReason = "InvalidPosition";

            PositionInfo playerPos = player?.Info?.PosInfo;
            if (playerPos == null || enemy == null || enemy.Info?.PosInfo == null || spec == null)
                return false;

            float dx = enemy.CombatAnchorX - player.CombatAnchorX;
            float dy = enemy.CombatAnchorY - player.CombatAnchorY;
            verticalDelta = Math.Abs(dy);
            forwardDx = player.LastFacingDir == MoveDir.Left ? -dx : dx;
            allowedRange = spec.Range + Math.Max(0f, enemy.HitRadius);
            distance = Math.Abs(dx);

            if (player.LastFacingDir != MoveDir.Left && player.LastFacingDir != MoveDir.Right)
            {
                invalidReason = "InvalidFacing";
                return false;
            }

            if (forwardDx < -FrontEpsilon)
            {
                invalidReason = "TargetBehind";
                return false;
            }

            if (verticalDelta > spec.LineHalfHeight)
            {
                invalidReason = "LineMismatch";
                return false;
            }

            if (distance > allowedRange)
            {
                invalidReason = "OutOfRange";
                return false;
            }

            invalidReason = "None";
            return true;
        }

        public void HandleSkill(Player player, C_Skill skillPacket)
        {
            if (player == null || skillPacket?.Info == null)
                return;

            int skillId = skillPacket.Info.SkillId;
            if (TryGetSkillSpec(skillId, out SkillCombatSpec spec) == false)
            {
                RecordRejectedSkillCast("InvalidSkill", $", Player={player.Info?.Name}, PlayerId={player.Id}, SkillId={skillId}");
                return;
            }

            SkillCastState cast = player.SkillCastState;
            DateTime nowUtc = DateTime.UtcNow;
            if (cast.TryGetCooldownRemaining(skillId, spec.CooldownMs, nowUtc, out int remainingMs))
            {
                RecordRejectedSkillCast("CooldownActive", $", Player={player.Info?.Name}, PlayerId={player.Id}, SkillId={skillId}, RemainingMs={remainingMs}");
                return;
            }

            if (cast.TryGetActiveLock(nowUtc, out SkillCastState.ActiveCast activeCast))
            {
                RecordRejectedSkillCast("ActionLocked", $", Player={player.Info?.Name}, PlayerId={player.Id}, ActiveSkillId={activeCast.SkillId}, LockUntil={activeCast.LockUntilUtc:O}, ActiveUntil={activeCast.ActiveUntilUtc:O}");
                return;
            }

            cast.BeginCast(skillId, spec.ActiveWindowMs, spec.ActionLockMs, nowUtc);
            Console.WriteLine($"[SKILL] Active cast recorded. Player={player.Info?.Name}, PlayerId={player.Id}, SkillId={skillId}, CastSeq={cast.CastSeq}, Damage={spec.Damage}, Range={spec.Range:0.00}, ActiveWindowMs={spec.ActiveWindowMs}, ActionLockMs={spec.ActionLockMs}, CooldownMs={spec.CooldownMs}, ActiveUntil={cast.ActiveUntilUtc:O}");

            ObjectInfo info = player.Info;
            S_Skill skill = new S_Skill() { Info = new SkillInfo() };

            skill.PlayerId = info.ObjectId;
            skill.Info.SkillId = skillId;
            Broadcast(skill);
        }
    }
}


















