using Google.Protobuf;
using Google.Protobuf.Protocol;
using Server;
using ServerCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

class PacketHandler
{
    public static void C_TestMsgHandler(PacketSession session, IMessage message)
    {
        C_TESTMsg movePacket = message as C_TESTMsg;
        Console.WriteLine($"recv Test : {movePacket.Message}");

        S_TESTMsg recv = new S_TESTMsg();
        recv.Message = $"C# to C++ : Hello Client!!!";
        
        ClientSession clientSession = (ClientSession)session;
        clientSession.Send(recv);
    }
}
