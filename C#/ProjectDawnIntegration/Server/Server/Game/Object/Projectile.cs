using System;
using System.Collections.Generic;
using System.Text;
using Google.Protobuf.Protocol;


namespace Server.Game.Object
{
    public class Projectile : GameObject
    {
        public Projectile() { ObjectType = GameObjectType.Projectile; }

        public GameObject Owner { get; set; }


        public void Update()
        {
        }
    }
}
