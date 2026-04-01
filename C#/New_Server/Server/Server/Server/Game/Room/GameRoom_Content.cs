using Server.Game.GameObjects;
using Server.Protocol;
using System;
using System.Collections.Generic;
using System.Text;
using static Server.Game.GameObjects.Enemy;

namespace Server.Game.Room
{
    public partial class GameRoom
    {
        private void TryStartPattern(Enemy enemy)
        {
            if (ServerTick < enemy.NextPatternAvailableTick)
                return;

            if (enemy.NextPatternType == Enemy.EnemyPatternType.Shockwave)
            {
                TryStartShockwavePattern(enemy);
                enemy.NextPatternType = Enemy.EnemyPatternType.LightZone;
            }
            else
            {
                TryStartLightZonePattern(enemy);
                enemy.NextPatternType = Enemy.EnemyPatternType.Shockwave;
            }
        }

        private void CreateLightZones(Enemy enemy)
        {
            enemy.ActiveZones.Clear();

            enemy.ActiveZones.Add(new PatternZone
            {
                ZoneId = 1,
                PosX = -ZoneCornerOffsetX,
                PosY = ZoneCornerOffsetY,
                Radius = LightZoneRadius,
                IsSatisfied = false
            });

            enemy.ActiveZones.Add(new PatternZone
            {
                ZoneId = 2,
                PosX = ZoneCornerOffsetX,
                PosY = ZoneCornerOffsetY,
                Radius = LightZoneRadius,
                IsSatisfied = false
            });

            enemy.ActiveZones.Add(new PatternZone
            {
                ZoneId = 3,
                PosX = -ZoneCornerOffsetX,
                PosY = -ZoneCornerOffsetY,
                Radius = LightZoneRadius,
                IsSatisfied = false
            });

            enemy.ActiveZones.Add(new PatternZone
            {
                ZoneId = 4,
                PosX = ZoneCornerOffsetX,
                PosY = -ZoneCornerOffsetY,
                Radius = LightZoneRadius,
                IsSatisfied = false
            });

            enemy.RequiredZoneCount = Math.Min(GetAlivePlayerCount(), enemy.ActiveZones.Count);
        }


        private void TryStartLightZonePattern(Enemy enemy)
        {
            if (ServerTick < enemy.NextPatternAvailableTick)
                return;

            enemy.State = Enemy.EnemyState.Pattern;
            enemy.CurrentPatternType = Enemy.EnemyPatternType.LightZone;
            enemy.PatternStartTick = ServerTick;
            enemy.PatternResolveTick = ServerTick + LightZoneDurationTick;
            enemy.PatternTriggered = false;

            CreateLightZones(enemy);
            enemy.MarkDirty();

            AddCombatEvent(
                CombatEventType.CombatEventSkill,
                enemy.Id,
                0,
                ActionType.ActionSkill2,
                0);

            BroadcastPatternZones(
                enemy,
                ActionType.ActionSkill2,
                enemy.ActiveZones,
                LightZoneDurationTick);

            Console.WriteLine("[LightZoneStart] enemy=" + enemy.Id +
                " tick=" + ServerTick +
                " resolveTick=" + enemy.PatternResolveTick +
                " required=" + enemy.RequiredZoneCount);
        }

        private bool IsPlayerInsideZone(Player player, PatternZone zone)
        {
            int dx = player.PosX - zone.PosX;
            int dy = player.PosY - zone.PosY;
            int distSqr = dx * dx + dy * dy;
            return distSqr <= zone.Radius * zone.Radius;
        }

        private void UpdateLightZoneOccupancy(Enemy enemy)
        {
            foreach (PatternZone zone in enemy.ActiveZones)
            {
                zone.IsSatisfied = false;

                foreach (Player player in _players.Values)
                {
                    if (player.IsDead)
                        continue;

                    if (IsPlayerInsideZone(player, zone))
                    {
                        zone.IsSatisfied = true;
                        break;
                    }
                }
            }
        }


        private bool IsLightZonePatternSuccess(Enemy enemy)
        {
            int satisfiedCount = 0;

            foreach (PatternZone zone in enemy.ActiveZones)
            {
                if (zone.IsSatisfied)
                    satisfiedCount++;
            }

            return satisfiedCount >= enemy.RequiredZoneCount;
        }

        private void ExecuteLightZoneFail(Enemy enemy)
        {
            Console.WriteLine("[LightZoneFail] enemy=" + enemy.Id +
                " tick=" + ServerTick);

            foreach (Player player in _players.Values)
            {
                if (player.IsDead)
                    continue;

                if (IsUnderSpawnProtection(player))
                {
                    Console.WriteLine("[LightZoneBlocked] player=" + player.Id +
                        " reason=SpawnProtection");
                    continue;
                }

                int beforeHp = player.Hp;
                player.Hp -= LightZoneFailDamage;
                if (player.Hp < 0)
                    player.Hp = 0;

                player.MarkDirty();

                AddCombatEvent(
                    CombatEventType.CombatEventHit,
                    enemy.Id,
                    player.Id,
                    ActionType.ActionSkill2,
                    LightZoneFailDamage);

                Console.WriteLine("[LightZoneHit] player=" + player.Id +
                    " hp=" + beforeHp + "->" + player.Hp);

                if (player.IsDead)
                {
                    player.OnDead();

                    AddCombatEvent(
                        CombatEventType.CombatEventDeath,
                        enemy.Id,
                        player.Id,
                        ActionType.ActionSkill2,
                        0);
                }
            }
        }


        private void ExecuteLightZoneSuccess(Enemy enemy)
        {
            Console.WriteLine("[LightZoneSuccess] enemy=" + enemy.Id +
                " tick=" + ServerTick);

            // 나중에 vulnerable 상태나 보상 타임 넣고 싶으면 여기 확장
        }

        private void UpdateLightZonePattern(Enemy enemy)
        {
            UpdateLightZoneOccupancy(enemy);

            if (ServerTick >= enemy.PatternResolveTick)
            {
                bool success = IsLightZonePatternSuccess(enemy);

                if (success)
                    ExecuteLightZoneSuccess(enemy);
                else
                    ExecuteLightZoneFail(enemy);

                enemy.ActiveZones.Clear();
                enemy.State = Enemy.EnemyState.Idle;
                enemy.CurrentPatternType = Enemy.EnemyPatternType.None;
                enemy.NextPatternAvailableTick = ServerTick + LightZonePostCooldownTick;
                enemy.MarkDirty();

                Console.WriteLine("[LightZoneEnd] enemy=" + enemy.Id +
                    " tick=" + ServerTick +
                    " nextAvailable=" + enemy.NextPatternAvailableTick);
            }
        }
    }
}
