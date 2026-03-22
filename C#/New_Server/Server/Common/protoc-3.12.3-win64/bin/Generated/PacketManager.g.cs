using Google.Protobuf;
using Server.Protocol;
using ServerCore;
using System;

namespace Server.Packet
{
    partial class PacketManager
    {
        partial void RegisterGenerated()
        {
            _onRecv.Add((ushort)MsgId.CPing, MakePacket<C_Ping>);
            _handler.Add((ushort)MsgId.CPing, PacketHandler.C_PingHandler);
            _onRecv.Add((ushort)MsgId.CMoveInput, MakePacket<C_MoveInput>);
            _handler.Add((ushort)MsgId.CMoveInput, PacketHandler.C_MoveInputHandler);
            _onRecv.Add((ushort)MsgId.CActionInput, MakePacket<C_ActionInput>);
            _handler.Add((ushort)MsgId.CActionInput, PacketHandler.C_ActionInputHandler);
        }
    }
}