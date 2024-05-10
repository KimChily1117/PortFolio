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
            Debug.Log($"{player}");

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

        GameObjectType type = ObjectManager.GetObjectTypeId(skillPacket.PlayerId);


        if (type == GameObjectType.Player)
        {
            OtherPlayer op = go.GetComponent<OtherPlayer>();
            if (op != null)
            {
                op.UseSkill(skillPacket.Info.SkillId);
            }
        }

        else if (type == GameObjectType.Enemy)
        {
            EnemyPlayer ep = go.GetComponent<EnemyPlayer>();
            if (ep != null)
            {
                ep.UseSkill(skillPacket.Info.SkillId);
            }


        }


    }

    public static void S_SceneMoveHandler(PacketSession session, IMessage packet)
    {
        S_SceneMove s_SCENEMOVE = packet as S_SceneMove;

        Debug.Log($"SceneMove Handler!!!");

        GameManager.SCENE.LoadSceneAsync(Define.Scenes.BAKAL, () =>
        {

            C_EnterGame c_Enter_Game = new C_EnterGame();
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

        Debug.Log($"{s_Collision.Playerinfo.Name}이  {s_Collision.Playerinfo.Damage}만큼의 피해를 받았습니다");


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

        if (s_CreatePlayer.Player == null)
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

    public static void S_CreateRoomHandler(PacketSession session, IMessage packet)
    {
        S_CreateRoom s_CreateRoom = (S_CreateRoom)packet;

        Debug.Log($"S_CreateRoomHandler : {s_CreateRoom.ResponseCode}");

        if (s_CreateRoom.ResponseCode == 1)
        {
            partyEntry = GameManager.UI.ShowPopupUI<UI_PartyEntry>("PartyPopUp");
            partyEntry.SetUIElement(true, $"{GameManager.ObjectManager.MyPlayer.name}");

        }
    }

    public static void S_EnterPartyHandler(PacketSession session, IMessage message)
    {
        S_EnterParty s_EnterParty = (S_EnterParty)message;

        if (partyEntry == null)
            partyEntry = GameManager.UI.ShowPopupUI<UI_PartyEntry>("PartyPopUp");

        partyEntry.SetUIElement(s_EnterParty.PartyMembers);
    }

    public static void S_DieHandler(PacketSession session, IMessage message)
    {
        S_Die s_Die = (S_Die)message;

        Debug.Log($"Who Dead ? {s_Die.Player.ObjectId},{s_Die.Player.Name}");

        GameObject go = GameManager.ObjectManager.FindById(s_Die.Player.ObjectId);

        BaseCharacter baseCharacter = go.GetComponent<BaseCharacter>();

        baseCharacter.OnDead();
    }

    public static void S_ItemListHandler(PacketSession session, IMessage packet)
    {
        S_ItemList itemList = (S_ItemList)packet;

        //UI_GameScene gameSceneUI = Managers.UI.SceneUI as UI_GameScene;
        //UI_Inventory invenUI = gameSceneUI.InvenUI;

        GameManager.Inven.Clear();

        // 메모리에 아이템 정보 적용
        foreach (ItemInfo itemInfo in itemList.Items)
        {
            Item item = Item.MakeItem(itemInfo);
            GameManager.Inven.Add(item);

            Debug.Log($"is Inven here??? ");
        }

        // UI 에서 표시
        //invenUI.gameObject.SetActive(true);
        //invenUI.RefreshUI();
    }

    public static void S_AddItemHandler(PacketSession session, IMessage packet)
    {
        S_AddItem itemList = (S_AddItem)packet;

        //UI_GameScene gameSceneUI = Managers.UI.SceneUI as UI_GameScene;
        //UI_Inventory invenUI = gameSceneUI.InvenUI;

        // 메모리에 아이템 정보 적용
        foreach (ItemInfo itemInfo in itemList.Items)
        {
            Item item = Item.MakeItem(itemInfo);
            GameManager.Inven.Add(item);
        }

        Debug.Log("아이템을 획득했습니다!");
    }

    public static void S_EquipItemHandler(PacketSession session, IMessage packet)
    {
        S_EquipItem equipItemOk = (S_EquipItem)packet;

        // 메모리에 아이템 정보 적용
        Item item = GameManager.Inven.Get(equipItemOk.ItemDbId);
        if (item == null)
            return;

        item.Equipped = equipItemOk.Equipped;
        Debug.Log("아이템 착용 변경!");

        if (GameManager.ObjectManager.MyPlayer != null)
        {
            ((MyPlayer)GameManager.ObjectManager.MyPlayer).InvenUI.RefreshUI();
            GameManager.ObjectManager.MyPlayer.RefreshAdditionalStat();

        }
    }

    public static void S_ChangeStatHandler(PacketSession session, IMessage packet)
    {

    }
}
