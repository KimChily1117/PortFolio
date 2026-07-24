using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using Google.Protobuf;
using Google.Protobuf.Protocol;
using Server.Game.Map;
using Server.Game.Room;

namespace Server.Udp
{
    public class UdpGamePacketHandler
    {
        private static readonly bool DebugUdpMovementLog = false;
        private const int RegisteredMoveRateWindowMs = 1000;
        private const int MaxRegisteredMovesPerWindow = 60;
        private const int UnregisteredRateWindowMs = 10000;
        private const int MaxUnregisteredLogsPerWindow = 5;
        private const float MaxUdpMoveSpeed = 12.0f;
        private const float UdpMoveDistanceTolerance = 1.25f;
        private const float FirstUdpMoveDistanceTolerance = 5.0f;
        private const double MinUdpMoveDeltaSeconds = 0.05;
        private const double MaxUdpMoveDeltaSeconds = 0.50;
        private const int DropLogInterval = 25;

        private readonly object _unregisteredRateLock = new object();
        private readonly Dictionary<string, EndpointRateState> _unregisteredEndpointRates = new Dictionary<string, EndpointRateState>();

        Action<byte[], EndPoint> _sendTo;

        private sealed class EndpointRateState
        {
            public DateTime WindowStartedAtUtc;
            public int Count;
            public int SuppressedCount;
        }

        public void SetSender(Action<byte[], EndPoint> sendTo)
        {
            _sendTo = sendTo;
        }

        public bool HandleHelloToken(string token, EndPoint remoteEndPoint)
        {
            ClientSession endpointSession = SessionManager.Instance.FindByUdpEndPoint(remoteEndPoint);
            if (endpointSession != null)
            {
                lock (endpointSession)
                {
                    bool tokenMatches = string.IsNullOrEmpty(endpointSession.UdpToken) || endpointSession.UdpToken == token;
                    if (tokenMatches)
                    {
                        endpointSession.LastUdpSeenAt = DateTime.UtcNow;
                        return true;
                    }
                }

                Console.WriteLine($"[UDP] Hello rejected. Reason=EndPointAlreadyRegistered, SessionId={endpointSession.SessionId}, EndPoint={remoteEndPoint}");
                return false;
            }

            ClientSession session = SessionManager.Instance.FindByUdpToken(token);
            if (session == null)
            {
                if (ShouldLogUnregistered(remoteEndPoint, out int suppressed))
                    Console.WriteLine($"[UDP] Invalid token from {remoteEndPoint}. Suppressed={suppressed}");
                return false;
            }

            EndPoint oldEndPoint = session.UdpEndPoint;
            if (SessionManager.Instance.TryBindUdpEndPoint(session, remoteEndPoint, out string reason) == false)
            {
                Console.WriteLine($"[UDP] Endpoint registration rejected. Reason={reason}, SessionId={session.SessionId}, Old={oldEndPoint}, New={remoteEndPoint}");
                return false;
            }

            Console.WriteLine($"[UDP] Endpoint registered. SessionId={session.SessionId}, EndPoint={remoteEndPoint}");
            return true;
        }

