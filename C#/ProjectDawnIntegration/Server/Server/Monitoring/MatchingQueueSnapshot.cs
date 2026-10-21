using System;
using System.Collections.Generic;

namespace Server.Monitoring
{
    public class MatchingQueuesSnapshot
    {
        public DateTime SnapshotUpdatedAtUtc { get; set; }
        public int TotalWaitingPlayers { get; set; }
        public int QueueCount { get; set; }
        public List<MatchingQueueSnapshot> Queues { get; set; } = new List<MatchingQueueSnapshot>();
    }

    public class MatchingQueueSnapshot
    {
        public string QueueKey { get; set; }
        public string DungeonType { get; set; }
        public string RoomType { get; set; }
        public int WaitingCount { get; set; }
        public int PartySize { get; set; }
        public List<MatchingQueuePlayerSnapshot> Players { get; set; } = new List<MatchingQueuePlayerSnapshot>();
    }

    public class MatchingQueuePlayerSnapshot
    {
        public int SessionId { get; set; }
        public int PlayerDbId { get; set; }
        public string PlayerName { get; set; }
        public int Level { get; set; }
        public int Mmr { get; set; }
        public bool HasWeapon { get; set; }
        public bool HasArmor { get; set; }
        public double WaitingSeconds { get; set; }
    }
}
