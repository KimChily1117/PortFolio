namespace PacketGenerator
{
    class PacketFormat
    {
        // {0} = register body
        public static string managerFormat =
@"using Google.Protobuf;
using Server.Protocol;
using ServerCore;
using System;

namespace Server.Packet
{{
    partial class PacketManager
    {{
        partial void RegisterGenerated()
        {{
{0}        }}
    }}
}}";

        // {0} = enum style, ex: CPing
        // {1} = class style, ex: C_Ping
        public static string managerRegisterFormat =
@"            _onRecv.Add((ushort)MsgId.{0}, MakePacket<{1}>);
            _handler.Add((ushort)MsgId.{0}, PacketHandler.{1}Handler);
";

        // {0} = serializer register body
        public static string serializerFormat =
@"using Server.Protocol;
using System;
using System.Collections.Generic;

namespace Server.Packet
{{
    partial class PacketSerializer
    {{
        static partial void RegisterGenerated(Dictionary<Type, MsgId> typeToId)
        {{
{0}        }}
    }}
}}";

        // {0} = class style, ex: S_Pong
        // {1} = enum style, ex: SPong
        public static string serializerRegisterFormat =
@"            typeToId.Add(typeof({0}), MsgId.{1});
";
    }
}