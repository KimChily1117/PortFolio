using Google.Protobuf;
using Server.Game.GameObjects;
using Server.Protocol;
using System;
using System.Collections.Generic;
using static Server.Game.GameObjects.Enemy;

namespace Server.Game.Room
{
    public partial class GameRoom
    {
        public int RoomId { get; set; }
        public int ServerTick { get; private set; }

        private int _snapshotElapsedMs = 0;

        public const int TickMs = 33;              // 현재 루프 기준
        private const int SnapshotIntervalMs = 100; // 100ms 송신

        private const int AttackRange = 50;
        private const int AttackDamage = 10;
        private const int AttackCooldownTick = 15;
        private const int JumpDurationTick = 40; // 0.5초

        private const int EnemyDetectRange = 200;
        private const int EnemyAttackRange = 30;
        private const int EnemyAttackDamage = 10;
        private const int EnemyAttackCooldownTick = 30;

        private const int SpawnProtectionTick = 60; // 2초 @ 30TPS

        // Shockwave
        private const int ShockwaveWarningTick = 20;       // 0.66초
        private const int ShockwaveRecoverTick = 35;       // 패턴 자체 종료 시점
        private const int ShockwavePostCooldownTick = 45;  // 종료 후 다음 패턴까지 쉬는 시간
        private const int ShockwaveRange = 80;
        private const int ShockwaveDamage = 20;

        #region Boss Pattern 2
        // Light Zone
        private const int LightZoneDurationTick = 210;     // 7초
        private const int LightZonePostCooldownTick = 75;  // 종료 후 다음 패턴까지 쉬는 시간
        private const int LightZoneRadius = 25;
        private const int LightZoneFailDamage = 20;
        #endregion

        private const int StageMinX = -660;
        private const int StageMaxX = 660;
        private const int StageMinY = -220;
        private const int StageMaxY = 0;

        // 맵 고정 위치용
        private const int ZoneCornerOffsetX = 180;
        private const int ZoneCornerOffsetY = 90;

        private const int PatternStartRange = 120;
        

        private readonly Dictionary<int, Player> _players = new Dictionary<int, Player>();
        private readonly Dictionary<int, Enemy> _enemies = new Dictionary<int, Enemy>();


        private readonly Queue<RoomCommand> _pendingCommands = new Queue<RoomCommand>();
        private readonly List<RoomCombatEvent> _pendingCombatEvents = new List<RoomCombatEvent>();

        #region Util(Getter)
        private int ClampX(int x)
        {
            if (x < StageMinX) return StageMinX;
            if (x > StageMaxX) return StageMaxX;
            return x;
        }

        private int ClampY(int y)
        {
            if (y < StageMinY) return StageMinY;
            if (y > StageMaxY) return StageMaxY;
            return y;
        }

        private int GetAlivePlayerCount()
        {
            int count = 0;

            foreach (Player player in _players.Values)
            {
                if (!player.IsDead)
                    count++;
            }

            return count;
        }
        private IEnumerable<GameObject> GetAllObjects()
        {
            foreach (Player player in _players.Values)
                yield return player;

            foreach (Enemy enemy in _enemies.Values)
                yield return enemy;
        }

        private IEnumerable<Creature> GetAllCreatures()
        {
            foreach (Player player in _players.Values)
                yield return player;

            foreach (Enemy enemy in _enemies.Values)
                yield return enemy;
        }
        public void Enqueue(RoomCommand command)
        {
            lock (_pendingCommands)
            {
                _pendingCommands.Enqueue(command);
            }
        }

        private bool IsUnderSpawnProtection(Player player)
        {
            if (player == null)
                return false;

            return ServerTick < player.SpawnProtectionEndTick;
        }
        #endregion
        public void Tick()
        {
            ServerTick++;
            _snapshotElapsedMs += TickMs;

            ConsumeCommands();
            UpdatePlayers();
            UpdateEnemies();
            ResolveActions();

            if (_snapshotElapsedMs >= SnapshotIntervalMs)
            {
                if (HasAnyDirtyCreature())
                    BroadcastSnapshot();

                _snapshotElapsedMs -= SnapshotIntervalMs;
            }

            BroadcastCombatEvents();
        }

        private void SpawnTestEnemyIfNeeded()
        {
            const int testEnemyId = 1000;

            if (_enemies.ContainsKey(testEnemyId))
                return;

            Enemy enemy = new Enemy();
            enemy.Id = testEnemyId;
            enemy.Name = "TestEnemy";
            enemy.Room = this;
            enemy.PosX = 20;
            enemy.PosY = 0;
            enemy.MarkDirty();

            _enemies.Add(enemy.Id, enemy);

            Console.WriteLine("[Room] TestEnemy Spawned. id=" + enemy.Id +
                " pos=(" + enemy.PosX + "," + enemy.PosY + ")" +
                " hp=" + enemy.Hp);
        }

