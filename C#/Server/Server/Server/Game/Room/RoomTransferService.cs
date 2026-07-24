using Google.Protobuf.Protocol;
using Server.Game.Object;
using Server.Game.Match;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Server.Game.Room
{
    public class RoomTransferService
    {
        public static RoomTransferService Instance { get; } = new RoomTransferService();

        private readonly object _lock = new object();
        private int _transferId = 1;

        private RoomTransferService()
        {
        }

        public void StartSoloDungeonTransfer(ClientSession session, RoomType targetRoomType)
        {
            if (session == null || session.MyPlayer == null)
                return;

            if (session.IsTransferring)
            {
                Console.WriteLine($"[TRANSFER] Already transferring. SessionId={session.SessionId}, PendingRoomId={session.PendingRoomId}");
                return;
            }

            Player player = session.MyPlayer;
            GameRoom sourceRoom = player.Room;
            GameRoom targetRoom = RoomManager.Instance.Add(targetRoomType);
            targetRoom.Push(targetRoom.InitEnemy);
            int transferId = NextTransferId();

            session.IsTransferring = true;
            session.PendingRoomId = targetRoom.RoomId;
            session.PendingRoomType = targetRoomType;
            session.PendingTransferId = transferId;
            session.PendingSceneType = GetSceneType(targetRoomType);

            Console.WriteLine($"[TRANSFER] Start. SessionId={session.SessionId}, PlayerId={player.Id}, SourceRoomId={sourceRoom?.RoomId ?? 0}, TargetRoomId={targetRoom.RoomId}, TargetRoomType={targetRoomType}");

            if (sourceRoom == null)
            {
                SendSceneMove(session);
                return;
            }

            sourceRoom.Push(() =>
            {
                sourceRoom.LeaveRoom(player.Id, sendLeaveToSelf: false);
                SendSceneMove(session);
            });
        }

        public bool TryEnterPendingRoom(ClientSession session)
        {
            if (session == null || session.MyPlayer == null || session.IsTransferring == false)
                return false;

            int matchPartyId = session.PendingMatchPartyId;
            int pendingRoomId = session.PendingRoomId;
            int pendingTransferId = session.PendingTransferId;
            GameRoom targetRoom = RoomManager.Instance.Find(pendingRoomId);
            if (targetRoom == null)
            {
                Console.WriteLine($"[TRANSFER] Pending target room not found. SessionId={session.SessionId}, PendingRoomId={pendingRoomId}");
                MatchHistoryPersistence.RecordFailed(matchPartyId, "TargetRoomNotFound");
                session.ClearPendingTransfer();
                return false;
            }

            Player player = session.MyPlayer;
            Console.WriteLine($"[TRANSFER] Enter pending room. SessionId={session.SessionId}, PlayerId={player.Id}, TargetRoomId={targetRoom.RoomId}");

            if (targetRoom.RoomType == RoomType.Town)
                TownSpawnService.ApplyMyRoomSpawn(player, targetRoom, "ReturnToTown");
            else
            {
                player.Info.PosInfo.PosX = 0f;
                player.Info.PosInfo.PosY = 0f;
                player.Info.PosInfo.State = PlayerState.Idle;
                player.Info.PosInfo.MoveDir = MoveDir.Right;
                player.UpdateFacing(MoveDir.Right);

                Console.WriteLine($"[TRANSFER] Dungeon spawn reset. SessionId={session.SessionId}, PlayerId={player.Id}, TargetRoomId={targetRoom.RoomId}, TargetRoomType={targetRoom.RoomType}, Pos=(0.00,0.00), State=Idle, MoveDir=Right");
            }

            // A room transfer is a server-authoritative teleport. Movement validation from
            // the previous room must not be compared with the new room's spawn position.
            lock (session)
                session.ResetUdpMoveSecurityState();
            Console.WriteLine($"[TRANSFER] UDP movement state reset. SessionId={session.SessionId}, PlayerId={player.Id}, TargetRoomId={targetRoom.RoomId}, TargetRoomType={targetRoom.RoomType}, Spawn=({player.Info.PosInfo.PosX:0.00},{player.Info.PosInfo.PosY:0.00})");

            player.Info.Damage = 10.0f;

            targetRoom.Push(targetRoom.EnterRoom, player);
            MatchHistoryPersistence.RecordDungeonEntered(matchPartyId, pendingRoomId, pendingTransferId, DateTime.UtcNow);
            session.ClearPendingTransfer();
            return true;
        }
        public bool TryEnterPendingRoom(ClientSession session, C_SceneReady sceneReady)
        {
            if (session == null)
                return false;

            if (sceneReady == null)
            {
                Console.WriteLine($"[TRANSFER] SceneReady rejected. Reason=InvalidPacket, SessionId={session.SessionId}");
                MatchHistoryPersistence.RecordFailed(session.PendingMatchPartyId, "InvalidPacket");
                return false;
            }

            if (session.IsTransferring == false)
            {
                Console.WriteLine($"[TRANSFER] SceneReady rejected. Reason=NotTransferring, SessionId={session.SessionId}");
                MatchHistoryPersistence.RecordFailed(session.PendingMatchPartyId, "NotTransferring");
                return false;
            }

            if (sceneReady.TargetRoomId != session.PendingRoomId)
            {
                Console.WriteLine($"[TRANSFER] SceneReady rejected. Reason=RoomIdMismatch, SessionId={session.SessionId}, Expected={session.PendingRoomId}, Actual={sceneReady.TargetRoomId}");
                MatchHistoryPersistence.RecordFailed(session.PendingMatchPartyId, "RoomIdMismatch");
                return false;
            }

            if (sceneReady.TransferId != session.PendingTransferId)
            {
                Console.WriteLine($"[TRANSFER] SceneReady rejected. Reason=TransferIdMismatch, SessionId={session.SessionId}, Expected={session.PendingTransferId}, Actual={sceneReady.TransferId}");
                MatchHistoryPersistence.RecordFailed(session.PendingMatchPartyId, "TransferIdMismatch");
                return false;
            }

            if (sceneReady.TargetRoomType != session.PendingRoomType)
            {
                Console.WriteLine($"[TRANSFER] SceneReady rejected. Reason=RoomTypeMismatch, SessionId={session.SessionId}, Expected={session.PendingRoomType}, Actual={sceneReady.TargetRoomType}");
                MatchHistoryPersistence.RecordFailed(session.PendingMatchPartyId, "RoomTypeMismatch");
                return false;
            }

            if (sceneReady.SceneType != session.PendingSceneType)
            {
                Console.WriteLine($"[TRANSFER] SceneReady rejected. Reason=SceneTypeMismatch, SessionId={session.SessionId}, Expected={session.PendingSceneType}, Actual={sceneReady.SceneType}");
                MatchHistoryPersistence.RecordFailed(session.PendingMatchPartyId, "SceneTypeMismatch");
                return false;
            }

            Console.WriteLine($"[TRANSFER] SceneReady received. SessionId={session.SessionId}, RoomId={sceneReady.TargetRoomId}, TransferId={sceneReady.TransferId}, SceneType={sceneReady.SceneType}");
            return TryEnterPendingRoom(session);
        }

        public void StartPartyDungeonTransfer(IReadOnlyList<ClientSession> sessions, RoomType targetRoomType, int matchPartyId = 0)
        {
            if (sessions == null || sessions.Count == 0)
                return;

            List<ClientSession> validSessions = sessions.Where(IsValidTransferSession).ToList();
            if (validSessions.Count != sessions.Count)
            {
                Console.WriteLine($"[TRANSFER] Party transfer rejected. Reason=InvalidMember, Count={sessions.Count}, ValidCount={validSessions.Count}");
                MatchHistoryPersistence.RecordFailed(matchPartyId, "InvalidMember");
                return;
            }

            if (validSessions.Any(s => s.IsTransferring))
            {
                Console.WriteLine($"[TRANSFER] Party transfer rejected. Reason=AlreadyTransferring, Members={FormatMembers(validSessions)}");
                MatchHistoryPersistence.RecordFailed(matchPartyId, "AlreadyTransferring");
                return;
            }

            GameRoom targetRoom = RoomManager.Instance.Add(targetRoomType);
            targetRoom.Push(targetRoom.InitEnemy);
            int transferId = NextTransferId();
            SceneType sceneType = GetSceneType(targetRoomType);

            Console.WriteLine($"[TRANSFER] Party transfer start. Count={validSessions.Count}, TargetRoomId={targetRoom.RoomId}, TargetRoomType={targetRoomType}, TransferId={transferId}, SceneType={sceneType}, Members={FormatMembers(validSessions)}");
            MatchHistoryPersistence.RecordTransferStarted(matchPartyId, targetRoomType, targetRoom.RoomId, transferId, DateTime.UtcNow);

            foreach (ClientSession session in validSessions)
            {
                Player player = session.MyPlayer;
                GameRoom sourceRoom = player.Room;

                session.IsTransferring = true;
                session.PendingRoomId = targetRoom.RoomId;
                session.PendingRoomType = targetRoomType;
                session.PendingTransferId = transferId;
                session.PendingSceneType = sceneType;
                session.PendingMatchPartyId = matchPartyId;

                if (sourceRoom == null)
                {
                    SendSceneMove(session);
                    continue;
                }

                sourceRoom.Push(() =>
                {
                    sourceRoom.LeaveRoom(player.Id, sendLeaveToSelf: false);
                    SendSceneMove(session);
                });
            }
        }


        public void StartReturnToTownTransfer(GameRoom sourceRoom)
        {
            if (sourceRoom == null || sourceRoom.IsDungeonRoom == false)
                return;

            GameRoom townRoom = RoomManager.Instance.Find(RoomType.Town);
            if (townRoom == null)
            {
                Console.WriteLine($"[TRANSFER] Return to town rejected. Reason=TownRoomNotFound, SourceRoomId={sourceRoom.RoomId}");
                return;
            }

            List<Player> players = sourceRoom.GetPlayersSnapshot();
            if (players.Count == 0)
            {
                Console.WriteLine($"[TRANSFER] Return to town skipped. Reason=NoPlayers, SourceRoomId={sourceRoom.RoomId}");
                RoomManager.Instance.TryRemoveEmptyDungeonRoom(sourceRoom, "ClearReturnNoPlayers", allowPendingTransfer: true);
                return;
            }

            int transferId = NextTransferId();
            Console.WriteLine($"[TRANSFER] Return to town start. SourceRoomId={sourceRoom.RoomId}, TownRoomId={townRoom.RoomId}, TransferId={transferId}, Players={string.Join(",", players.Select(p => p?.Info?.Name ?? "null"))}");

            foreach (Player player in players)
            {
                ClientSession session = player?.Session;
                if (IsValidTransferSession(session) == false)
                    continue;

                if (session.IsTransferring)
                {
                    Console.WriteLine($"[TRANSFER] Return member skipped. Reason=AlreadyTransferring, SessionId={session.SessionId}, PlayerId={player.Id}, PendingRoomId={session.PendingRoomId}");
                    continue;
                }

                session.IsTransferring = true;
                session.PendingRoomId = townRoom.RoomId;
                session.PendingRoomType = RoomType.Town;
                session.PendingTransferId = transferId;
                session.PendingSceneType = SceneType.SceneTown;

                sourceRoom.LeaveRoom(player.Id, sendLeaveToSelf: false);
                SendSceneMove(session);
            }
        }
        private void SendSceneMove(ClientSession session)
        {
            if (session == null || session.MyPlayer == null)
                return;

            S_SceneMove sceneMove = new S_SceneMove();
            sceneMove.Playerinfo = session.MyPlayer.Info;
            sceneMove.TargetRoomType = session.PendingRoomType;
            sceneMove.TargetRoomId = session.PendingRoomId;
            sceneMove.TransferId = session.PendingTransferId;
            sceneMove.SceneType = session.PendingSceneType;
            session.Send(sceneMove);

            Console.WriteLine($"[TRANSFER] S_SceneMove sent. SessionId={session.SessionId}, PendingRoomId={session.PendingRoomId}, TransferId={session.PendingTransferId}, SceneType={session.PendingSceneType}");
        }

        private bool IsValidTransferSession(ClientSession session)
        {
            return session != null &&
                   session.MyPlayer != null &&
                   session.MyPlayer.Info != null;
        }

        private string FormatMembers(IReadOnlyList<ClientSession> sessions)
        {
            return string.Join(",", sessions.Select(s => s?.MyPlayer?.Info?.Name ?? "null"));
        }

        private int NextTransferId()
        {
            lock (_lock)
            {
                return _transferId++;
            }
        }

        private SceneType GetSceneType(RoomType roomType)
        {
            switch (roomType)
            {
                case RoomType.Bakal:
                    return SceneType.SceneBakal;
                case RoomType.Town:
                    return SceneType.SceneTown;
                default:
                    return SceneType.SceneNone;
            }
        }
    }
}

