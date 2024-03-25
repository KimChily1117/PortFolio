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
    public class GameRoom : JobSerializer
    {
        object _lock = new object();
        public int RoomId { get; set; }

        Dictionary<int, Player> _players = new Dictionary<int, Player>();
        Dictionary<int, Enemy> _enemys = new Dictionary<int, Enemy>();
        Dictionary<int, Projectile> _Projectiles = new Dictionary<int, Projectile>();

        List<LobbyPlayerInfo> _playerList = new List<LobbyPlayerInfo>();


        public void InitEnemy()
        {
            Enemy enemy = ObjectManager.Instance.Add<Enemy>();

            enemy.Info.Name = $"Bakal_2Phase";
            enemy.Info.PosInfo.PosX = -2f;
            enemy.Info.PosInfo.PosY = -0.55f;
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

                    _playerList.Add(playerInfo);


                    // 본인한테 정보 전송
                    {
                        S_Enter_Game enterPacket = new S_Enter_Game();
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
                        S_Leave_Game leavePacket = new S_Leave_Game();
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


                    _playerList.Add(playerInfo);


                    // 본인한테 정보 전송
                    {
                        S_Enter_Party enterPacket = new S_Enter_Party();
                        enterPacket.Playerinfo = player.Info;
                        enterPacket.ResponseCode = player.Info.IsMaster == true ? 1 : 2;
                        foreach (LobbyPlayerInfo Lp in _playerList)
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
                S_Leave_Game leavePacket = new S_Leave_Game();
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




        public void HandleMove(Player player, C_Move movePacket)
        {
            if (player == null)
                return;


            // TODO : 검증

            // 일단 서버에서 좌표 이동
            ObjectInfo info = player.Info;
            info.PosInfo = movePacket.PosInfo;

            // 다른 플레이어한테도 알려준다
            S_Move resMovePacket = new S_Move();
            resMovePacket.PlayerId = player.Info.ObjectId;
            resMovePacket.PosInfo = movePacket.PosInfo;

            Broadcast(resMovePacket);

        }

        public void HandleJump(Player player, C_Jump jumpPacket)
        {
            if (player == null)
                return;


            // TODO : 검증

            // 일단 서버에서 좌표 이동
            ObjectInfo info = player.Info;
            info.PosInfo = jumpPacket.PosInfo;
            info.PosInfo.MoveDir = jumpPacket.PosInfo.MoveDir;

            // 다른 플레이어한테도 알려준다
            S_Jump resJumpPacket = new S_Jump();
            resJumpPacket.PlayerId = player.Info.ObjectId;
            resJumpPacket.PosInfo = jumpPacket.PosInfo;
            resJumpPacket.PosInfo.MoveDir = jumpPacket.PosInfo.MoveDir;


            Broadcast(resJumpPacket);

        }


        public void HandleMoveScene(Player player, C_Scene_Move scenePacket)
        {
            if (player == null)
                return;

            S_Scene_Move s_Scene_Move = new S_Scene_Move();
            s_Scene_Move.Playerinfo = player.Info;

            if (scenePacket.Playerinfo.IsMaster)
            {
                Broadcast(s_Scene_Move);
            }
        }


        /// <summary>
        /// </summary>
        /// <param name="player"> 시전자의 정보 </param>
        /// <param name="collisionPacket"> 피폭자의 정보를 넣을려고 </param>
        public void HandleCollision(Player player, C_Collision collisionPacket)
        {
            if (player == null)
                return;

            Enemy e = null;

            if (!_enemys.TryGetValue(collisionPacket.Playerinfo.ObjectId, out e))
            {
                Console.WriteLine($"_enemys is NULL(Not Found)");
            }


            S_Collision s_Collision = new S_Collision();



            //s_Collision.Playerinfo = collisionPacket.Playerinfo;

            //s_Collision.PlayerId = player.Info.ObjectId;


            if (e != null)
            {
                s_Collision.Playerinfo = collisionPacket.Playerinfo;
                s_Collision.Playerinfo.Damage = 10.0f;
                s_Collision.PlayerId = collisionPacket.Playerinfo.ObjectId;
                e.OnDamaged(10.0f);
            }

            else
            {
                s_Collision.PlayerId = collisionPacket.Playerinfo.ObjectId;
                s_Collision.Playerinfo = collisionPacket.Playerinfo;

                player.OnDamaged(collisionPacket.Playerinfo.Damage);
            }

            Broadcast(s_Collision);
        }

        public void HandleSkill(Player player, C_Skill skillPacket)
        {
            if (player == null)
                return;


            ObjectInfo info = player.Info;
            S_Skill skill = new S_Skill() { Info = new SkillInfo() };

            //info.PosInfo.State = PlayerState.Skill;
            Console.WriteLine($"skill ??? : {skillPacket.Info.SkillId}");

            skill.PlayerId = info.ObjectId;
            skill.Info.SkillId = skillPacket.Info.SkillId;
            Broadcast(skill);


            if (skill.Info.SkillId == 1)
            {

            }
            else if (skill.Info.SkillId == 2)
            {

            }
            else if (skill.Info.SkillId == 3)
            {

            }
            else if (skill.Info.SkillId == 4)
            {

            }

            else if (skill.Info.SkillId == 5)
            {
                Projectile projectile = ObjectManager.Instance.Add<Projectile>();
                if (projectile == null)
                    return;

                projectile.Owner = player;
                projectile.PosInfo.State = PlayerState.Moving;
                projectile.PosInfo.MoveDir = player.PosInfo.MoveDir;
                projectile.PosInfo.PosX = player.PosInfo.PosX;
                projectile.PosInfo.PosY = player.PosInfo.PosY;
                EnterRoom(projectile);
            }

            // TODO : 데미지 판정
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