        public bool HandleDatagram(byte[] buffer, int count, EndPoint remoteEndPoint)
        {
            if (count < sizeof(ushort))
                return false;

            ushort packetId = BitConverter.ToUInt16(buffer, 0);
            MsgId msgId = (MsgId)packetId;

            try
            {
                switch (msgId)
                {
                    case MsgId.CUdpHello:
                    {
                        C_UdpHello helloPacket = C_UdpHello.Parser.ParseFrom(ByteString.CopyFrom(buffer, sizeof(ushort), count - sizeof(ushort)));
                        bool registered = HandleHelloToken(helloPacket.Token, remoteEndPoint);
                        Console.WriteLine($"[UDP] Proto hello handled. Registered={registered}, EndPoint={remoteEndPoint}");
                        SendUdpHelloResponse(registered, remoteEndPoint);
                        return true;
                    }
                    case MsgId.CUdpMove:
                    {
                        C_UdpMove udpMovePacket = C_UdpMove.Parser.ParseFrom(ByteString.CopyFrom(buffer, sizeof(ushort), count - sizeof(ushort)));
                        if (udpMovePacket.PosInfo == null)
                        {
                            if (ShouldLogUnregistered(remoteEndPoint, out int suppressed))
                                Console.WriteLine($"[UDP] Proto move rejected. Reason=PosInfoNull, EndPoint={remoteEndPoint}, Suppressed={suppressed}");
                            return true;
                        }

                        HandleMovePosition(udpMovePacket.Sequence, udpMovePacket.PosInfo, remoteEndPoint);
                        return true;
                    }
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                if (ShouldLogUnregistered(remoteEndPoint, out int suppressed))
                    Console.WriteLine($"[UDP] Proto parse failed. PacketId={packetId}, EndPoint={remoteEndPoint}, Error={ex.Message}, Suppressed={suppressed}");
                return false;
            }
        }

        private void SendUdpHelloResponse(bool ok, EndPoint remoteEndPoint)
        {
            S_UdpHello response = new S_UdpHello();
            response.Ok = ok;
            response.Message = ok ? "UDP registration success." : "UDP registration rejected.";

            byte[] payload = response.ToByteArray();
            byte[] datagram = new byte[sizeof(ushort) + payload.Length];
            byte[] packetIdBytes = BitConverter.GetBytes((ushort)MsgId.SUdpHello);

            Buffer.BlockCopy(packetIdBytes, 0, datagram, 0, sizeof(ushort));
            Buffer.BlockCopy(payload, 0, datagram, sizeof(ushort), payload.Length);

            try
            {
                if (_sendTo == null)
                {
                    Console.WriteLine($"[UDP] S_UdpHello send failed. EndPoint={remoteEndPoint}, Error=Sender is null");
                    return;
                }

                _sendTo.Invoke(datagram, remoteEndPoint);
                Console.WriteLine($"[UDP] S_UdpHello sent. Ok={ok}, EndPoint={remoteEndPoint}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UDP] S_UdpHello send failed. EndPoint={remoteEndPoint}, Error={ex.Message}");
            }
        }

        private void HandleMovePosition(uint sequence, PositionInfo posInfo, EndPoint remoteEndPoint)
        {
            ClientSession session = SessionManager.Instance.FindByUdpEndPoint(remoteEndPoint);
            if (session == null)
            {
                if (ShouldLogUnregistered(remoteEndPoint, out int suppressed))
                    Console.WriteLine($"[UDP] Move rejected. Reason=UnregisteredEndPoint, EndPoint={remoteEndPoint}, Suppressed={suppressed}");
                return;
            }

            DateTime nowUtc = DateTime.UtcNow;
            lock (session)
            {
                session.LastUdpSeenAt = nowUtc;

                if (AllowRegisteredMoveByRate(session, nowUtc) == false)
                    return;

                if (IsUdpMoveSequenceAccepted(session, sequence) == false)
                    return;
            }

            Game.Object.Player player = session.MyPlayer;
            if (player == null)
            {
                Console.WriteLine($"[UDP] Move rejected. Reason=MyPlayerNull, SessionId={session.SessionId}");
                return;
            }

            GameRoom room = player.Room;
            if (room == null)
            {
                if (session.IsTransferring)
                    return;

                Console.WriteLine($"[UDP] Move rejected. Reason=RoomNull, SessionId={session.SessionId}, PlayerId={player.Id}");
                return;
            }

            if (ValidateAndRecordMove(session, player, room, posInfo, nowUtc) == false)
                return;

            C_Move movePacket = new C_Move();
            movePacket.PosInfo = new PositionInfo();
            movePacket.PosInfo.PosX = posInfo.PosX;
            movePacket.PosInfo.PosY = posInfo.PosY;
            movePacket.PosInfo.State = posInfo.State;
            movePacket.PosInfo.MoveDir = posInfo.MoveDir;

            room.Push(room.HandleMove, player, movePacket);

            if (DebugUdpMovementLog)
            {
                Console.WriteLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "[UDP] Move enqueued. SessionId={0}, Seq={1}, Pos=({2},{3}), State={4}, MoveDir={5}",
                    session.SessionId,
                    sequence,
                    movePacket.PosInfo.PosX,
                    movePacket.PosInfo.PosY,
                    movePacket.PosInfo.State,
                    movePacket.PosInfo.MoveDir));
            }
        }

