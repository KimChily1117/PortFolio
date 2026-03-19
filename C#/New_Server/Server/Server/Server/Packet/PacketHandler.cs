using Server.Protocol;
using Server.Session;
using System;

namespace Server.Packet
{
    public static class PacketHandler
    {
        public static void HandleC_Ping(ClientSession session, C_Ping packet)
        {
            Console.WriteLine($"[Recv Ping] seq={packet.Sequence}, msg={packet.Message}");

            S_Pong pong = new S_Pong
            {
                Sequence = packet.Sequence,
                Message = $"Pong: {packet.Message}"
            };

            session.SendProto(pong);
        }
    }
}