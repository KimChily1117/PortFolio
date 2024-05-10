using Google.Protobuf.Protocol;
using Server.Game.Room;
using System;
using System.Collections.Generic;
using System.Text;

namespace Server.Game.Object
{
    public class Player : GameObject
    {
        public int PlayerDbId { get; set; }
        public ClientSession Session { get; set; }
        public Inventory Inven { get; private set; } = new Inventory();
        public Player() 
        {
            ObjectType = GameObjectType.Player;
            CurrentPlayerState = PlayerState.Idle;
            HP = 100;   
        }

        public override void OnDead(GameObject attacker)
        {
            S_Die s_Die = new S_Die();
            s_Die.Player = Info;
            Room.Broadcast(s_Die);

            GameRoom room = Room;
            room.LeaveRoom(Id);
        }

    }
}
