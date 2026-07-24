using DG.Tweening;
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
    // S_MOVE diagnostics: limited logs for dummy follow-enemy movement checks.
    static readonly Dictionary<int, int> s_moveLogCounts = new Dictionary<int, int>();
    static readonly HashSet<int> s_missingMoveTargetsLogged = new HashSet<int>();
    static bool TryConvertSceneType(SceneType sceneType, out Define.Scenes scene)
    {
        switch (sceneType)
        {
            case SceneType.SceneLogin:
                scene = Define.Scenes.LOGIN;
                return true;
            case SceneType.SceneLobby:
                scene = Define.Scenes.LOBBY;
                return true;
            case SceneType.SceneTown:
                scene = Define.Scenes.TOWN;
                return true;
            case SceneType.SceneDungeonselect:
                scene = Define.Scenes.DUNGEONSELECT;
                return true;
            case SceneType.SceneBakal:
                scene = Define.Scenes.BAKAL;
                return true;
            default:
                scene = Define.Scenes.NONE;
                return false;
        }
    }


    public static void S_EnterGameHandler(PacketSession session, IMessage packet)
    {
        S_EnterGame enterGamePacket = packet as S_EnterGame;


        ServerSession serverSession = session as ServerSession;

        Debug.Log($"[SPAWN_SYNC][CLIENT_ENTER_RECV] Scene={GameManager.SCENE.CurrentScene}, Object={FormatSpawnSyncObject(enterGamePacket.Player)}");
        GameManager.ObjectManager.Add(enterGamePacket.Player, isMyPlayer: true);
        GameObject myPlayerObject = GameManager.ObjectManager.FindById(enterGamePacket.Player.ObjectId);
        Debug.Log($"[SPAWN_SYNC][CLIENT_ENTER_APPLIED] ObjectId={enterGamePacket.Player.ObjectId}, Found={myPlayerObject != null}, Active={myPlayerObject != null && myPlayerObject.activeSelf}, Transform={FormatSpawnSyncTransform(myPlayerObject)}");
        if (SceneLoadingOverlay.IsVisible)
        {
            SceneLoadingOverlay.SetDetail("입장 완료");
            SceneLoadingOverlay.Hide();
            GameManager.Input.SetInputLocked(false);
            Debug.Log("[SCENE][LOADING] Hidden after S_EnterGame.");
        }
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
        if (spawnPacket == null)
            return;

        Debug.Log($"[SPAWN_SYNC][CLIENT_SPAWN_RECV] Scene={GameManager.SCENE.CurrentScene}, Count={spawnPacket.Objects.Count}");
        foreach (ObjectInfo player in spawnPacket.Objects)
        {
            Debug.Log($"[SPAWN_SYNC][CLIENT_SPAWN_ITEM] Scene={GameManager.SCENE.CurrentScene}, Object={FormatSpawnSyncObject(player)}");
            GameManager.ObjectManager.Add(player, isMyPlayer: false);

            GameObject spawnedObject = GameManager.ObjectManager.FindById(player.ObjectId);
            Debug.Log($"[SPAWN_SYNC][CLIENT_SPAWN_APPLIED] ObjectId={player.ObjectId}, Name={player.Name}, Found={spawnedObject != null}, Active={spawnedObject != null && spawnedObject.activeSelf}, Transform={FormatSpawnSyncTransform(spawnedObject)}");
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

    public static void S_ChannelMoveHandler(PacketSession session, IMessage packet)
    {
        S_ChannelMove channelMove = packet as S_ChannelMove;
        if (channelMove == null)
            return;

        Debug.Log($"[CHANNEL_UI] S_ChannelMove. Success={channelMove.Success}, CurrentRoomId={channelMove.CurrentRoomId}, TargetRoomId={channelMove.TargetRoomId}, Reason={channelMove.Reason}");
        ChannelSelectionOverlay.HandleChannelMoveResult(channelMove);
    }
    public static void S_MoveHandler(PacketSession session, IMessage packet)
    {
        S_Move movePacket = packet as S_Move;
        ServerSession serverSession = session as ServerSession;

        if (movePacket == null || movePacket.PosInfo == null)
            return;

        GameObject go = GameManager.ObjectManager.FindById(movePacket.PlayerId);
        bool shouldLog = ShouldLogMoveDiagnostics(movePacket.PlayerId, go);
        if (shouldLog)
        {
            Debug.Log($"[CLIENT][S_MOVE] ObjectId={movePacket.PlayerId}, PacketPos=({movePacket.PosInfo.PosX:0.00},{movePacket.PosInfo.PosY:0.00}), State={movePacket.PosInfo.State}, Dir={movePacket.PosInfo.MoveDir}");
            Debug.Log($"[CLIENT][S_MOVE] Found={go != null}, Type={(go != null ? go.GetType().Name : "null")}, Name={(go != null ? go.name : "null")}");
        }

        if (go == null)
        {
            if (s_missingMoveTargetsLogged.Add(movePacket.PlayerId))
                Debug.LogWarning($"[SPAWN_SYNC][CLIENT_MOVE_TARGET_MISSING] Scene={GameManager.SCENE.CurrentScene}, ObjectId={movePacket.PlayerId}, Pos=({movePacket.PosInfo.PosX:0.00},{movePacket.PosInfo.PosY:0.00})");
            return;
        }

        s_missingMoveTargetsLogged.Remove(movePacket.PlayerId);
        OtherPlayer op = go.GetComponent<OtherPlayer>();
        if (op != null)
        {
            Vector3 beforeTransform = go.transform.position;
            Vector2 beforeCell = op.CellPos;
            if (shouldLog)
                Debug.Log($"[CLIENT][S_MOVE] BeforeTransform=({beforeTransform.x:0.00},{beforeTransform.y:0.00},{beforeTransform.z:0.00}), BeforeCell=({beforeCell.x:0.00},{beforeCell.y:0.00})");

            op.ApplyRemoteMove(movePacket.PosInfo);

            Vector3 afterTransform = go.transform.position;
            Vector2 afterCell = op.CellPos;
            if (shouldLog)
                Debug.Log($"[CLIENT][S_MOVE] AfterApplyTransform=({afterTransform.x:0.00},{afterTransform.y:0.00},{afterTransform.z:0.00}), AfterCell=({afterCell.x:0.00},{afterCell.y:0.00})");
            return;
        }

        EnemyPlayer ep = go.GetComponent<EnemyPlayer>();
        if (ep != null)
        {
            ep.ApplyRemoteMove(movePacket.PosInfo);
            if (shouldLog)
                Debug.Log($"[CLIENT][S_MOVE] EnemyPlayer applied. ObjectId={movePacket.PlayerId}, Name={go.name}");
            return;
        }

        if (shouldLog)
            Debug.Log($"[CLIENT][S_MOVE] Move target component missing. ObjectId={movePacket.PlayerId}, Name={go.name}");
    }
    private static string FormatSpawnSyncObject(ObjectInfo info)
    {
        if (info == null)
            return "null";

        PositionInfo pos = info.PosInfo;
        return $"Id={info.ObjectId},Name={info.Name},Pos=({pos?.PosX ?? 0f:0.00},{pos?.PosY ?? 0f:0.00}),State={pos?.State},Dir={pos?.MoveDir}";
    }

    private static string FormatSpawnSyncTransform(GameObject go)
    {
        if (go == null)
            return "null";

        Vector3 pos = go.transform.position;
        return $"({pos.x:0.00},{pos.y:0.00},{pos.z:0.00})";
    }

    private static bool ShouldLogMoveDiagnostics(int objectId, GameObject go)
    {
        bool isTargetObjectId = objectId >= 0 && objectId <= 2;
        bool isDummy = go != null && go.name.StartsWith("PD_Dummy", StringComparison.Ordinal);
        if (isTargetObjectId == false && isDummy == false)
            return false;

        int count = 0;
        s_moveLogCounts.TryGetValue(objectId, out count);
        count++;
        s_moveLogCounts[objectId] = count;

        return count <= 20 || count % 30 == 0;
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

        if (!TryConvertSceneType(s_SCENEMOVE.SceneType, out Define.Scenes targetScene))
        {
            Debug.LogWarning($"[SCENE] SceneMove rejected. Unknown SceneType={s_SCENEMOVE.SceneType}");
            return;
        }

        Debug.Log($"[SCENE] SceneMove received. TargetScene={targetScene}, RoomId={s_SCENEMOVE.TargetRoomId}, TransferId={s_SCENEMOVE.TransferId}");

        bool showLoading = targetScene == Define.Scenes.BAKAL;
        if (showLoading)
        {
            GameManager.Input.SetInputLocked(true);
            SceneLoadingOverlay.Show("매칭이 완료되어 던전으로 이동합니다", "Bakal 전장에 입장하고 있습니다.");
            Debug.Log($"[SCENE][LOADING] Shown. TargetScene={targetScene}, RoomId={s_SCENEMOVE.TargetRoomId}, TransferId={s_SCENEMOVE.TransferId}");
        }

        GameManager.SCENE.LoadSceneAsync(targetScene, () =>
        {
            if (showLoading)
                SceneLoadingOverlay.SetDetail("서버에 입장 준비를 알리는 중입니다.");

            C_SceneReady sceneReady = new C_SceneReady();
            sceneReady.TargetRoomId = s_SCENEMOVE.TargetRoomId;
            sceneReady.TransferId = s_SCENEMOVE.TransferId;
            sceneReady.TargetRoomType = s_SCENEMOVE.TargetRoomType;
            sceneReady.SceneType = s_SCENEMOVE.SceneType;

            Debug.Log($"[SCENE] Scene loaded. Sending C_SceneReady. RoomId={sceneReady.TargetRoomId}, TransferId={sceneReady.TransferId}");
            GameManager.Network.Send(sceneReady);
        });
    }

    public static void S_CollisionHandler(PacketSession session, IMessage packet)
    {
        S_Collision s_Collision = packet as S_Collision;
        if (s_Collision == null || s_Collision.Playerinfo == null)
            return;

        int objectId = s_Collision.Playerinfo.ObjectId > 0 ? s_Collision.Playerinfo.ObjectId : s_Collision.PlayerId;
        Debug.Log($"[CLIENT][S_COLLISION] PlayerId={s_Collision.PlayerId}, ObjectId={objectId}, Name={s_Collision.Playerinfo.Name}, Damage={s_Collision.Playerinfo.Damage}");

        GameObject go = GameManager.ObjectManager.FindById(objectId);

        if (go == null)
        {
            Debug.LogWarning($"[CLIENT][S_COLLISION_MISSING_TARGET] PlayerId={s_Collision.PlayerId}, ObjectId={objectId}, Damage={s_Collision.Playerinfo.Damage}");
            return;
        }

        Debug.Log($"{s_Collision.Playerinfo.Name}이  {s_Collision.Playerinfo.Damage}만큼의 피해를 받았습니다");


        BaseCharacter bc = go.GetComponent<BaseCharacter>();

        if (bc != null)
        {
            float beforeHp = bc.HP;
            bc.TakeDamage(s_Collision.Playerinfo.Damage);

            EnemyPlayer damagedEnemy = bc as EnemyPlayer;
            if (damagedEnemy != null)
                GameManager.UI.ShowComboHit();

            if (s_Collision.Playerinfo.StatInfo != null && s_Collision.Playerinfo.StatInfo.MaxHp > 0)
            {
                bc.MaxHP = s_Collision.Playerinfo.StatInfo.MaxHp;
                bc.HP = s_Collision.Playerinfo.StatInfo.Hp;
                RefreshMyPlayerHudIfNeeded(bc);
                Debug.Log($"[CLIENT][S_COLLISION_HP_SYNC] ObjectId={objectId}, BeforeHp={beforeHp}, Damage={s_Collision.Playerinfo.Damage}, SyncedHp={bc.HP}/{bc.MaxHP}");
            }
        }
    }

    private static void RefreshMyPlayerHudIfNeeded(BaseCharacter damagedCharacter)
    {
        if (damagedCharacter == null || GameManager.ObjectManager == null || GameManager.ObjectManager.MyPlayer != damagedCharacter)
            return;

        UI_HUD hud = null;
        UI_BakalSceneUI bakalSceneUI = GameManager.UI._scene as UI_BakalSceneUI;
        if (bakalSceneUI != null)
            hud = bakalSceneUI.HUD;

        if (hud == null && GameManager.UI._scene != null)
            hud = GameManager.UI._scene.GetComponent<UI_HUD>();

        if (hud == null)
        {
            Debug.LogWarning("[CLIENT][HUD_HP_SYNC_SKIP] Reason=HudNotFound");
            return;
        }

        hud.targetChar = damagedCharacter;
        hud.RefreshHpBarImmediate();
    }

    public static void S_UdpHelloHandler(PacketSession session, IMessage packet)
    {
        S_UdpHello udpHello = packet as S_UdpHello;
        Debug.Log($"[UDP] S_UdpHello received on TCP PacketManager. Ok={udpHello?.Ok}, Message={udpHello?.Message}");
    }
    public static void S_ConnectedHandler(PacketSession session, IMessage packet)
    {
        C_Login c_Login = new C_Login();

        c_Login.UniqueId = GameManager.GetLoginUniqueId();
        Debug.Log($"[LOGIN] UniqueId={c_Login.UniqueId}");
        GameManager.Network.Send(c_Login);
    }


    public static void S_LoginHandler(PacketSession session, IMessage packet)
    {
        S_Login s_Login = packet as S_Login;

        Debug.Log($"Login Response{s_Login.LoginOK}");
        Debug.Log($"Login Recv : {s_Login.UdpToken}");

        if (s_Login.LoginOK != 1)
        {
            Debug.LogWarning($"[LOGIN] Login failed. LoginOK={s_Login.LoginOK}");
            return;
        }

        GameManager.Network.SetUdpToken(s_Login.UdpToken);
        GameManager.Network.ConnectUdpToGameServer();
        GameManager.Network.RegisterUdpWithSavedToken();

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

        if (s_CreatePlayer.Player == null || string.IsNullOrWhiteSpace(s_CreatePlayer.Player.Name))
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

        if (s_CreateRoom.ResponseCode == 0)
        {
            Debug.LogWarning("[MATCH] CreateRoom rejected by server. Check equipment requirement.");
            GameManager.UI.ShowToast("무기와 방어구를 장착해야 입장할 수 있습니다.");

            if (partyEntry != null)
            {
                partyEntry.ClosePopupUI();
                partyEntry = null;
            }

            TownScene townScene = GameManager.SCENE.CurrentActiveScene as TownScene;
            if (townScene != null)
                townScene.ResetDungeonTriggerEvent();

            return;
        }

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


        GameObject go = GameManager.ObjectManager.FindById(s_Die.Player.ObjectId);

        BaseCharacter baseCharacter = go.GetComponent<BaseCharacter>();

        GameObjectType type = ObjectManager.GetObjectTypeId(s_Die.Player.ObjectId);

        Debug.Log($"Who Dead ? {s_Die.Player.ObjectId},{s_Die.Player.Name} , {type.ToString()}");
        switch (type)
        {
            case GameObjectType.Player:

                MyPlayer mp = baseCharacter.GetComponent<MyPlayer>();

                if (mp)
                    mp.OnDead();

                else

                {
                    OtherPlayer op = baseCharacter.GetComponent<OtherPlayer>();
                    op.OnDead();
                }

                break;
            case GameObjectType.Enemy:
                EnemyPlayer ep = baseCharacter.GetComponent<EnemyPlayer>();

                ep.OnDead();
                break;
        }

    }


    public static void S_DungeonClearHandler(PacketSession session, IMessage message)
    {
        S_DungeonClear dungeonClear = (S_DungeonClear)message;
        int delaySeconds = Mathf.Max(0, dungeonClear.ReturnDelaySeconds);
        string clearMessage = string.IsNullOrWhiteSpace(dungeonClear.Message)
            ? $"클리어 하였습니다. 마을로 {delaySeconds}초뒤 이동합니다."
            : dungeonClear.Message;

        Debug.Log($"[DUNGEON_CLEAR] Received. RoomId={dungeonClear.ClearedRoomId}, Delay={delaySeconds}, Target={dungeonClear.TargetRoomType}, Scene={dungeonClear.SceneType}");

        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        sequence.AppendCallback(() => GameManager.UI.ShowToast(clearMessage));

        for (int second = delaySeconds - 1; second >= 1; second--)
        {
            int remaining = second;
            sequence.AppendInterval(1f);
            sequence.AppendCallback(() => GameManager.UI.ShowToast($"마을로 {remaining}초뒤 이동합니다."));
        }
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
        int addedCount = 0;
        string firstItemMessage = null;

        foreach (ItemInfo itemInfo in itemList.Items)
        {
            Item item = Item.MakeItem(itemInfo);
            if (item == null)
            {
                Debug.LogWarning($"[ITEM] S_AddItem ignored. Invalid TemplateId={itemInfo.TemplateId}, ItemDbId={itemInfo.ItemDbId}");
                continue;
            }

            GameManager.Inven.Add(item);
            addedCount++;

            string itemName = GetItemDisplayName(item.TemplateId);
            string itemMessage = string.IsNullOrEmpty(itemName)
                ? $"아이템 획득: TemplateId={item.TemplateId}, Count={item.Count}"
                : $"{itemName} x{item.Count} 획득";

            if (firstItemMessage == null)
                firstItemMessage = itemMessage;

            Debug.Log($"[ITEM] AddItem received. ItemDbId={item.ItemDbId}, TemplateId={item.TemplateId}, Count={item.Count}, Name={itemName ?? "Unknown"}");
        }

        if (addedCount <= 0)
            return;

        GameManager.UI.ShowToast(firstItemMessage ?? "아이템을 획득했습니다.");
        RefreshInventoryIfOpen();
    }

    private static string GetItemDisplayName(int templateId)
    {
        Data.ItemData itemData = null;
        if (GameManager.DataManager.ItemDict.TryGetValue(templateId, out itemData) == false)
            return null;

        return itemData?.name;
    }

    private static void RefreshInventoryIfOpen()
    {
        MyPlayer myPlayer = GameManager.ObjectManager.MyPlayer as MyPlayer;
        if (myPlayer == null || myPlayer.InvenUI == null || myPlayer.InvenUI.gameObject.activeInHierarchy == false)
            return;

        myPlayer.InvenUI.RefreshUI();
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

            ((MyPlayer)GameManager.ObjectManager.MyPlayer).InvenUI?.RefreshUI();

            ((MyPlayer)GameManager.ObjectManager.MyPlayer).StatUI?.RefreshUI();

            GameManager.ObjectManager.MyPlayer.RefreshAdditionalStat();

        }
    }

    public static void S_ChangeStatHandler(PacketSession session, IMessage packet)
    {
        S_ChangeStat itemList = (S_ChangeStat)packet;
    }
}














