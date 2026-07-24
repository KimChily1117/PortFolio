using Google.Protobuf;
using Google.Protobuf.Protocol;
using Server;
using Server.DB;
using Server.Game.Object;
using Server.Game.Room;
using Server.Game.Match;
using ServerCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

class PacketHandler
{
    private static bool IsTransferBlocked(ClientSession session, string packetName)
    {
        if (session == null || session.IsTransferring == false)
            return false;

        Console.WriteLine($"[TRANSFER] {packetName} ignored while transferring. SessionId={session.SessionId}, PendingRoomId={session.PendingRoomId}");
        return true;
    }

    public static void C_UdpHelloHandler(PacketSession session, IMessage packet)
    {
        Console.WriteLine("[UDP] C_UdpHello received on TCP PacketManager and ignored. Use UDP datagram parser.");
    }

    public static void C_UdpMoveHandler(PacketSession session, IMessage packet)
    {
        Console.WriteLine("[UDP] C_UdpMove received on TCP PacketManager and ignored. Use UDP datagram parser.");
    }
    public static void C_MoveHandler(PacketSession session, IMessage packet)
    {
        C_Move movePacket = packet as C_Move;
        ClientSession clientSession = session as ClientSession;
        if (IsTransferBlocked(clientSession, nameof(C_Move)))
            return;


        //Console.WriteLine($"C_Move ({movePacket.PosInfo.MoveDir})");


        Player player = clientSession.MyPlayer;

        if (player == null)
            return;

        GameRoom room = player.Room;
        if (room == null)
            return;

        room.Push(room.HandleMove, player, movePacket);

    }

    public static void C_SkillHandler(PacketSession session, IMessage packet)
    {
        C_Skill skillPacket = packet as C_Skill;
        ClientSession clientSession = session as ClientSession;
        if (IsTransferBlocked(clientSession, nameof(C_Skill)))
            return;

        if (clientSession == null)
        {
            Console.WriteLine("[SKILL] C_Skill rejected. Reason=InvalidSession");
            return;
        }

        if (skillPacket == null || skillPacket.Info == null)
        {
            Console.WriteLine($"[SKILL] C_Skill rejected. Reason=InvalidPacket, SessionId={clientSession.SessionId}");
            return;
        }

        Player player = clientSession.MyPlayer;
        if (player == null)
            return;

        GameRoom room = player.Room;
        if (room == null)
            return;

        Console.WriteLine($"skillPacket : {skillPacket.Info.SkillId}");
        //room.HandleSkill(player, skillPacket);
        room.Push(room.HandleSkill, player, skillPacket);

    }

    public static void C_JumpHandler(PacketSession session, IMessage packet)
    {
        C_Jump jumpPacket = packet as C_Jump;
        ClientSession clientSession = session as ClientSession;
        if (IsTransferBlocked(clientSession, nameof(C_Jump)))
            return;


        Player player = clientSession.MyPlayer;

        if (player == null)
            return;

        GameRoom room = player.Room;
        if (room == null)
            return;

        room.Push(room.HandleJump, player, jumpPacket);
    }

    public static void C_SceneMoveHandler(PacketSession session, IMessage packet)
    {
        C_SceneMove c_SceneMove = packet as C_SceneMove;

        ClientSession clientSession = session as ClientSession;       
        if (IsTransferBlocked(clientSession, nameof(C_SceneMove)))
            return;



        if (clientSession.MyPlayer == null)
        {
            Console.WriteLine($"C_SceneMOveHandler ] Player is null!!");
            return;

        }


        if (c_SceneMove.Playerinfo == null || c_SceneMove.Playerinfo.IsMaster == false)
        {
            Console.WriteLine($"[TRANSFER] C_SceneMove rejected. Only party master can start transfer. SessionId={clientSession.SessionId}");
            return;
        }

        MatchManager.Instance.StartPartyDungeonTransfer(clientSession);


    }

