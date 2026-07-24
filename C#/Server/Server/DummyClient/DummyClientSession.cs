using Google.Protobuf;
using Google.Protobuf.Protocol;
using ServerCore;
using System;
using System.Net;

namespace DummyClient
{
    public sealed class DummyClientSession : PacketSession
    {
        public DummyClient Client { get; }

        public DummyClientSession(DummyClient client)
        {
            Client = client;
        }

        public override void OnConnected(EndPoint endPoint)
        {
            Client.Log($"Connected to {endPoint}");
        }

        public override void OnRecvPacket(ArraySegment<byte> buffer)
        {
            DummyPacketManager.Instance.OnRecvPacket(this, buffer);
        }

        public override void OnSend(int numOfBytes)
        {
        }

        public override void OnDisconnected(EndPoint endPoint)
        {
            Client.Log($"Disconnected. EndPoint={endPoint}");
            if (Client.State != DummyClientState.Completed && Client.State != DummyClientState.Failed)
                Client.Fail("Disconnected before completion");
        }

        public void Send(IMessage packet)
        {
            string msgName = packet.Descriptor.Name.Replace("_", string.Empty);
            MsgId msgId = (MsgId)Enum.Parse(typeof(MsgId), msgName);
            ushort payloadSize = (ushort)packet.CalculateSize();
            byte[] sendBuffer = new byte[payloadSize + 4];
            Array.Copy(BitConverter.GetBytes((ushort)(payloadSize + 4)), 0, sendBuffer, 0, sizeof(ushort));
            Array.Copy(BitConverter.GetBytes((ushort)msgId), 0, sendBuffer, 2, sizeof(ushort));
            Array.Copy(packet.ToByteArray(), 0, sendBuffer, 4, payloadSize);
            Send(new ArraySegment<byte>(sendBuffer));
        }
    }
}
