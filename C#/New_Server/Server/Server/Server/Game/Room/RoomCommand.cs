using Server.Game.GameObjects;
using Server.Protocol;

namespace Server.Game.Room
{
    public abstract class RoomCommand
    {
    }

    public sealed class EnterRoomCommand : RoomCommand
    {
        public Player Player { get; private set; }

        public EnterRoomCommand(Player player)
        {
            Player = player;
        }
    }

    public sealed class LeaveRoomCommand : RoomCommand
    {
        public int PlayerId { get; private set; }

        public LeaveRoomCommand(int playerId)
        {
            PlayerId = playerId;
        }
    }

    public sealed class MoveInputCommand : RoomCommand
    {
        public Player Player { get; private set; }
        public uint InputSeq { get; private set; }
        public int ClientTick { get; private set; }
        public int MoveX { get; private set; }
        public int MoveY { get; private set; }

        public MoveInputCommand(Player player, uint inputSeq, int clientTick, int moveX, int moveY)
        {
            Player = player;
            InputSeq = inputSeq;
            ClientTick = clientTick;
            MoveX = moveX;
            MoveY = moveY;
        }
    }

    public class ActionInputCommand : RoomCommand
    {
        public Player Player { get; set; }
        public uint InputSeq { get; set; }
        public ActionType ActionType { get; set; }
        public int DirX { get; set; }
        public int DirY { get; set; }
    }

}