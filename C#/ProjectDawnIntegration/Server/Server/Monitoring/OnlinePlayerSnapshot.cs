using Google.Protobuf.Protocol;
using Server.Game.Object;
using Server.Game.Room;
using System;
using System.Collections.Generic;

namespace Server.Monitoring
{
    public class OnlinePlayersSnapshot
    {
        public DateTime SnapshotUpdatedAtUtc { get; set; }
        public int Count { get; set; }
        public List<OnlinePlayerSnapshot> Players { get; set; } = new List<OnlinePlayerSnapshot>();
    }

    public class OnlinePlayerSnapshot
    {
        public int SessionId { get; set; }
        public int ObjectId { get; set; }
        public int PlayerDbId { get; set; }
        public string Name { get; set; }
        public int? RoomId { get; set; }
        public string RoomType { get; set; }
        public bool IsTransferring { get; set; }
        public int? PendingRoomId { get; set; }
        public int? TransferId { get; set; }
        public int Hp { get; set; }
        public int MaxHp { get; set; }
        public float PosX { get; set; }
        public float PosY { get; set; }
        public string State { get; set; }

        public static OnlinePlayerSnapshot FromSession(ClientSession session)
        {
            Player player = session?.MyPlayer;
            PositionInfo posInfo = player?.Info?.PosInfo;
            GameRoom room = player?.Room;

            return new OnlinePlayerSnapshot
            {
                SessionId = session?.SessionId ?? 0,
                ObjectId = player?.Id ?? 0,
                PlayerDbId = player?.PlayerDbId ?? 0,
                Name = player?.Info?.Name ?? "Unknown",
                RoomId = room == null ? (int?)null : room.RoomId,
                RoomType = room == null ? null : room.RoomType.ToString(),
                IsTransferring = session?.IsTransferring ?? false,
                PendingRoomId = session != null && session.PendingRoomId > 0 ? (int?)session.PendingRoomId : null,
                TransferId = session != null && session.PendingTransferId > 0 ? (int?)session.PendingTransferId : null,
                Hp = ToSnapshotInt(player?.HP ?? 0f),
                MaxHp = ToSnapshotInt(player?.MaxHP ?? 0f),
                PosX = posInfo?.PosX ?? 0f,
                PosY = posInfo?.PosY ?? 0f,
                State = posInfo?.State.ToString() ?? player?.CurrentPlayerState.ToString() ?? "Connected"
            };
        }

        private static int ToSnapshotInt(float value)
        {
            if (value <= 0f)
                return 0;

            return (int)Math.Ceiling(value);
        }
    }
}
