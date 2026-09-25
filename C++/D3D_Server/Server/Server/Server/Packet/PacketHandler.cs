using Google.Protobuf;
using Google.Protobuf.Protocol;
using Server;
using Server.Game.Objects;
using Server.Game.Room;
using Server.Game.Movement;
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
    private const string CombatGatherCommand = "combat-gather-v1";

    private static bool CombatTestCommandsEnabled
    {
        get
        {
#if DEBUG
            return true;
#else
            return string.Equals(
                Environment.GetEnvironmentVariable("D3D_ENABLE_TEST_COMMANDS"),
                "1",
                StringComparison.Ordinal);
#endif
        }
    }
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
        C_TestMsg testPacket = message as C_TestMsg;
        ClientSession clientSession = session as ClientSession;
        if (testPacket == null || clientSession == null)
            return;

        if (string.Equals(testPacket.Message, CombatGatherCommand, StringComparison.Ordinal))
        {
            if (!CombatTestCommandsEnabled)
            {
                clientSession.Send(new S_TestMsg
                {
                    Message = "Combat gather is disabled. Use a Debug server or set D3D_ENABLE_TEST_COMMANDS=1."
                });
                return;
            }

            GameRoom room = clientSession.GameRoom;
            Player requester = clientSession.Player;
            if (room == null || requester?.Info == null)
            {
                clientSession.Send(new S_TestMsg { Message = "Combat gather rejected: no active Player or Room." });
                return;
            }

            room.Push(() =>
            {
                if (clientSession.IsDisconnected ||
                    !ReferenceEquals(clientSession.GameRoom, room) ||
                    !ReferenceEquals(clientSession.Player, requester) ||
                    !ReferenceEquals(requester.Room, room) ||
                    !room.ContainsPlayer(requester))
                {
                    return;
                }

                room.TryGatherPlayersForCombatTest(requester, out string resultMessage);
                clientSession.Send(new S_TestMsg { Message = resultMessage });
            });
            return;
        }

        Console.WriteLine("recv Test : " + testPacket.Message);
        clientSession.Send(new S_TestMsg { Message = "C# to C++ : Hello Client!!!" });
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
        if (!MovementProtocolPolicy.LegacyMoveEnabled)
        {
            Console.WriteLine("[Movement] Deprecated C_Move rejected. Set D3D_ALLOW_LEGACY_MOVE=1 only for development compatibility.");
            return;
        }

        Console.WriteLine($"[Server] Received Move Packet: {movePacket.ObjectId} to ({movePacket.TargetPos.X}, {movePacket.TargetPos.Y}, {movePacket.TargetPos.Z}) | Tile({movePacket.CellPos.X}, {movePacket.CellPos.Z})");

        ClientSession clientSession = (ClientSession)session;
        GameRoom room = clientSession.GameRoom;
        Player sessionPlayer = clientSession.Player;

        if (room == null || sessionPlayer?.Info == null)
            return;

        if (movePacket.ObjectId != sessionPlayer.Info.ObjectId)
        {
            Console.WriteLine($"[Security] Rejected C_Move objectId {movePacket.ObjectId}; session owns {sessionPlayer.Info.ObjectId}.");
            return;
        }

        room.Push(() =>
        {
            // The movement target is session-owned. Recheck ownership because this job runs later.
            if (clientSession.GameRoom != room || clientSession.Player != sessionPlayer)
                return;

            if (sessionPlayer.Info.Hp <= 0 || sessionPlayer.IsMovementLockedByCombat)
                return;
            GameObject obj = sessionPlayer;

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

    public static void C_MoveRequestHandler(PacketSession session, IMessage message)
    {
        C_MoveRequest request = message as C_MoveRequest;
        ClientSession clientSession = session as ClientSession;
        if (request == null || clientSession == null)
            return;

        GameRoom room = clientSession.GameRoom;
        Player player = clientSession.Player;
        if (player?.Info == null)
        {
            SendMoveRejected(clientSession, request.ClientMoveSequence, MOVE_REJECT_REASON.NoPlayer, null, null, request.NavigationMapId, request.NavigationContentHash);
            return;
        }
        if (room == null)
        {
            SendMoveRejected(clientSession, request.ClientMoveSequence, MOVE_REJECT_REASON.NoRoom, player, null, request.NavigationMapId, request.NavigationContentHash);
            return;
        }

        room.Push(() =>
        {
            MoveRequestDecision decision = AuthoritativeMoveService.Process(clientSession, room, player, request);
            if (decision.Accepted)
            {
                var accepted = new S_MoveAccepted
                {
                    ClientMoveSequence = decision.ClientMoveSequence,
                    ServerMoveId = decision.ServerMoveId,
                    NavigationMapId = room.NavigationMapId,
                    NavigationContentHash = room.NavigationContentHash,
                    RequestedDestination = decision.RequestedDestination.ToProtoVector3(),
                    AcceptedDestination = decision.AcceptedDestination.ToProtoVector3(),
                    ServerStartPosition = decision.ServerStartPosition.ToProtoVector3(),
                    WasDestinationAdjusted = decision.WasDestinationAdjusted,
                };
                clientSession.Send(accepted);
                Console.WriteLine("[Movement] Accepted Room=" + room.RoomId +
                    " Player=" + player.Info.ObjectId +
                    " Sequence=" + decision.ClientMoveSequence +
                    " ServerMoveId=" + decision.ServerMoveId +
                    " Adjusted=" + decision.WasDestinationAdjusted +
                    " Expanded=" + decision.PathExpandedNodeCount +
                    " PathMs=" + decision.PathfindingElapsedMilliseconds.ToString("F3"));
                return;
            }

            SendMoveRejected(clientSession, decision.ClientMoveSequence, decision.RejectReason, player, room);
            Console.WriteLine("[Movement] Rejected Room=" + room.RoomId +
                " Player=" + player.Info.ObjectId +
                " Sequence=" + decision.ClientMoveSequence +
                " Reason=" + decision.RejectReason);
        });
    }

    private static void SendMoveRejected(
        ClientSession session,
        uint sequence,
        MOVE_REJECT_REASON reason,
        Player player,
        GameRoom room,
        string requestedMapId = null,
        string requestedHash = null)
    {
        Vec3 serverPosition = player?.Info?.Position == null
            ? Vec3.Zero
            : new Vec3(player.Info.Position.X, player.Info.Position.Y, player.Info.Position.Z);
        session.Send(new S_MoveRejected
        {
            ClientMoveSequence = sequence,
            RejectReason = reason,
            ServerPosition = serverPosition.ToProtoVector3(),
            NavigationMapId = room?.NavigationMapId ?? requestedMapId ?? string.Empty,
            NavigationContentHash = room?.NavigationContentHash ?? requestedHash ?? string.Empty,
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

        ClientSession clientSession = (ClientSession)session;
        GameRoom room = clientSession.GameRoom;
        Player sessionPlayer = clientSession.Player;
        if (room == null || sessionPlayer == null || sessionPlayer.Info == null)
            return;
        if (skillPacket.CasterId != 0 && skillPacket.CasterId != sessionPlayer.Info.ObjectId)
        {
            Console.WriteLine("[Security] Skill cast rejected: casterId does not belong to the session.");
            return;
        }

        room.Push(() =>
        {
            if (!ReferenceEquals(clientSession.Player, sessionPlayer) ||
                !ReferenceEquals(clientSession.GameRoom, room) ||
                !ReferenceEquals(sessionPlayer.Room, room) ||
                !room.ContainsPlayer(sessionPlayer))
            {
                return;
            }

            IChampionSkillHandler skillHandler = sessionPlayer.Info.ChampType switch
            {
                PLAYER_CHAMPION_TYPE.PlayerTypeGaren => new GarenSkillHandler(),
                PLAYER_CHAMPION_TYPE.PlayerTypeAnnie => new AnnieSkillHandler(),
                _ => null
            };

            if (skillHandler == null)
                return;

            skillHandler.HandleSkill(room, sessionPlayer, skillPacket);
        });
    }
    public static void C_EnterGameHandler(PacketSession session, IMessage message)
    {
        // TODO
    }
}

