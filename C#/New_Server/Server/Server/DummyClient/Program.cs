using Google.Protobuf;
using Server.Protocol;
using System;
using System.Net;
using System.Net.NetworkInformation;
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

            Console.WriteLine("Connected to server.");

            // 1) Ping 전송
            C_Ping ping = new C_Ping
            {
                Sequence = 1,
                Message = "hello"
            };

            SendPacket(socket, MsgId.CPing, ping);
            Console.WriteLine("Ping sent.");

            // 2) Pong 수신
            ReceiveOnePacket(socket);

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

        static void ReceiveOnePacket(Socket socket)
        {
            // 먼저 헤더 4바이트 읽기
            byte[] headerBuffer = ReceiveExactly(socket, 4);

            ushort size = BitConverter.ToUInt16(headerBuffer, 0);
            ushort packetId = BitConverter.ToUInt16(headerBuffer, 2);

            int payloadSize = size - 4;
            byte[] payloadBuffer = ReceiveExactly(socket, payloadSize);

            Console.WriteLine($"Received packetId={packetId}, size={size}");

            if ((MsgId)packetId == MsgId.SPong)
            {
                S_Pong pong = S_Pong.Parser.ParseFrom(payloadBuffer);
                Console.WriteLine($"Pong received! seq={pong.Sequence}, msg={pong.Message}");
            }
            else
            {
                Console.WriteLine("Unknown or unexpected packet.");
            }
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
    }
}