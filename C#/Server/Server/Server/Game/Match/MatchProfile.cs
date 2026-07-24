using Google.Protobuf.Protocol;

namespace Server.Game.Match
{
    public class MatchProfile
    {
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
    }
}
