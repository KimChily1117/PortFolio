using Google.Protobuf;
using Google.Protobuf.Protocol;
using Server;
using Server.Game.Movement;
using Server.Game.Navigation;
using Server.Game.Objects;
using Server.Game.Room;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using NumericsVector3 = System.Numerics.Vector3;
using ProtoVector3 = Google.Protobuf.Protocol.Vector3;

internal static class MovementPhase5Tests
{
    private static int _assertions;
    private static int _nextId = 50000;

    private sealed class Context
    {
        public GameRoom Room;
        public ClientSession Session;
        public Player Player;
    }

    public static int Run()
    {
        _assertions = 0;
        RunProtocolAndDeltaTests();
        RunMovementAndSnapshotTests();
        RunRateLimitTests();
        RunPopulationMeasurements();
        Console.WriteLine("Phase 5 authoritative path following tests passed (" + _assertions + " assertions).");
        return _assertions;
    }

    private static void Check(bool condition, string message)
    {
        ++_assertions;
        if (!condition) throw new InvalidOperationException(message);
    }

    private static NavGridAsset Asset(int width, int height, byte[] cells = null)
    {
        return new NavGridAsset(1, "phase5-grid", (uint)width, (uint)height, 1.0f,
            NumericsVector3.Zero, 0.5f, 1, new byte[32], cells ?? new byte[width * height]);
    }

    private static Context CreateContext(NavGridAsset asset, int startX = 0, int startZ = 0)
    {
        int id = _nextId++;
        var config = new NavigationMapConfig
        {
            RoomId = id, SceneId = 0, NavigationMapId = asset.MapId,
            AssetPath = "tests/phase5.navgrid", FormatVersion = 1,
            ContentHashHex = asset.ContentHashHex, Enabled = true, Required = true,
        };
        var room = new GameRoom(new NavigationRegistration(config, asset));
        Check(asset.TryGetCellWorldCenter(startX, startZ, out NumericsVector3 start), "Invalid fixture start.");
        var session = new ClientSession { SessionId = id };
        var player = new Player
        {
            Info = new ObjectInfo
            {
                ObjectId = (ulong)id, ObjType = OBJECT_TYPE.Player, State = OBJECT_STATE_TYPE.Idle,
                Hp = 100, Position = new ProtoVector3 { X = start.X, Y = 2.0f, Z = start.Z },
            },
            Session = session,
        };
        session.Player = player;
        session.GameRoom = room;
        room.AddObject(player);
        room.Flush();
        return new Context { Room = room, Session = session, Player = player };
    }

    private static MoveRequestDecision Request(Context context, uint sequence, int goalX, int goalZ)
    {
        Check(context.Room.Navigation.TryGetCellWorldCenter(goalX, goalZ, out NumericsVector3 goal), "Invalid fixture goal.");
        return AuthoritativeMoveService.Process(context.Session, context.Room, context.Player, new C_MoveRequest
        {
            ClientMoveSequence = sequence,
            RequestedDestinationX = goal.X,
            RequestedDestinationZ = goal.Z,
            NavigationMapId = context.Room.NavigationMapId,
            NavigationContentHash = context.Room.NavigationContentHash,
        });
    }

    private static void RunProtocolAndDeltaTests()
    {
        Check((int)MsgId.SMovementSnapshot == 24, "Snapshot packet ID changed.");
        Check((int)MOVE_REJECT_REASON.RateLimited == 18, "RateLimited value changed.");
        var packet = new S_MovementSnapshot
        {
            ObjectId = 1, ServerMoveId = 2, ServerTick = 3, RoomId = 4,
            Position = new ProtoVector3 { X = 5, Y = 6, Z = 7 },
            MovementState = MOVEMENT_SNAPSHOT_STATE.Moving,
            CurrentWaypointIndex = 8, ClientMoveSequence = 9,
        };
        S_MovementSnapshot copy = S_MovementSnapshot.Parser.ParseFrom(packet.ToByteArray());
        Check(copy.ServerMoveId == 2 && copy.ServerTick == 3 && copy.Position.Z == 7 && copy.MovementState == MOVEMENT_SNAPSHOT_STATE.Moving, "Snapshot protobuf round-trip failed.");

        Check(ServerMovementSettings.SanitizeDeltaTime(0.1f, out MovementDeltaTimeStatus normal) == 0.1f && normal == MovementDeltaTimeStatus.Valid, "Normal delta rejected.");
        Check(ServerMovementSettings.SanitizeDeltaTime(0.0f, out MovementDeltaTimeStatus zero) == 0.0f && zero == MovementDeltaTimeStatus.Valid, "Zero delta policy changed.");
        Check(ServerMovementSettings.SanitizeDeltaTime(-1.0f, out MovementDeltaTimeStatus negative) == 0.0f && negative == MovementDeltaTimeStatus.Invalid, "Negative delta must be ignored.");
        Check(ServerMovementSettings.SanitizeDeltaTime(float.NaN, out MovementDeltaTimeStatus nan) == 0.0f && nan == MovementDeltaTimeStatus.Invalid, "NaN delta must be ignored.");
        Check(ServerMovementSettings.SanitizeDeltaTime(float.PositiveInfinity, out MovementDeltaTimeStatus infinity) == 0.0f && infinity == MovementDeltaTimeStatus.Invalid, "Infinite delta must be ignored.");
        Check(ServerMovementSettings.SanitizeDeltaTime(5.0f, out MovementDeltaTimeStatus clamped) == 0.25f && clamped == MovementDeltaTimeStatus.Clamped, "Large delta must clamp to 250ms.");
    }

