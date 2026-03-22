using Server.Game.Room;
using Server.Packet;
using Server.Session;
using ServerCore;
using System;
using System.Net;
using System.Threading;

namespace Server
{
    class Program
    {
        static Listener _listener = new Listener();

        static void Main(string[] args)
        {
            PacketHandler.Init();

            string host = "127.0.0.1";
            int port = 8080;

            IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse(host), port);
            _listener.Init(endPoint, () => new ClientSession());

            Console.WriteLine("Server Start: " + host + ":" + port);

            while (true)
            {
                RoomManager.Instance.Tick();
                Thread.Sleep(33); // 30 TPS
            }
        }
    }
}