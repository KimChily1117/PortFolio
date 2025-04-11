using Google.Protobuf;
using Google.Protobuf.Protocol;
using Server;
using Server.Game.Objects;
using Server.Game.Room;
using ServerCore;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

using Vec3 = System.Numerics.Vector3;
using System.Threading.Tasks;
using Server.Game;


class PacketHandler
{
    private static int GetSkillRange(int skillId)
    {
        switch (skillId)
        {
            case 1: return 4;
            case 2: return 6;
            case 3: return 5;
            case 4: return 5;
            default: return 4;
        }
    }

    public static void C_TestMsgHandler(PacketSession session, IMessage message)
    {
        var movePacket = message as C_TestMsg;
        Console.WriteLine($"recv Test : {movePacket.Message}");

        S_TestMsg recv = new S_TestMsg();
        recv.Message = $"C# to C++ : Hello Client!!!";

        ClientSession clientSession = (ClientSession)session;
        clientSession.Send(recv);
    }

    public static void C_ChatMessageHandler(PacketSession session, IMessage message)
    {
        throw new NotImplementedException();
    }

    public static void C_MoveHandler(PacketSession session, IMessage message)
    {
        var movePacket = message as C_Move;
        if (movePacket == null)
            return;

        Console.WriteLine($"[Server] Received Move Packet: {movePacket.ObjectId} to ({movePacket.TargetPos.X}, {movePacket.TargetPos.Y}, {movePacket.TargetPos.Z}) | Tile({movePacket.CellPos.X}, {movePacket.CellPos.Z})");

        ClientSession clientSession = (ClientSession)session;
        GameRoom room = clientSession.GameRoom;

        room.Push(() =>
        {
            GameObject obj = room.FindObject(movePacket.ObjectId);
            if (obj == null)
                return;

            int tileX = movePacket.CellPos.X;
            int tileZ = movePacket.CellPos.Z;

            if (tileX < 0 || tileZ < 0)
            {
                Console.WriteLine("[Server] ❌ Move failed: Out of bounds!");
                return;
            }

            Tile targetTile = room._tilemap.GetTileAt(new Vec3(tileX, 0, tileZ));
            if (targetTile == null || !targetTile.IsWalkable)
            {
                Console.WriteLine("[Server] 🚧 Invalid Move: Tile is not walkable!");
                return;
            }

            int prevTileX = obj.TileX;
            int prevTileZ = obj.TileZ;

            Vec3 correctedPos = new Vec3(tileX + 0.5f, movePacket.TargetPos.Y, tileZ + 0.5f);

            obj.Info.Position = correctedPos.ToProtoVector3();
            obj.TileX = tileX;
            obj.TileZ = tileZ;

            room.UpdatePlayerTilePosition(obj, prevTileX, prevTileZ, tileX, tileZ);

            S_Move sMovePacket = new S_Move { Info = obj.Info };
            room.Broadcast(sMovePacket);

            Console.WriteLine($"[Server] ✅ Player {movePacket.ObjectId} moved to corrected Position ({correctedPos.X}, {correctedPos.Y}, {correctedPos.Z}) | New Tile ({tileX}, {tileZ})");
        });
    }

    public static void C_RequestMapHandler(PacketSession session, IMessage message)
    {
    }

    public static void C_SkillCastHandler(PacketSession session, IMessage message)
    {
        var skillPacket = message as C_SkillCast;
        if (skillPacket == null)
            return;

        Console.WriteLine($"[Server] 🎯 Skill Cast Received: Caster {skillPacket.CasterId} used Skill {skillPacket.SkillId} at ({skillPacket.TargetPos?.X}, {skillPacket.TargetPos?.Y}, {skillPacket.TargetPos?.Z})");

        ClientSession clientSession = (ClientSession)session;
        GameRoom room = clientSession.GameRoom;

        room.Push(() =>
        {
            GameObject caster = room.FindObject(skillPacket.CasterId);
            if (caster == null)
            {
                Console.WriteLine("[Server] ❌ Skill cast failed: Caster not found!");
                return;
            }

            IChampionSkillHandler skillHandler = caster.Info.ChampType switch
            {
                PLAYER_CHAMPION_TYPE.PlayerTypeGaren => new GarenSkillHandler(),
                PLAYER_CHAMPION_TYPE.PlayerTypeAnnie => new AnnieSkillHandler(),
                _ => null
            };

            if (skillHandler == null)
            {
                Console.WriteLine("[Server] ❌ Unknown Champion Type");
                return;
            }

            skillHandler.HandleSkill(room, caster, skillPacket);
        });
    }

    public static void C_EnterGameHandler(PacketSession session, IMessage message)
    {
        // TODO
    }
}

