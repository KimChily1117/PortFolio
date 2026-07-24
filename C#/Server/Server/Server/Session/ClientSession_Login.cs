using Google.Protobuf.Protocol;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Server.DB;
using Server.Game;
using Server.Game.Object;
using Server.Game.Room;
using Server.Data;
using ServerCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Server
{
    public partial class ClientSession : PacketSession
    {
        public int AccountDbId { get; set; }

        private const string DummyPlayerPrefix = "PD_Dummy";
        private const int PreferredDummyStarterWeaponTemplateId = 1;
        private const int PreferredDummyStarterArmorTemplateId = 100;
        private const int MinInventorySlot = 0;
        private const int MaxInventorySlot = 19;

        private List<LobbyPlayerInfo> LobbyPlayers { get; set; } = new List<LobbyPlayerInfo>();

        void IssueUdpToken()
        {
            UdpToken = Guid.NewGuid().ToString("N");
            UdpTokenExpiresAt = DateTime.UtcNow.AddMinutes(2);
            UdpEndPoint = null;
            LastUdpSeenAt = DateTime.MinValue;
            ResetUdpMoveSecurityState();

            Console.WriteLine($"[UDP] Token issued. SessionId={SessionId}, TokenLength={UdpToken.Length}, ExpiresAt={UdpTokenExpiresAt:O}");
        }

        public void HandleLogin(C_Login c_Login)
        {
            if (ServerState != PlayerServerState.ServerStateLogin)
            {
                Console.WriteLine($"Current State is not LoginState!!!");
                return;
            }

            LobbyPlayers.Clear();

            using (AppDbContext db = new AppDbContext())
            {
                AccountDb findDb = db.Accounts.Include(a => a.Players).
                    Where(a =>
                a.AccountName == c_Login.UniqueId).FirstOrDefault();

                if (findDb != null)
                {
                    S_Login s_Login = new S_Login();
                    AccountDbId = findDb.AccountDbId;
                    s_Login.LoginOK = 1;
                    IssueUdpToken();
                    s_Login.UdpToken = UdpToken;


                    bool equipmentChanged = false;
                    foreach (PlayerDb playerDb in findDb.Players)
                    {
                        equipmentChanged |= EnsureDummyRequiredEquipment(db, playerDb);

                        LobbyPlayerInfo playerInfo = new LobbyPlayerInfo()
                        {
                            Name = playerDb.PlayerName,
                            // 이곳에 캐선창에 있는 캐릭터들(스텟이라거나. 기타 다른 정보들을 넣어줘야함)
                            PlayerDbId = playerDb.PlayerDbId
                        };

                        LobbyPlayers.Add(playerInfo);
                        s_Login.Players.Add(LobbyPlayers);
                    }

                    if (equipmentChanged)
                        db.SaveChanges();

                    Send(s_Login);

                    ServerState = PlayerServerState.ServerStateCharecterselect;
                }


                else // 계정정보가 없을 때 계정을 어떻게 만들어 줄것인지?
                {
                    AccountDb newDb = new AccountDb();
                    newDb.AccountName = c_Login.UniqueId;

                    db.Add(newDb);
                    db.SaveChanges();

                    AccountDbId = newDb.AccountDbId;



                    S_Login s_Login = new S_Login();
                    s_Login.LoginOK = 1;
                    IssueUdpToken();
                    s_Login.UdpToken = UdpToken;

                    Send(s_Login);
                    ServerState = PlayerServerState.ServerStateCharecterselect;

                }
            }
        }
        public void HandleEnterGame(C_EnterGame c_EnterGame)
        {

            LobbyPlayerInfo playerInfo = LobbyPlayers.Find(p => p.Name == c_EnterGame.Name);

            if (IsTransferring)
            {
                if (MyPlayer != null && c_EnterGame.Name == MyPlayer.Info.Name)
                {
                    Console.WriteLine($"[TRANSFER] Fallback C_EnterGame used as SceneReady. SessionId={SessionId}, PendingRoomId={PendingRoomId}, TransferId={PendingTransferId}");
                    if (RoomTransferService.Instance.TryEnterPendingRoom(this))
                        return;
                }
            }

            if (ServerState == PlayerServerState.ServerStateIngame)
            {
                if (c_EnterGame.Name == MyPlayer.Info.Name)
                {
                    Console.WriteLine($"HandleEnterGame : {c_EnterGame.Name} , {MyPlayer.Info.Name}");

                    GameRoom findroom = RoomManager.Instance.Find(RoomType.Bakal);

                    MyPlayer.Info.PosInfo.PosX = 0f;
                    MyPlayer.Info.PosInfo.PosY = 0f;
                    MyPlayer.Info.Damage = 10.0f;

                    findroom.Push(findroom.EnterRoom, MyPlayer);
                    return;
                }
            }

            if (ServerState != PlayerServerState.ServerStateCharecterselect)
                return;

            MyPlayer = ObjectManager.Instance.Add<Player>();
            {
                MyPlayer.Info.Name = c_EnterGame.Name;
                MyPlayer.PlayerDbId = playerInfo.PlayerDbId;
                MyPlayer.Info.Damage = 10.0f;
                MyPlayer.Session = this;

                S_ItemList itemListPacket = new S_ItemList();



                using (AppDbContext db = new AppDbContext())    
                {
                    List<ItemDb> items = db.Items.Where(i => i.OwnerDbId == playerInfo.PlayerDbId).ToList();
                    foreach (ItemDb itemDb in items)
                    {
                        Item item = Item.MakeItem(itemDb);
                        if (item != null)
                        {
                            MyPlayer.Inven.Add(item);

                            ItemInfo info = new ItemInfo();
                            info.MergeFrom(item.Info);
                            itemListPacket.Items.Add(info);
                        }
                    }
                }

                Send(itemListPacket);
            }

            ServerState = PlayerServerState.ServerStateIngame;

            GameRoom room = RoomManager.Instance.FindOrCreateTownChannel();
            Console.WriteLine($"[TOWN_CHANNEL] EnterGame assigned. SessionId={SessionId}, Player={MyPlayer.Info.Name}, RoomId={room?.RoomId ?? 0}, MaxPlayers={RoomManager.TownChannelMaxPlayers}");

            TownSpawnService.ApplyMyRoomSpawn(MyPlayer, room, "EnterGame");
            room.Push(room.EnterRoom, MyPlayer);
        }
        private static bool EnsureDummyRequiredEquipment(AppDbContext db, PlayerDb playerDb)
        {
            if (db == null || playerDb == null || string.IsNullOrWhiteSpace(playerDb.PlayerName))
                return false;

            if (playerDb.PlayerName.StartsWith(DummyPlayerPrefix, StringComparison.Ordinal) == false)
                return false;

            int weaponTemplateId = ResolveStarterTemplateId(ItemType.Weapon, PreferredDummyStarterWeaponTemplateId);
            int armorTemplateId = ResolveStarterTemplateId(ItemType.Armor, PreferredDummyStarterArmorTemplateId);
            if (weaponTemplateId <= 0 || armorTemplateId <= 0)
            {
                Console.WriteLine($"[DUMMY_SETUP][EQUIPMENT_CHECK] Player={playerDb.PlayerName}, HasWeapon=False, HasArmor=False, Reason=StarterTemplateNotFound, WeaponTemplateId={weaponTemplateId}, ArmorTemplateId={armorTemplateId}");
                return false;
            }

            List<ItemDb> playerItems = db.Items
                .Where(i => i.OwnerDbId == playerDb.PlayerDbId)
                .ToList();

            bool hasWeapon = playerItems.Any(i => i.Equipped && IsTemplateType(i.TemplateId, ItemType.Weapon));
            bool hasArmor = playerItems.Any(i => i.Equipped && IsTemplateType(i.TemplateId, ItemType.Armor));
            Console.WriteLine($"[DUMMY_SETUP][EQUIPMENT_CHECK] Player={playerDb.PlayerName}, HasWeapon={hasWeapon}, HasArmor={hasArmor}");

            bool changed = false;
            ItemDb weapon = EnsureTemplateItem(db, playerDb, playerItems, weaponTemplateId, "Weapon", ref changed);
            ItemDb armor = EnsureTemplateItem(db, playerDb, playerItems, armorTemplateId, "Armor", ref changed);

            if (weapon != null && weapon.Equipped == false)
            {
                weapon.Equipped = true;
                changed = true;
                Console.WriteLine($"[DUMMY_SETUP][EQUIPMENT_EQUIPPED] Player={playerDb.PlayerName}, TemplateId={weapon.TemplateId}, Slot={weapon.Slot}");
            }

            if (armor != null && armor.Equipped == false)
            {
                armor.Equipped = true;
                changed = true;
                Console.WriteLine($"[DUMMY_SETUP][EQUIPMENT_EQUIPPED] Player={playerDb.PlayerName}, TemplateId={armor.TemplateId}, Slot={armor.Slot}");
            }

            bool readyWeapon = playerItems.Any(i => i.Equipped && IsTemplateType(i.TemplateId, ItemType.Weapon));
            bool readyArmor = playerItems.Any(i => i.Equipped && IsTemplateType(i.TemplateId, ItemType.Armor));
            Console.WriteLine($"[DUMMY_SETUP][EQUIPMENT_READY] Player={playerDb.PlayerName}, HasWeapon={readyWeapon}, HasArmor={readyArmor}");

            return changed;
        }

        private static ItemDb EnsureTemplateItem(AppDbContext db, PlayerDb playerDb, List<ItemDb> playerItems, int templateId, string slotLabel, ref bool changed)
        {
            ItemDb item = playerItems
                .OrderByDescending(i => i.Equipped)
                .ThenBy(i => i.Slot)
                .FirstOrDefault(i => i.TemplateId == templateId);

            if (item != null)
                return item;

            int? slot = FindEmptyInventorySlot(playerItems);
            if (slot.HasValue == false)
            {
                Console.WriteLine($"[DUMMY_SETUP][EQUIPMENT_CHECK] Player={playerDb.PlayerName}, Reason=NoEmptySlot, TemplateId={templateId}, SlotType={slotLabel}");
                return null;
            }

            item = new ItemDb
            {
                TemplateId = templateId,
                Count = 1,
                Slot = slot.Value,
                OwnerDbId = playerDb.PlayerDbId,
                Equipped = false
            };

            playerItems.Add(item);
            db.Items.Add(item);
            changed = true;
            Console.WriteLine($"[DUMMY_SETUP][EQUIPMENT_CREATED] Player={playerDb.PlayerName}, TemplateId={templateId}, Slot={item.Slot}, SlotType={slotLabel}");
            return item;
        }

        private static int? FindEmptyInventorySlot(List<ItemDb> playerItems)
        {
            HashSet<int> usedSlots = new HashSet<int>(playerItems.Select(i => i.Slot));
            for (int slot = MinInventorySlot; slot <= MaxInventorySlot; slot++)
            {
                if (usedSlots.Contains(slot) == false)
                    return slot;
            }

            return null;
        }

        private static int ResolveStarterTemplateId(ItemType itemType, int preferredTemplateId)
        {
            if (IsTemplateType(preferredTemplateId, itemType))
                return preferredTemplateId;

            KeyValuePair<int, ItemData> fallback = DataManager.ItemDict
                .OrderBy(pair => pair.Key)
                .FirstOrDefault(pair => pair.Value != null && pair.Value.itemType == itemType);

            if (fallback.Value == null)
                return 0;

            Console.WriteLine($"[DUMMY_SETUP][EQUIPMENT_CHECK] PreferredTemplateInvalid. ItemType={itemType}, PreferredTemplateId={preferredTemplateId}, FallbackTemplateId={fallback.Key}");
            return fallback.Key;
        }

        private static bool IsTemplateType(int templateId, ItemType itemType)
        {
            ItemData itemData;
            return DataManager.ItemDict.TryGetValue(templateId, out itemData) && itemData != null && itemData.itemType == itemType;
        }

        public void HandleCreateCharecter(C_CreatePlayer c_CreatePlayer)
        {
            // TODO : 중복이벤트를 방지하기 위한 보안처리

            if (ServerState != PlayerServerState.ServerStateCharecterselect)
                return;

            using (AppDbContext db = new AppDbContext())
            {
                PlayerDb findPlayer = db.Players.Where(p => p.PlayerName == c_CreatePlayer.Name).FirstOrDefault();


                if (findPlayer != null)
                {
                    // 비어있는 패킷을 보냄으로서 의미가없다는걸 알려줌
                    Send(new C_CreatePlayer());
                }
                else
                {
                    PlayerDb createDB = new PlayerDb()
                    {
                        PlayerName = c_CreatePlayer.Name,
                        AccountDbId = AccountDbId
                    };

                    db.Players.Add(createDB);
                    db.SaveChanges();

                    if (EnsureDummyRequiredEquipment(db, createDB))
                        db.SaveChanges();

                    LobbyPlayerInfo lobbyPlayer = new LobbyPlayerInfo()
                    {
                        Name = c_CreatePlayer.Name,
                        PlayerDbId = createDB.PlayerDbId
                    };

                    // 메모리에도 들고 있다
                    LobbyPlayers.Add(lobbyPlayer);

                    // 클라에 전송
                    S_CreatePlayer newPlayer = new S_CreatePlayer() { Player = new LobbyPlayerInfo() };
                    newPlayer.Player.MergeFrom(lobbyPlayer);

                    Send(newPlayer);
                }


            }
        }


    }
}