    private static void RunMovementAndSnapshotTests()
    {
        Context partial = CreateContext(Asset(6, 1));
        var snapshots = new List<S_MovementSnapshot>();
        partial.Room.MovementSnapshotSink = snapshots.Add;
        Check(Request(partial, 1, 3, 0).Accepted, "Partial movement request failed.");
        partial.Room.Update(0.1f);
        Check(Math.Abs(partial.Player.Info.Position.X - 0.7f) < 0.0001f, "Default speed must move 0.2 units in 100ms.");
        Check(partial.Player.Info.Position.Y == 2.0f, "Server Y authority must be preserved.");
        Check(snapshots.Count == 1 && snapshots[0].MovementState == MOVEMENT_SNAPSHOT_STATE.Moving, "Moving snapshot missing.");
        Check(snapshots[0].ServerMoveId == partial.Player.MovementState.ServerMoveId && snapshots[0].ObjectId == partial.Player.Info.ObjectId, "Snapshot identity mismatch.");

        Context multi = CreateContext(Asset(8, 1));
        Check(multi.Player.TrySetMovementSpeed(10.0f), "Valid speed rejected.");
        var multiSnapshots = new List<S_MovementSnapshot>();
        multi.Room.MovementSnapshotSink = multiSnapshots.Add;
        Check(Request(multi, 1, 6, 0).Accepted, "Multi-waypoint request failed.");
        multi.Room.Update(0.25f);
        Check(Math.Abs(multi.Player.Info.Position.X - 3.0f) < 0.0001f, "One tick must spend remaining distance across multiple waypoints.");
        Check(multi.Player.MovementState.CurrentWaypointIndex == 3, "Waypoint index did not advance across multiple Cells.");

        Context arrived = CreateContext(Asset(3, 1));
        Check(arrived.Player.TrySetMovementSpeed(10.0f), "Arrival speed rejected.");
        var arrivalSnapshots = new List<S_MovementSnapshot>();
        arrived.Room.MovementSnapshotSink = arrivalSnapshots.Add;
        MoveRequestDecision arrivalDecision = Request(arrived, 1, 2, 0);
        arrived.Room.Update(0.25f);
        Check(!arrived.Player.MovementState.IsActive && arrived.Player.Info.State == OBJECT_STATE_TYPE.Idle, "Arrival must complete movement and restore Idle.");
        Check(arrived.Player.Info.Position.X == arrivalDecision.AcceptedDestination.X && arrived.Player.Info.Position.Y == 2.0f, "Arrival must snap to approved destination.");
        Check(arrivalSnapshots.Count == 1 && arrivalSnapshots[0].MovementState == MOVEMENT_SNAPSHOT_STATE.Arrived, "Final arrival snapshot missing or duplicated.");
        arrived.Room.Update(0.1f);
        Check(arrived.Room.LastMovementSnapshotCount == 0, "Stationary Player must not repeat snapshots.");

        Context same = CreateContext(Asset(2, 1));
        var sameSnapshots = new List<S_MovementSnapshot>();
        same.Room.MovementSnapshotSink = sameSnapshots.Add;
        Check(Request(same, 1, 0, 0).Accepted, "Single Cell request failed.");
        same.Room.Update(0.0f);
        Check(sameSnapshots.Count == 1 && sameSnapshots[0].MovementState == MOVEMENT_SNAPSHOT_STATE.Arrived, "Single Cell path must arrive on first tick even at zero delta.");

        byte[] slowCells = { (byte)NavCellType.Walkable, (byte)NavCellType.Slow };
        Context slow = CreateContext(Asset(2, 1, slowCells));
        Check(Request(slow, 1, 1, 0).Accepted, "Slow destination request failed.");
        slow.Room.MovementSnapshotSink = _ => { };
        slow.Room.Update(0.1f);
        Check(Math.Abs(slow.Player.Info.Position.X - 0.6f) < 0.0001f, "Slow target segment must apply 50% actual speed.");

        Context cancelled = CreateContext(Asset(4, 1));
        var cancelledSnapshots = new List<S_MovementSnapshot>();
        cancelled.Room.MovementSnapshotSink = cancelledSnapshots.Add;
        Check(Request(cancelled, 1, 3, 0).Accepted, "Cancellation fixture request failed.");
        cancelled.Room.CancelPlayerMovement(cancelled.Player);
        Check(!cancelled.Player.MovementState.IsActive && cancelledSnapshots.Count == 1 && cancelledSnapshots[0].MovementState == MOVEMENT_SNAPSHOT_STATE.Cancelled, "Cancellation must preserve position and emit one stop snapshot.");

        Context clampedMove = CreateContext(Asset(4, 1));
        clampedMove.Room.MovementSnapshotSink = _ => { };
        Check(Request(clampedMove, 1, 3, 0).Accepted, "Clamp fixture request failed.");
        clampedMove.Room.Update(10.0f);
        Check(clampedMove.Room.LastMovementDeltaTimeStatus == MovementDeltaTimeStatus.Clamped && Math.Abs(clampedMove.Player.Info.Position.X - 1.0f) < 0.0001f, "Debugger-sized delta moved farther than the 250ms clamp.");

        Check(!partial.Player.TrySetMovementSpeed(-1.0f) && !partial.Player.TrySetMovementSpeed(float.NaN) && !partial.Player.TrySetMovementSpeed(51.0f), "Invalid server speed accepted.");
    }

