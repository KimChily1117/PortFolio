using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.Protocol;
using Google.Protobuf.WellKnownTypes;
using Server.Data;
using Server.DB;
using Server.Game.Room;
using ServerCore;

namespace Server
{
    class Program
	{
		static Listener _listener = new Listener();
        
		
		static List<System.Timers.Timer> _timers = new List<System.Timers.Timer>();

        static void TickRooms(int tick = 100)
        {
            var timer = new System.Timers.Timer();
            timer.Interval = tick;
            timer.Elapsed += ((s,e) => { RoomManager.Instance.UpdateRooms(); });
            timer.AutoReset = true;
            timer.Enabled = true;

            _timers.Add(timer);
        }

        static void Main(string[] args)
		{
			ConfigManager.LoadConfig();
			DataManager.LoadData();
            RoomManager.Instance.Add(RoomType.Town);

			
            // DNS (Domain Name System)
            string host = Dns.GetHostName();
			IPHostEntry ipHost = Dns.GetHostEntry(host);
			IPAddress ipAddr = ipHost.AddressList[1];
            IPEndPoint endPoint = new IPEndPoint(ipAddr, 8080);

            Console.WriteLine($"ipadd : {ipAddr.ToString()}");
            _listener.Init(endPoint, () => { return SessionManager.Instance.Generate(); });
			Console.WriteLine("Listening...");

			TickRooms(); // 100ms 마다 만들어진 Room에 Update함수를 호출해줌 

			//JobTimer.Instance.Push(FlushRoom);

			while (true)
			{
				Thread.Sleep(100);

				DbTransaction.Instance.Flush();
			}
		}
	}
}
