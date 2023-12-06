using Google.Protobuf;
using Google.Protobuf.Protocol;
using Server;
using Server.Game.Object;
using Server.Game.Room;
using ServerCore;
using System;
using System.Collections.Generic;
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

        Console.WriteLine($"skill ??? : {skillPacket.Info.SkillId}");
        room.HandleSkill(player, skillPacket);

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

    public static void C_ScenemoveHandler(PacketSession session , IMessage packet)
    {

    }

    public static void C_CollisionHandler(PacketSession session, IMessage packet)
    {
        C_Collision c_Collision = packet as C_Collision;
        ClientSession clientSession = session as ClientSession;

        Console.WriteLine($"Collision ??? : {c_Collision.Playerinfo.ObjectId}");



        Player player = clientSession.MyPlayer;

        if (player == null) return;
        GameRoom room = player.Room;

        room.HandleCollision(player, c_Collision);

    }



}
