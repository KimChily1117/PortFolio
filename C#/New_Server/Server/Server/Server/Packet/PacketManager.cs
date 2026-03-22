using Google.Protobuf;
using ServerCore;
using System;
using System.Collections.Generic;

namespace Server.Packet
{
    partial class PacketManager
    {
        #region Singleton
        static PacketManager _instance = new PacketManager();
        public static PacketManager Instance { get { return _instance; } }
        #endregion

        Dictionary<ushort, Action<PacketSession, ArraySegment<byte>, ushort>> _onRecv
            = new Dictionary<ushort, Action<PacketSession, ArraySegment<byte>, ushort>>();

        Dictionary<ushort, Action<PacketSession, IMessage>> _handler
            = new Dictionary<ushort, Action<PacketSession, IMessage>>();

        public Action<PacketSession, IMessage, ushort> CustomHandler { get; set; }

        PacketManager()
        {
            RegisterGenerated();
        }


        // 얘를 Partial 개념을 사용해가지고 쭉 연결시키는 개념은 괜찮은거 같음
        partial void RegisterGenerated();

        public void OnRecvPacket(PacketSession session, ArraySegment<byte> buffer)
        {
            ushort count = 0;

            ushort size = BitConverter.ToUInt16(buffer.Array, buffer.Offset);
            count += 2;
            ushort id = BitConverter.ToUInt16(buffer.Array, buffer.Offset + count);
            count += 2;

            Action<PacketSession, ArraySegment<byte>, ushort> action = null;
            if (_onRecv.TryGetValue(id, out action))
                action.Invoke(session, buffer, id);
            else
                Console.WriteLine("[PacketManager] Unknown Packet Id = " + id);
        }

        void MakePacket<T>(PacketSession session, ArraySegment<byte> buffer, ushort id)
            where T : IMessage, new()
        {
            T pkt = new T();
            pkt.MergeFrom(buffer.Array, buffer.Offset + 4, buffer.Count - 4);

            if (CustomHandler != null)
            {
                CustomHandler.Invoke(session, pkt, id);
            }
            else
            {
                Action<PacketSession, IMessage> action = null;
                if (_handler.TryGetValue(id, out action))
                    action.Invoke(session, pkt);
            }
        }
    }
}