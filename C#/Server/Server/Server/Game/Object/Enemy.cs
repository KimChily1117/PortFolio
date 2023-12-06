using Google.Protobuf.Protocol;
using System;
using System.Collections.Generic;
using System.Text;

namespace Server.Game.Object
{
    public class Enemy : GameObject
    {
        public Enemy() { ObjectType = GameObjectType.Enemy; } 
    }
}