        private bool AllowRegisteredMoveByRate(ClientSession session, DateTime nowUtc)
        {
            if (session.UdpMoveRateWindowStartedAt == DateTime.MinValue ||
                (nowUtc - session.UdpMoveRateWindowStartedAt).TotalMilliseconds >= RegisteredMoveRateWindowMs)
            {
                session.UdpMoveRateWindowStartedAt = nowUtc;
                session.UdpMoveRateWindowCount = 0;
            }

            session.UdpMoveRateWindowCount++;
            if (session.UdpMoveRateWindowCount <= MaxRegisteredMovesPerWindow)
                return true;

            session.UdpMoveRateLimitedDropCount++;
            if (session.UdpMoveRateLimitedDropCount == 1 || session.UdpMoveRateLimitedDropCount % DropLogInterval == 0)
            {
                Console.WriteLine($"[UDP] Move dropped. Reason=RateLimit, SessionId={session.SessionId}, Count={session.UdpMoveRateWindowCount}, WindowMs={RegisteredMoveRateWindowMs}, DropCount={session.UdpMoveRateLimitedDropCount}");
            }

            return false;
        }

        private bool IsUdpMoveSequenceAccepted(ClientSession session, uint sequence)
        {
            if (session.HasLastUdpMoveSequence == false)
            {
                session.HasLastUdpMoveSequence = true;
                session.LastUdpMoveSequence = sequence;
                return true;
            }

            if (IsSequenceNewer(sequence, session.LastUdpMoveSequence))
            {
                session.LastUdpMoveSequence = sequence;
                return true;
            }

            session.UdpMoveSequenceDropCount++;
            if (session.UdpMoveSequenceDropCount == 1 || session.UdpMoveSequenceDropCount % DropLogInterval == 0)
            {
                Console.WriteLine($"[UDP] Move dropped. Reason=OldOrDuplicateSequence, SessionId={session.SessionId}, Seq={sequence}, LastSeq={session.LastUdpMoveSequence}, DropCount={session.UdpMoveSequenceDropCount}");
            }

            return false;
        }

        private static bool IsSequenceNewer(uint incoming, uint last)
        {
            return incoming != last && (int)(incoming - last) > 0;
        }

        private bool ValidateAndRecordMove(ClientSession session, Game.Object.Player player, GameRoom room, PositionInfo posInfo, DateTime nowUtc)
        {
            if (player?.Info?.PosInfo == null || room == null || posInfo == null)
                return false;

            bool isPrivateTownMove = room.RoomType == RoomType.Town && player.IsInPublicTownArea == false;
            bool entersPublicTownArea = false;
            if (room.RoomType == RoomType.Town)
            {
                bool requestedInPublicLobby = TownSpawnService.IsPublicLobbyPosition(posInfo.PosX, posInfo.PosY);
                entersPublicTownArea = isPrivateTownMove && requestedInPublicLobby;
                if (requestedInPublicLobby == false && isPrivateTownMove == false)
                {
                    RecordValidationDrop(session, $"Bounds, RoomId={room.RoomId}, RoomType={room.RoomType}, Pos=({posInfo.PosX:0.00},{posInfo.PosY:0.00})");
                    return false;
                }
            }
            else if (MovementBoundsProvider.TryGet(room.RoomType, out MovementBoundsMap map) && map.IsWalkable(posInfo.PosX, posInfo.PosY) == false)
            {
                RecordValidationDrop(session, $"Bounds, RoomId={room.RoomId}, RoomType={room.RoomType}, Pos=({posInfo.PosX:0.00},{posInfo.PosY:0.00})");
                return false;
            }

            float lastX = session.HasLastAcceptedUdpMove ? session.LastAcceptedUdpMoveX : player.Info.PosInfo.PosX;
            float lastY = session.HasLastAcceptedUdpMove ? session.LastAcceptedUdpMoveY : player.Info.PosInfo.PosY;
            double elapsedSeconds = session.HasLastAcceptedUdpMove
                ? (nowUtc - session.LastAcceptedUdpMoveAt).TotalSeconds
                : 0.0;

            float dx = posInfo.PosX - lastX;
            float dy = posInfo.PosY - lastY;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);
            float allowedDistance;

