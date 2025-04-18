﻿using Google.Protobuf;
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

        room.Push(room.HandleMove, player, movePacket);

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

    public static void C_SceneMoveHandler(PacketSession session, IMessage packet)
    {
        C_SceneMove c_SceneMove = packet as C_SceneMove;

        ClientSession clientSession = session as ClientSession;       


        if (clientSession.MyPlayer == null)
        {
            Console.WriteLine($"C_SceneMOveHandler ] Player is null!!");
            return;

        }


        GameRoom room = clientSession.MyPlayer.Room;
        if (room == null)
            return;

        room.HandleMoveScene(clientSession.MyPlayer, c_SceneMove);


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

    public static void C_CreateRoomHandler(PacketSession session, IMessage packet)
    {
        C_CreateRoom c_CreateRoom = (C_CreateRoom)packet;
        ClientSession clientSession = (ClientSession)session;

        Console.WriteLine($"C_CreateRoomHandler!!! ");

        if (RoomManager.Instance.Find(RoomType.Bakal) == null)
        {
            clientSession.HandleCreateRoom(c_CreateRoom);

        }

        else
        {
            clientSession.HandleEnterParty(c_CreateRoom);
        }
    }

    public static void C_EnterPartyHandler(PacketSession session, IMessage packet)
    {
        C_EnterParty c_EnterParty = (C_EnterParty)packet;
        ClientSession clientSession = (ClientSession)session;
        clientSession.HandleEnterParty(c_EnterParty);
    }

    public static void C_EquipItemHandler(PacketSession session, IMessage packet)
    {
        C_EquipItem equipPacket = (C_EquipItem)packet;

        ClientSession clientSession = (ClientSession)session;

        Player p = clientSession.MyPlayer;


        if (p == null)
            return;

        GameRoom room = p.Room;

        if (room == null)
            return;


        room.Push(room.HandleEquipItem, p, equipPacket);
        
    }
}
