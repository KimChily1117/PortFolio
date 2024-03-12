using Google.Protobuf.Protocol;
using System;
using System.Collections.Generic;
using System.Text;

namespace Server.Game.Object
{
    public class ObjectManager
    {
        public static ObjectManager Instance { get; } = new ObjectManager();

        object _lock = new object();

        Dictionary<int, Player> _players = new Dictionary<int, Player>();


        /* 바뀌게 될 Id 설계 설명
        // 4바이트 = 32비트 크기인 int 자료형을 
        비트로 쪼개서 안에다가 id 정보들을 집어 넣어보자
        [********][********][********][********] -> 32비트의 구조를 가시적으로 보여줌
        //[UNUSED(1)] [TYPE(7)] [ ID(24)]

        */

        int _counter = 0;


        public T Add<T>() where T : GameObject, new()
        {
            T gameObject = new T();

            lock (_lock)
            {
                gameObject.Id = GenerateId(gameObject.ObjectType);

                if (gameObject.ObjectType == GameObjectType.Player)
                {
                    _players.Add(gameObject.Id, gameObject as Player);
                }
            }
            return gameObject;
        }


        int GenerateId(GameObjectType type)
        {
            lock (_lock) 
            {
                return ((int)type << 24 | (_counter++));
            }
        }

        public static GameObjectType GetObjectTypebyId(int id)
        {
            // 비트 연산자 공부할것
            int type = (id >> 24) & 0x7F;
            return (GameObjectType)type;
        }

        public bool Remove(int objectId)
        {
            GameObjectType objectType = GetObjectTypebyId(objectId);
            lock (_lock)
            {
                if(objectType == GameObjectType.Player)
                {
                    return _players.Remove(objectId);
                }
            }
            return false;
        }

        public Player Find(int objectId)
        {
            GameObjectType objectType = GetObjectTypebyId(objectId);

            lock (_lock)
            {
                if( objectType == GameObjectType.Player ) 
                {
                    Player player = null;
                    if (_players.TryGetValue(objectId, out player))
                        return player;
                }              
            }
            return null;
        }
    }
}
