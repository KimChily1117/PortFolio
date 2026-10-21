using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using Server;
using Server.Data;
using Server.DB;
using Server.Game.Room;
using ProjectDawn.Combat;
using Google.Protobuf.Protocol;
// Test-only loopback executable. No fixture hook or command exists in the shipping server.
// SQL/auth/matching/skills/clear/rewards are production paths. Boss HP is reduced after 15s
// only to bound this UI/transaction acceptance test; clients must land the killing attack.
class Program {
 static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
 static int Main(string[] args) {
  int port=int.Parse(args[0]);ConfigManager.LoadConfig();DataManager.LoadData();DatabaseStartup.Verify(false);
  RaidRewardOutbox.Start();RoomManager.Instance.Add(RoomType.Town);RoomManager.Instance.Add(RoomType.Town);
  var listener=new TcpListener(IPAddress.Loopback,port);listener.Start();var clock=Stopwatch.StartNew();var prepared=new HashSet<int>();
  Console.WriteLine("[REWARD_FIXTURE_READY] loopback="+port+" production SQL; reduced boss HP fixture after 15 seconds");
  try {while(clock.Elapsed.TotalSeconds<160){
   while(listener.Pending()){var socket=listener.AcceptSocket();socket.NoDelay=true;var session=SessionManager.Instance.Generate();session.Start(socket);session.OnConnected(socket.RemoteEndPoint);}
   RoomManager.Instance.UpdateRooms();DbTransaction.Instance.Flush();
   foreach(var view in RoomManager.Instance.GetRoomSnapshots()){
    var room=RoomManager.Instance.Find(view.RoomId);if(room==null||view.RoomType!="Bakal"||prepared.Contains(view.RoomId))continue;
    var combat=Field<object>(room,"_raid");if(combat==null)continue;
    var engine=(EncounterEngine)combat.GetType().GetProperty("Engine").GetValue(combat);var state=Field<CombatState>(engine,"_final");
    if(state.Content==null||state.Content.Simulation.TickIndex<DnfRaidDemo.RaidSimulation.EncounterIntroTicks+60)continue;
    if(room.GetPlayersSnapshot().Exists(p=>!p.Info.Name.StartsWith("PD_RwdFixture_")))throw new InvalidOperationException("Only isolated fixture characters are allowed");
    state.Content.Simulation.Boss.Hp=500;state.Fighters[state.Content.BossId].Hp=500;prepared.Add(view.RoomId);
    Console.WriteLine("[REWARD_FIXTURE_HP] encounter="+engine.EncounterId+" tick="+state.Content.Simulation.TickIndex+" hp=500; next actual attack must kill");
   }
   Thread.Sleep(5);
  }}finally{listener.Stop();}
  return 0;
 }
}
