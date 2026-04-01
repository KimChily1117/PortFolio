using Server.Game.GameObjects;
using Server.Protocol;
using System;
using static Server.Game.GameObjects.Enemy;

namespace Server.Game.Room
{
    public partial class GameRoom
    {
        private const bool PatternTestMode = true;


        private void UpdatePlayers()
        {
            foreach (Player player in _players.Values)
            {
                if (player.IsDead)
                    continue;

                if (player.IsJumping && ServerTick >= player.JumpEndTick)
                {
                    player.IsJumping = false;
                    player.MarkDirty();

                    Console.WriteLine("[JumpEnd] tick=" + ServerTick +
                        " player=" + player.Id);
                }

                int newX = player.PosX + player.MoveInputX * player.Speed;
                int newY = player.PosY + player.MoveInputY * player.Speed;

                newX = ClampX(newX);
                newY = ClampY(newY);

                if (player.PosX != newX || player.PosY != newY)
                {
                    player.PosX = newX;
                    player.PosY = newY;
                    player.MarkDirty();
                }

                if (player.IsAttacking && ServerTick >= player.NextActionTick)
                    player.IsAttacking = false;
            }
        }

        private void UpdateEnemies()
        {
            foreach (Enemy enemy in _enemies.Values)
            {
                if (enemy.IsDead)
                    continue;

                if (enemy.State == Enemy.EnemyState.Pattern)
                {
                    switch (enemy.CurrentPatternType)
                    {
                        case Enemy.EnemyPatternType.Shockwave:
                            UpdateShockwavePattern(enemy);
                            break;

                        case Enemy.EnemyPatternType.LightZone:
                            UpdateLightZonePattern(enemy);
                            break;
                    }

                    continue;
                }

                if (enemy.IsAttacking && ServerTick >= enemy.NextActionTick)
                    enemy.IsAttacking = false;

                Player target = FindNearestAlivePlayer(enemy, EnemyDetectRange);

                if (target == null)
                {
                    if (enemy.MainState != ActorMainState.Idle)
                    {
                        enemy.MainState = ActorMainState.Idle;
                        enemy.MarkDirty();
                    }

                    enemy.TargetPlayerId = 0;
                    continue;
                }

                enemy.TargetPlayerId = target.Id;

                int distSqr = GetDistSqr(enemy, target);
                int attackRangeSqr = EnemyAttackRange * EnemyAttackRange;
                int patternRangeSqr = PatternStartRange * PatternStartRange;

                // 패턴 우선
                if (distSqr <= patternRangeSqr)
                {
                    TryStartPattern(enemy);
                    if (enemy.State == Enemy.EnemyState.Pattern)
                        continue;
                }

                // 패턴 테스트 중에는 평타 비활성
                if (PatternTestMode)
                {
                    if (enemy.MainState != ActorMainState.Idle)
                    {
                        enemy.MainState = ActorMainState.Idle;
                        enemy.MarkDirty();
                    }

                    continue;
                }

                // 기본 공격
                if (distSqr <= attackRangeSqr)
                {
                    if (enemy.MainState != ActorMainState.Idle)
                    {
                        enemy.MainState = ActorMainState.Idle;
                        enemy.MarkDirty();
                    }

                    if (ServerTick >= enemy.NextActionTick && !enemy.IsAttacking)
                    {
                        ExecuteEnemyAttack(enemy, target);
                    }
                }
                else
                {
                    MoveEnemyTowardTarget(enemy, target);
                }
            }
        }
        private void ExecuteEnemyAttack(Enemy attacker, Player target)
        {
            if (attacker == null || target == null)
                return;

            if (IsUnderSpawnProtection(target))
            {
                Console.WriteLine("[EnemyAttackBlocked] tick=" + ServerTick +
                    " target=" + target.Id +
                    " reason=SpawnProtection");
                return;
            }

            attacker.IsAttacking = true;
            attacker.NextActionTick = ServerTick + EnemyAttackCooldownTick;

            AddCombatEvent(
                CombatEventType.CombatEventAttack,
                attacker.Id,
                0,
                ActionType.ActionAttack,
                0);

            int beforeHp = target.Hp;
            target.Hp -= EnemyAttackDamage;
            if (target.Hp < 0)
                target.Hp = 0;

            target.MarkDirty();

            Console.WriteLine("[EnemyAttack] tick=" + ServerTick +
                " attacker=" + attacker.Id +
                " target=" + target.Id +
                " damage=" + EnemyAttackDamage +
                " hp=" + beforeHp + "->" + target.Hp);

            AddCombatEvent(
                CombatEventType.CombatEventHit,
                attacker.Id,
                target.Id,
                ActionType.ActionAttack,
                EnemyAttackDamage);

            if (target.IsDead)
            {
                target.OnDead();

                AddCombatEvent(
                    CombatEventType.CombatEventDeath,
                    attacker.Id,
                    target.Id,
                    ActionType.ActionAttack,
                    0);

                Console.WriteLine("[PlayerDeath] tick=" + ServerTick +
                    " target=" + target.Id);
            }
        }

