using Google.Protobuf.Protocol;
using Server.Game.Map;
using Server.Game.Object;
using System;

namespace Server.Game.Room
{
    public static class TownSpawnService
    {
        private const float LobbySpawnCenterX = 25.79f;
        private const float LobbySpawnCenterY = 0f;
        private const float LobbySpawnScatterX = 6f;
        private const float LobbySpawnScatterY = 1.5f;
        private const int WalkableLobbyAttempts = 12;
        private const float MyRoomSpawnX = 0f;
        private const float MyRoomSpawnY = 0f;
        private const float PublicLobbyMinX = 18f;

        private static readonly object Lock = new object();
        private static readonly Random Random = new Random();

        public static void ApplyMyRoomSpawn(Player player, GameRoom room, string reason)
        {
            if (player?.Info?.PosInfo == null)
                return;

            player.IsInPublicTownArea = false;
            player.Info.PosInfo.PosX = MyRoomSpawnX;
            player.Info.PosInfo.PosY = MyRoomSpawnY;
            player.Info.PosInfo.State = PlayerState.Idle;
            player.Info.PosInfo.MoveDir = MoveDir.Right;
            player.UpdateFacing(MoveDir.Right);

            Console.WriteLine($"[TOWN_SPAWN] MyRoom spawn assigned. Reason={reason}, RoomId={room?.RoomId ?? 0}, Player={player.Info.Name}, PlayerId={player.Id}, Pos=({MyRoomSpawnX:0.00},{MyRoomSpawnY:0.00}), Public=False");
        }

        public static void ApplyLobbySpawn(Player player, GameRoom room, string reason)
        {
            if (player?.Info?.PosInfo == null)
                return;

            float x;
            float y;
            string source;

            lock (Lock)
            {
                ResolveLobbySpawn(out x, out y, out source);
            }

            player.IsInPublicTownArea = true;
            player.Info.PosInfo.PosX = x;
            player.Info.PosInfo.PosY = y;
            player.Info.PosInfo.State = PlayerState.Idle;
            player.Info.PosInfo.MoveDir = MoveDir.Right;
            player.UpdateFacing(MoveDir.Right);

            Console.WriteLine($"[TOWN_SPAWN] Lobby spawn assigned. Reason={reason}, RoomId={room?.RoomId ?? 0}, Player={player.Info.Name}, PlayerId={player.Id}, Pos=({x:0.00},{y:0.00}), Source={source}, Public=True");
        }

        public static bool IsPublicLobbyPosition(float x, float y)
        {
            return x >= PublicLobbyMinX &&
                MovementBoundsProvider.TryGet(RoomType.Town, out MovementBoundsMap map) &&
                map.IsWalkable(x, y);
        }

        private static void ResolveLobbySpawn(out float x, out float y, out string source)
        {
            if (MovementBoundsProvider.TryGet(RoomType.Town, out MovementBoundsMap map))
            {
                for (int i = 0; i < WalkableLobbyAttempts; i++)
                {
                    float candidateX = RandomRange(LobbySpawnCenterX - LobbySpawnScatterX, LobbySpawnCenterX + LobbySpawnScatterX);
                    float candidateY = RandomRange(LobbySpawnCenterY - LobbySpawnScatterY, LobbySpawnCenterY + LobbySpawnScatterY);
                    if (map.IsWalkable(candidateX, candidateY))
                    {
                        x = candidateX;
                        y = candidateY;
                        source = "LobbyScatterWithinMovementBounds";
                        return;
                    }
                }

                if (map.TryGetRandomPoint(Random, out x, out y))
                {
                    source = "MovementBoundsRandomFallback";
                    return;
                }
            }

            x = RandomRange(LobbySpawnCenterX - LobbySpawnScatterX, LobbySpawnCenterX + LobbySpawnScatterX);
            y = RandomRange(LobbySpawnCenterY - LobbySpawnScatterY, LobbySpawnCenterY + LobbySpawnScatterY);
            source = "LobbyScatterFallback";
        }

        private static float RandomRange(float min, float max)
        {
            return min + (float)Random.NextDouble() * (max - min);
        }
    }
}
