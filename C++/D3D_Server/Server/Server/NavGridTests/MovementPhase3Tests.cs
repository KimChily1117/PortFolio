using Google.Protobuf;
using Google.Protobuf.Protocol;
using Server;
using Server.Game.Movement;
using Server.Game.Navigation;
using Server.Game.Objects;
using Server.Game.Room;
using System;
using System.IO;
using NumericsVector3 = System.Numerics.Vector3;
using ProtoVector3 = Google.Protobuf.Protocol.Vector3;

internal static class MovementPhase3Tests
{
    private const string GoldenHash = "d74739cc31aa532815ce2d3551d58962fc430e9e871df4fd374f8f2f26480a77";
    private static int _assertions;
    private static int _nextObjectId = 100;

    private sealed class Context
    {
        public GameRoom Room;
        public ClientSession Session;
        public Player Player;
    }

    public static int Run(string goldenPath)
    {
        _assertions = 0;
        NavigationRegistration registration = BuildRegistration(goldenPath);
        RunProtocolTests();
        RunAuthorityAndSequenceTests(registration);
        RunCoordinateAndNavigationTests(registration);
        RunMovementStateTests(registration);
        Console.WriteLine("Phase 3 authoritative movement tests passed (" + _assertions + " assertions).");
        return _assertions;
    }

