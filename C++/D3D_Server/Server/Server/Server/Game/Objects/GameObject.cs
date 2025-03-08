using System;
using System.Collections.Generic;
using System.Text;
using Google.Protobuf.Protocol;
using Server.Game.Room;


namespace Server.Game.Objects
{
    public class GameObject
    {
        public ObjectInfo Info { get; set; }
        public GameRoom Room { get; set; }

        public int TileX { get; set; }
        public int TileZ { get; set; }


    }
}
