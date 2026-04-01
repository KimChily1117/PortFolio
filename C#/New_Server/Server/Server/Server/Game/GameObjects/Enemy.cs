using System.Collections.Generic;
using Server.Protocol;

namespace Server.Game.GameObjects
{
    public class Enemy : Creature
    {
        public enum EnemyState
        {
            Idle = 0,
            Chase = 1,
            Pattern = 2,
            Dead = 3,
        }

        public enum EnemyPatternType
        {
            None = 0,
            Shockwave = 1,
            LightZone = 2,
        }

        public int NextAiTick { get; set; }
        public int TargetPlayerId { get; set; }
        public int NextActionTick { get; set; }
        public bool IsAttacking { get; set; }

        public EnemyState State { get; set; } = EnemyState.Idle;
        public EnemyPatternType CurrentPatternType { get; set; } = EnemyPatternType.None;
        public EnemyPatternType NextPatternType { get; set; } = EnemyPatternType.Shockwave;

        public int PatternStartTick { get; set; }
        public int PatternResolveTick { get; set; }
        public bool PatternTriggered { get; set; }

        // 핵심: 패턴 시작 가능 시점
        public int NextPatternAvailableTick { get; set; }

        public List<Room.PatternZone> ActiveZones { get; set; } = new List<Room.PatternZone>();
        public int RequiredZoneCount { get; set; }

        public Enemy()
        {
            ObjectType = GameObjectType.Enemy;
            Speed = 5;
            MaxHp = 30;
            Hp = 30;
            MainState = ActorMainState.Idle;

            State = EnemyState.Idle;
            CurrentPatternType = EnemyPatternType.None;
            NextPatternType = EnemyPatternType.Shockwave;
            NextPatternAvailableTick = 0;
        }
    }
}