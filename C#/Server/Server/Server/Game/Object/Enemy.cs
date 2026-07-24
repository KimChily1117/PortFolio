using Google.Protobuf.Protocol;
using Server.Data;
using Server.Game.Room;
using System;
using System.Collections.Generic;
using Server.DB;
using Server.Game.Map;
using System.Text;

namespace Server.Game.Object
{
    public class Enemy : GameObject
    {
        public int TemplateId { get; private set; }
        public float HurtBoxOffsetX { get; private set; }
        public float HurtBoxOffsetY { get; private set; }
        public float CombatLineOffsetY { get; private set; }
        public float CombatAnchorOffsetX { get; private set; }
        public float CombatAnchorOffsetY { get; private set; }
        public float HitRadius { get; private set; } = 0.5f;
        private float FacingSign => Info?.PosInfo?.MoveDir == MoveDir.Left ? -1f : 1f;
        public float HitCenterX => Info?.PosInfo == null ? 0f : Info.PosInfo.PosX + HurtBoxOffsetX * FacingSign;
        public float HitCenterY => Info?.PosInfo == null ? 0f : Info.PosInfo.PosY + HurtBoxOffsetY;
        public float CombatLineY => Info?.PosInfo == null ? 0f : Info.PosInfo.PosY + CombatLineOffsetY;
        public float CombatAnchorX => Info?.PosInfo == null ? 0f : Info.PosInfo.PosX + CombatAnchorOffsetX * FacingSign;
        public float CombatAnchorY => Info?.PosInfo == null ? 0f : Info.PosInfo.PosY + CombatAnchorOffsetY;
        private const float BakalPrefabRootScale = 0.8f;
        private const float BakalShadowLocalOffsetX = 1.25f;
        private const float BakalShadowLocalOffsetY = -1.4100002f;
        private const float BakalShadowWorldOffsetX = BakalShadowLocalOffsetX * BakalPrefabRootScale;
        private const float BakalShadowWorldOffsetY = BakalShadowLocalOffsetY * BakalPrefabRootScale;
        private const bool BakalAiEnabledForCollisionTest = true;
        private const float BakalAiMinX = -4.0f;
        private const float BakalAiMaxX = 4.0f;
        private const float BakalAiMinY = -2.25f;
        private const float BakalAiMaxY = 2.25f;
        private const float BakalAiMoveSpeed = 1.0f;
        private const float BakalAiStopDistance = 0.9f;
        private const int BakalAiMoveIntervalMs = 500;
        private static readonly bool BakalMeleeEnabled = false;
        private const int BakalMeleeSkillId = 5;
        private const float BakalMeleeRange = 1.15f;
        private const float BakalMeleeLineHalfHeight = 0.45f;
        private const int BakalMeleeRawDamage = 25;
        private const int BakalMeleeCooldownMs = 2500;
        private const int BakalMeleeActionLockMs = 900;
        private const int BakalMeteorActionLockMs = 10000;
        private const int BakalAggroDurationMs = 8000;
        private const int BakalPatrolRetargetMs = 2500;
        long _currentTime = 0;
        long _elapsedTime = 0;
        long _lastUsedTime = Environment.TickCount64;
        long _lastAiMoveTime = Environment.TickCount64;
        long _lastMeleeAttackTime;
        long _actionLockedUntilTime;
        long _aggroUntilTime;
        long _nextPatrolTargetTime;
        int _aggroPlayerId;
        float _patrolTargetX;
        float _patrolTargetY;
        int _aiMoveLogCount;
        readonly Random _aiRandom = new Random();
        bool _deathHandled;
        bool _rewardGranted;

        public Enemy()
        {
            ObjectType = GameObjectType.Enemy;            
        }


