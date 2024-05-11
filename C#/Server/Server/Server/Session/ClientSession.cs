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

namespace Server
{
    public partial class ClientSession : PacketSession
    {
        public PlayerServerState ServerState { get; private set; }

        public Player MyPlayer { get; set; }
        public int SessionId { get; set; }

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
            if (MyPlayer != null)
            {
                RoomManager.Instance.Find(RoomType.Town)?.LeaveRoom(MyPlayer.Info.ObjectId);
                RoomManager.Instance.Find(RoomType.Bakal)?.LeaveRoom(MyPlayer.Info.ObjectId);

            }
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
            // Step 1 : 개설 된 방이 없다면 방(Room , 파티)를 새로 개설.

            if (RoomManager.Instance.Find(RoomType.Bakal) == null)
            {
                GameRoom room = RoomManager.Instance.Add(RoomType.Bakal);
              
                
                Player p = ObjectManager.Instance.Find(c_CreateRoom.Playerinfo.ObjectId);
                p.Info.IsMaster = true;

                GameRoom preRoom = RoomManager.Instance.Find(RoomType.Town);
                preRoom.LeaveRoom(p.Id);

                room.EnterParty(p);
                room.InitEnemy();
            };
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
