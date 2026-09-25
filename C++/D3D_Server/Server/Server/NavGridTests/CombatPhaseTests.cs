using Google.Protobuf;
using Google.Protobuf.Protocol;
using Server;
using Server.Game.Navigation;
using Server.Game.Objects;
using Server.Game.Room;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NumericsVector3 = System.Numerics.Vector3;
using ProtoVector3 = Google.Protobuf.Protocol.Vector3;

internal static class CombatPhaseTests
{
    private static int _assertions;

    private static void Check(bool condition, string message)
    {
        ++_assertions;
        if (!condition) throw new InvalidOperationException(message);
    }

    public static int Run()
    {
        _assertions = 0;
        RunCombatGatherTests();
        byte[] cells = new byte[12 * 12];
        var asset = new NavGridAsset(1, "combat-grid", 12, 12, 1.0f,
            NumericsVector3.Zero, 0.5f, 1, new byte[32], cells);
        var config = new NavigationMapConfig
        {
            RoomId = 70000,
            SceneId = 0,
            NavigationMapId = asset.MapId,
            AssetPath = "tests/combat.navgrid",
            FormatVersion = 1,
            ContentHashHex = asset.ContentHashHex,
            Enabled = true,
            Required = true,
        };
        var room = new GameRoom(new NavigationRegistration(config, asset));
        Player caster = Player(70001, 1, 2.5f, 2.5f);
        caster.Info.ChampType = PLAYER_CHAMPION_TYPE.PlayerTypeAnnie;
        Player enemy = Player(70002, 2, 5.5f, 2.5f);
        Player ally = Player(70003, 1, 3.5f, 2.5f);
        Player sideEnemy = Player(70004, 2, 2.5f, 5.5f);
        Player behindEnemy = Player(70005, 2, 1.5f, 2.5f);
        room.AddObject(caster);
        room.AddObject(enemy);
        room.AddObject(ally);
        room.AddObject(sideEnemy);
        room.AddObject(behindEnemy);
        room.Flush();

        var packets = new List<IMessage>();
        room.PacketBroadcastSink = packets.Add;
        new AnnieSkillHandler().HandleSkill(room, caster, new C_SkillCast
        {
            CasterId = caster.Info.ObjectId,
            SkillId = (int)SkillType.WSpell,
            TargetPos = new ProtoVector3 { X = 5.5f, Y = 0.0f, Z = 2.5f },
            IsAreaSkill = true,
            AreaRadius = 4.0f,
        });

        // First Flush starts the cast. The zero-delay damage job is consumed by
        // the following Flush because JobTimer is flushed before queued jobs.
        room.Flush();
        room.Flush();
        for (int i = 1; i < 10; ++i)
        {
            Thread.Sleep(210);
            room.Flush();
        }

        List<S_Damage> damagePackets = packets.OfType<S_Damage>().ToList();
        Check(packets.OfType<S_SkillResult>().Count() == 1, "Annie W must emit one cast result.");
        S_SkillResult wResult = packets.OfType<S_SkillResult>().Single();
        Check(wResult.CastOrigin != null && wResult.CastDirection != null, "Annie W must broadcast authoritative cone data.");
        Check(Math.Abs(wResult.CastDirection.X - 1.0f) < 0.0001f && Math.Abs(wResult.CastDirection.Z) < 0.0001f, "Annie W direction must be deterministic.");
        Check(damagePackets.Count == 10, "Annie W must emit exactly ten S_Damage packets.");
        Check(damagePackets.All(packet => packet.TargetId == enemy.Info.ObjectId), "Annie W damaged a non-enemy target.");
        Check(damagePackets.All(packet => packet.Damage == 3), "Each Annie W tick must deal exactly three damage.");
        for (int i = 0; i < damagePackets.Count; ++i)
            Check(damagePackets[i].RemainHp == 100 - (i + 1) * 3, "Annie W HP sequence is not tick-accurate.");
        Check(enemy.Info.Hp == 70, "Annie W total damage must be thirty.");
        Check(ally.Info.Hp == 100, "Annie W must not damage allies.");
        Check(sideEnemy.Info.Hp == 100, "Annie W must reject enemies outside the cone angle.");
        Check(behindEnemy.Info.Hp == 100, "Annie W must reject enemies behind the caster.");
        var garenConfig = new NavigationMapConfig
        {
            RoomId = 70010,
            SceneId = 0,
            NavigationMapId = asset.MapId,
            AssetPath = "tests/combat.navgrid",
            FormatVersion = 1,
            ContentHashHex = asset.ContentHashHex,
            Enabled = true,
            Required = true,
        };
        var garenRoom = new GameRoom(new NavigationRegistration(garenConfig, asset));
        Player garen = Player(70011, 1, 2.5f, 2.5f, 1000);
        garen.Info.ChampType = PLAYER_CHAMPION_TYPE.PlayerTypeGaren;
        Player garenEnemy = Player(70012, 2, 4.5f, 2.5f, 1000);
        garenRoom.AddObject(garen);
        garenRoom.AddObject(garenEnemy);
        garenRoom.Flush();
        var garenPackets = new List<IMessage>();
        garenRoom.PacketBroadcastSink = garenPackets.Add;
        new GarenSkillHandler().HandleSkill(garenRoom, garen, new C_SkillCast
        {
            CasterId = garen.Info.ObjectId,
            SkillId = (int)SkillType.ESpell,
        });
        garenRoom.Flush();
        Check(garenPackets.OfType<S_Damage>().Count() == 1, "Garen E first hit must be immediate.");
        // Simulate a stalled Room update. Only one overdue chained tick may run;
        // the remaining ticks must stay spaced instead of catching up in one Flush.
        Thread.Sleep(1300);
        garenRoom.Flush();
        Check(garenPackets.OfType<S_Damage>().Count() == 2,
            "Garen E overdue ticks must not burst in one Room Flush.");
        for (int i = 2; i < 6; ++i)
        {
            Thread.Sleep(260);
            garenRoom.Flush();
        }
        List<S_Damage> garenDamage = garenPackets.OfType<S_Damage>().ToList();
        Check(garenDamage.Count == 6, "Garen E must emit exactly six S_Damage packets.");
        Check(garenDamage.All(packet => packet.Damage == 47), "Each Garen E tick must deal 47 damage.");
        Check(garenEnemy.Info.Hp == 718, "Garen E total damage must remain 282.");

        Console.WriteLine("Combat multi-hit tests passed (" + _assertions + " assertions, Annie 10 x 3 and Garen 6 x 47).");
        return _assertions;
    }