        private ActorSnapshot BuildActorSnapshot(Creature creature)
        {
            ActorSnapshot actor = new ActorSnapshot();
            actor.ActorId = creature.Id;
            actor.MainState = creature.MainState;
            actor.Pos = new Vec2Int
            {
                X = creature.PosX,
                Y = creature.PosY
            };
            actor.Hp = creature.Hp;
            actor.MaxHp = creature.MaxHp;
            actor.IsDead = creature.IsDead;
            actor.IsJumping = creature.IsJumping;

            switch (creature.ObjectType)
            {
                case GameObjectType.Player:
                    actor.ActorType = ActorType.Player;
                    break;
                case GameObjectType.Enemy:
                    actor.ActorType = ActorType.Enemy;
                    break;
                default:
                    actor.ActorType = ActorType.None;
                    break;
            }

            return actor;
        }

        private void BroadcastSnapshot()
        {
            foreach (Player receiver in _players.Values)
            {
                if (receiver.Session == null)
                    continue;

                S_RoomSnapshot snapshot = new S_RoomSnapshot();
                snapshot.ServerTick = ServerTick;
                snapshot.AckInputSeq = receiver.LastAckInputSeq;

                foreach (Creature creature in GetAllCreatures())
                {
                    if (!creature.IsDirty)
                        continue;

                    snapshot.Actors.Add(BuildActorSnapshot(creature));
                }

                receiver.Session.SendProto(snapshot);

                Console.WriteLine("[Snapshot] tick=" + ServerTick +
                    " receiver=" + receiver.Id +
                    " ack=" + receiver.LastAckInputSeq +
                    " actorCount=" + snapshot.Actors.Count);
            }

            foreach (GameObject obj in GetAllObjects())
                obj.ClearDirty();
        }


        private bool HasAnyDirtyCreature()
        {
            foreach (Creature creature in GetAllCreatures())
            {
                if (creature.IsDirty)
                    return true;
            }

            return false;
        }

        private Player FindNearestAlivePlayer(Enemy enemy, int range)
        {
            Player nearest = null;
            int bestDistSqr = int.MaxValue;
            int rangeSqr = range * range;

            foreach (Player player in _players.Values)
            {
                if (player.IsDead)
                    continue;

                int distSqr = GetDistSqr(enemy, player);
                if (distSqr > rangeSqr)
                    continue;

                if (distSqr < bestDistSqr)
                {
                    bestDistSqr = distSqr;
                    nearest = player;
                }
            }

            return nearest;
        }

        private void MoveEnemyTowardTarget(Enemy enemy, Player target)
        {
            int moveX = 0;
            int moveY = 0;

            if (target.PosX > enemy.PosX) moveX = 1;
            else if (target.PosX < enemy.PosX) moveX = -1;

            if (target.PosY > enemy.PosY) moveY = 1;
            else if (target.PosY < enemy.PosY) moveY = -1;

            int newX = enemy.PosX + moveX * enemy.Speed;
            int newY = enemy.PosY + moveY * enemy.Speed;

            newX = ClampX(newX);
            newY = ClampY(newY);

            if (enemy.PosX != newX || enemy.PosY != newY)
            {
                enemy.PosX = newX;
                enemy.PosY = newY;
                enemy.MarkDirty();
            }

            ActorMainState newState = (moveX == 0 && moveY == 0)
                ? ActorMainState.Idle
                : ActorMainState.Move;

            if (enemy.MainState != newState)
            {
                enemy.MainState = newState;
                enemy.MarkDirty();
            }
        }
    }
}