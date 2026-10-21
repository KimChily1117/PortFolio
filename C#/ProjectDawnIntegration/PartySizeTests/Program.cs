using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using Google.Protobuf;
using Google.Protobuf.Protocol;
using ProjectDawn.Combat;
using ProjectDawn.Network;
using Server;
using Server.DB;
using Server.Data;
using Server.Game.Room;
using DnfRaidDemo;
class Program {
 static int checks;static readonly List<Peer> peers=new List<Peer>();
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
 static T Field<T>(object x,string name)=>(T)x.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(x);
 static void Pump(int ms){var t=Stopwatch.StartNew();while(t.ElapsedMilliseconds<ms){RoomManager.Instance.UpdateRooms();DbTransaction.Instance.Flush();foreach(var p in peers.ToArray())p.Poll();Thread.Sleep(2);}}
 static void Until(Func<bool> f,string name){var t=Stopwatch.StartNew();while(!f()){Pump(5);if(t.ElapsedMilliseconds>15000)throw new Exception("Timeout "+name);}}
 static Peer Login(string name){var p=new Peer();peers.Add(p);p.Name=name;p.Client.Negotiate(3);p.Send(new C_Login{UniqueId=name});Until(()=>p.Messages.OfType<S_Login>().Any(),"login");if(!p.Messages.OfType<S_Login>().Last().Players.Any()){p.Send(new C_CreatePlayer{Name=name});Until(()=>p.Messages.OfType<S_CreatePlayer>().Any(),"create");}p.Send(new C_EnterGame{Name=name});return p;}
 static void Core(){
  foreach(int n in Enumerable.Range(1,4)){
   var c=new RaidContentState(Enumerable.Range(1,n).ToArray(),99);var s=c.Simulation;
   Check(s.Players.Count==n&&s.Boss.MaxHp==32500,"roster and fixed boss HP "+n);
   Check(RaidSnapshotCodec.Encode(s).SequenceEqual(RaidSnapshotCodec.Encode(RaidSnapshotCodec.Decode(RaidSnapshotCodec.Encode(s)))),"snapshot roundtrip "+n);
   var lua=new LuaPatternPlanner();
   for(int ordinal=0;ordinal<12;ordinal++){
    s.SelectionOrdinal=ordinal;s.PendingPatternTick=100;s.SelectionRoll=(uint)ordinal;s.SelectionPhase=1;s.SelectionAliveMask=(1<<n)-1;
    var plan=lua.Request(s);
    Check(BossPatternProgram.Valid(plan.Program,plan.Pattern,ordinal,s.SelectionRoll,s.SelectionAliveMask,0,n)&&BossPatternProgram.Parse(plan.Program).All(a=>a.Target<n),"Lua/server targets "+n+" ordinal "+ordinal);
    if(n<4)Check(!BossPatternProgram.Valid("0,0,3;1,36,3",0,ordinal,0,(1<<n)-1,0,n),"nonparticipant target rejected "+n+" ordinal "+ordinal);
   }
   s.RoomSelectionAliveMask=(1<<n)-1;s.RoomSelectionRoll=3;s.PendingRoomMarkTick=300;
   var marks=lua.RequestRoom(s);var targets=BossPatternProgram.Parse(marks.Program);
   Check(BossPatternProgram.ValidRoomMarks(marks.Program,3,(1<<n)-1,0)&&targets.Length==Math.Min(2,n)&&targets.Select(x=>x.Target).Distinct().Count()==targets.Length,"bounded room marks "+n);
   var policy=new RaidParticipation(n);policy.Disconnect(n-1,0);Check(policy.Finish(1).Length==n,"participation reward size "+n);
  }
 }
 static void Integration(){
  ConfigManager.LoadConfig();DataManager.LoadData();DatabaseStartup.Verify(false);RaidRewardOutbox.Start();RoomManager.Instance.Add(RoomType.Town);
  string prefix="size_"+Guid.NewGuid().ToString("N").Substring(0,8);
  var groups=Enumerable.Range(1,4).ToDictionary(n=>n,n=>Enumerable.Range(0,n).Select(i=>Login(prefix+"_"+n+"_"+i)).ToArray());
  Until(()=>peers.All(p=>p.Session.MyPlayer?.Room?.RoomType==RoomType.Town),"town");
  // Seed only these isolated test sessions; matching must forward server-owned looks.
  foreach(var p in peers)p.Session.MyPlayer.Info.Appearance="skin:0003|face:0000|hair:24105|coat:27500|weapon:club:7600";
  var invalid=groups[4][3];foreach(int size in new[]{-1,5}){int before=invalid.Messages.OfType<S_CreateRoom>().Count();invalid.Send(new C_CreateRoom{PartySize=size});Until(()=>invalid.Messages.OfType<S_CreateRoom>().Count()>before,"invalid size");Check(invalid.Messages.OfType<S_CreateRoom>().Last().ResponseCode==0,"server rejects size "+size);}
  foreach(int n in new[]{4,3,2})foreach(var p in groups[n].Take(n-1))p.Send(new C_CreateRoom{PartySize=n==4?0:n});
  Pump(250);Check(peers.All(p=>!p.Messages.OfType<S_SceneMove>().Any()),"underfilled queues of different sizes never combine");
  foreach(int n in Enumerable.Range(1,4)){
   var group=groups[n];group[n-1].Send(new C_CreateRoom{PartySize=n});Until(()=>group.All(p=>p.Messages.OfType<S_SceneMove>().Any()),"match "+n);
   Check(group.All(p=>p.Messages.OfType<S_EnterParty>().Last().PartyMembers.Count==n),"party packet count "+n);
   Check(group.All(p=>p.Messages.OfType<S_EnterParty>().Last().PartyMembers.All(m=>m.Appearance==group.Single(g=>g.Name==m.Name).Session.MyPlayer.Info.Appearance)),"server appearance accompanies every matching slot "+n);
   foreach(var p in group)p.Ready();Until(()=>group.All(p=>p.Client.Start!=null),"start "+n);
   Check(group.All(p=>p.Client.Start.PlayerSlots.Count==n&&RaidSnapshotCodec.Decode(p.Client.Start.Content.ToByteArray()).Players.Count==n),"TCP start roster "+n);
   Check(group.Count(p=>p.Client.IsHost)==1&&group.Select(p=>p.Client.Start.Encounter).Distinct().Count()==1,"single host and encounter "+n);
  }
  Check(groups.Values.Select(g=>g[0].Client.Start.Encounter).Distinct().Count()==4,"four isolated encounters");
  Pump(800);
  var pair=groups[2];var leaving=pair.Single(p=>!p.Client.IsHost);int self=leaving.Client.Start.Self;leaving.Dispose();Until(()=>leaving.Session.ConnectionClosed,"disconnect");Pump(30);
  var resumed=Login(leaving.Name);Until(()=>resumed.Messages.OfType<S_SceneMove>().Any(),"resume scene");resumed.Ready();Until(()=>resumed.Client.Start!=null,"resume start");
  Check(resumed.Client.Start.Resumed&&resumed.Client.Start.Self==self&&resumed.Client.Start.PlayerSlots.Count==2,"two-player reconnect retains roster and identity");pair[Array.IndexOf(pair,leaving)]=resumed;
  foreach(int n in Enumerable.Range(1,4)){
   var group=groups[n];var host=group.Single(p=>p.Client.IsHost);var room=host.Session.MyPlayer.Room;
   var engine=Field<Server.Game.RaidCombatSession>(room,"_raid").Engine;
   // Test-only fixture: production clear, outbox, SQL, and return paths remain unchanged.
   var state=Field<CombatState>(engine,"_final");state.Content.Simulation.Mode=EncounterMode.Victory;state.Content.Simulation.Boss.Hp=0;state.Fighters[state.Content.BossId].Hp=0;
   Until(()=>host.Messages.OfType<S_RaidRewards>().Any(r=>r.Status=="Saved"),"durable rewards "+n);
   var record=Field<RaidRewardRecord>(room,"_raidReward");RaidRewardStore.Save(record);RaidRewardStore.Save(record);
   var history=RaidRewardStore.History(host.Session.AccountDbId,host.Session.MyPlayer.PlayerDbId,record.Encounter);
   Check(history.Entries.Count==n&&history.TotalTokens==100&&history.Entries.All(e=>e.Eligible&&e.Amount==100&&e.EquipmentTemplate>=92001),"SQL reward recipients and idempotent retry "+n);
   Until(()=>host.Messages.OfType<S_RaidCinematic>().Any(c=>c.ElapsedMs>=RaidClearCinematic.FadeMs),"cinematic voting ready "+n);
   foreach(var p in group)p.Send(new C_RaidCinematicSkip{Encounter=p.Client.Start.Encounter});
   Until(()=>group.All(p=>p.Messages.OfType<S_RaidCinematic>().Any(c=>c.Complete)),"majority result barrier "+n);
   foreach(var p in group)p.Send(new C_RaidLeave{Encounter=p.Client.Start.Encounter});Until(()=>group.All(p=>p.Messages.OfType<S_SceneMove>().Last().TargetRoomType==RoomType.Town),"return transfer "+n);
   foreach(var p in group)p.Ready();Until(()=>group.All(p=>p.Session.MyPlayer?.Room?.RoomType==RoomType.Town&&p.Messages.OfType<S_ChatContext>().Last().Scope==ChatScope.ChatTown),"returned town "+n);
   Check(group.All(p=>p.Messages.OfType<S_ChatContext>().Last().Scope==ChatScope.ChatTown),"town and chat restored "+n);
  }
 }
 static int Main(){try{Core();Integration();Console.WriteLine("[PARTY_SIZE_VALIDATED] "+checks);return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}finally{foreach(var p in peers)p.Dispose();}}
 sealed class Peer:IDisposable {
  readonly TcpClient tcp;readonly List<byte> data=new List<byte>();bool closed;public string Name;public ClientSession Session;public RaidNetworkClient Client=new RaidNetworkClient();public List<IMessage> Messages=new List<IMessage>();
  static readonly Dictionary<MsgId,MessageParser> parsers=ProtocolReflection.Descriptor.MessageTypes.Concat(RaidReflection.Descriptor.MessageTypes).Where(x=>Enum.TryParse<MsgId>(x.Name.Replace("_",""),out _)).ToDictionary(x=>(MsgId)Enum.Parse(typeof(MsgId),x.Name.Replace("_","")),x=>x.Parser);
  public Peer(){var l=new TcpListener(IPAddress.Loopback,0);l.Start();tcp=new TcpClient("127.0.0.1",((IPEndPoint)l.LocalEndpoint).Port);var socket=l.AcceptSocket();l.Stop();tcp.NoDelay=true;socket.NoDelay=true;Session=SessionManager.Instance.Generate();Session.Start(socket);Session.OnConnected(socket.RemoteEndPoint);Client.Send=Send;}
  public void Send(IMessage m){if(closed)return;byte[] b=m.ToByteArray(),p=new byte[b.Length+4];BitConverter.GetBytes((ushort)p.Length).CopyTo(p,0);BitConverter.GetBytes((ushort)(MsgId)Enum.Parse(typeof(MsgId),m.Descriptor.Name.Replace("_",""))).CopyTo(p,2);b.CopyTo(p,4);tcp.GetStream().Write(p);}
  public void Ready(){var m=Messages.OfType<S_SceneMove>().Last();Send(new C_SceneReady{TargetRoomId=m.TargetRoomId,TargetRoomType=m.TargetRoomType,TransferId=m.TransferId,SceneType=m.SceneType});}
  public void Poll(){if(closed)return;while(tcp.Available>0){var b=new byte[Math.Min(tcp.Available,65536)];int n=tcp.GetStream().Read(b);data.AddRange(b.Take(n));}while(data.Count>=4){int n=data[0]|data[1]<<8;if(data.Count<n)break;var id=(MsgId)(data[2]|data[3]<<8);var b=data.GetRange(4,n-4).ToArray();data.RemoveRange(0,n);if(parsers.TryGetValue(id,out var parser)){var m=parser.ParseFrom(b);Messages.Add(m);Client.Handle(m);}}}
  public void Dispose(){if(closed)return;closed=true;tcp.Dispose();}
 }
}
