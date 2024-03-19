using Google.Protobuf.Protocol;
using Server.Game.Room;
using System;
using System.Collections.Generic;
using System.Text;

namespace Server.Game.Object
{
    public class Player : GameObject
    {
        public ClientSession Session { get; set; }
        public Player() 
        {
            ObjectType = GameObjectType.Player;
            CurrentPlayerState = PlayerState.Idle;
            HP = 100;   
        }   


    }
}
