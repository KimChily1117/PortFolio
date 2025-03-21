﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
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

            this.GameRoom.RemoveObject((ulong)SessionId);
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