    public static void C_ChannelMoveHandler(PacketSession session, IMessage packet)
    {
        C_ChannelMove channelMove = packet as C_ChannelMove;
        ClientSession clientSession = session as ClientSession;
        if (IsTransferBlocked(clientSession, nameof(C_ChannelMove)))
            return;

        if (clientSession == null || channelMove == null)
            return;

        Player player = clientSession.MyPlayer;
        if (player == null)
        {
            SendChannelMoveResult(clientSession, false, 0, channelMove.TargetRoomId, "PlayerNull");
            return;
        }

        GameRoom sourceRoom = player.Room;
        int currentRoomId = sourceRoom?.RoomId ?? 0;
        int targetRoomId = channelMove.TargetRoomId;

        if (sourceRoom == null || sourceRoom.RoomType != RoomType.Town)
        {
            SendChannelMoveResult(clientSession, false, currentRoomId, targetRoomId, "NotInTown");
            Console.WriteLine($"[TOWN_CHANNEL] Move rejected. Reason=NotInTown, Player={player.Info?.Name}, PlayerId={player.Id}, CurrentRoomId={currentRoomId}, TargetRoomId={targetRoomId}");
            return;
        }

        if (targetRoomId == sourceRoom.RoomId)
        {
            SendChannelMoveResult(clientSession, false, currentRoomId, targetRoomId, "AlreadyInChannel");
            Console.WriteLine($"[TOWN_CHANNEL] Move rejected. Reason=AlreadyInChannel, Player={player.Info?.Name}, PlayerId={player.Id}, CurrentRoomId={currentRoomId}, TargetRoomId={targetRoomId}");
            return;
        }

        GameRoom targetRoom = null;
        string reserveReason = null;
        if (RoomManager.Instance.TryReserveTownChannel(targetRoomId, out targetRoom, out reserveReason) == false)
        {
            SendChannelMoveResult(clientSession, false, currentRoomId, targetRoomId, reserveReason ?? "TargetUnavailable");
            Console.WriteLine($"[TOWN_CHANNEL] Move rejected. Reason={reserveReason ?? "TargetUnavailable"}, Player={player.Info?.Name}, PlayerId={player.Id}, CurrentRoomId={currentRoomId}, TargetRoomId={targetRoomId}");
            return;
        }

        Console.WriteLine($"[TOWN_CHANNEL] Move requested. Player={player.Info?.Name}, PlayerId={player.Id}, CurrentRoomId={currentRoomId}, TargetRoomId={targetRoomId}");

        sourceRoom.Push(() =>
        {
            if (player.Room != sourceRoom)
            {
                RoomManager.Instance.ReleaseTownChannelReservation(targetRoomId);
                SendChannelMoveResult(clientSession, false, currentRoomId, targetRoomId, "RoomChanged");
                Console.WriteLine($"[TOWN_CHANNEL] Move rejected. Reason=RoomChanged, Player={player.Info?.Name}, PlayerId={player.Id}, CurrentRoomId={currentRoomId}, TargetRoomId={targetRoomId}");
                return;
            }

            sourceRoom.LeaveRoom(player.Id, sendLeaveToSelf: false);

            MoveDir preservedFacing = player.LastFacingDir == MoveDir.Left ? MoveDir.Left : MoveDir.Right;
            player.Info.PosInfo.State = PlayerState.Idle;
            player.Info.PosInfo.MoveDir = preservedFacing;
            player.UpdateFacing(preservedFacing);
            Console.WriteLine($"[TOWN_CHANNEL] Preserved position for channel move. Player={player.Info?.Name}, PlayerId={player.Id}, TargetRoomId={targetRoom.RoomId}, Pos=({player.Info.PosInfo.PosX:0.00},{player.Info.PosInfo.PosY:0.00}), State=Idle, MoveDir={preservedFacing}");

            targetRoom.Push(() =>
            {
                SendChannelMoveResult(clientSession, true, targetRoom.RoomId, targetRoom.RoomId, "Ok");
                targetRoom.EnterRoom(player);
                Console.WriteLine($"[TOWN_CHANNEL] Move completed. Player={player.Info?.Name}, PlayerId={player.Id}, SourceRoomId={sourceRoom.RoomId}, TargetRoomId={targetRoom.RoomId}");
            });
        });
    }

