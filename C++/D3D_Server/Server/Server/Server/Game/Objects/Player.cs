using System;
using System.Collections.Generic;
using System.Text;
using Google.Protobuf.Protocol;
namespace Server.Game.Objects
{
    public class Player : GameObject
    {
        // session Info
        // current RoomInfo는 따로 받지않는다. Session에 포함되어있음
        public ClientSession Session { get; set; }
        

        // Server ] Player Update
        public void Update()
        {


        }



    }

}