        public void Init(int tmpId)
        {
            TemplateId = tmpId;
            CurrentPlayerState = PlayerState.Idle;
            MaxHP = 1200;
            HP = MaxHP;
            ConfigureCombatAnchor(tmpId);
            IsDead = false;
            _deathHandled = false;
            _rewardGranted = false;
            _lastAiMoveTime = Environment.TickCount64;
            _lastMeleeAttackTime = 0;
            _actionLockedUntilTime = 0;
            _aggroUntilTime = 0;
            _aggroPlayerId = 0;
            _nextPatrolTargetTime = 0;
            _patrolTargetX = 0f;
            _patrolTargetY = 0f;
            _aiMoveLogCount = 0;
        }
        private void ConfigureCombatAnchor(int templateId)
        {
            HurtBoxOffsetX = 0f;
            HurtBoxOffsetY = 0f;
            CombatLineOffsetY = 0f;
            CombatAnchorOffsetX = 0f;
            CombatAnchorOffsetY = 0f;
            HitRadius = 0.5f;

            if (templateId == 1)
            {
                // Match the Unity enemy_Bakal Base/Shadow transform exactly.
                // PositionInfo is the prefab root; navigation and combat use the
                // visible foot/shadow point, mirrored only by facing direction.
                HurtBoxOffsetX = BakalShadowWorldOffsetX;
                HurtBoxOffsetY = BakalShadowWorldOffsetY;
                CombatLineOffsetY = BakalShadowWorldOffsetY;
                CombatAnchorOffsetX = BakalShadowWorldOffsetX;
                CombatAnchorOffsetY = BakalShadowWorldOffsetY;
                HitRadius = 1.2f;
            }
        }
        public void Update()
        {
            if (IsDead)
                return;

            if (Room?.RoomType == Google.Protobuf.Protocol.RoomType.Bakal && BakalAiEnabledForCollisionTest == false)
            {
                if (Info?.PosInfo != null)
                    Info.PosInfo.State = PlayerState.Idle;
                return;
            }

            // Skill timers run first so meteor can lock movement before the patrol tick.
            ProcIdle();
            if (CurrentPlayerState == PlayerState.Skill)
                ProcSkill();

            UpdateBakalAiMovement();
        }
        private void UpdateBakalAiMovement()
        {
            if (Room == null || Room.RoomType != Google.Protobuf.Protocol.RoomType.Bakal || Info?.PosInfo == null)
                return;

            long now = Environment.TickCount64;
            long elapsed = now - _lastAiMoveTime;
            if (elapsed < BakalAiMoveIntervalMs)
                return;

            _lastAiMoveTime = now;

            if (IsBakalActionLocked(now))
                return;

            if (BakalMeleeEnabled)
            {
                Player meleeTarget = FindBakalMeleeTarget(now, out string targetPolicy, out float distance, out MoveDir moveDir);
                if (meleeTarget != null && TryBakalMeleeAttack(meleeTarget, distance, moveDir, now, targetPolicy))
                    return;
            }

            UpdateBakalPatrol(now, elapsed);
        }

        private bool IsBakalActionLocked(long now)
        {
            return now < _actionLockedUntilTime;
        }

        private Player FindBakalMeleeTarget(long now, out string policy, out float distance, out MoveDir moveDir)
        {
            policy = "None";
            distance = 0f;
            moveDir = Info?.PosInfo?.MoveDir ?? MoveDir.Right;

            if (Room == null || Info?.PosInfo == null)
                return null;

            List<Player> players = Room.GetPlayersSnapshot();
            Player aggroTarget = FindAggroTarget(players, now);
            if (IsBakalHumanTarget(aggroTarget) && IsBakalMeleeTargetValid(aggroTarget, out _, out _))
            {
                policy = "AggroInRange";
                distance = GetRootDistanceTo(aggroTarget, out moveDir);
                return aggroTarget;
            }

            Player nearest = null;
            float bestDistance = float.MaxValue;
            foreach (Player player in players)
            {
                if (IsBakalHumanTarget(player) == false)
                    continue;

                if (IsBakalMeleeTargetValid(player, out float horizontalDelta, out float verticalDelta) == false)
                    continue;

                float score = horizontalDelta * horizontalDelta + verticalDelta * verticalDelta;
                if (score >= bestDistance)
                    continue;

                bestDistance = score;
                nearest = player;
            }

            if (nearest == null)
                return null;

            policy = "PatrolContact";
            distance = GetRootDistanceTo(nearest, out moveDir);
            return nearest;
        }

