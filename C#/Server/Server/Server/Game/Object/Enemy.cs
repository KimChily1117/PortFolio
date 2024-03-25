using Google.Protobuf.Protocol;
using Server.Game.Room;
using System;
using System.Collections.Generic;
using System.Text;

namespace Server.Game.Object
{
    public class Enemy : GameObject
    {
        long _currentTime = 0;
        long _elapsedTime = 0;
        long _lastUsedTime = Environment.TickCount64;// 10초

        public Enemy()
        {
            ObjectType = GameObjectType.Enemy;
            CurrentPlayerState = PlayerState.Idle;
            HP = 100;
        }

        public void Update()
        {

            switch (CurrentPlayerState)
            {
                case PlayerState.Idle:
                    ProcIdle();
                    break;
                case PlayerState.Moving:
                    ProcMoving();
                    break;
                case PlayerState.Run:
                    break;
                case PlayerState.Skill:
                    ProcSkill();
                    break;
                case PlayerState.Atk:
                    break;
                }
        }


        public void ProcIdle()
        {
            _currentTime = Environment.TickCount64;
            _elapsedTime = _currentTime - _lastUsedTime;
        
            if(_elapsedTime >= 20000) // 20초가 지났다면
            {
                CurrentPlayerState = PlayerState.Skill;
                _lastUsedTime = Environment.TickCount64;
            }



     }


        public void ProcMoving()
        {



        }
        public void ProcAtk()
        {

        }

        public void ProcSkill()
        {
            S_Skill skill = new S_Skill() { Info = new SkillInfo() };

            skill.PlayerId = Info.ObjectId;
            skill.Info.SkillId = 4;
            Room.Broadcast(skill);


            CurrentPlayerState = PlayerState.Idle;
        }



    }
}