    private static void SendChannelMoveResult(ClientSession session, bool success, int currentRoomId, int targetRoomId, string reason)
    {
        if (session == null)
            return;

        S_ChannelMove result = new S_ChannelMove
        {
            Success = success,
            CurrentRoomId = currentRoomId,
            TargetRoomId = targetRoomId,
            Reason = reason ?? string.Empty
        };
        session.Send(result);
    }
    public static void C_CollisionHandler(PacketSession session, IMessage packet)
    {
        C_Collision c_Collision = packet as C_Collision;
        ClientSession clientSession = session as ClientSession;
        if (IsTransferBlocked(clientSession, nameof(C_Collision)))
            return;

        if (clientSession == null)
        {
            Console.WriteLine("[HIT] C_Collision rejected. Reason=InvalidSession");
            return;
        }

        if (c_Collision == null || c_Collision.Playerinfo == null)
        {
            Console.WriteLine($"[HIT] C_Collision rejected. Reason=InvalidPacket, SessionId={clientSession.SessionId}");
            return;
        }

        Player player = clientSession.MyPlayer;
        if (player == null)
        {
            Console.WriteLine($"[HIT] C_Collision rejected. Reason=PlayerNull, SessionId={clientSession.SessionId}");
            return;
        }

        GameRoom room = player.Room;
        if (room == null)
        {
            Console.WriteLine($"[HIT] C_Collision rejected. Reason=RoomNull, SessionId={clientSession.SessionId}, PlayerId={player.Id}");
            return;
        }

        Console.WriteLine($"[HIT] C_Collision received. SessionId={clientSession.SessionId}, AttackerId={player.Id}, TargetId={c_Collision.Playerinfo.ObjectId}");
        room.Push(room.HandleCollision, player, c_Collision);
    }

    public static void C_LoginHandler(PacketSession session, IMessage packet)
    {
        C_Login c_Login = packet as C_Login;
        ClientSession clientSession = session as ClientSession;

        Console.WriteLine($"Recived Request Login packet!! : UniqueId : {c_Login.UniqueId}");
        clientSession.HandleLogin(c_Login);
    }

    public static void C_CreatePlayerHandler(PacketSession session, IMessage packet)
    {
        C_CreatePlayer c_CreatePlayer = (C_CreatePlayer)packet;
        ClientSession clientSession = (ClientSession)session;


        clientSession.HandleCreateCharecter(c_CreatePlayer);
    }

    public static void C_SceneReadyHandler(PacketSession session, IMessage packet)
    {
        C_SceneReady c_SceneReady = packet as C_SceneReady;
        ClientSession clientSession = session as ClientSession;

        if (clientSession == null)
        {
            Console.WriteLine("[TRANSFER] SceneReady rejected. Reason=InvalidSession");
            return;
        }

        RoomTransferService.Instance.TryEnterPendingRoom(clientSession, c_SceneReady);
    }
    public static void C_EnterGameHandler(PacketSession session, IMessage packet)
    {
        C_EnterGame c_EnterGame = (C_EnterGame)packet;
        ClientSession clientSession = (ClientSession)session;

        clientSession.HandleEnterGame(c_EnterGame);
    }

    public static void C_CreateRoomHandler(PacketSession session, IMessage packet)
    {
        C_CreateRoom c_CreateRoom = (C_CreateRoom)packet;
        ClientSession clientSession = (ClientSession)session;

        Console.WriteLine($"C_CreateRoomHandler!!! ");

        if (IsTransferBlocked(clientSession, nameof(C_CreateRoom)))
            return;

        clientSession.HandleCreateRoom(c_CreateRoom);
    }

    public static void C_EnterPartyHandler(PacketSession session, IMessage packet)
    {
        C_EnterParty c_EnterParty = (C_EnterParty)packet;
        ClientSession clientSession = (ClientSession)session;
        if (IsTransferBlocked(clientSession, nameof(C_EnterParty)))
            return;

        clientSession.HandleEnterParty(c_EnterParty);
    }

    public static void C_EquipItemHandler(PacketSession session, IMessage packet)
    {
        C_EquipItem equipPacket = (C_EquipItem)packet;

        ClientSession clientSession = (ClientSession)session;

        Player p = clientSession.MyPlayer;


        if (p == null)
            return;

        GameRoom room = p.Room;

        if (room == null)
            return;


        room.Push(room.HandleEquipItem, p, equipPacket);
        
    }
}







