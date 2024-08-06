using Google.Protobuf.Protocol;
using Server.Game.Object;
using System;
using System.Collections.Generic;
using System.Text;

namespace Server.Game.Room
{
    public partial class GameRoom : JobSerializer
    {
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

            resMovePacket.PosInfo = new PositionInfo();

            //Console.WriteLine($"Player : {player.Info.ObjectId} POS X : {movePacket.PosInfo.PosX} ");
            //Console.WriteLine($"Player : {player.Info.ObjectId} POS Y : {movePacket.PosInfo.PosY} ");
            //Console.WriteLine($"Player : {player.Info.ObjectId} POS STATE : {movePacket.PosInfo.State} ");
            //Console.WriteLine($"Player : {player.Info.ObjectId} POS MOVEDIR : {movePacket.PosInfo.MoveDir} ");


            resMovePacket.PosInfo.PosX = movePacket.PosInfo.PosX;
            resMovePacket.PosInfo.PosY = movePacket.PosInfo.PosY;
            resMovePacket.PosInfo.State = movePacket.PosInfo.State;
            resMovePacket.PosInfo.MoveDir = movePacket.PosInfo.MoveDir;

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
            if (player == null)
                return;

            S_SceneMove s_Scene_Move = new S_SceneMove();
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
                s_Collision.Playerinfo.Damage = player.TotalDamage;
                s_Collision.PlayerId = collisionPacket.Playerinfo.ObjectId;
                e.OnDamaged(player.TotalDamage, player);
            }

            else
            {
                s_Collision.PlayerId = collisionPacket.Playerinfo.ObjectId;
                s_Collision.Playerinfo = collisionPacket.Playerinfo;

                player.OnDamaged(collisionPacket.Playerinfo.Damage, player);
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
    }
}
