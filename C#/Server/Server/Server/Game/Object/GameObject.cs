using Google.Protobuf.Protocol;
using Server.Game.Room;
using System;
using System.Collections.Generic;
using System.Text;

namespace Server.Game.Object
{


    public class GameObject
    {       
        public GameObjectType ObjectType { get; protected set; } 

        public int Id
        { 
            get
            {
                return Info.ObjectId;
            }       
            set
            {
                Info.ObjectId = value;
            }
        }



        public GameRoom Room { get; set; }
        public ObjectInfo Info { get; set; } = new ObjectInfo();
        public PositionInfo PosInfo { get; private set; } = new PositionInfo();

        public PlayerState CurrentPlayerState { get; protected set; }


        public float HP { get; protected set; }


        public GameObject()
        {
            Info.PosInfo = PosInfo;
        }


    }
}
