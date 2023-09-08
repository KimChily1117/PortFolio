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
        // Upcasting

        //Managers.Object.Add(enterGamePacket.Player, myPlayer: true);

        GameManager.ObjectManager.Add(enterGamePacket.Player, isMyPlayer: true);



        Debug.Log("S_EnterGameHandler");

    }

    public static void S_LeaveGameHandler(PacketSession session, IMessage packet)
    {
        S_LeaveGame leaveGameHandler = packet as S_LeaveGame;

        GameManager.ObjectManager.RemoveMyPlayer();

        //Managers.Object.RemoveMyPlayer();
    }

    public static void S_SpawnHandler(PacketSession session, IMessage packet)
    {
        S_Spawn spawnPacket = packet as S_Spawn;
        foreach (PlayerInfo player in spawnPacket.Players)
        {
            GameManager.ObjectManager.Add(player, isMyPlayer : false);

            //Object.Add(player, myPlayer: false);
        }
    }

    public static void S_DespawnHandler(PacketSession session, IMessage packet)
    {
        S_Despawn despawnPacket = packet as S_Despawn;
        foreach (int id in despawnPacket.PlayerIds)
        {
            GameManager.ObjectManager.Remove(id);
            //Managers.Object.Remove(id);
        }
    }

    public static void S_MoveHandler(PacketSession session, IMessage packet)
    {
        S_Move movePacket = packet as S_Move;
        ServerSession serverSession = session as ServerSession;

        Debug.Log("S_MoveHandler");
    }
}
