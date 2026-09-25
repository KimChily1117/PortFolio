using Google.Protobuf;
using Google.Protobuf.Protocol;
using Server;
using Server.Game.Movement;
using Server.Game.Navigation;
using Server.Game.Objects;
using Server.Game.Room;
using System;
using System.Diagnostics;
using NumericsVector3 = System.Numerics.Vector3;
using System.Text;
using ProtoVector3 = Google.Protobuf.Protocol.Vector3;

internal static class PathfindingPhase4Tests
{
    private static int _assertions;
    private static int _nextObjectId = 10000;

    private sealed class Context
    {
        public GameRoom Room;
        public ClientSession Session;
        public Player Player;
    }

    public static int Run()
    {
        _assertions = 0;
        RunBasicPathTests();
        RunObstacleAndCostTests();
        RunLimitAndValidationTests();
        RunMoveRequestIntegrationTests();
        RunPerformanceMeasurements();
        Console.WriteLine("Phase 4 pathfinding tests passed (" + _assertions + " assertions).");
        return _assertions;
    }

    private static void Check(bool condition, string message)
    {
        ++_assertions;
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static NavGridAsset Asset(int width, int height, params string[] rows)
    {
        if (rows.Length != height)
            throw new ArgumentException("Fixture row count does not match height.");
        var cells = new byte[checked(width * height)];
        for (int z = 0; z < height; ++z)
        {
            if (rows[z].Length != width)
                throw new ArgumentException("Fixture row width mismatch at z=" + z + ".");
            for (int x = 0; x < width; ++x)
            {
                char cell = rows[z][x];
                cells[z * width + x] = cell == '#' ? (byte)NavCellType.Blocked :
                    cell == 's' ? (byte)NavCellType.Slow : (byte)NavCellType.Walkable;
            }
        }
        return Asset(width, height, cells);
    }

    private static NavGridAsset Asset(int width, int height, byte[] cells)
    {
        return new NavGridAsset(
            1,
            "phase4-grid",
            (uint)width,
            (uint)height,
            1.0f,
            NumericsVector3.Zero,
            0.5f,
            1,
            new byte[32],
            cells);
    }

    private static Context CreateContext(NavGridAsset asset, NavGridCoordinate start)
    {
        var config = new NavigationMapConfig
        {
            RoomId = _nextObjectId,
            SceneId = 0,
            NavigationMapId = asset.MapId,
            AssetPath = "tests/phase4.navgrid",
            FormatVersion = asset.FormatVersion,
            ContentHashHex = asset.ContentHashHex,
            Enabled = true,
            Required = true,
        };
        var room = new GameRoom(new NavigationRegistration(config, asset));
        int objectId = _nextObjectId++;
        Check(asset.TryGetCellWorldCenter(start.X, start.Z, out NumericsVector3 position), "Fixture start Cell is invalid.");
        var session = new ClientSession { SessionId = objectId };
        var player = new Player
        {
            Info = new ObjectInfo
            {
                ObjectId = (ulong)objectId,
                State = OBJECT_STATE_TYPE.Idle,
                ObjType = OBJECT_TYPE.Player,
                Hp = 100,
                Position = new ProtoVector3 { X = position.X, Y = 2.0f, Z = position.Z },
            },
            Session = session,
        };
        session.GameRoom = room;
        session.Player = player;
        room.AddObject(player);
        room.Flush();
        Check(room.ContainsPlayer(player) && object.ReferenceEquals(player.Room, room), "Fixture Room ownership failed.");
        return new Context { Room = room, Session = session, Player = player };
    }

    private static C_MoveRequest Request(Context context, uint sequence, NavGridCoordinate goal)
    {
        Check(context.Room.Navigation.TryGetCellWorldCenter(goal.X, goal.Z, out NumericsVector3 center), "Fixture goal Cell is invalid.");
        return Request(context, sequence, center.X, center.Z);
    }

    private static C_MoveRequest Request(Context context, uint sequence, float x, float z)
    {
        return new C_MoveRequest
        {
            ClientMoveSequence = sequence,
            RequestedDestinationX = x,
            RequestedDestinationZ = z,
            NavigationMapId = context.Room.NavigationMapId,
            NavigationContentHash = context.Room.NavigationContentHash,
        };
    }

    private static string PathText(NavGridPath path)
    {
        var builder = new StringBuilder();
        for (int i = 0; i < path.Cells.Count; ++i)
        {
            if (i != 0) builder.Append('|');
            builder.Append(path.Cells[i].ToString());
        }
        return builder.ToString();
    }

    private static void RunProtocolCompatibilityTests()
    {
        Check((int)MOVE_REJECT_REASON.ServerError == 13, "Existing RejectReason numeric value changed.");
        Check((int)MOVE_REJECT_REASON.NoPath == 14, "NoPath enum value mismatch.");
        Check((int)MOVE_REJECT_REASON.PathSearchLimitExceeded == 15, "PathSearchLimitExceeded enum value mismatch.");
        Check((int)MOVE_REJECT_REASON.PathTooLong == 16, "PathTooLong enum value mismatch.");
        Check((int)MOVE_REJECT_REASON.InvalidNavigationStart == 17, "InvalidNavigationStart enum value mismatch.");
        var rejected = new S_MoveRejected { ClientMoveSequence = 7, RejectReason = MOVE_REJECT_REASON.NoPath };
        S_MoveRejected roundTrip = S_MoveRejected.Parser.ParseFrom(rejected.ToByteArray());
        Check(roundTrip.ClientMoveSequence == 7 && roundTrip.RejectReason == MOVE_REJECT_REASON.NoPath, "Phase 4 RejectReason protobuf round-trip failed.");
    }
    private static void RunBasicPathTests()
    {
        var pathfinder = new NavGridPathfinder();
        NavGridAsset line = Asset(5, 1, ".....");
        NavGridPathResult straight = pathfinder.FindPath(line, new NavGridCoordinate(0, 0), new NavGridCoordinate(4, 0));
        Check(straight.Success, "Straight path must succeed.");
        Check(straight.Path.Cells.Count == 5, "Straight path must include start and goal.");
        Check(straight.Path.Cells[0].Equals(new NavGridCoordinate(0, 0)), "Path start inclusion changed.");
        Check(straight.Path.Cells[4].Equals(new NavGridCoordinate(4, 0)), "Path goal inclusion changed.");
        Check(straight.Path.TotalCost == 40, "Straight path cost must be 40.");

        NavGridAsset diagonalGrid = Asset(3, 3, "...", "...", "...");
        NavGridPathResult diagonal = pathfinder.FindPath(diagonalGrid, new NavGridCoordinate(0, 0), new NavGridCoordinate(2, 2));
        Check(diagonal.Success && diagonal.Path.Cells.Count == 3, "Diagonal path must use two diagonal steps.");
        Check(diagonal.Path.TotalCost == 28, "Diagonal path cost must be 28.");
        Check(NavGridPathfinder.OctileDistance(new NavGridCoordinate(0, 0), new NavGridCoordinate(4, 2)) == 48, "Octile heuristic formula changed.");

        NavGridPathResult same = pathfinder.FindPath(line, new NavGridCoordinate(2, 0), new NavGridCoordinate(2, 0));
        Check(same.Success && same.Path.Cells.Count == 1 && same.Path.TotalCost == 0, "Same Cell path policy must be one Cell at zero cost.");
    }

    private static void RunObstacleAndCostTests()
    {
        var pathfinder = new NavGridPathfinder();
        NavGridAsset obstacle = Asset(5, 3, "..#..", "..#..", ".....");
        NavGridPathResult around = pathfinder.FindPath(obstacle, new NavGridCoordinate(0, 0), new NavGridCoordinate(4, 0));
        Check(around.Success, "A path must route around a finite wall.");
        Check(around.Path.TotalCost == 68, "Obstacle detour cost changed.");
        for (int i = 0; i < around.Path.Cells.Count; ++i)
            Check(obstacle.IsWalkable(around.Path.Cells[i].X, around.Path.Cells[i].Z), "Path crossed a Blocked Cell.");

        NavGridAsset noPath = Asset(3, 2, ".#.", "###");
        Check(pathfinder.FindPath(noPath, new NavGridCoordinate(0, 0), new NavGridCoordinate(2, 0)).Status == NavGridPathStatus.NoPath, "Disconnected islands must report NoPath.");

        NavGridAsset corner = Asset(2, 2, ".#", "#.");
        Check(pathfinder.FindPath(corner, new NavGridCoordinate(0, 0), new NavGridCoordinate(1, 1)).Status == NavGridPathStatus.NoPath, "Diagonal corner cutting must be forbidden.");

        NavGridAsset narrow = Asset(5, 3, "#####", ".....", "#####");
        NavGridPathResult corridor = pathfinder.FindPath(narrow, new NavGridCoordinate(0, 1), new NavGridCoordinate(4, 1));
        Check(corridor.Success && corridor.Path.TotalCost == 40, "One-Cell corridor must remain traversable.");

        NavGridAsset slow = Asset(5, 3, ".....", ".sss.", ".....");
        NavGridPathResult slowAvoidance = pathfinder.FindPath(slow, new NavGridCoordinate(0, 1), new NavGridCoordinate(4, 1));
        Check(slowAvoidance.Success && slowAvoidance.Path.TotalCost == 48, "Walkable detour should beat the Slow straight route.");
        Check(!PathText(slowAvoidance.Path).Contains("2:1"), "Deterministic low-cost path should avoid the central Slow Cell.");
        NavGridPathResult enterSlow = pathfinder.FindPath(Asset(2, 1, ".s"), new NavGridCoordinate(0, 0), new NavGridCoordinate(1, 0));
        Check(enterSlow.Success && enterSlow.Path.TotalCost == 20, "Slow cost must apply when entering the next Cell.");

        string expected = PathText(pathfinder.FindPath(obstacle, new NavGridCoordinate(0, 0), new NavGridCoordinate(4, 0)).Path);
        for (int i = 0; i < 20; ++i)
            Check(PathText(pathfinder.FindPath(obstacle, new NavGridCoordinate(0, 0), new NavGridCoordinate(4, 0)).Path) == expected, "A* tie-break must be deterministic.");
    }

    private static void RunLimitAndValidationTests()
    {
        var pathfinder = new NavGridPathfinder();
        NavGridAsset line = Asset(5, 1, ".....");
        Check(pathfinder.FindPath(line, new NavGridCoordinate(-1, 0), new NavGridCoordinate(4, 0)).Status == NavGridPathStatus.InvalidStart, "Invalid start must be rejected.");
        Check(pathfinder.FindPath(line, new NavGridCoordinate(0, 0), new NavGridCoordinate(5, 0)).Status == NavGridPathStatus.InvalidGoal, "Invalid goal must be rejected.");

        NavGridAsset blockedEnds = Asset(3, 1, "#.#");
        Check(pathfinder.FindPath(blockedEnds, new NavGridCoordinate(0, 0), new NavGridCoordinate(1, 0)).Status == NavGridPathStatus.BlockedStart, "Blocked start must be rejected.");
        Check(pathfinder.FindPath(blockedEnds, new NavGridCoordinate(1, 0), new NavGridCoordinate(2, 0)).Status == NavGridPathStatus.BlockedGoal, "Blocked goal must be rejected.");

        var nodeLimit = new NavGridPathOptions { MaxExpandedNodes = 1, MaxPathCells = 100 };
        Check(pathfinder.FindPath(line, new NavGridCoordinate(0, 0), new NavGridCoordinate(4, 0), nodeLimit).Status == NavGridPathStatus.SearchLimitExceeded, "Expansion limit must have a distinct result.");
        var lengthLimit = new NavGridPathOptions { MaxExpandedNodes = 100, MaxPathCells = 3 };
        Check(pathfinder.FindPath(line, new NavGridCoordinate(0, 0), new NavGridCoordinate(4, 0), lengthLimit).Status == NavGridPathStatus.PathTooLong, "Path length limit must have a distinct result.");
    }

    private static void RunMoveRequestIntegrationTests()
    {
        NavGridAsset open = Asset(5, 3, ".....", ".....", ".....");
        Context success = CreateContext(open, new NavGridCoordinate(0, 1));
        ProtoVector3 original = success.Player.Info.Position.Clone();
        MoveRequestDecision accepted = AuthoritativeMoveService.Process(success.Session, success.Room, success.Player, Request(success, 1, new NavGridCoordinate(4, 1)));
        Check(accepted.Accepted, "MoveRequest must be accepted only after A* succeeds.");
        Check(success.Player.MovementState.Path.Cells.Count == 5, "MovementState must retain the authoritative path.");
        Check(success.Player.MovementState.Path.StartCell.Equals(new NavGridCoordinate(0, 1)), "MovementState Path start mismatch.");
        Check(success.Player.MovementState.Path.GoalCell.Equals(success.Player.MovementState.DestinationCell), "MovementState destination and Path goal mismatch.");
        Check(success.Player.MovementState.CurrentWaypointIndex == 1, "Next waypoint index must begin after the start Cell.");
        Check(success.Player.Info.Position.X == original.X && success.Player.Info.Position.Y == original.Y && success.Player.Info.Position.Z == original.Z, "A* acceptance must not move Player position.");

        NavGridAsset islands = Asset(5, 3, "..#..", "..#..", "..#..");
        Context failure = CreateContext(islands, new NavGridCoordinate(0, 1));
        MoveRequestDecision initial = AuthoritativeMoveService.Process(failure.Session, failure.Room, failure.Player, Request(failure, 1, new NavGridCoordinate(1, 1)));
        Check(initial.Accepted, "Initial command on the connected island must succeed.");
        PlayerMovementState oldState = failure.Player.MovementState;
        MoveRequestDecision noPath = AuthoritativeMoveService.Process(failure.Session, failure.Room, failure.Player, Request(failure, 2, new NavGridCoordinate(4, 1)));
        Check(!noPath.Accepted && noPath.RejectReason == MOVE_REJECT_REASON.NoPath, "Unreachable Walkable goal must return NoPath.");
        Check(object.ReferenceEquals(failure.Player.MovementState, oldState) && oldState.IsActive, "Failed replacement must preserve the previous MovementState.");
        Check(failure.Session.LastProcessedMoveSequence == 2, "A failed path request must still consume its Sequence.");
        Check(AuthoritativeMoveService.Process(failure.Session, failure.Room, failure.Player, Request(failure, 2, new NavGridCoordinate(1, 2))).RejectReason == MOVE_REJECT_REASON.InvalidSequence, "Failed request Sequence must not be replayable.");

        Context limited = CreateContext(open, new NavGridCoordinate(0, 1));
        MoveRequestDecision limitDecision = AuthoritativeMoveService.Process(
            limited.Session, limited.Room, limited.Player, Request(limited, 1, new NavGridCoordinate(4, 1)),
            new NavGridPathOptions { MaxExpandedNodes = 1, MaxPathCells = 100 });
        Check(limitDecision.RejectReason == MOVE_REJECT_REASON.PathSearchLimitExceeded && limited.Player.MovementState == null, "Search limit must reject without creating MovementState.");

        Context tooLong = CreateContext(open, new NavGridCoordinate(0, 1));
        MoveRequestDecision longDecision = AuthoritativeMoveService.Process(
            tooLong.Session, tooLong.Room, tooLong.Player, Request(tooLong, 1, new NavGridCoordinate(4, 1)),
            new NavGridPathOptions { MaxExpandedNodes = 100, MaxPathCells = 3 });
        Check(longDecision.RejectReason == MOVE_REJECT_REASON.PathTooLong && tooLong.Player.MovementState == null, "Path length limit must reject without creating MovementState.");

        NavGridAsset invalidStartGrid = Asset(3, 1, "#..");
        Context invalidStart = CreateContext(invalidStartGrid, new NavGridCoordinate(0, 0));
        MoveRequestDecision invalidStartDecision = AuthoritativeMoveService.Process(invalidStart.Session, invalidStart.Room, invalidStart.Player, Request(invalidStart, 1, new NavGridCoordinate(2, 0)));
        Check(invalidStartDecision.RejectReason == MOVE_REJECT_REASON.InvalidNavigationStart, "Blocked authoritative start must be a distinct rejection.");

        Context outsideStart = CreateContext(open, new NavGridCoordinate(0, 1));
        outsideStart.Player.Info.Position.X = -0.5f;
        MoveRequestDecision outsideStartDecision = AuthoritativeMoveService.Process(outsideStart.Session, outsideStart.Room, outsideStart.Player, Request(outsideStart, 1, new NavGridCoordinate(4, 1)));
        Check(outsideStartDecision.RejectReason == MOVE_REJECT_REASON.InvalidNavigationStart, "Out-of-bounds authoritative start must be rejected as a server state error.");

        NavGridAsset corrected = Asset(5, 3, "..#..", ".....", ".....");
        Context correctedContext = CreateContext(corrected, new NavGridCoordinate(0, 0));
        MoveRequestDecision correctedDecision = AuthoritativeMoveService.Process(correctedContext.Session, correctedContext.Room, correctedContext.Player, Request(correctedContext, 1, new NavGridCoordinate(2, 0)));
        Check(correctedDecision.Accepted && correctedDecision.WasDestinationAdjusted, "Reachable adjusted destination must be accepted after A*.");
        Check(correctedContext.Player.MovementState.Path.WasDestinationAdjusted, "Path must retain destination adjustment metadata.");

        NavGridAsset correctedIsland = Asset(5, 3, "..#..", "..##.", "..#..");
        Context correctedFailure = CreateContext(correctedIsland, new NavGridCoordinate(0, 1));
        MoveRequestDecision correctedNoPath = AuthoritativeMoveService.Process(correctedFailure.Session, correctedFailure.Room, correctedFailure.Player, Request(correctedFailure, 1, new NavGridCoordinate(3, 1)));
        Check(!correctedNoPath.Accepted && correctedNoPath.RejectReason == MOVE_REJECT_REASON.NoPath, "Adjusted but disconnected destination must be rejected by A*.");
    }

    private static void RunPerformanceMeasurements()
    {
        const int size = 145;
        var pathfinder = new NavGridPathfinder();
        var openCells = new byte[size * size];
        Measure("open-diagonal", pathfinder, Asset(size, size, openCells), new NavGridCoordinate(0, 0), new NavGridCoordinate(144, 144), NavGridPathStatus.Success);

        var obstacleCells = new byte[size * size];
        for (int z = 0; z < size; ++z)
            if (z != 120) obstacleCells[z * size + 72] = (byte)NavCellType.Blocked;
        Measure("partial-wall", pathfinder, Asset(size, size, obstacleCells), new NavGridCoordinate(0, 0), new NavGridCoordinate(144, 144), NavGridPathStatus.Success);

        var noPathCells = new byte[size * size];
        for (int z = 0; z < size; ++z)
            noPathCells[z * size + 72] = (byte)NavCellType.Blocked;
        Measure("no-path-wall", pathfinder, Asset(size, size, noPathCells), new NavGridCoordinate(0, 0), new NavGridCoordinate(144, 144), NavGridPathStatus.NoPath);
    }

    private static void Measure(string name, NavGridPathfinder pathfinder, NavGridAsset asset, NavGridCoordinate start, NavGridCoordinate goal, NavGridPathStatus expected)
    {
        var stopwatch = Stopwatch.StartNew();
        NavGridPathResult result = pathfinder.FindPath(asset, start, goal, new NavGridPathOptions
        {
            MaxExpandedNodes = 25000,
            MaxPathCells = 4096,
        });
        stopwatch.Stop();
        Check(result.Status == expected, "Performance fixture " + name + " returned " + result.Status + ".");
        Console.WriteLine("[Pathfinding benchmark] " + name +
            " status=" + result.Status +
            " expanded=" + result.ExpandedNodeCount +
            " pathCells=" + (result.Path == null ? 0 : result.Path.Cells.Count) +
            " elapsedMs=" + stopwatch.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
    }
}
