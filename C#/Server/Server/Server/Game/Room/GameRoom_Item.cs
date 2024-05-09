using Google.Protobuf.Protocol;
using Server.Game.Object;
using System;
using System.Collections.Generic;
using System.Text;

namespace Server.Game.Room
{
    public partial class GameRoom : JobSerializer
    {

        public void HandleEquipItem(Player player ,C_EquipItem euqipPacket)
        {
            if(player == null) 
                return;

            player.HandleEquipItem(euqipPacket); 
        }

    }
}