    private static void RunCombatGatherTests()
    {
        byte[] cells = new byte[6 * 6];
        var asset = new NavGridAsset(1, "combat-gather-grid", 6, 6, 1.0f,
            NumericsVector3.Zero, 0.5f, 1, new byte[32], cells);
        var config = new NavigationMapConfig
        {
            RoomId = 69990,
            SceneId = 0,
            NavigationMapId = asset.MapId,
            AssetPath = "tests/combat-gather.navgrid",
            FormatVersion = 1,
            ContentHashHex = asset.ContentHashHex,
            Enabled = true,
            Required = true,
        };
        var room = new GameRoom(new NavigationRegistration(config, asset));
        Player requester = Player(69991, 1, 2.5f, 2.5f);
        Player opponent = Player(69992, 2, 5.5f, 5.5f);
        room.AddObject(requester);
        room.AddObject(opponent);
        room.Flush();

        var snapshots = new List<S_MovementSnapshot>();
        room.MovementSnapshotSink = snapshots.Add;
        Check(room.TryGatherPlayersForCombatTest(requester, out string resultMessage),
            "Combat gather must succeed with two registered players.");
        Check(resultMessage.Contains("2 players"), "Combat gather result must report gathered player count.");
        Check(snapshots.Count == 2, "Combat gather must emit one authoritative Snapshot per player.");
        Check(snapshots.All(snapshot => snapshot.ServerMoveId != 0 &&
            snapshot.MovementState == MOVEMENT_SNAPSHOT_STATE.Arrived),
            "Combat gather Snapshots must be final authoritative arrivals.");
        Check(snapshots[0].ServerMoveId != snapshots[1].ServerMoveId,
            "Combat gather ServerMoveIds must be unique.");
        bool requesterInside = asset.TryWorldToCell(
            requester.Info.Position.ToNumericsVector3(), out NavGridCoordinate requesterCell);
        bool opponentInside = asset.TryWorldToCell(
            opponent.Info.Position.ToNumericsVector3(), out NavGridCoordinate opponentCell);
        Check(requesterInside && opponentInside,
            "Gathered positions must remain inside the NavGrid.");
        Check(asset.IsWalkable(requesterCell.X, requesterCell.Z) &&
            asset.IsWalkable(opponentCell.X, opponentCell.Z),
            "Gathered positions must be Walkable.");
        Check(!requesterCell.Equals(opponentCell),
            "Combat test players must not overlap in one Cell.");
        Check(Math.Abs(requesterCell.X - opponentCell.X) + Math.Abs(requesterCell.Z - opponentCell.Z) == 1,
            "Combat test players must be placed in adjacent Cells.");
        Check(requester.Info.Position.Y == 2.0f && opponent.Info.Position.Y == 2.0f,
            "Combat gather must preserve authoritative Y.");
        Check(requester.Info.State == OBJECT_STATE_TYPE.Idle && opponent.Info.State == OBJECT_STATE_TYPE.Idle,
            "Combat gather must reset both players to Idle.");

        var singleConfig = new NavigationMapConfig
        {
            RoomId = 69993,
            SceneId = 0,
            NavigationMapId = asset.MapId,
            AssetPath = "tests/combat-gather.navgrid",
            FormatVersion = 1,
            ContentHashHex = asset.ContentHashHex,
            Enabled = true,
            Required = true,
        };
        var singleRoom = new GameRoom(new NavigationRegistration(singleConfig, asset));
        Player single = Player(69994, 1, 1.5f, 1.5f);
        singleRoom.AddObject(single);
        singleRoom.Flush();
        Check(!singleRoom.TryGatherPlayersForCombatTest(single, out string waitingMessage) &&
            waitingMessage.Contains("at least two"),
            "Combat gather must reject a Room with fewer than two players.");
    }
    private static Player Player(int id, int teamId, float x, float z, int hp = 100)
    {
        return new Player
        {
            Info = new ObjectInfo
            {
                ObjectId = (ulong)id,
                ObjType = OBJECT_TYPE.Player,
                State = OBJECT_STATE_TYPE.Idle,
                TeamId = teamId,
                Hp = hp,
                MaxHp = hp,
                Position = new ProtoVector3 { X = x, Y = 2.0f, Z = z },
            },
        };
    }
}
