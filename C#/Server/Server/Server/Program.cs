using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Google.Protobuf.Protocol;
using Server.Data;
using Server.DB;
using Server.Game.Room;
using Server.Monitoring;
using Server.Udp;
using ServerCore;


namespace Server
{
	class Program
	{
		static Listener _listener = new Listener();
		static UdpListener _udpListener = new UdpListener();
        static UdpGamePacketHandler _udpPacketHandler = new UdpGamePacketHandler();
        static MonitoringApiHost _monitoringApiHost;
        
		
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
            // TCP
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 8080);

            Console.WriteLine($"TCP Listening : {endPoint}");
            _listener.Init(endPoint, () => { return SessionManager.Instance.Generate(); });

            // UDP
            _udpPacketHandler.SetSender(_udpListener.SendTo);
            _udpListener.UdpDatagramHandler = _udpPacketHandler.HandleDatagram;
            _udpListener.Init(new IPEndPoint(IPAddress.Any, 8081));

            Console.WriteLine($"UDP Listening : 0.0.0.0:8081");
            try
            {
                _monitoringApiHost = new MonitoringApiHost(RoomManager.Instance, new MonitoringApiOptions());
                _monitoringApiHost.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MONITOR_API][ERROR] Failed to start monitoring API. {ex}");
            }

            Console.CancelKeyPress += (sender, e) =>
            {
                _udpListener.Close();
                _monitoringApiHost?.Stop();
            };

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