        private bool CanUseAction(Player player)
        {
            if (player == null)
                return false;

            if (player.IsDead)
                return false;

            if (ServerTick < player.NextActionTick)
                return false;

            if (player.IsAttacking)
                return false;

            return true;
        }

        private bool CanJump(Player player)
        {
            if (player == null)
                return false;

            if (player.IsDead)
                return false;

            if (player.IsJumping)
                return false;
            if(player.LastJumpRequestTick == ServerTick)
                return false;

            return true;
        }
        private void ResolveActions()
        {
            foreach (Player player in _players.Values)
            {
                if (!player.HasPendingAction)
                    continue;

                switch (player.PendingActionType)
                {
                    case ActionType.ActionAttack:
                        {
                            Console.WriteLine("[ResolveActions] player=" + player.Id +
                                " action=Attack canUse=" + CanUseAction(player));

                            if (CanUseAction(player))
                                ExecuteAttack(player);

                            break;
                        }

                    case ActionType.ActionJump:
                        {
                            Console.WriteLine("[ResolveActions] player=" + player.Id +
                                " action=Jump canUse=" + CanJump(player));

                            if (CanJump(player))
                                ExecuteJump(player);

                            break;
                        }

                    case ActionType.ActionNone:
                        break;
                    case ActionType.ActionSkill1:
                        break;
                    case ActionType.ActionSkill2:
                        break;
                }

                player.HasPendingAction = false;
            }
        }

        private void BroadcastPatternZones(Enemy enemy, ActionType actionType, List<PatternZone> zones, int durationTick)
        {
            S_PatternZones packet = new S_PatternZones();
            packet.ServerTick = ServerTick;
            packet.OwnerEnemyId = enemy.Id;
            packet.ActionType = actionType;
            packet.DurationTick = durationTick;

            foreach (PatternZone zone in zones)
            {
                ZoneInfo info = new ZoneInfo();
                info.ZoneId = zone.ZoneId;
                info.Pos = new Vec2Int
                {
                    X = zone.PosX,
                    Y = zone.PosY
                };
                info.Radius = zone.Radius;

                packet.Zones.Add(info);
            }

            Broadcast(packet);

            Console.WriteLine("[PatternZones] tick=" + ServerTick +
                " owner=" + enemy.Id +
                " action=" + actionType +
                " zoneCount=" + zones.Count +
                " durationTick=" + durationTick);
        }


        private void TryStartShockwavePattern(Enemy enemy)
        {
            if (ServerTick < enemy.NextPatternAvailableTick)
                return;

            enemy.State = Enemy.EnemyState.Pattern;
            enemy.CurrentPatternType = Enemy.EnemyPatternType.Shockwave;
            enemy.PatternStartTick = ServerTick;
            enemy.PatternTriggered = false;
            enemy.MarkDirty();

            AddCombatEvent(
                CombatEventType.CombatEventSkill,
                enemy.Id,
                0,
                ActionType.ActionSkill1,
                0);

            Console.WriteLine("[PatternStart] enemy=" + enemy.Id +
                " tick=" + ServerTick +
                " pattern=Shockwave");
        }


        private void ExecuteJump(Player player)
        {
            player.LastJumpRequestTick = ServerTick;
            player.IsJumping = true;
            player.JumpEndTick = ServerTick + JumpDurationTick;
            player.MarkDirty();

            Console.WriteLine("[Jump] tick=" + ServerTick +
                " player=" + player.Id +
                " jumpEndTick=" + player.JumpEndTick);

            AddCombatEvent(
                CombatEventType.CombatEventSkill,
                player.Id,
                0,
                ActionType.ActionJump,
                0);
        }
        private void ExecuteAttack(Player attacker)
        {
            if (attacker == null)
                return;

            attacker.IsAttacking = true;
            attacker.NextActionTick = ServerTick + AttackCooldownTick;

            AddCombatEvent(
                CombatEventType.CombatEventAttack,
                attacker.Id,
                0,
                ActionType.ActionAttack,
                0);

            Console.WriteLine("[Attack] tick=" + ServerTick +
                " attacker=" + attacker.Id +
                " nextActionTick=" + attacker.NextActionTick);

            Enemy target = FindEnemyInRange(attacker, AttackRange);
            if (target == null)
            {
                Console.WriteLine("[HitCheck] attacker=" + attacker.Id + " no enemy in range");
                return;
            }

            int beforeHp = target.Hp;
            target.Hp -= AttackDamage;
            if (target.Hp < 0)
                target.Hp = 0;

            target.MarkDirty();

            Console.WriteLine("[Hit] tick=" + ServerTick +
                " attacker=" + attacker.Id +
                " target=" + target.Id +
                " damage=" + AttackDamage +
                " hp=" + beforeHp + "->" + target.Hp);

            AddCombatEvent(
                CombatEventType.CombatEventHit,
                attacker.Id,
                target.Id,
                ActionType.ActionAttack,
                AttackDamage);

            if (target.IsDead)
            {
                target.OnDead();

                AddCombatEvent(
                    CombatEventType.CombatEventDeath,
                    attacker.Id,
                    target.Id,
                    ActionType.ActionAttack,
                    0);

                Console.WriteLine("[Death] tick=" + ServerTick +
                    " target=" + target.Id);
            }
        }

