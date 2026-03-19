using Google.Protobuf;
using Server.Protocol;
using Server.Session;
using System;
using System.Collections.Generic;

namespace Server.Packet
{
    public sealed class PacketManager
    {
        public static PacketManager Instance { get; } = new PacketManager();

        private readonly Dictionary<ushort, Action<ClientSession, ArraySegment<byte>>> _handlers =
            new Dictionary<ushort, Action<ClientSession, ArraySegment<byte>>>();

        private PacketManager() { }

        public void Init()
        {
            Register<C_Ping>((ushort)MsgId.CPing, PacketHandler.HandleC_Ping);
        }

        private void Register<T>(ushort packetId, Action<ClientSession, T> handler)
            where T : IMessage<T>, new()
        {
            _handlers[packetId] = (session, buffer) =>
            {
                int payloadOffset = buffer.Offset + 4;
                int payloadCount = buffer.Count - 4;

                T packet = new T();
                packet.MergeFrom(buffer.Array, payloadOffset, payloadCount);

                handler(session, packet);
            };
        }

        public void HandlePacket(ClientSession session, ushort packetId, ArraySegment<byte> buffer)
        {
            if (_handlers.TryGetValue(packetId, out var action))
            {
                action(session, buffer);
            }
            else
            {
                Console.WriteLine($"[Unknown Packet] id={packetId}");
            }
        }
    }
}