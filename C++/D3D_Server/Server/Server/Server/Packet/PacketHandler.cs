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
using Server.Game;




class PacketHandler
{
    private static int GetSkillRange(int skillId)
    {
        switch (skillId)
        {
           
            case 1: return 3;  // Q 스킬 (3 타일 범위)
            case 2: return 4;  // W 스킬 (4 타일 범위)
            case 3: return 2;  // E 스킬 (2 타일 범위)
            case 4: return 5;  // R 스킬 (5 타일 범위)
            default: return 3;  // 기본값 (3 타일 범위)
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

    //public static void C_MoveHandler(PacketSession session, IMessage message)
    //{
    //    var movePacket = message as C_Move;
    //    if (movePacket == null)
    //        return;

    //    Console.WriteLine($"[Server] Received Move Packet: {movePacket.ObjectId} to ({movePacket.TargetPos.X}, {movePacket.TargetPos.Y}, {movePacket.TargetPos.Z})");

    //    ClientSession clientSession = (ClientSession)session;
    //    GameRoom room = clientSession.GameRoom;

    //    // ✅ 해당 오브젝트 찾기
    //    GameObject obj = room.FindObject(movePacket.ObjectId);
    //    if (obj == null)
    //        return;

    //    // 🔥 실제 좌표 (float)
    //    float x = movePacket.TargetPos.X;
    //    float y = movePacket.TargetPos.Y;
    //    float z = movePacket.TargetPos.Z;

    //    // 🔥 타일 좌표 (int)
    //    int tileX = movePacket.CellPos.X;
    //    int tileZ = movePacket.CellPos.Z;


    //    // ✅ 🔥 이동 가능 검사 제거 (일단 테스트를 위해!)
    //    Vec3 targetPos = movePacket.TargetPos.ToNumericsVector3();
    //    if (!room.CanGo(targetPos)) return;  // ⛔ 제거!

    //    // ✅ 위치 갱신
    //    obj.Info.Position = targetPos.ToProtoVector3();

    //    // ✅ 모든 플레이어에게 이동 정보 브로드캐스트
    //    S_Move sMovePacket = new S_Move { Info = obj.Info };
    //    room.Broadcast(sMovePacket);


    //}

    public static void C_MoveHandler(PacketSession session, IMessage message)
    {
        var movePacket = message as C_Move;
        if (movePacket == null)
            return;

        Console.WriteLine($"[Server] Received Move Packet: {movePacket.ObjectId} to ({movePacket.TargetPos.X}, {movePacket.TargetPos.Y}, {movePacket.TargetPos.Z}) | Tile({movePacket.CellPos.X}, {movePacket.CellPos.Z})");

        ClientSession clientSession = (ClientSession)session;
        GameRoom room = clientSession.GameRoom;

        // ✅ 해당 오브젝트 찾기
        GameObject obj = room.FindObject(movePacket.ObjectId);
        if (obj == null)
            return;

        // 🔥 타일 좌표 (int)
        int tileX = movePacket.CellPos.X;
        int tileZ = movePacket.CellPos.Z;

        // 🔥 맵 범위 검사
        if (tileX < 0 || tileZ < 0 /*|| tileX >= room.Tilemap.MapWidth || tileZ >= room.Tilemap.MapHeight*/)
        {
            Console.WriteLine("[Server] ❌ Move failed: Out of bounds!");
            return;
        }

        // 🔥 이동하려는 타일 정보 가져오기
        Tile targetTile = room._tilemap.GetTileAt(new Vec3(tileX, 0, tileZ));
        if (targetTile == null || !targetTile.IsWalkable)
        {
            Console.WriteLine("[Server] 🚧 Invalid Move: Tile is not walkable!");
            return;
        }

        // 🔥 현재 플레이어가 위치한 타일 좌표 확인 (이전 위치)
        int prevTileX = obj.TileX;
        int prevTileZ = obj.TileZ;

        // 🔥 타일 중심으로 이동 좌표 보정
        Vec3 correctedPos = new Vec3(tileX + 0.5f, movePacket.TargetPos.Y, tileZ + 0.5f);

        // ✅ 플레이어 위치 및 타일 정보 갱신
        obj.Info.Position = correctedPos.ToProtoVector3();
        obj.TileX = tileX;
        obj.TileZ = tileZ;

        // ✅ room에서 타일 점유 상태 업데이트
        room.UpdatePlayerTilePosition(obj, prevTileX, prevTileZ, tileX, tileZ);

        // ✅ 모든 플레이어에게 이동 정보 브로드캐스트
        S_Move sMovePacket = new S_Move { Info = obj.Info };
        room.Broadcast(sMovePacket);

        Console.WriteLine($"[Server] ✅ Player {movePacket.ObjectId} moved to corrected Position ({correctedPos.X}, {correctedPos.Y}, {correctedPos.Z}) | New Tile ({tileX}, {tileZ})");
    }



    public static void C_RequestMapHandler(PacketSession session, IMessage message)
    {
    }

    public static void C_SkillCastHandler(PacketSession session, IMessage message)
    {
        var skillPacket = message as C_SkillCast;
        if (skillPacket == null)
            return;

        Console.WriteLine($"[Server] 🎯 Skill Cast Received: Caster {skillPacket.CasterId} used Skill {skillPacket.SkillId} at ({skillPacket.TargetPos.X}, {skillPacket.TargetPos.Y}, {skillPacket.TargetPos.Z})");

        ClientSession clientSession = (ClientSession)session;
        GameRoom room = clientSession.GameRoom;
        Tilemap tilemap = room._tilemap;

        // ✅ 시전자 찾기
        GameObject caster = room.FindObject(skillPacket.CasterId);
        if (caster == null)
        {
            Console.WriteLine("[Server] ❌ Skill cast failed: Caster not found!");
            return;
        }

        Vec3 casterPos = new Vec3(caster.TileX,0,caster.TileZ); // 시전자의 위치
        int skillRange = GetSkillRange(skillPacket.SkillId);
        int skillDamage = 100;

        List<UInt64> hitObjects = new List<UInt64>();

        // ✅ 타일맵에서 스킬 범위 내의 타일만 가져오기 (최적화)
        List<Tile> tilesInRange = tilemap.GetTilesInRange(casterPos, skillRange);

        // ✅ 해당 타일 위의 적 검색
        foreach (var tile in tilesInRange)
        {
            foreach (var obj in tile.GetPlayers())
            {
                if (obj == caster) continue; // 자기 자신 공격 X
                if (obj.Info.TeamId == caster.Info.TeamId) continue; // 같은 팀 공격 X

                hitObjects.Add(obj.Info.ObjectId);
                obj.Info.Hp -= skillDamage;
                obj.Info.Hp = Math.Max(0, obj.Info.Hp);
            }
        }

        // ✅ 클라이언트로 브로드캐스트할 데이터 생성
        S_SkillResult skillResultPacket = new S_SkillResult
        {
            CasterId = skillPacket.CasterId,
            SkillId = skillPacket.SkillId
        };
        skillResultPacket.HitObjects.AddRange(hitObjects);

        room.Broadcast(skillResultPacket);
    }





    public static void C_EnterGameHandler(PacketSession session, IMessage message)
    {
        // 여기다가 챔프타입받고. S_EnterPacket을 다시 보내주고. 
        // 거기서 다시 Room으로 넘어와서 Room에다가 생성 패킷 보내준다.
    }
}
