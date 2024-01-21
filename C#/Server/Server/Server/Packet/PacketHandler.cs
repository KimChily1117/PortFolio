using Google.Protobuf;
using Google.Protobuf.Protocol;
using Server;
using Server.DB;
using Server.Game.Object;
using Server.Game.Room;
using ServerCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

class PacketHandler
{
    public static void C_MoveHandler(PacketSession session, IMessage packet)
    {
        C_Move movePacket = packet as C_Move;
        ClientSession clientSession = session as ClientSession;

        //Console.WriteLine($"C_Move ({movePacket.PosInfo.MoveDir})");


        Player player = clientSession.MyPlayer;

        if (player == null)
            return;

        GameRoom room = player.Room;
        if (room == null)
            return;

        room.HandleMove(player, movePacket);

    }

    public static void C_SkillHandler(PacketSession session, IMessage packet)
    {
        C_Skill skillPacket = packet as C_Skill;
        ClientSession clientSession = session as ClientSession;

        Player player = clientSession.MyPlayer;
        if (player == null)
            return;

        GameRoom room = player.Room;
        if (room == null)
            return;

        Console.WriteLine($"skillPacket : {skillPacket.Info.SkillId}");
        //room.HandleSkill(player, skillPacket);
        room.Push(room.HandleSkill, player, skillPacket);

    }

    public static void C_JumpHandler(PacketSession session, IMessage packet)
    {
        C_Jump jumpPacket = packet as C_Jump;
        ClientSession clientSession = session as ClientSession;

        Player player = clientSession.MyPlayer;

        if (player == null)
            return;

        GameRoom room = player.Room;
        if (room == null)
            return;

        room.HandleJump(player, jumpPacket);
    }

    public static void C_ScenemoveHandler(PacketSession session, IMessage packet)
    {
        C_SceneMove c_SceneMove = packet as C_SceneMove;

        ClientSession clientSession = session as ClientSession;


        // Todo : SceneMove 발생할때. Client -> Server로 Scene 전환 패킷 보내 놓고 서버에서 
        // Scene에대한 요청?(던전입장을 어떻게 할것인지에 대한 설계가 필요함) 
    }

    public static void C_CollisionHandler(PacketSession session, IMessage packet)
    {
        C_Collision c_Collision = packet as C_Collision;
        ClientSession clientSession = session as ClientSession;

        Console.WriteLine($"Collision ??? : {c_Collision.Playerinfo.ObjectId}");
        // c_collision : 피폭자의 정보
        // 시전자는 다른 사람이다.

        Player player = clientSession.MyPlayer;

        if (player == null) return;

        GameRoom room = player.Room;

        room.Push(room.HandleCollision, player, c_Collision);
        //room.HandleCollision(player, c_Collision);

    }

    public static void C_LoginHandler(PacketSession session, IMessage packet)
    {
        C_Login c_Login = packet as C_Login;
        ClientSession clientSession = session as ClientSession;

        Console.WriteLine($"Recived Request Login packet!! : UniqueId : {c_Login.UniqueId}");
        clientSession.HandleLogin(c_Login);
    }

    public static void C_CreatePlayerHandler(PacketSession session, IMessage packet)
    {
        C_CreatePlayer c_CreatePlayer = (C_CreatePlayer)packet;
        ClientSession clientSession = (ClientSession)session;


        clientSession.HandleCreateCharecter(c_CreatePlayer);
    }

    public static void C_EnterGameHandler(PacketSession session, IMessage packet)
    {
        C_EnterGame c_EnterGame = (C_EnterGame)packet;
        ClientSession clientSession = (ClientSession)session;
 
        clientSession.HandleEnterGame(c_EnterGame);
    
    }
}
