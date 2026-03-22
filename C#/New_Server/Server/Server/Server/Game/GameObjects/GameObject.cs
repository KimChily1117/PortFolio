using System;
using System.Collections.Generic;
using System.Text;

namespace Server.Game.GameObjects
{
    public class GameObject
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int PosX { get; set; }
        public int PosY { get; set; }
        public Room.GameRoom Room { get; set; }

    }
}
