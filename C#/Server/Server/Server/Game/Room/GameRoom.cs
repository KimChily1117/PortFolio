using Google.Protobuf;
using Google.Protobuf.Protocol;
using Microsoft.EntityFrameworkCore.Internal;
using Server.DB;
using Server.Game.Object;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Server.Game.Room
{
    public partial class GameRoom : JobSerializer
    {
        object _lock = new object();
        public int RoomId { get; set; }

        Dictionary<int, Player> _players = new Dictionary<int, Player>();
        Dictionary<int, Enemy> _enemys = new Dictionary<int, Enemy>();
        Dictionary<int, Projectile> _Projectiles = new Dictionary<int, Projectile>();

        Dictionary<int,LobbyPlayerInfo> _PartyplayerList = new Dictionary<int,LobbyPlayerInfo>();


        public void InitEnemy()
        {
            Enemy enemy = ObjectManager.Instance.Add<Enemy>();


            enemy.Init(1);


            //enemy.Info.Name = $"Bakal_2Phase";
            //enemy.Info.PosInfo.PosX = -2f;
            //enemy.Info.PosInfo.PosY = -0.55f;
            EnterRoom(enemy);
        }
        public void Update()

        {
            foreach (Enemy enemy in _enemys.Values)
            {
                enemy.Update();
            }

            Flush();
        }

        #region EnterRoom
        public void EnterRoom(GameObject gameObject)
        {
            Console.WriteLine($"Enteroom ] room Type : {RoomId}");
            if (gameObject == null)
                return;

            GameObjectType type = ObjectManager.GetObjectTypebyId(gameObject.Id);

            lock (_lock)
            {

                if (type == GameObjectType.Player)
                {
                    Player player = gameObject as Player;

                    if (_players.ContainsKey(gameObject.Id))
                    {
                        _players.Remove(gameObject.Id);
                    }
                    _players.Add(gameObject.Id, player);
                    player.Room = this;

                    LobbyPlayerInfo playerInfo = new LobbyPlayerInfo()
                    {
                        Name = player.Info.Name
                    };

                    //_PartyplayerList.Add(playerInfo);


                    // 본인한테 정보 전송
                    {
                        S_EnterGame enterPacket = new S_EnterGame();
                        enterPacket.Player = player.Info;
                        player.Session.Send(enterPacket);

                        S_Spawn spawnPacket = new S_Spawn();

                        foreach (Player p in _players.Values)
                        {
                            if (player != p)
                                spawnPacket.Objects.Add(p.Info);
                        }

                        foreach (Enemy e in _enemys.Values)
                        {
                            spawnPacket.Objects.Add(e.Info);
                        }


                        player.Session.Send(spawnPacket);

                    }
                }

                else if (type == GameObjectType.Enemy)
                {

                    if (_enemys.ContainsKey(gameObject.Id))
                    {
                        _enemys.Remove(gameObject.Id);
                    }


                    Enemy enemy = gameObject as Enemy;
                    enemy.Room = this;
                    _enemys.Add(gameObject.Id, enemy);

                }

                else if (type == GameObjectType.Projectile)
                {
                    Projectile projectile = gameObject as Projectile;
                    _Projectiles.Add(gameObject.Id, projectile);
                }

                // 타인한테 정보 전송
                {
                    S_Spawn spawnPacket = new S_Spawn();

                    spawnPacket.Objects.Add(gameObject.Info);

                    foreach (Player p in _players.Values)
                    {
                        Console.WriteLine($"Room Other Player Packet? :{gameObject.Info.Name} , {p.Info.Name} {gameObject.Id} , {p.Id}");
                        if (gameObject.Info.Name != p.Info.Name)
                            p.Session.Send(spawnPacket);
                    }
                }
            }

        }

        public void LeaveRoom(int objectId)
        {
            GameObjectType type = ObjectManager.GetObjectTypebyId(objectId);

            lock (_lock)
            {                
                if (type == GameObjectType.Player)
                {
                    _PartyplayerList.Remove(objectId);
                    Player player = null;
                    if (_players.TryGetValue(objectId, out player) == false)
                    {
                        Console.WriteLine($"LevaRoom PLayer not found");
                        return;
                    }
                    _players.Remove(objectId);
                    player.Room = null;

                    // 본인한테 정보 전송
                    {
                        S_LeaveGame leavePacket = new S_LeaveGame();
                        player.Session.Send(leavePacket);
                    }

                    // 타인한테 정보 전송
                    {
                        S_Despawn despawnPacket = new S_Despawn();
                        despawnPacket.PlayerIds.Add(player.Id);
                        foreach (Player p in _players.Values)
                        {
                            if (player != p)
                                p.Session.Send(despawnPacket);
                        }
                    }
                }

                if(type == GameObjectType.Enemy)
                {
                    Enemy enemy = null;
                    if(_enemys.TryGetValue(objectId, out enemy) == false)
                    {
                        Console.WriteLine($"LeaveRoom Enemy not found");
                        return;
                    }

                    _enemys.Remove(objectId);
                    enemy.Room = null;
                   
                    // 타인한테 정보 전송
                    {
                        S_Despawn despawnPacket = new S_Despawn();
                        despawnPacket.PlayerIds.Add(enemy.Id);                      
                        Broadcast(despawnPacket);
                    }
                }

            }
        }

        #endregion EnterRoom

        #region EnterParty
        public void EnterParty(GameObject gameObject)
        {
            GameObjectType type = ObjectManager.GetObjectTypebyId(gameObject.Id);

            lock (_lock)
            {

                if (gameObject == null)
                    return;

                if (type == GameObjectType.Player)
                {
                    Player player = gameObject as Player;

                    _players.Add(gameObject.Id, player);
                    player.Room = this;

                    LobbyPlayerInfo playerInfo = new LobbyPlayerInfo()
                    {
                        Name = player.Info.Name
                    };


                    _PartyplayerList.Add(gameObject.Id,playerInfo);


                    // 본인한테 정보 전송
                    {
                        S_EnterParty enterPacket = new S_EnterParty();
                        enterPacket.Playerinfo = player.Info;
                        enterPacket.ResponseCode = player.Info.IsMaster == true ? 1 : 2;
                        foreach (LobbyPlayerInfo Lp in _PartyplayerList.Values)
                        {
                            enterPacket.PartyMembers.Add(Lp);
                        }
                        player.Session.Send(enterPacket);

                        foreach (Player p in _players.Values)
                        {
                            if (gameObject.Id != p.Id)
                                p.Session.Send(enterPacket);
                        }
                    }
                }

                else if (type == GameObjectType.Enemy)
                {
                    Enemy enemy = gameObject as Enemy;
                    _enemys.Add(enemy.Id, enemy);
                }
            }
        }

        public void LeaveParty(int objectId)
        {

            Player player = null;
            if (_players.TryGetValue(objectId, out player) == false)
                return;

            _players.Remove(objectId);
            player.Room = null;

            // 본인한테 정보 전송
            {
                S_LeaveGame leavePacket = new S_LeaveGame();
                player.Session.Send(leavePacket);
            }

            // 타인한테 정보 전송
            {
                S_Despawn despawnPacket = new S_Despawn();
                despawnPacket.PlayerIds.Add(player.Info.ObjectId);
                foreach (Player p in _players.Values)
                {
                    if (player != p)
                        p.Session.Send(despawnPacket);
                }
            }

        }

        #endregion EnterParty

        public void OnLeaveGame()
        {
            // TODO
            // DB 연동?
            // -- 피가 깎일 때마다 DB 접근할 필요가 있을까?
            // 1) 서버 다운되면 아직 저장되지 않은 정보 날아감
            // 2) 코드 흐름을 다 막아버린다 !!!!
            // - 비동기(Async) 방법 사용?
            // - 다른 쓰레드로 DB 일감을 던져버리면 되지 않을까?
            // -- 결과를 받아서 이어서 처리를 해야 하는 경우가 많음.
            // -- 아이템 생성

            //DbTransaction.SavePlayerStatus_Step1(this, Room);
        }


        // Game Room 내부에 패킷을 보낼때 사용
        public void Broadcast(IMessage message)
        {

            foreach (Player player in _players.Values)
            {
                player.Session.Send(message);
            }

        }

    }
}
