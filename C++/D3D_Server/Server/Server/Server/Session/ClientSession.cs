using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using ServerCore;
using System.Net;
using Google.Protobuf.Protocol;
using Google.Protobuf;
using Server.Game.Objects;
using Server.Game.Room;

namespace Server
{
    public partial class ClientSession : PacketSession
    { 
        public int SessionId { get; set; }

        public GameRoom GameRoom { get; set; }

        public Player Player { get; set; }

        private bool _hasProcessedMoveSequence;
        private uint _lastProcessedMoveSequence;
        private const double MoveRequestTokensPerSecond = 12.0;
        private const double MoveRequestBurstCapacity = 4.0;
        private double _moveRequestTokens = MoveRequestBurstCapacity;
        private long _moveRequestBudgetTimestamp;

        public uint LastProcessedMoveSequence => _lastProcessedMoveSequence;

        internal bool TryConsumeMoveSequence(uint sequence)
        {
            if (sequence == 0 || (_hasProcessedMoveSequence && sequence <= _lastProcessedMoveSequence))
                return false;

            _hasProcessedMoveSequence = true;
            _lastProcessedMoveSequence = sequence;
            return true;
        }

        internal bool TryConsumeMoveRequestBudget(long timestamp)
        {
            if (timestamp < 0)
                return false;
            if (_moveRequestBudgetTimestamp == 0)
            {
                _moveRequestBudgetTimestamp = timestamp;
            }
            else if (timestamp >= _moveRequestBudgetTimestamp)
            {
                double elapsedSeconds = (timestamp - _moveRequestBudgetTimestamp) / (double)Stopwatch.Frequency;
                _moveRequestTokens = Math.Min(MoveRequestBurstCapacity, _moveRequestTokens + elapsedSeconds * MoveRequestTokensPerSecond);
                _moveRequestBudgetTimestamp = timestamp;
            }

            if (_moveRequestTokens < 1.0)
                return false;
            _moveRequestTokens -= 1.0;
            return true;
        }

        internal void ResetMoveSequence()
        {
            _hasProcessedMoveSequence = false;
            _lastProcessedMoveSequence = 0;
            _moveRequestTokens = MoveRequestBurstCapacity;
            _moveRequestBudgetTimestamp = 0;
        }

        #region Network
        public void Send(IMessage packet)
        {
            string msgName = packet.Descriptor.Name.Replace("_", string.Empty);
            MsgId msgId = (MsgId)Enum.Parse(typeof(MsgId), msgName,ignoreCase : true);
            ushort size = (ushort)packet.CalculateSize();
            byte[] sendBuffer = new byte[size + 4];
            Array.Copy(BitConverter.GetBytes((ushort)(size + 4)), 0, sendBuffer, 0, sizeof(ushort));
            Array.Copy(BitConverter.GetBytes((ushort)msgId), 0, sendBuffer, 2, sizeof(ushort));
            Array.Copy(packet.ToByteArray(), 0, sendBuffer, 4, size);
            Send(new ArraySegment<byte>(sendBuffer));
        }

        public override void OnConnected(EndPoint endPoint)
        {
            //2
            Console.WriteLine($"OnConnected : {endPoint}");

            // TODO Enter 패킷 보내준다
            {
                var recv = new S_EnterGame();
                recv.AccountId = (ulong)SessionId;
                recv.Success = true;
                Send(recv);
            }

            var room = RoomManager.Instance.Find(0);
            GameRoom = room;
            room.EnterRoom(this);
        }

        public override void OnRecvPacket(ArraySegment<byte> buffer)
        {
            PacketManager.Instance.OnRecvPacket(this, buffer);
        }

        public override void OnDisconnected(EndPoint endPoint)
        {

            GameRoom room = GameRoom;
            room?.RemoveObject((ulong)SessionId);
            SessionManager.Instance.Remove(this);
            Console.WriteLine($"OnDisconnected : {endPoint}");
        }

        public override void OnSend(int numOfBytes)
        {
            //Console.WriteLine($"Transferred bytes: {numOfBytes}");
        }
        #endregion Network
    } 
}
