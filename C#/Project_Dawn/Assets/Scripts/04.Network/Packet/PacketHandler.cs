using Character;
using Google.Protobuf;
using Google.Protobuf.Protocol;
using ServerCore;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

class PacketHandler
{
    public static void S_EnterGameHandler(PacketSession session, IMessage packet)
    {
        S_EnterGame enterGamePacket = packet as S_EnterGame;


        ServerSession serverSession = session as ServerSession;

        GameManager.ObjectManager.Add(enterGamePacket.Player, isMyPlayer: true);

        Debug.Log("S_EnterGameHandler");
        Debug.Log($"{enterGamePacket.Player}");

    }

    public static void S_LeaveGameHandler(PacketSession session, IMessage packet)
    {
        S_LeaveGame leaveGameHandler = packet as S_LeaveGame;

        GameManager.ObjectManager.RemoveMyPlayer();
    }

    public static void S_SpawnHandler(PacketSession session, IMessage packet)
    {
        S_Spawn spawnPacket = packet as S_Spawn;

        foreach (ObjectInfo player in spawnPacket.Objects)
        {
            GameManager.ObjectManager.Add(player, isMyPlayer: false);
        }
    }

    public static void S_DespawnHandler(PacketSession session, IMessage packet)
    {
        S_Despawn despawnPacket = packet as S_Despawn;
        foreach (int id in despawnPacket.PlayerIds)
        {
            GameManager.ObjectManager.Remove(id);
        }
    }

    public static void S_MoveHandler(PacketSession session, IMessage packet)
    {
        S_Move movePacket = packet as S_Move;
        ServerSession serverSession = session as ServerSession;


        Debug.Log("S_MoveHandler");

        GameObject go = GameManager.ObjectManager.FindById(movePacket.PlayerId);
        if (go == null)
            return;

        OtherPlayer op = go.GetComponent<OtherPlayer>();
        if (op == null)
            return;

        op.PosInfo = movePacket.PosInfo;
    }


    public static void S_JumpHandler(PacketSession session, IMessage packet)
    {
        S_Jump jumpPacket = packet as S_Jump;
        ServerSession serverSession = session as ServerSession;

        Debug.Log("S_MoveHandler");

        GameObject go = GameManager.ObjectManager.FindById(jumpPacket.PlayerId);

        OtherPlayer op = go.GetComponent<OtherPlayer>();

        if (go == null)
            return;

        if (op == null)
            return;

        op.PosInfo = jumpPacket.PosInfo;

    }

    public static void S_SkillHandler(PacketSession session, IMessage packet)
    {



        S_Skill skillPacket = packet as S_Skill;


        Debug.Log($"recive? {skillPacket.Info.SkillId}");

        GameObject go = GameManager.ObjectManager.FindById(skillPacket.PlayerId);
        if (go == null)
            return;

        OtherPlayer op = go.GetComponent<OtherPlayer>();
        if (op != null)
        {
            op.UseSkill(skillPacket.Info.SkillId);
        }

    }

    public static void S_SceneMoveHandler(PacketSession session, IMessage packet)
    {
        S_SceneMove s_SCENEMOVE = packet as S_SceneMove;
    }

    public static void S_CollisionHandler(PacketSession session, IMessage packet)
    {
        S_Collision s_Collision = packet as S_Collision;

        GameObject go = GameManager.ObjectManager.FindById(s_Collision.Playerinfo.ObjectId);

        if (go == null)
            return;

        Debug.Log($"{s_Collision.PlayerId}이가 {s_Collision.Playerinfo.ObjectId}에게 {s_Collision.Playerinfo.Damage}만큼의 피해를 주었습니다");


        BaseCharacter bc = go.GetComponent<BaseCharacter>();

        if (bc != null)
        {
            bc.TakeDamage(s_Collision.Playerinfo.Damage);
        }
    }



    public static void S_ConnectedHandler(PacketSession session, IMessage packet)
    {
        C_Login c_Login = new C_Login();

        c_Login.UniqueId = SystemInfo.deviceUniqueIdentifier;
        GameManager.Network.Send(c_Login);
    }


    public static void S_LoginHandler(PacketSession session, IMessage packet)
    {
        S_Login s_Login = packet as S_Login;

        Debug.Log($"Login Response{s_Login.LoginOK}");

        if (s_Login.Players == null || s_Login.Players.Count == 0)
        {
            C_CreatePlayer createPlayer = new C_CreatePlayer();
            createPlayer.Name = $"Player_{UnityEngine.Random.Range(1, 100).ToString("0000")}";
            GameManager.Network.Send(createPlayer);
        }

        else
        {
            LobbyPlayerInfo info = s_Login.Players[0];
            C_EnterGame c_EnterGame = new C_EnterGame();
            c_EnterGame.Name = info.Name;

            GameManager.Network.Send(c_EnterGame);

        }


    }


    public static void S_CreatePlayerHandler(PacketSession session, IMessage packet)
    {
        S_CreatePlayer s_CreatePlayer = (S_CreatePlayer)packet;

        if(s_CreatePlayer.Player == null)
        {
            C_CreatePlayer createPlayer = new C_CreatePlayer();
            createPlayer.Name = $"Player_{UnityEngine.Random.Range(1, 100).ToString("0000")}";
            GameManager.Network.Send(createPlayer);
        }
        else
        {            
            C_EnterGame c_EnterGame = new C_EnterGame();
            c_EnterGame.Name = s_CreatePlayer.Player.Name;

            GameManager.Network.Send(c_EnterGame);
        }

    }
}
