using Google.Protobuf;
using Server.Protocol;
using System;
using System.Collections.Generic;

namespace Server.Packet
{
    partial class PacketSerializer
    {
        static readonly Dictionary<Type, MsgId> _typeToId = new Dictionary<Type, MsgId>();

        static PacketSerializer()
        {
            RegisterGenerated(_typeToId);
        }

        static partial void RegisterGenerated(Dictionary<Type, MsgId> typeToId);

        public static ArraySegment<byte> Serialize(IMessage packet)
        {
            MsgId msgId;
            if (_typeToId.TryGetValue(packet.GetType(), out msgId) == false)
                throw new InvalidOperationException("Packet type mapping not found: " + packet.GetType().FullName);

            byte[] payload = packet.ToByteArray();

            ushort size = (ushort)(payload.Length + 4);
            ushort packetId = (ushort)msgId;

            byte[] buffer = new byte[size];
            Array.Copy(BitConverter.GetBytes(size), 0, buffer, 0, 2);
            Array.Copy(BitConverter.GetBytes(packetId), 0, buffer, 2, 2);
            Array.Copy(payload, 0, buffer, 4, payload.Length);

            return new ArraySegment<byte>(buffer);
        }
    }
}