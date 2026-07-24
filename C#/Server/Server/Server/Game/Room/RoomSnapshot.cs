using System;
using System.Collections.Generic;

namespace Server.Game.Room
{
    public class RoomSnapshot
    {
        public int RoomId { get; set; }
        public string RoomType { get; set; }
        public string State { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime LastUpdatedAtUtc { get; set; }
        public double ElapsedSeconds { get; set; }
        public int PlayerCount { get; set; }
        public int EnemyCount { get; set; }
        public bool IsEmpty { get; set; }
        public bool IsDungeonRoom { get; set; }
        public List<RoomPlayerSnapshot> Players { get; set; } = new List<RoomPlayerSnapshot>();
        public List<RoomEnemySnapshot> Enemies { get; set; } = new List<RoomEnemySnapshot>();
        public long UpdateCount { get; set; }
        public double LastUpdateMs { get; set; }
        public double MaxUpdateMs { get; set; }
        public int? BossObjectId { get; set; }
        public string BossName { get; set; }
        public int? BossHp { get; set; }
        public int? BossMaxHp { get; set; }
        public bool BossIsDead { get; set; }
    }

    public class RoomPlayerSnapshot
    {
        public int ObjectId { get; set; }
        public int PlayerDbId { get; set; }
        public string Name { get; set; }
        public int Hp { get; set; }
        public int MaxHp { get; set; }
        public float PosX { get; set; }
        public float PosY { get; set; }
        public string State { get; set; }
    }

    public class RoomEnemySnapshot
    {
        public int ObjectId { get; set; }
        public int TemplateId { get; set; }
        public string Name { get; set; }
        public int Hp { get; set; }
        public int MaxHp { get; set; }
        public float PosX { get; set; }
        public float PosY { get; set; }
        public bool IsDead { get; set; }
    }
}