    private static void Check(bool condition, string message)
    {
        ++_assertions;
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static NavigationRegistration BuildRegistration(string goldenPath)
    {
        string root = Path.Combine(Path.GetTempPath(), "D3D-MovePhase3-" + Guid.NewGuid().ToString("N"));
        string navDirectory = Path.Combine(root, "Navigation");
        Directory.CreateDirectory(navDirectory);
        File.Copy(goldenPath, Path.Combine(navDirectory, "golden-grid-v1.navgrid"));
        var config = new NavigationMapConfig
        {
            RoomId = 0,
            SceneId = 0,
            NavigationMapId = "golden-grid-v1",
            AssetPath = "Navigation/golden-grid-v1.navgrid",
            FormatVersion = 1,
            ContentHashHex = GoldenHash,
            Enabled = true,
            Required = true,
        };
        NavigationRegistryLoadResult result = NavigationRegistry.Build(root, new[] { config });
        Check(result.Success, "Phase 3 fixture Registry failed: " + result.Error);
        Check(result.Registry.TryGetRegistrationByRoomId(0, out NavigationRegistration registration), "Phase 3 fixture registration missing.");
        return registration;
    }

    private static Context CreateContext(NavigationRegistration registration, NumericsVector3 position)
    {
        int objectId = _nextObjectId++;
        var room = new GameRoom(registration);
        var session = new ClientSession { SessionId = objectId };
        var player = new Player
        {
            Info = new ObjectInfo
            {
                ObjectId = (ulong)objectId,
                State = OBJECT_STATE_TYPE.Idle,
                ObjType = OBJECT_TYPE.Player,
                Hp = 100,
                Position = new ProtoVector3 { X = position.X, Y = position.Y, Z = position.Z },
            },
            Session = session,
        };
        session.GameRoom = room;
        session.Player = player;
        room.AddObject(player);
        room.Flush();
        Check(room.ContainsPlayer(player), "Fixture Player must be registered in Room.");
        Check(object.ReferenceEquals(player.Room, room), "Fixture Player Room ownership mismatch.");
        return new Context { Room = room, Session = session, Player = player };
    }

    private static C_MoveRequest Request(uint sequence, float x, float z, string mapId = "golden-grid-v1", string hash = GoldenHash)
    {
        return new C_MoveRequest
        {
            ClientMoveSequence = sequence,
            RequestedDestinationX = x,
            RequestedDestinationZ = z,
            NavigationMapId = mapId,
            NavigationContentHash = hash,
        };
    }

    private static void RunProtocolTests()
    {
        Check(C_MoveRequest.Descriptor.FindFieldByName("objectId") == null, "C_MoveRequest must not expose ObjectId.");
        var request = Request(7, -1.25f, 0.75f);
        C_MoveRequest requestRoundTrip = C_MoveRequest.Parser.ParseFrom(request.ToByteArray());
        Check(requestRoundTrip.ClientMoveSequence == 7 && requestRoundTrip.RequestedDestinationX == -1.25f && requestRoundTrip.RequestedDestinationZ == 0.75f, "MoveRequest round-trip failed.");
        Check(requestRoundTrip.NavigationMapId == "golden-grid-v1" && requestRoundTrip.NavigationContentHash == GoldenHash, "MoveRequest Navigation identity round-trip failed.");

        var accepted = new S_MoveAccepted
        {
            ClientMoveSequence = 7,
            ServerMoveId = 99,
            NavigationMapId = "golden-grid-v1",
            NavigationContentHash = GoldenHash,
            RequestedDestination = new ProtoVector3 { X = 1, Y = 2, Z = 3 },
            AcceptedDestination = new ProtoVector3 { X = 4, Y = 5, Z = 6 },
            ServerStartPosition = new ProtoVector3 { X = 7, Y = 8, Z = 9 },
            WasDestinationAdjusted = true,
        };
        S_MoveAccepted acceptedRoundTrip = S_MoveAccepted.Parser.ParseFrom(accepted.ToByteArray());
        Check(acceptedRoundTrip.ServerMoveId == 99 && acceptedRoundTrip.WasDestinationAdjusted, "MoveAccepted round-trip failed.");
        Check(acceptedRoundTrip.AcceptedDestination.X == 4 && acceptedRoundTrip.ServerStartPosition.Z == 9, "MoveAccepted vectors were not preserved.");

        var rejected = new S_MoveRejected
        {
            ClientMoveSequence = 8,
            RejectReason = MOVE_REJECT_REASON.NavigationHashMismatch,
            ServerPosition = new ProtoVector3 { X = 1, Y = 2, Z = 3 },
            NavigationMapId = "golden-grid-v1",
            NavigationContentHash = GoldenHash,
        };
        S_MoveRejected rejectedRoundTrip = S_MoveRejected.Parser.ParseFrom(rejected.ToByteArray());
        Check(rejectedRoundTrip.RejectReason == MOVE_REJECT_REASON.NavigationHashMismatch && rejectedRoundTrip.ServerPosition.Y == 2, "MoveRejected round-trip failed.");
        Check((int)MsgId.CMoveRequest == 21 && (int)MsgId.SMoveAccepted == 22 && (int)MsgId.SMoveRejected == 23, "Phase 3 packet IDs changed unexpectedly.");
    }

    private static void RunAuthorityAndSequenceTests(NavigationRegistration registration)
    {
        Context context = CreateContext(registration, new NumericsVector3(-1.5f, 2.0f, -0.5f));
        MoveRequestDecision first = AuthoritativeMoveService.Process(context.Session, context.Room, context.Player, Request(1, -1.5f, -0.5f));
        Check(first.Accepted, "First sequence must be accepted.");
        Check(context.Session.LastProcessedMoveSequence == 1, "First sequence was not consumed.");
        MoveRequestDecision duplicate = AuthoritativeMoveService.Process(context.Session, context.Room, context.Player, Request(1, -0.5f, -0.5f));
        Check(!duplicate.Accepted && duplicate.RejectReason == MOVE_REJECT_REASON.InvalidSequence, "Duplicate sequence must be rejected.");
        MoveRequestDecision older = AuthoritativeMoveService.Process(context.Session, context.Room, context.Player, Request(0, -0.5f, -0.5f));
        Check(!older.Accepted && older.RejectReason == MOVE_REJECT_REASON.InvalidSequence, "Zero/older sequence must be rejected.");
        MoveRequestDecision next = AuthoritativeMoveService.Process(context.Session, context.Room, context.Player, Request(2, -0.5f, -0.5f));
        Check(next.Accepted, "Increasing sequence must be accepted.");

        Context newSession = CreateContext(registration, new NumericsVector3(-1.5f, 2.0f, -0.5f));
        Check(AuthoritativeMoveService.Process(newSession.Session, newSession.Room, newSession.Player, Request(1, -1.5f, -0.5f)).Accepted, "A new session must start with a fresh sequence.");

        Context departed = CreateContext(registration, new NumericsVector3(-1.5f, 2.0f, -0.5f));
        departed.Session.GameRoom = null;
        MoveRequestDecision roomMismatch = AuthoritativeMoveService.Process(departed.Session, departed.Room, departed.Player, Request(1, -1.5f, -0.5f));
        Check(!roomMismatch.Accepted && roomMismatch.RejectReason == MOVE_REJECT_REASON.RoomMismatch, "Room change before Job execution must reject the request.");

        Context foreign = CreateContext(registration, new NumericsVector3(-1.5f, 2.0f, -0.5f));
        Context owner = CreateContext(registration, new NumericsVector3(-1.5f, 2.0f, -0.5f));
        MoveRequestDecision foreignAttempt = AuthoritativeMoveService.Process(owner.Session, owner.Room, foreign.Player, Request(1, -1.5f, -0.5f));
        Check(!foreignAttempt.Accepted && foreignAttempt.RejectReason == MOVE_REJECT_REASON.RoomMismatch, "A session must not select another Player as the movement target.");

        Context skill = CreateContext(registration, new NumericsVector3(-1.5f, 2.0f, -0.5f));
        skill.Player.Info.State = OBJECT_STATE_TYPE.Skill;
        MoveRequestDecision invalidState = AuthoritativeMoveService.Process(skill.Session, skill.Room, skill.Player, Request(1, -1.5f, -0.5f));
        Check(!invalidState.Accepted && invalidState.RejectReason == MOVE_REJECT_REASON.InvalidPlayerState, "Skill state must reject a move request.");
    }

    private static void RunCoordinateAndNavigationTests(NavigationRegistration registration)
    {
        Context min = CreateContext(registration, new NumericsVector3(-1.5f, 2.0f, -0.5f));
        MoveRequestDecision minDecision = AuthoritativeMoveService.Process(min.Session, min.Room, min.Player, Request(1, -2.0f, -1.0f));
        Check(minDecision.Accepted && minDecision.AcceptedDestination == new NumericsVector3(-1.5f, 2.0f, -0.5f), "Minimum boundary must resolve to Cell 0:0 center.");

        Context maxInside = CreateContext(registration, new NumericsVector3(-1.5f, 2.0f, -0.5f));
        Check(AuthoritativeMoveService.Process(maxInside.Session, maxInside.Room, maxInside.Player, Request(1, 1.9999f, 1.9999f)).Accepted, "Maximum boundary interior must be accepted.");
        Context maxExact = CreateContext(registration, new NumericsVector3(-1.5f, 2.0f, -0.5f));
        MoveRequestDecision outside = AuthoritativeMoveService.Process(maxExact.Session, maxExact.Room, maxExact.Player, Request(1, 2.0f, 2.0f));
        Check(!outside.Accepted && outside.RejectReason == MOVE_REJECT_REASON.OutsideNavigationBounds, "Exclusive maximum boundary must be rejected.");

        Context below = CreateContext(registration, new NumericsVector3(-1.5f, 2.0f, -0.5f));
        Check(AuthoritativeMoveService.Process(below.Session, below.Room, below.Player, Request(1, -2.0001f, -1.0f)).RejectReason == MOVE_REJECT_REASON.OutsideNavigationBounds, "Position below Origin must be rejected.");
        Context nan = CreateContext(registration, new NumericsVector3(-1.5f, 2.0f, -0.5f));
        Check(AuthoritativeMoveService.Process(nan.Session, nan.Room, nan.Player, Request(1, float.NaN, 0)).RejectReason == MOVE_REJECT_REASON.InvalidCoordinate, "NaN must be rejected.");
        Context infinity = CreateContext(registration, new NumericsVector3(-1.5f, 2.0f, -0.5f));
        Check(AuthoritativeMoveService.Process(infinity.Session, infinity.Room, infinity.Player, Request(1, float.PositiveInfinity, 0)).RejectReason == MOVE_REJECT_REASON.InvalidCoordinate, "Infinity must be rejected.");
        Context huge = CreateContext(registration, new NumericsVector3(-1.5f, 2.0f, -0.5f));
        Check(AuthoritativeMoveService.Process(huge.Session, huge.Room, huge.Player, Request(1, 1000001.0f, 0)).RejectReason == MOVE_REJECT_REASON.InvalidCoordinate, "Abnormally large coordinate must be rejected.");

        Context mapMismatch = CreateContext(registration, new NumericsVector3(-1.5f, 2.0f, -0.5f));
        Check(AuthoritativeMoveService.Process(mapMismatch.Session, mapMismatch.Room, mapMismatch.Player, Request(1, -1.5f, -0.5f, "other-map")).RejectReason == MOVE_REJECT_REASON.NavigationMapMismatch, "MapId mismatch must be rejected.");
        Context hashMismatch = CreateContext(registration, new NumericsVector3(-1.5f, 2.0f, -0.5f));
        Check(AuthoritativeMoveService.Process(hashMismatch.Session, hashMismatch.Room, hashMismatch.Player, Request(1, -1.5f, -0.5f, hash: new string('0', 64))).RejectReason == MOVE_REJECT_REASON.NavigationHashMismatch, "Hash mismatch must be rejected.");

        Context blocked = CreateContext(registration, new NumericsVector3(-1.5f, 2.0f, -0.5f));
        MoveRequestDecision adjusted = AuthoritativeMoveService.Process(blocked.Session, blocked.Room, blocked.Player, Request(1, 0.5f, -0.5f));
        Check(adjusted.Accepted && adjusted.WasDestinationAdjusted, "Blocked Cell must use limited nearest-walkable adjustment.");
        Check(adjusted.AcceptedDestination == new NumericsVector3(-0.5f, 2.0f, -0.5f), "Blocked Cell tie-break result changed.");
    }

    private static void RunMovementStateTests(NavigationRegistration registration)
    {
        Context context = CreateContext(registration, new NumericsVector3(-1.5f, 2.0f, -0.5f));
        ProtoVector3 originalPosition = context.Player.Info.Position.Clone();
        MoveRequestDecision first = AuthoritativeMoveService.Process(context.Session, context.Room, context.Player, Request(1, -0.5f, -0.5f));
        Check(first.Accepted && context.Player.MovementState != null && context.Player.MovementState.IsActive, "Accepted request must create active MovementState.");
        Check(context.Player.MovementState.StartPosition == new NumericsVector3(-1.5f, 2.0f, -0.5f), "MovementState must start from server position.");
        Check(context.Player.MovementState.Destination == new NumericsVector3(-0.5f, 2.0f, -0.5f), "MovementState destination must be accepted Cell center.");
        Check(context.Player.Info.Position.X == originalPosition.X && context.Player.Info.Position.Y == originalPosition.Y && context.Player.Info.Position.Z == originalPosition.Z, "Move acceptance must not immediately change authoritative Player position.");
        Check(context.Player.Info.State == OBJECT_STATE_TYPE.Move, "Accepted MovementState must enter the server Moving state without changing position.");

        PlayerMovementState oldState = context.Player.MovementState;
        MoveRequestDecision second = AuthoritativeMoveService.Process(context.Session, context.Room, context.Player, Request(2, -1.5f, 0.5f));
        Check(second.Accepted && second.ServerMoveId > first.ServerMoveId, "ServerMoveId must increase within a Room.");
        Check(!oldState.IsActive && context.Player.MovementState.IsActive, "New command must replace and deactivate the previous command.");
        Check(context.Player.MovementState.ClientMoveSequence == 2, "Replacement MovementState sequence mismatch.");
    }
}
