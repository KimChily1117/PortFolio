using Server.Protocol;
using ServerCore;
using System;
using System.Net;
using System.Net.Sockets;

namespace Server.Session
{
    public sealed class ClientSession : PacketSession
    {
        public override void OnConnected(EndPoint endPoint)
        {
            Console.WriteLine($"[Connected] {endPoint}");
        }

        public override void OnDisconnected(EndPoint endPoint)
        {
            Console.WriteLine($"[Disconnected] {endPoint}");
        }

        public override void OnRecvPacket(ArraySegment<byte> buffer)
        {
            if (buffer.Count < 4)
                return;

            ushort packetId = BitConverter.ToUInt16(buffer.Array, buffer.Offset + 2);
            Packet.PacketManager.Instance.HandlePacket(this, packetId, buffer);
        }

        public override void OnSend(int numOfBytes)
        {
            Console.WriteLine($"[Send] bytes={numOfBytes}");
        }

        public void SendProto(Google.Protobuf.IMessage packet)
        {
            ArraySegment<byte> sendBuffer = Packet.PacketSerializer.Serialize(packet);
            Send(sendBuffer);
        }
    }
}