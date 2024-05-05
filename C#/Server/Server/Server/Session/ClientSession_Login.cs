using Google.Protobuf.Protocol;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Server.DB;
using Server.Game;
using Server.Game.Object;
using Server.Game.Room;
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

        private List<LobbyPlayerInfo> LobbyPlayers { get; set; } = new List<LobbyPlayerInfo>();

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


                    foreach (PlayerDb playerDb in findDb.Players)
                    {

                        LobbyPlayerInfo playerInfo = new LobbyPlayerInfo()
                        {
                            Name = playerDb.PlayerName,
                            // 이곳에 캐선창에 있는 캐릭터들(스텟이라거나. 기타 다른 정보들을 넣어줘야함)
                            PlayerDbId = playerDb.PlayerDbId
                        };

                        LobbyPlayers.Add(playerInfo);
                        s_Login.Players.Add(LobbyPlayers);
                    }

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

                    Send(s_Login);
                    ServerState = PlayerServerState.ServerStateCharecterselect;

                }
            }
        }
        public void HandleEnterGame(C_EnterGame c_EnterGame)
        {

            LobbyPlayerInfo playerInfo = LobbyPlayers.Find(p => p.Name == c_EnterGame.Name);


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
                MyPlayer.Info.PosInfo.State = PlayerState.Idle;
                MyPlayer.Info.PosInfo.MoveDir = MoveDir.Right;

                MyPlayer.Info.Damage = 10.0f;
                MyPlayer.Info.PosInfo.PosX = 0;
                MyPlayer.Info.PosInfo.PosY = 0;
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

            GameRoom room = RoomManager.Instance.Find(RoomType.Town);

            room.Push(room.EnterRoom, MyPlayer);
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

                    LobbyPlayerInfo lobbyPlayer = new LobbyPlayerInfo()
                    {
                        Name = c_CreatePlayer.Name
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
