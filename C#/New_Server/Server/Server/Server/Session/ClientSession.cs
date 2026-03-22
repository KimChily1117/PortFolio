using Google.Protobuf;
using Server.Game.GameObjects;
using Server.Packet;
using ServerCore;
using System;
using System.Net;

namespace Server.Session
{
    public sealed class ClientSession : PacketSession
    {
        public Player MyPlayer { get; set; }

        public override void OnConnected(EndPoint endPoint)
        {
            Console.WriteLine("[Connected] " + endPoint.ToString());
        }

        public override void OnDisconnected(EndPoint endPoint)
        {
            Console.WriteLine("[Disconnected] " + endPoint.ToString());

            if (MyPlayer != null && MyPlayer.Room != null)
                MyPlayer.Room.Enqueue(new Game.Room.LeaveRoomCommand(MyPlayer.Id));
        }

        public override void OnRecvPacket(ArraySegment<byte> buffer)
        {
            if (buffer.Count < 4)
                return;

            PacketManager.Instance.OnRecvPacket(this, buffer);
        }

        public override void OnSend(int numOfBytes)
        {
            Console.WriteLine("[Send] bytes=" + numOfBytes);
        }

        public void SendProto(IMessage packet)
        {
            ArraySegment<byte> sendBuffer = PacketSerializer.Serialize(packet);
            Send(sendBuffer);
        }
    }
}