    private static void RunRateLimitTests()
    {
        var session = new ClientSession();
        session.ResetMoveSequence();
        long now = Stopwatch.GetTimestamp();
        Check(session.TryConsumeMoveRequestBudget(now), "Burst token 1 rejected.");
        Check(session.TryConsumeMoveRequestBudget(now), "Burst token 2 rejected.");
        Check(session.TryConsumeMoveRequestBudget(now), "Burst token 3 rejected.");
        Check(session.TryConsumeMoveRequestBudget(now), "Burst token 4 rejected.");
        Check(!session.TryConsumeMoveRequestBudget(now), "Burst overflow must be rejected.");
        long afterRefill = now + Stopwatch.Frequency;
        Check(session.TryConsumeMoveRequestBudget(afterRefill), "Rate limit did not recover after refill.");

        Context context = CreateContext(Asset(3, 1));
        PlayerMovementState original = null;
        for (uint sequence = 1; sequence <= 5; ++sequence)
        {
            MoveRequestDecision decision = Request(context, sequence, sequence % 2 == 0 ? 0 : 1, 0);
            if (sequence == 1) original = context.Player.MovementState;
            if (sequence == 5)
            {
                Check(!decision.Accepted && decision.RejectReason == MOVE_REJECT_REASON.RateLimited, "Fifth immediate MoveRequest must be rate limited.");
                Check(context.Player.MovementState.IsActive, "Rate limit rejection must not remove active movement.");
            }
        }
    }

    private static void RunPopulationMeasurements()
    {
        foreach (int count in new[] { 1, 10, 50, 100 })
        {
            const int size = 145;
            NavGridAsset asset = Asset(size, size);
            int roomId = _nextId++;
            var config = new NavigationMapConfig { RoomId = roomId, SceneId = 0, NavigationMapId = asset.MapId, AssetPath = "tests/perf.navgrid", FormatVersion = 1, ContentHashHex = asset.ContentHashHex, Enabled = true, Required = true };
            var room = new GameRoom(new NavigationRegistration(config, asset));
            var pathfinder = new NavGridPathfinder();
            NavGridPath path = pathfinder.FindPath(asset, new NavGridCoordinate(0, 0), new NavGridCoordinate(144, 144)).Path;
            Check(asset.TryGetCellWorldCenter(0, 0, out NumericsVector3 start), "Performance start invalid.");
            Check(asset.TryGetCellWorldCenter(144, 144, out NumericsVector3 destination), "Performance destination invalid.");
            destination.Y = 2.0f;
            int snapshotCount = 0;
            int snapshotBytes = 0;
            room.MovementSnapshotSink = snapshot => { snapshotCount++; snapshotBytes += snapshot.CalculateSize() + 4; };
            for (int i = 0; i < count; ++i)
            {
                int id = _nextId++;
                var session = new ClientSession { SessionId = id };
                var player = new Player { Info = new ObjectInfo { ObjectId = (ulong)id, ObjType = OBJECT_TYPE.Player, State = OBJECT_STATE_TYPE.Idle, Hp = 100, Position = new ProtoVector3 { X = start.X, Y = 2.0f, Z = start.Z } }, Session = session };
                session.Player = player; session.GameRoom = room;
                room.AddObject(player);
                room.Flush();
                player.ReplaceMovementState(new PlayerMovementState((ulong)(i + 1), 1, new NumericsVector3(start.X, 2.0f, start.Z), destination, new NavGridCoordinate(144, 144), path, DateTime.UtcNow.Ticks));
            }
            long beforeAllocation = GC.GetAllocatedBytesForCurrentThread();
            var stopwatch = Stopwatch.StartNew();
            room.Update(0.1f);
            stopwatch.Stop();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - beforeAllocation;
            Check(snapshotCount == count, "Moving Player snapshot count mismatch for population " + count + ".");
            double broadcastBytesPerSecond = snapshotBytes * (double)count * 10.0;
            Console.WriteLine("[Movement benchmark] players=" + count +
                " roomUpdateMs=" + stopwatch.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                " snapshots=" + snapshotCount + " payloadBytes=" + snapshotBytes +
                " estimatedBroadcastBytesPerSecond=" + broadcastBytesPerSecond.ToString("F0", System.Globalization.CultureInfo.InvariantCulture) +
                " threadAllocatedBytes=" + allocated);
        }
    }
}