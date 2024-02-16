using Google.Protobuf;
using Google.Protobuf.Protocol;
using Server.Game.Object;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Server.Game.Room
{
    public class GameRoom : JobSerializer
    {
        public int RoomId { get; set; }

        Dictionary<int, Player> _players = new Dictionary<int, Player>();
        Dictionary<int, Enemy> _Enemys = new Dictionary<int, Enemy>();
        Dictionary<int, Projectile> _Projectiles = new Dictionary<int, Projectile>();

        public void EnterGame(GameObject gameObject)
        {
            if (gameObject == null)
                return;



            if (gameObject.ObjectType == GameObjectType.Player)
            {
                Player player = gameObject as Player;

                _players.Add(gameObject.Id, player);
                player.Room = this;


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
                    player.Session.Send(spawnPacket);
                }
            }

            else if (gameObject.ObjectType == GameObjectType.Enemy)
            {
                Enemy enemy = gameObject as Enemy;
                _Enemys.Add(enemy.Id, enemy);
            }

            else if (gameObject.ObjectType == GameObjectType.Projectile)
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
                    if (gameObject.Id != p.Id)
                        p.Session.Send(spawnPacket);
                }
            }

        }

        public void LeaveGame(int objectId)
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


        public void HandleMoveScene(Player player, C_SceneMove scenePacket)
        {
            //if (player == null)
            //    return;

            //    // TODO : 검증

            //    // 일단 서버에서 좌표 이동
            //    ObjectInfo info = player.Info;


            //    // 다른 플레이어한테도 알려준다
            //    S_Run resRunPacket = new S_Run();
            //    resRunPacket.PlayerId = player.Info.ObjectId;
            //    resRunPacket.PosInfo = runPacket.PosInfo;

            //    Broadcast(resRunPacket);

        }


        /// <summary>
        /// 일단은 패킷 핸들러 뚫어 놓긴했는데. 이게 맞나.. 싶어..
        /// </summary>
        /// <param name="player"> 시전자의 정보 </param>
        /// <param name="collisionPacket"> 피폭자의 정보를 넣을려고 </param>
        public void HandleCollision(Player player, C_Collision collisionPacket)
        {
            if (player == null) return;

            S_Collision s_Collision = new S_Collision();

            s_Collision.Playerinfo = collisionPacket.Playerinfo;

            s_Collision.PlayerId = player.Info.ObjectId;
            s_Collision.Playerinfo.Damage = 10.0f;
            
            Broadcast(s_Collision);
        }



        public void HandleSkill(Player player, C_Skill skillPacket)
        {


            // lock 떄문에 생긴 issue로 생각됨
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
                EnterGame(projectile);
            }

            // TODO : 데미지 판정
        }



        // Game Room 내부에 패킷을 보낼때 사용
        private void Broadcast(IMessage message)
        {

            foreach (Player player in _players.Values)
            {
                player.Session.Send(message);
            }

        }

    }
}
