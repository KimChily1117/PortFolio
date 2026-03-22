using Google.Protobuf;
using Server.Protocol;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace DummyClient
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string host = "127.0.0.1";
            int port = 8080;

            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(new IPEndPoint(IPAddress.Parse(host), port));
            socket.ReceiveTimeout = 1000;

            Console.WriteLine("Connected to server.");

            // 1. Ping
            C_Ping ping = new C_Ping();
            ping.Sequence = 1;
            ping.Message = "hello";

            SendPacket(socket, MsgId.CPing, ping);
            Console.WriteLine("Ping sent.");

            ReceiveOnePacket(socket); // Pong

            // 2. 오른쪽 이동 입력 여러 번
            uint seq = 1;

            for (int i = 0; i < 5; i++)
            {
                C_MoveInput move = new C_MoveInput();
                move.InputSeq = seq++;
                move.ClientTick = i;
                move.MoveX = 1;
                move.MoveY = 0;

                SendPacket(socket, MsgId.CMoveInput, move);
                Console.WriteLine("MoveInput sent. seq=" + move.InputSeq);

                TryReceiveOnePacket(socket);
                Thread.Sleep(100);
            }

            // 3. 정지
            C_MoveInput stop = new C_MoveInput();
            stop.InputSeq = seq++;
            stop.ClientTick = 999;
            stop.MoveX = 0;
            stop.MoveY = 0;

            SendPacket(socket, MsgId.CMoveInput, stop);
            Console.WriteLine("Stop sent.");

            // 정지 후 마지막 snapshot 수신 시도
            for (int i = 0; i < 3; i++)
            {
                if (!TryReceiveOnePacket(socket))
                    break;
            }

            // 4. 공격 입력
            C_ActionInput action = new C_ActionInput();
            action.InputSeq = seq++;
            action.ActionType = ActionType.ActionAttack;
            action.DirX = 1;
            action.DirY = 0;

            SendPacket(socket, MsgId.CActionInput, action);
            Console.WriteLine("ActionInput sent. seq=" + action.InputSeq);

            // CombatEvents 수신 시도
            for (int i = 0; i < 5; i++)
            {
                if (!TryReceiveOnePacket(socket))
                    break;
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();

            socket.Close();
        }

        static void SendPacket(Socket socket, MsgId msgId, IMessage packet)
        {
            byte[] payload = packet.ToByteArray();

            ushort size = (ushort)(payload.Length + 4);
            ushort packetId = (ushort)msgId;

            byte[] buffer = new byte[size];

            Array.Copy(BitConverter.GetBytes(size), 0, buffer, 0, 2);
            Array.Copy(BitConverter.GetBytes(packetId), 0, buffer, 2, 2);
            Array.Copy(payload, 0, buffer, 4, payload.Length);

            socket.Send(buffer);
        }

        static byte[] ReceiveExactly(Socket socket, int size)
        {
            byte[] buffer = new byte[size];
            int received = 0;

            while (received < size)
            {
                int recv = socket.Receive(buffer, received, size - received, SocketFlags.None);
                if (recv == 0)
                    throw new Exception("Disconnected from server.");

                received += recv;
            }

            return buffer;
        }

        static bool TryReceiveOnePacket(Socket socket)
        {
            try
            {
                ReceiveOnePacket(socket);
                return true;
            }
            catch (SocketException)
            {
                Console.WriteLine("Receive timeout.");
                return false;
            }
        }

        static void ReceiveOnePacket(Socket socket)
        {
            byte[] headerBuffer = ReceiveExactly(socket, 4);

            ushort size = BitConverter.ToUInt16(headerBuffer, 0);
            ushort packetId = BitConverter.ToUInt16(headerBuffer, 2);

            int payloadSize = size - 4;
            byte[] payloadBuffer = ReceiveExactly(socket, payloadSize);

            HandleReceivedPacket(packetId, payloadBuffer);
        }

        static void HandleReceivedPacket(ushort packetId, byte[] payloadBuffer)
        {
            Console.WriteLine("Received packetId=" + packetId);

            if ((MsgId)packetId == MsgId.SPong)
            {
                S_Pong pong = S_Pong.Parser.ParseFrom(payloadBuffer);
                Console.WriteLine("Pong received! seq=" + pong.Sequence + ", msg=" + pong.Message);
            }
            else if ((MsgId)packetId == MsgId.SRoomSnapshot)
            {
                S_RoomSnapshot snapshot = S_RoomSnapshot.Parser.ParseFrom(payloadBuffer);

                Console.WriteLine("Snapshot received! tick=" + snapshot.ServerTick + ", ack=" + snapshot.AckInputSeq);

                foreach (ActorSnapshot actor in snapshot.Actors)
                {
                    Console.WriteLine(
                        " ActorId=" + actor.ActorId +
                        " Pos=(" + actor.Pos.X + "," + actor.Pos.Y + ")" +
                        " State=" + actor.MainState);
                }
            }
            else if ((MsgId)packetId == MsgId.SCombatEvents)
            {
                S_CombatEvents combatEvents = S_CombatEvents.Parser.ParseFrom(payloadBuffer);

                Console.WriteLine("CombatEvents received! tick=" + combatEvents.ServerTick);

                foreach (CombatEvent ev in combatEvents.Events)
                {
                    Console.WriteLine(" EventType=" + ev.EventType +
                        " AttackerId=" + ev.AttackerId +
                        " TargetId=" + ev.TargetId +
                        " ActionType=" + ev.ActionType +
                        " Value=" + ev.Value);
                }
            }
            else
            {
                Console.WriteLine("Unknown packet");
            }
        }
    }
}