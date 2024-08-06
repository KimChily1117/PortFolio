using Google.Protobuf.Protocol;
using Server.Data;
using Server.Game.Room;
using System;
using System.Collections.Generic;
using Server.DB;
using System.Text;

namespace Server.Game.Object
{
    public class Enemy : GameObject
    {
        public int TemplateId { get; private set; }


        long _currentTime = 0;
        long _elapsedTime = 0;
        long _lastUsedTime = Environment.TickCount64;

        public Enemy()
        {
            ObjectType = GameObjectType.Enemy;            
        }


        public void Init(int tmpId)
        {
            TemplateId = tmpId;
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


        public override void OnDead(GameObject attacker)
        {
            GameObject owner = attacker.GetOwner();

            if(owner.ObjectType == GameObjectType.Player)
            {
                RewardData rewardData = GetRewardData();
                if(rewardData != null)
                {
                    Player player = (Player)owner;

                    DbTransaction.RewardPlayer(player, rewardData,Room);
                }
            }


            S_Die s_Die = new S_Die();
            s_Die.Player = Info;

            Room.Broadcast(s_Die);
        }

        private RewardData GetRewardData()
        {
            MonsterData monsterData = null;
            DataManager.MonsterDict.TryGetValue(TemplateId, out monsterData);

            int rand = new Random().Next(1, 101);

            int sum = 0;
            foreach (RewardData rewardData in monsterData.rewards)
            {
                sum += rewardData.probability;

                if(rand <= sum)
                {
                    return rewardData;
                }
            }

            return null;
        }
    }
}
