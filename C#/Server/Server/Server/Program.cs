using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.Protocol;
using Google.Protobuf.WellKnownTypes;
using Server.DB;
using Server.Game.Room;
using ServerCore;

namespace Server
{
    class Program
	{
		static Listener _listener = new Listener();

		static void FlushRoom()
		{
			JobTimer.Instance.Push(FlushRoom, 250);
		}
		 
		static void Main(string[] args)
		{
			RoomManager.Instance.Add(RoomType.Town);

			
            // DNS (Domain Name System)
            string host = Dns.GetHostName();
			IPHostEntry ipHost = Dns.GetHostEntry(host);
			IPAddress ipAddr = ipHost.AddressList[0];

			ipAddr = IPAddress.Parse("172.31.10.77");
			
            IPEndPoint endPoint = new IPEndPoint(ipAddr, 8080);

            _listener.Init(endPoint, () => { return SessionManager.Instance.Generate(); });
			Console.WriteLine("Listening...");

			//FlushRoom();
			//JobTimer.Instance.Push(FlushRoom);

			while (true)
			{
				RoomManager.Instance.UpdateRooms();
				JobTimer.Instance.Flush();
				Thread.Sleep(250);
			}
		}
	}
}