        private float GetRootDistanceTo(Player target, out MoveDir moveDir)
        {
            moveDir = Info?.PosInfo?.MoveDir ?? MoveDir.Right;
            if (target?.Info?.PosInfo == null || Info?.PosInfo == null)
                return 0f;

            float dx = target.Info.PosInfo.PosX - Info.PosInfo.PosX;
            float dy = target.Info.PosInfo.PosY - Info.PosInfo.PosY;
            moveDir = dx < 0f ? MoveDir.Left : MoveDir.Right;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private Player FindAiTarget(long now)
        {
            string ignored;
            return FindAiTarget(now, out ignored);
        }

        private Player FindAiTarget(long now, out string policy)
        {
            policy = "None";
            if (Room == null || Info?.PosInfo == null)
                return null;

            List<Player> players = Room.GetPlayersSnapshot();
            Player aggroTarget = FindAggroTarget(players, now);
            if (aggroTarget != null)
            {
                policy = "Aggro";
                return aggroTarget;
            }

            Player nearest = FindNearestLivingPlayer(players);
            if (nearest != null)
            {
                policy = "Nearest";
                return nearest;
            }

            return null;
        }

        private Player FindAggroTarget(List<Player> players, long now)
        {
            if (_aggroPlayerId == 0 || now > _aggroUntilTime)
                return null;

            foreach (Player player in players)
            {
                if (IsBakalHumanTarget(player) == false || player.Id != _aggroPlayerId)
                    continue;

                return player;
            }

            return null;
        }

        private Player FindNearestLivingPlayer(List<Player> players)
        {
            Player nearest = null;
            float bestDistSq = float.MaxValue;
            foreach (Player player in players)
            {
                if (IsBakalHumanTarget(player) == false)
                    continue;

                float dx = player.Info.PosInfo.PosX - Info.PosInfo.PosX;
                float dy = player.Info.PosInfo.PosY - Info.PosInfo.PosY;
                float distSq = dx * dx + dy * dy;
                if (distSq >= bestDistSq)
                    continue;

                bestDistSq = distSq;
                nearest = player;
            }

            return nearest;
        }

        public void RegisterAggro(Player attacker, string reason)
        {
            if (IsBakalHumanTarget(attacker) == false)
                return;

            long now = Environment.TickCount64;
            _aggroPlayerId = attacker.Id;
            _aggroUntilTime = now + BakalAggroDurationMs;
            Console.WriteLine($"[ENEMY_AI] AggroChanged. RoomId={Room?.RoomId ?? 0}, EnemyId={Id}, Target={attacker.Info?.Name}, TargetId={attacker.Id}, Reason={reason}, UntilMs={BakalAggroDurationMs}");
        }

        private bool TryBakalMeleeAttack(Player target, float distance, MoveDir moveDir, long now, string targetPolicy)
        {
            if (IsBakalHumanTarget(target) == false || IsBakalMeleeTargetValid(target, out float horizontalDelta, out float verticalDelta) == false)
                return false;

            if (now - _lastMeleeAttackTime < BakalMeleeCooldownMs)
                return false;

            _lastMeleeAttackTime = now;
            _actionLockedUntilTime = now + BakalMeleeActionLockMs;
            Info.PosInfo.State = PlayerState.Atk;
            Info.PosInfo.MoveDir = moveDir;
            BroadcastEnemyMove(PlayerState.Atk, moveDir);
            BroadcastEnemySkill(BakalMeleeSkillId);
            ApplyMeleeDamage(target, distance, horizontalDelta, verticalDelta, targetPolicy);
            return true;
        }

        private static bool IsBakalHumanTarget(Player player)
        {
            if (player == null || player.IsDead || player.Info?.PosInfo == null)
                return false;

            string name = player.Info.Name ?? string.Empty;
            return name.StartsWith("PD_Dummy", StringComparison.Ordinal) == false;
        }

        private bool IsBakalMeleeTargetValid(Player target, out float horizontalDelta, out float verticalDelta)
        {
            horizontalDelta = float.MaxValue;
            verticalDelta = float.MaxValue;
            if (target?.Info?.PosInfo == null || Info?.PosInfo == null)
                return false;

            // Match the player->enemy hit model: combat anchors represent the visible foot/shadow line,
            // while raw root positions can be far from the sprite's real attack contact point.
            horizontalDelta = Math.Abs(target.CombatAnchorX - CombatAnchorX);
            verticalDelta = Math.Abs(target.CombatAnchorY - CombatAnchorY);
            return horizontalDelta <= BakalMeleeRange && verticalDelta <= BakalMeleeLineHalfHeight;
        }

        private void BroadcastEnemySkill(int skillId)
        {
            if (Room == null)
                return;

            S_Skill skill = new S_Skill { Info = new SkillInfo() };
            skill.PlayerId = Info.ObjectId;
            skill.Info.SkillId = skillId;
            Room.Broadcast(skill);
        }

        private void ApplyMeleeDamage(Player target, float distance, float horizontalDelta, float verticalDelta, string targetPolicy)
        {
            int damage = Math.Max(0, BakalMeleeRawDamage - target.TotalDefence);
            Console.WriteLine($"[ENEMY_AI] MeleeAttack. RoomId={Room?.RoomId ?? 0}, EnemyId={Id}, Target={target.Info?.Name}, TargetId={target.Id}, TargetPolicy={targetPolicy}, RootDist={distance:0.00}, EnemyRoot=({Info?.PosInfo?.PosX ?? 0f:0.00},{Info?.PosInfo?.PosY ?? 0f:0.00}), EnemyCombatAnchor=({CombatAnchorX:0.00},{CombatAnchorY:0.00}), TargetRoot=({target.Info?.PosInfo?.PosX ?? 0f:0.00},{target.Info?.PosInfo?.PosY ?? 0f:0.00}), TargetCombatAnchor=({target.CombatAnchorX:0.00},{target.CombatAnchorY:0.00}), HorizontalDelta={horizontalDelta:0.00}, VerticalDelta={verticalDelta:0.00}, Range={BakalMeleeRange:0.00}, LineHalfHeight={BakalMeleeLineHalfHeight:0.00}, RawDamage={BakalMeleeRawDamage}, Defence={target.TotalDefence}, Damage={damage}, CooldownMs={BakalMeleeCooldownMs}");

            if (damage <= 0)
                return;

            target.OnDamaged(damage, this);

            S_Collision collision = new S_Collision();
            collision.PlayerId = target.Id;
            collision.Playerinfo = target.Info.Clone();
            collision.Playerinfo.ObjectId = target.Id;
            collision.Playerinfo.Damage = damage;
            if (collision.Playerinfo.StatInfo == null)
                collision.Playerinfo.StatInfo = new StatInfo();
            collision.Playerinfo.StatInfo.Hp = Math.Max(0, (int)target.HP);
            collision.Playerinfo.StatInfo.MaxHp = Math.Max(1, (int)target.MaxHP);

            Console.WriteLine($"[ENEMY_AI] S_Collision broadcast. RoomId={Room?.RoomId ?? 0}, EnemyId={Id}, Target={target.Info?.Name}, TargetId={target.Id}, ObjectId={collision.Playerinfo.ObjectId}, Damage={collision.Playerinfo.Damage}, Hp={collision.Playerinfo.StatInfo.Hp}/{collision.Playerinfo.StatInfo.MaxHp}");
            Room.Broadcast(collision);
        }

        private void UpdateBakalPatrol(long now, long elapsed)
        {
            if (Info?.PosInfo == null)
                return;

            if (now >= _nextPatrolTargetTime)
            {
                _nextPatrolTargetTime = now + BakalPatrolRetargetMs;
                PickBakalPatrolTarget();
            }

            PositionInfo enemyPos = Info.PosInfo;
            float dx = _patrolTargetX - enemyPos.PosX;
            float dy = _patrolTargetY - enemyPos.PosY;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
            if (dist <= 0.1f)
            {
                BroadcastEnemyMove(PlayerState.Idle, enemyPos.MoveDir);
                return;
            }

            float intervalSec = Math.Max(0.001f, elapsed / 1000f);
            float step = Math.Min(BakalAiMoveSpeed * intervalSec * 0.6f, dist);
            MoveDir moveDir = dx < 0f ? MoveDir.Left : MoveDir.Right;
            float nextX = enemyPos.PosX + dx / dist * step;
            float nextY = enemyPos.PosY + dy / dist * step;
            if (IsBakalWalkable(nextX, nextY, moveDir) == false)
            {
                PickBakalPatrolTarget();
                BroadcastEnemyMove(PlayerState.Idle, enemyPos.MoveDir);
                return;
            }

            enemyPos.PosX = nextX;
            enemyPos.PosY = nextY;
            ClampBakalAiBounds(enemyPos);
            enemyPos.State = PlayerState.Moving;
            enemyPos.MoveDir = moveDir;
            BroadcastEnemyMove(PlayerState.Moving, moveDir);
            LogBakalAiPatrol(dist, step);
        }

        private void PickBakalPatrolTarget()
        {
            if (MovementBoundsProvider.TryGetRandomPoint(RoomType.Bakal, _aiRandom, out float anchorX, out float anchorY))
            {
                MoveDir targetDir = anchorX < CombatAnchorX ? MoveDir.Left : MoveDir.Right;
                float targetSign = targetDir == MoveDir.Left ? -1f : 1f;
                _patrolTargetX = anchorX - CombatAnchorOffsetX * targetSign;
                _patrolTargetY = anchorY - CombatAnchorOffsetY;
                return;
            }

            _patrolTargetX = Lerp(BakalAiMinX, BakalAiMaxX, (float)_aiRandom.NextDouble());
            _patrolTargetY = Lerp(BakalAiMinY, BakalAiMaxY, (float)_aiRandom.NextDouble());
        }

        private bool IsBakalWalkable(float rootX, float rootY, MoveDir moveDir)
        {
            if (MovementBoundsProvider.TryGet(RoomType.Bakal, out _))
            {
                float sign = moveDir == MoveDir.Left ? -1f : 1f;
                float anchorX = rootX + CombatAnchorOffsetX * sign;
                float anchorY = rootY + CombatAnchorOffsetY;
                return MovementBoundsProvider.IsWalkable(RoomType.Bakal, anchorX, anchorY);
            }

            return rootX >= BakalAiMinX && rootX <= BakalAiMaxX && rootY >= BakalAiMinY && rootY <= BakalAiMaxY;
        }
        private static float Lerp(float min, float max, float t)
        {
            return min + (max - min) * t;
        }
        private static void ClampBakalAiBounds(PositionInfo posInfo)
        {
            if (MovementBoundsProvider.TryGet(RoomType.Bakal, out _))
                return;

            posInfo.PosX = Math.Max(BakalAiMinX, Math.Min(BakalAiMaxX, posInfo.PosX));
            posInfo.PosY = Math.Max(BakalAiMinY, Math.Min(BakalAiMaxY, posInfo.PosY));
        }

        private void BroadcastEnemyMove(PlayerState state, MoveDir moveDir)
        {
            if (Room == null || Info?.PosInfo == null)
                return;

            S_Move move = new S_Move
            {
                PlayerId = Info.ObjectId,
                PosInfo = new PositionInfo
                {
                    PosX = Info.PosInfo.PosX,
                    PosY = Info.PosInfo.PosY,
                    State = state,
                    MoveDir = moveDir
                }
            };

            Room.Broadcast(move);
        }



        private void LogBakalAiPatrol(float distanceBeforeMove, float step)
        {
            _aiMoveLogCount++;
            if (_aiMoveLogCount > 8 && _aiMoveLogCount % 20 != 0)
                return;

            float targetSign = Info.PosInfo.MoveDir == MoveDir.Left ? -1f : 1f;
            float targetAnchorX = _patrolTargetX + CombatAnchorOffsetX * targetSign;
            float targetAnchorY = _patrolTargetY + CombatAnchorOffsetY;
            Console.WriteLine($"[ENEMY_AI] Patrol. RoomId={Room?.RoomId ?? 0}, EnemyId={Id}, Root=({Info.PosInfo.PosX:0.00},{Info.PosInfo.PosY:0.00}), CombatAnchor=({CombatAnchorX:0.00},{CombatAnchorY:0.00}), TargetRoot=({_patrolTargetX:0.00},{_patrolTargetY:0.00}), TargetAnchor=({targetAnchorX:0.00},{targetAnchorY:0.00}), Dir={Info.PosInfo.MoveDir}, DistBefore={distanceBeforeMove:0.00}, Step={step:0.00}");
        }
        private void LogBakalAiIdleInRange(Player target, float distance, string targetPolicy)
        {
            _aiMoveLogCount++;
            if (_aiMoveLogCount > 8 && _aiMoveLogCount % 20 != 0)
                return;

            Console.WriteLine($"[ENEMY_AI] IdleInRange. RoomId={Room?.RoomId ?? 0}, EnemyId={Id}, Target={target?.Info?.Name}, TargetId={target?.Id ?? 0}, TargetPolicy={targetPolicy}, Pos=({Info.PosInfo.PosX:0.00},{Info.PosInfo.PosY:0.00}), Dist={distance:0.00}, Stop={BakalAiStopDistance:0.00}");
        }
        private void LogBakalAiMove(Player target, float distanceBeforeMove, float step, string targetPolicy)
        {
            _aiMoveLogCount++;
            if (_aiMoveLogCount > 8 && _aiMoveLogCount % 20 != 0)
                return;

            Console.WriteLine($"[ENEMY_AI] Move. RoomId={Room?.RoomId ?? 0}, EnemyId={Id}, Target={target?.Info?.Name}, TargetId={target?.Id ?? 0}, TargetPolicy={targetPolicy}, Pos=({Info.PosInfo.PosX:0.00},{Info.PosInfo.PosY:0.00}), DistBefore={distanceBeforeMove:0.00}, Step={step:0.00}, Bounds=({BakalAiMinX:0.00},{BakalAiMinY:0.00})..({BakalAiMaxX:0.00},{BakalAiMaxY:0.00})");
        }
        public void ProcIdle()
        {
            _currentTime = Environment.TickCount64;
            if (IsBakalActionLocked(_currentTime))
                return;

            _elapsedTime = _currentTime - _lastUsedTime;
        
            if(_elapsedTime >= 20000) // 20초가 지났다면
            {
                CurrentPlayerState = PlayerState.Skill;
                _lastUsedTime = Environment.TickCount64;
            }
        }

        public void ProcMoving()
        {



        }
        public void ProcAtk()
        {

        }

        public void ProcSkill()
        {
            if (IsDead || Room == null)
                return;

            long now = Environment.TickCount64;
            _actionLockedUntilTime = now + BakalMeteorActionLockMs;
            if (Info?.PosInfo != null)
            {
                Info.PosInfo.State = PlayerState.Skill;
                BroadcastEnemyMove(PlayerState.Skill, Info.PosInfo.MoveDir);
            }

            BroadcastEnemySkill(4);
            CurrentPlayerState = PlayerState.Idle;
            Console.WriteLine($"[ENEMY_AI] Action locked. Reason=Meteor, RoomId={Room?.RoomId ?? 0}, EnemyId={Id}, LockMs={BakalMeteorActionLockMs}");
        }
        public override void OnDead(GameObject attacker)
        {
            if (_deathHandled)
                return;

            _deathHandled = true;

            GameObject owner = attacker?.GetOwner();

            if(owner != null && owner.ObjectType == GameObjectType.Player && _rewardGranted == false)
            {
                RewardData rewardData = GetRewardData();
                if(rewardData != null)
                {
                    Player player = (Player)owner;
                    _rewardGranted = true;
                    DbTransaction.RewardPlayer(player, rewardData,Room);
                }
            }

            if (Room == null)
                return;

            S_Die s_Die = new S_Die();
            s_Die.Player = Info;

            Room.Broadcast(s_Die);

            if (Room.RoomType == Google.Protobuf.Protocol.RoomType.Bakal)
                Room.HandleDungeonClear(this);
        }

        private RewardData GetRewardData()
        {
            MonsterData monsterData = null;
            DataManager.MonsterDict.TryGetValue(TemplateId, out monsterData);
            if (monsterData == null || monsterData.rewards == null)
                return null;

            int rand = new Random().Next(1, 101);

            int sum = 0;
            foreach (RewardData rewardData in monsterData.rewards)
            {
                sum += rewardData.probability;

                if(rand <= sum)
                {
                    return rewardData;
                }
            }

            return null;
        }
    }
}

