        private void ExecuteShockwave(Enemy enemy)
        {
            int rangeSqr = ShockwaveRange * ShockwaveRange;

            Console.WriteLine("[Shockwave] enemy=" + enemy.Id +
                " tick=" + ServerTick);

            foreach (Player player in _players.Values)
            {
                if (player.IsDead)
                    continue;

                if (IsUnderSpawnProtection(player))
                {
                    Console.WriteLine("[ShockwaveBlocked] player=" + player.Id +
                        " reason=SpawnProtection");
                    continue;
                }

                int distSqr = GetDistSqr(enemy, player);
                if (distSqr > rangeSqr)
                    continue;

                if (player.IsJumping)
                {
                    Console.WriteLine("[ShockwaveEvaded] player=" + player.Id);
                    continue;
                }

                int beforeHp = player.Hp;
                player.Hp -= ShockwaveDamage;
                if (player.Hp < 0)
                    player.Hp = 0;

                player.MarkDirty();

                AddCombatEvent(
                    CombatEventType.CombatEventHit,
                    enemy.Id,
                    player.Id,
                    ActionType.ActionSkill1,
                    ShockwaveDamage);

                Console.WriteLine("[ShockwaveHit] enemy=" + enemy.Id +
                    " player=" + player.Id +
                    " hp=" + beforeHp + "->" + player.Hp);

                if (player.IsDead)
                {
                    player.OnDead();

                    AddCombatEvent(
                        CombatEventType.CombatEventDeath,
                        enemy.Id,
                        player.Id,
                        ActionType.ActionSkill1,
                        0);

                    Console.WriteLine("[PlayerDeath] tick=" + ServerTick +
                        " player=" + player.Id);
                }
            }
        }


        private void UpdateShockwavePattern(Enemy enemy)
        {
            int elapsed = ServerTick - enemy.PatternStartTick;

            if (elapsed >= ShockwaveWarningTick && !enemy.PatternTriggered)
            {
                enemy.PatternTriggered = true;
                ExecuteShockwave(enemy);
            }

            if (elapsed >= ShockwaveRecoverTick)
            {
                enemy.State = Enemy.EnemyState.Idle;
                enemy.CurrentPatternType = Enemy.EnemyPatternType.None;
                enemy.NextPatternAvailableTick = ServerTick + ShockwavePostCooldownTick;
                enemy.MarkDirty();

                Console.WriteLine("[PatternEnd] enemy=" + enemy.Id +
                    " tick=" + ServerTick +
                    " pattern=Shockwave" +
                    " nextAvailable=" + enemy.NextPatternAvailableTick);
            }
        }
        private void BroadcastCombatEvents()
        {
            if (_pendingCombatEvents.Count == 0)
                return;

            S_CombatEvents packet = new S_CombatEvents();
            packet.ServerTick = ServerTick;

            foreach (RoomCombatEvent ev in _pendingCombatEvents)
            {
                CombatEvent protoEvent = new CombatEvent();
                protoEvent.EventType = ev.EventType;
                protoEvent.AttackerId = ev.AttackerId;
                protoEvent.TargetId = ev.TargetId;
                protoEvent.ActionType = ev.ActionType;
                protoEvent.Value = ev.Value;

                packet.Events.Add(protoEvent);
            }

            Broadcast(packet);

            Console.WriteLine("[CombatEvents] tick=" + ServerTick +
                " count=" + packet.Events.Count);

            _pendingCombatEvents.Clear();
        }

        private void ConsumeCommands()
        {
            while (true)
            {
                RoomCommand command = null;

                lock (_pendingCommands)
                {
                    if (_pendingCommands.Count == 0)
                        break;

                    command = _pendingCommands.Dequeue();
                }

                HandleCommand(command);
            }
        }

        private void HandleCommand(RoomCommand command)
        {
            if (command is EnterRoomCommand)
                HandleEnter((EnterRoomCommand)command);
            else if (command is LeaveRoomCommand)
                HandleLeave((LeaveRoomCommand)command);
            else if (command is MoveInputCommand)
                HandleMoveInput((MoveInputCommand)command);
            else if (command is ActionInputCommand)
                HandleActionInput((ActionInputCommand)command);
        }


        private void Broadcast(IMessage packet)
        {
            foreach (Player player in _players.Values)
            {
                if (player.Session == null)
                    continue;

                player.Session.SendProto(packet);
            }
        }
    }
}