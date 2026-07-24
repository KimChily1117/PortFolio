using Google.Protobuf.Protocol;
using System;

namespace Server.Game.Match
{
    public class MatchTicket
    {
        public string TicketId { get; set; }
        public int SessionId { get; set; }
        public int PlayerId { get; set; }
        public string PlayerName { get; set; }
        public MatchMode Mode { get; set; }
        public RoomType TargetRoomType { get; set; }
        public int Level { get; set; }
        public int LevelBucket { get; set; }
        public int Mmr { get; set; }
        public int MmrBucket { get; set; }
        public bool HasEquippedWeapon { get; set; }
        public bool HasEquippedArmor { get; set; }
        public MatchQueueKey QueueKey { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public MatchTicketState State { get; set; }
    }
}
