using Character;
using Google.Protobuf;
using Google.Protobuf.Protocol;
using ServerCore;
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

        foreach (PlayerInfo player in spawnPacket.Players)
        {
            GameManager.ObjectManager.Add(player, isMyPlayer : false);
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

        GameObject go = GameManager.ObjectManager.FindById(skillPacket.PlayerId);
        if (go == null)
            return;

        OtherPlayer op = go.GetComponent<OtherPlayer>();
        if (op != null)
        {
            op.UseSkill(skillPacket.Info.SkillId);
        }

    }

    public static void S_RunHandler(PacketSession session, IMessage packet)
    {
       

    }


}
