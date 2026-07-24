using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using ServerCore;
using System.Net;
using Google.Protobuf.Protocol;
using Google.Protobuf;
using Server.Game.Room;
using Server.Game.Object;
using Server.Game.Match;

namespace Server
{
    public partial class ClientSession : PacketSession
    {
        public PlayerServerState ServerState { get; private set; }

        public Player MyPlayer { get; set; }
        public int SessionId { get; set; }
        public string UdpToken { get; set; }
        public DateTime UdpTokenExpiresAt { get; set; }
        public EndPoint UdpEndPoint { get; set; }
        public DateTime LastUdpSeenAt { get; set; }
        public bool HasLastUdpMoveSequence { get; set; }
        public uint LastUdpMoveSequence { get; set; }
        public DateTime UdpMoveRateWindowStartedAt { get; set; }
        public int UdpMoveRateWindowCount { get; set; }
        public int UdpMoveRateLimitedDropCount { get; set; }
        public bool HasLastAcceptedUdpMove { get; set; }
        public float LastAcceptedUdpMoveX { get; set; }
        public float LastAcceptedUdpMoveY { get; set; }
        public DateTime LastAcceptedUdpMoveAt { get; set; }
        public int UdpMoveSequenceDropCount { get; set; }
        public int UdpMoveValidationDropCount { get; set; }
        public bool IsTransferring { get; set; }
        public int PendingRoomId { get; set; }
        public RoomType PendingRoomType { get; set; }
        public int PendingTransferId { get; set; }
        public SceneType PendingSceneType { get; set; }
        public int PendingMatchPartyId { get; set; }

        public void ClearPendingTransfer()
        {
            IsTransferring = false;
            PendingRoomId = 0;
            PendingRoomType = RoomType.Town;
            PendingTransferId = 0;
            PendingSceneType = SceneType.SceneNone;
            PendingMatchPartyId = 0;
        }

        public void ClearUdpSecurityState()
        {
            UdpToken = null;
            UdpTokenExpiresAt = DateTime.MinValue;
            UdpEndPoint = null;
            LastUdpSeenAt = DateTime.MinValue;
            ResetUdpMoveSecurityState();
        }

        public void ResetUdpMoveSecurityState()
        {
            HasLastUdpMoveSequence = false;
            LastUdpMoveSequence = 0;
            UdpMoveRateWindowStartedAt = DateTime.MinValue;
            UdpMoveRateWindowCount = 0;
            UdpMoveRateLimitedDropCount = 0;
            HasLastAcceptedUdpMove = false;
            LastAcceptedUdpMoveX = 0f;
            LastAcceptedUdpMoveY = 0f;
            LastAcceptedUdpMoveAt = DateTime.MinValue;
            UdpMoveSequenceDropCount = 0;
            UdpMoveValidationDropCount = 0;
        }

        #region Network
        public void Send(IMessage packet)
        {
            string msgName = packet.Descriptor.Name.Replace("_", string.Empty);
            MsgId msgId = (MsgId)Enum.Parse(typeof(MsgId), msgName);
            ushort size = (ushort)packet.CalculateSize();
            byte[] sendBuffer = new byte[size + 4];
            Array.Copy(BitConverter.GetBytes((ushort)(size + 4)), 0, sendBuffer, 0, sizeof(ushort));
            Array.Copy(BitConverter.GetBytes((ushort)msgId), 0, sendBuffer, 2, sizeof(ushort));
            Array.Copy(packet.ToByteArray(), 0, sendBuffer, 4, size);
            Send(new ArraySegment<byte>(sendBuffer));
        }

        public override void OnConnected(EndPoint endPoint)
        {
            Console.WriteLine($"OnConnected : {endPoint}");
            {
                S_Connected s_Connected = new S_Connected();
                Send(s_Connected);
            }
        }

        public override void OnRecvPacket(ArraySegment<byte> buffer)
        {
            PacketManager.Instance.OnRecvPacket(this, buffer);
        }

        public override void OnDisconnected(EndPoint endPoint)
        {
            MatchManager.Instance.OnDisconnected(this);

            if (MyPlayer != null)
            {
                GameRoom currentRoom = MyPlayer.Room;
                if (currentRoom != null && IsTransferring == false)
                {
                    currentRoom.LeaveRoom(MyPlayer.Info.ObjectId);
                }
                else if (currentRoom != null)
                {
                    Console.WriteLine($"[SESSION] Disconnect during transfer. Room leave is owned by transfer flow. SessionId={SessionId}, PlayerId={MyPlayer.Id}, RoomId={currentRoom.RoomId}");
                }
            }

            ClearUdpSecurityState();
            SessionManager.Instance.Remove(this);

            Console.WriteLine($"OnDisconnected : {endPoint}");
        }

        public override void OnSend(int numOfBytes)
        {
            //Console.WriteLine($"Transferred bytes: {numOfBytes}");
        }
        #endregion Network


        public void HandleCreateRoom(C_CreateRoom c_CreateRoom)
        {
            MatchManager.Instance.RequestMatch(this);
        }


        public void HandleEnterParty(C_EnterParty c_EnterParty)
        {
            // 이미 만들어져있음.(파장이 아닌 일반 파티원이 들어간다는뜻)
            GameRoom room = RoomManager.Instance.Find(RoomType.Bakal);

            Player p = ObjectManager.Instance.Find(c_EnterParty.Playerinfo.ObjectId);
            c_EnterParty.Playerinfo.IsMaster = false;

            RoomManager.Instance.Find(RoomType.Town).LeaveRoom(p.Info.ObjectId);
            room.Push(room.EnterParty, p);
        }

        public void HandleEnterParty(C_CreateRoom c_Create_Room)
        {
            GameRoom room = RoomManager.Instance.Find(RoomType.Bakal);

            Player p = ObjectManager.Instance.Find(c_Create_Room.Playerinfo.ObjectId);
            c_Create_Room.Playerinfo.IsMaster = false;

            GameRoom preRoom = RoomManager.Instance.Find(RoomType.Town);
            preRoom.LeaveRoom(p.Id);

            room.EnterParty(p);

        }

    } 
}

