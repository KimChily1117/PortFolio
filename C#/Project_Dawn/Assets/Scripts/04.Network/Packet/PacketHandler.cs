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


    static UI_PartyEntry partyEntry;


    public static void S_EnterGameHandler(PacketSession session, IMessage packet)
    {
        S_Enter_Game enterGamePacket = packet as S_Enter_Game;


        ServerSession serverSession = session as ServerSession;

        GameManager.ObjectManager.Add(enterGamePacket.Player, isMyPlayer: true);

        Debug.Log("S_EnterGameHandler");
        Debug.Log($"{enterGamePacket.Player}");

    }

    public static void S_LeaveGameHandler(PacketSession session, IMessage packet)
    {
        S_Leave_Game leaveGameHandler = packet as S_Leave_Game;

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

        op.PositionInfo = movePacket.PosInfo;
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

        op.PositionInfo = jumpPacket.PosInfo;


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
        S_Scene_Move s_SCENEMOVE = packet as S_Scene_Move;

        Debug.Log($"SceneMove Handler!!!");

        GameManager.SCENE.LoadSceneAsync(Define.Scenes.BAKAL, () => {

            C_Enter_Game c_Enter_Game = new C_Enter_Game();
            c_Enter_Game.Name = GameManager.MyName;
            GameManager.Network.Send(c_Enter_Game);
        });
    }

    public static void S_CollisionHandler(PacketSession session, IMessage packet)
    {
        S_Collision s_Collision = packet as S_Collision;

        GameObject go = GameManager.ObjectManager.FindById(s_Collision.Playerinfo.ObjectId);

        if (go == null)
            return;

        Debug.Log($"{s_Collision.Playerinfo.ObjectId}이가 {s_Collision.Playerinfo.ObjectId}에게 {s_Collision.Playerinfo.Damage}만큼의 피해를 주었습니다");


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
            C_Create_Player createPlayer = new C_Create_Player();
            createPlayer.Name = $"Player_{UnityEngine.Random.Range(1, 100).ToString("0000")}";
            GameManager.Network.Send(createPlayer);
        }

        else
        {
            LobbyPlayerInfo info = s_Login.Players[0];
            C_Enter_Game c_EnterGame = new C_Enter_Game();
            c_EnterGame.Name = info.Name;

            GameManager.Network.Send(c_EnterGame);

        }
    }
    

    public static void S_CreatePlayerHandler(PacketSession session, IMessage packet)
    {
        S_Create_Player s_CreatePlayer = (S_Create_Player)packet;

        if (s_CreatePlayer.Player == null)
        {
            C_Create_Player createPlayer = new C_Create_Player();
            createPlayer.Name = $"Player_{UnityEngine.Random.Range(1, 100).ToString("0000")}";
            GameManager.Network.Send(createPlayer);
        }
        else
        {
            C_Enter_Game c_EnterGame = new C_Enter_Game();
            c_EnterGame.Name = s_CreatePlayer.Player.Name;

            GameManager.Network.Send(c_EnterGame);
        }

    }

    public static void S_CreateRoomHandler(PacketSession session, IMessage packet)
    {
        S_Create_Room s_CreateRoom = (S_Create_Room)packet;

        Debug.Log($"S_CreateRoomHandler : {s_CreateRoom.ResponseCode}");

        if (s_CreateRoom.ResponseCode == 1)
        {
            partyEntry = GameManager.UI.ShowPopupUI<UI_PartyEntry>("PartyPopUp");
            partyEntry.SetUIElement(true, $"{GameManager.ObjectManager.MyPlayer.name}");

        }
    }

    public static void S_EnterPartyHandler(PacketSession session, IMessage message)
    {
        S_Enter_Party s_EnterParty = (S_Enter_Party)message;

        if (partyEntry == null)
            partyEntry = GameManager.UI.ShowPopupUI<UI_PartyEntry>("PartyPopUp");

        partyEntry.SetUIElement(s_EnterParty.PartyMembers);
    }
}
