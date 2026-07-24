using System;

namespace Server.Monitoring
{
    public class ServerStatusSnapshot
    {
        public bool Online { get; set; }
        public DateTime StartedAtUtc { get; set; }
        public DateTime SnapshotUpdatedAtUtc { get; set; }
        public double UptimeSeconds { get; set; }
        public int TotalRooms { get; set; }
        public int TownRooms { get; set; }
        public int DungeonRooms { get; set; }
        public int TotalPlayers { get; set; }
        public int TotalEnemies { get; set; }
        public int OnlinePlayers { get; set; }
        public int MatchingWaitingPlayers { get; set; }
        public int MatchingQueueCount { get; set; }
        public double MaxRoomUpdateMs { get; set; }
        public double LastMaxRoomUpdateMs { get; set; }
        public string ServerName { get; set; }
        public string ApiVersion { get; set; }
    }
}

