using Server.Protocol;
using System;
using System.Collections.Generic;

namespace Server.Packet
{
    partial class PacketSerializer
    {
        static partial void RegisterGenerated(Dictionary<Type, MsgId> typeToId)
        {
            typeToId.Add(typeof(S_Pong), MsgId.SPong);
            typeToId.Add(typeof(S_RoomSnapshot), MsgId.SRoomSnapshot);
            typeToId.Add(typeof(S_CombatEvents), MsgId.SCombatEvents);
            typeToId.Add(typeof(S_PatternZones), MsgId.SPatternZones);
        }
    }
}