            if (session.HasLastAcceptedUdpMove == false)
            {
                allowedDistance = FirstUdpMoveDistanceTolerance;
            }
            else
            {
                double clampedDelta = Math.Max(MinUdpMoveDeltaSeconds, Math.Min(MaxUdpMoveDeltaSeconds, elapsedSeconds));
                allowedDistance = (float)(MaxUdpMoveSpeed * clampedDelta) + UdpMoveDistanceTolerance;
            }

            // Crossing the Seria portal is a client teleport from private MyRoom
            // into the public lobby. It is validated by the Town walkable map,
            // not by normal per-frame speed distance.
            if (distance > allowedDistance && entersPublicTownArea == false)
            {
                RecordValidationDrop(session, $"Speed, RoomId={room.RoomId}, RoomType={room.RoomType}, Distance={distance:0.00}, Allowed={allowedDistance:0.00}, Dt={elapsedSeconds:0.000}, From=({lastX:0.00},{lastY:0.00}), To=({posInfo.PosX:0.00},{posInfo.PosY:0.00})");
                return false;
            }

            if (entersPublicTownArea)
            {
                Console.WriteLine($"[TOWN_FLOW][UDP_PUBLIC_TRANSITION] RoomId={room.RoomId}, Player={player.Info?.Name}, PlayerId={player.Id}, From=({lastX:0.00},{lastY:0.00}), To=({posInfo.PosX:0.00},{posInfo.PosY:0.00}), Distance={distance:0.00}");
            }

            session.HasLastAcceptedUdpMove = true;
            session.LastAcceptedUdpMoveX = posInfo.PosX;
            session.LastAcceptedUdpMoveY = posInfo.PosY;
            session.LastAcceptedUdpMoveAt = nowUtc;
            return true;
        }

        private void RecordValidationDrop(ClientSession session, string reason)
        {
            lock (session)
            {
                session.UdpMoveValidationDropCount++;
                if (session.UdpMoveValidationDropCount == 1 || session.UdpMoveValidationDropCount % DropLogInterval == 0)
                {
                    Console.WriteLine($"[UDP] Move dropped. Reason={reason}, SessionId={session.SessionId}, DropCount={session.UdpMoveValidationDropCount}");
                }
            }
        }

        private bool ShouldLogUnregistered(EndPoint remoteEndPoint, out int suppressed)
        {
            suppressed = 0;
            string key = remoteEndPoint?.ToString() ?? string.Empty;
            DateTime nowUtc = DateTime.UtcNow;

            lock (_unregisteredRateLock)
            {
                if (_unregisteredEndpointRates.TryGetValue(key, out EndpointRateState state) == false ||
                    (nowUtc - state.WindowStartedAtUtc).TotalMilliseconds >= UnregisteredRateWindowMs)
                {
                    state = new EndpointRateState
                    {
                        WindowStartedAtUtc = nowUtc,
                        Count = 0,
                        SuppressedCount = 0
                    };
                    _unregisteredEndpointRates[key] = state;
                }

                state.Count++;
                if (state.Count <= MaxUnregisteredLogsPerWindow)
                {
                    suppressed = state.SuppressedCount;
                    state.SuppressedCount = 0;
                    return true;
                }

                state.SuppressedCount++;
                return false;
            }
        }
    }
}
