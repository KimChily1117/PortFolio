using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Server;
using Server.Data;
using Server.DB;
using Server.Game.Room;
using Google.Protobuf.Protocol;
class Program {
 static int Main(string[] args) {
  bool initialize=Array.IndexOf(args,"--initialize-empty-database")>=0;
  int port=args.Length>0 && !initialize?int.Parse(args[0]):28182;
  if(initialize && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PROJECT_DAWN_DB_CONNECTION_STRING"))){Console.Error.WriteLine("Explicit DB connection is required for initialization");return 1;}
  try {ConfigManager.LoadConfig();DataManager.LoadData();DatabaseStartup.Verify(initialize);}catch(Exception e){Console.Error.WriteLine("[FLOW_DB_NOT_READY] "+e.GetType().Name);return 1;}
  if(initialize)return 0;
  RaidRewardOutbox.Start();RaidRewardOutbox.Flush();
  RoomManager.Instance.Add(RoomType.Town);
  RoomManager.Instance.Add(RoomType.Town);
  var channels=new Server.Monitoring.MonitoringApiHost(RoomManager.Instance,new Server.Monitoring.MonitoringApiOptions());
  try{channels.Start();}catch(Exception e){Console.Error.WriteLine("[CHANNEL_LIST_UNAVAILABLE] "+e.Message);}
  var listener=new TcpListener(IPAddress.Loopback,port);listener.Start(16);
  Console.WriteLine("[DAWN_FLOW_SERVER_READY] SQL login / original session / port="+port);
  while(true) {
   while(listener.Pending()){var socket=listener.AcceptSocket();socket.NoDelay=true;var session=SessionManager.Instance.Generate();session.Start(socket);session.OnConnected(socket.RemoteEndPoint);}
   RoomManager.Instance.UpdateRooms();DbTransaction.Instance.Flush();Thread.Sleep(5);
  }
 }
}
