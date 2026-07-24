using Google.Protobuf.Protocol;
using System;

namespace Server.Game.Match
{
    public class MatchQueueKey : IEquatable<MatchQueueKey>
    {
        public MatchMode Mode { get; set; }
        public RoomType TargetRoomType { get; set; }
        public int LevelBucket { get; set; }
        public int MmrBucket { get; set; }

        public static MatchQueueKey Create(MatchMode mode, RoomType targetRoomType, int levelBucket, int mmrBucket)
        {
            return new MatchQueueKey()
            {
                Mode = mode,
                TargetRoomType = targetRoomType,
                LevelBucket = levelBucket,
                MmrBucket = mmrBucket
            };
        }

        public bool Equals(MatchQueueKey other)
        {
            if (ReferenceEquals(other, null))
                return false;

            return Mode == other.Mode &&
                   TargetRoomType == other.TargetRoomType &&
                   LevelBucket == other.LevelBucket &&
                   MmrBucket == other.MmrBucket;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as MatchQueueKey);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + Mode.GetHashCode();
                hash = hash * 31 + TargetRoomType.GetHashCode();
                hash = hash * 31 + LevelBucket.GetHashCode();
                hash = hash * 31 + MmrBucket.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return $"{Mode}:{TargetRoomType}:L{LevelBucket}:M{MmrBucket}";
        }
    }
}
