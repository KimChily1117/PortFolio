using Google.Protobuf;
using Server.Game.GameObjects;
using Server.Game.Room;
using Server.Protocol;
using Server.Session;
using ServerCore;
using System;



namespace Server.Packet
{
    public static class PacketHandler
    {
        static int _playerId = 1;
        static GameRoom _testRoom;

        public static void Init()
        {
            _testRoom = RoomManager.Instance.CreateRoom();
        }

        public static void C_PingHandler(PacketSession session, IMessage packet)
        {
            ClientSession clientSession = session as ClientSession;
            C_Ping pingPacket = packet as C_Ping;

            if (clientSession == null || pingPacket == null)
                return;

            Console.WriteLine("[Recv Ping] seq=" + pingPacket.Sequence + ", msg=" + pingPacket.Message);

            if (clientSession.MyPlayer == null)
            {
                Player player = new Player();
                player.Id = _playerId++;
                player.Session = clientSession;
                clientSession.MyPlayer = player;

                _testRoom.Enqueue(new EnterRoomCommand(player));
            }

            S_Pong pong = new S_Pong();
            pong.Sequence = pingPacket.Sequence;
            pong.Message = "Pong: " + pingPacket.Message;

            clientSession.SendProto(pong);
        }

        public static void C_MoveInputHandler(PacketSession session, IMessage packet)
        {
            ClientSession clientSession = session as ClientSession;
            C_MoveInput movePacket = packet as C_MoveInput;

            if (clientSession == null || movePacket == null)
                return;

            if (clientSession.MyPlayer == null)
                return;

            if (clientSession.MyPlayer.Room == null)
                return;

            MoveInputCommand cmd = new MoveInputCommand(
                clientSession.MyPlayer,
                movePacket.InputSeq,
                movePacket.ClientTick,
                movePacket.MoveX,
                movePacket.MoveY);


            Console.WriteLine("[MoveInput] player=" + cmd.Player.Id +
                  " seq=" + cmd.InputSeq +
                  " move=(" + cmd.MoveX + "," + cmd.MoveY + ")");
            clientSession.MyPlayer.Room.Enqueue(cmd);
        }

        internal static void C_ActionInputHandler(PacketSession session, IMessage message)
        {
            ClientSession clientSession = session as ClientSession;
            C_ActionInput actionInput = message as C_ActionInput;

            if (clientSession == null || actionInput == null)
                return;

            Player myPlayer = clientSession.MyPlayer;
            if (myPlayer == null || myPlayer.Room == null)
                return;

            ActionInputCommand command = new ActionInputCommand
            {
                Player = myPlayer,
                InputSeq = actionInput.InputSeq,
                ActionType = actionInput.ActionType,
                DirX = actionInput.DirX,
                DirY = actionInput.DirY
            };

            myPlayer.Room.Enqueue(command);
        }
    }
}