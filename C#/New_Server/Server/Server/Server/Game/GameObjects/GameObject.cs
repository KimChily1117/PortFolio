using System;
using System.Collections.Generic;
using System.Text;

namespace Server.Game.GameObjects
{

    public enum GameObjectType
    {
        None = 0,
        Player = 1,
        Enemy = 2,
    }

    public abstract class GameObject
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public int PosX { get; set; }
        public int PosY { get; set; }

        public Room.GameRoom Room { get; set; }

        public GameObjectType ObjectType { get; protected set; } = GameObjectType.None;

        public bool IsDirty { get; private set; } = true;

        public void MarkDirty()
        {
            IsDirty = true;
        }

        public void ClearDirty()
        {
            IsDirty = false;
        }
    }
}
