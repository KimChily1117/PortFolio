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
 static Peer Login(string name){var p=new Peer();peers.Add(p);p.Client.Negotiate(3);p.Send(new C_Login{UniqueId=name});Until(()=>p.Messages.OfType<S_Login>().Any(),"login");if(!p.Messages.OfType<S_Login>().Last().Players.Any()){p.Send(new C_CreatePlayer{Name=name});Until(()=>p.Messages.OfType<S_CreatePlayer>().Any(),"create");}p.Send(new C_EnterGame{Name=name});return p;}
 static void HealthAndClock(){
  var health=new HostProposalHealth();for(int i=0;i<5;i++)health.Omitted();
  Check(!health.Failed(500000),"five transient omissions do not immediately terminate raid");
  health.Progress(700000);Check(health.Missing==0,"valid host action clears omission streak");
  for(int i=0;i<3;i++)health.Omitted();
  Check(!health.Failed(2699999)&&health.Failed(2700000),"sustained host silence still fails closed at two seconds");
  var sync=new ClockSynchronizer(160000);ClockMapping map=null;
  for(int i=0;i<3;i++){long sent=i*100000;var token=sync.Challenge(sent);map=sync.Respond(token,sent+1002000,sent+1002000,sent+4000);}
  Check(map.Trusted&&map.MaxBackdateUs==82000,"low RTT retains bounded dispatch jitter allowance");
  long now=400000;var timing=RaidContentRules.Timing();var binding=new SessionBinding(1,1,1,0,false);binding.InstallClock(map);
  var initial=CombatState.FourPlayers();initial.Content=new RaidContentState(new[]{1,2,3,4},100);
  var engine=new EncounterEngine(50,1,1,timing,RaidContentRules.Create(),initial,()=>now);engine.Bind(binding);
  engine.ReceiveInput(binding,new PlayerInput(new InputKey(50,1,1,1),now-70000-map.OffsetUs,map.Version,InputKind.Controls,0));engine.Pump();
  Check(engine.DrainInputReceipts().Single().Result==Admission.Admitted,"70 ms dispatch spike admitted without moving finality");
  engine.ReceiveInput(binding,new PlayerInput(new InputKey(50,1,1,2),now-82001-map.OffsetUs,map.Version,InputKind.Controls,0));engine.Pump();
  Check(engine.DrainInputReceipts().Single().Result==Admission.TooLate,"timestamp beyond measured bound remains rejected");
  Check(timing.InputGraceUs==160000&&timing.FinalizationDelayUs==480000,"input and irreversible finality limits unchanged");
 }
 static void Revive(){
  long now=0;var timing=RaidContentRules.Timing();var initial=CombatState.FourPlayers();initial.Content=new RaidContentState(new[]{1,2,3,4},100);initial.Content.Simulation.Players[0].Hp=0;
  var binding=new SessionBinding(1,1,1,0,false);var engine=new EncounterEngine(51,1,1,timing,RaidContentRules.Create(),initial,()=>now);engine.Bind(binding);
  var input=new PlayerInput(new InputKey(51,1,1,1),0,1,InputKind.Controls,0,0,0,16);
  engine.ReceiveInput(binding,input);engine.Pump();now=timing.TickUs;engine.Pump();
  Check(engine.PreviewSnapshot.Content.Simulation.Players[0].Hp==0,"revive input alone cannot change server HP");
  var proposal=new HostProposal(51,1,1,1,1,100,input.Key);engine.ReceiveProposal(binding,proposal);engine.Pump();
  now=timing.FinalDeadline(1);engine.Pump();var result=engine.FinalSnapshot.Content.Simulation;
  Check(result.Players[0].Hp==50&&result.ReviveCharges==2&&result.Players[0].Invulnerable>2.9f,"host approved C# revive finalizes half HP and one shared charge");
  Check(engine.DrainCommittedEvents().Count(e=>e.Kind=="ContentRevive")==1,"revive emits one authoritative event");
  engine.ReceiveInput(binding,input);engine.ReceiveProposal(binding,proposal);engine.Pump();now+=timing.TickUs;engine.Pump();
  Check(engine.FinalSnapshot.Content.Simulation.ReviveCharges==2,"duplicate input and proposal cannot spend twice");
  var roundtrip=RaidSnapshotCodec.Decode(RaidSnapshotCodec.Encode(result));Check(roundtrip.ReviveCharges==2&&roundtrip.Players[0].Hp==50,"snapshot preserves revival state");
  var timeline=new RaidPresentationTimeline(0);timeline.Track(1,1,new PlayerCommand{PlayerId=0,Revive=true});
  Check(timeline.Project(initial.Content.Simulation,1).Players[0].Hp==0,"presentation cannot predict resurrection");
  var sim=new RaidSimulation(2,42);sim.Start();foreach(var p in sim.Players)p.Hp=0;sim.ReviveCharges=1;
  sim.NetworkTick(new[]{new PlayerCommand{PlayerId=1,Revive=true},new PlayerCommand{PlayerId=0,Revive=true}},-1);
  Check(sim.Players[0].Hp==50&&sim.Players[1].Hp==0&&sim.ReviveCharges==0,"simultaneous last charge requests resolve by stable slot");
  sim.NetworkTick(new[]{new PlayerCommand{PlayerId=0,Revive=true},new PlayerCommand{PlayerId=1,Revive=true}},-1);
  Check(sim.ReviveCharges==0&&sim.Players[1].Hp==0,"alive or exhausted requests grant no free revival");
  sim.Players[0].Hp=0;sim.NetworkTick(Array.Empty<PlayerCommand>(),-1);Check(sim.Mode==EncounterMode.Defeat,"wipe with no charges ends raid");
  sim=new RaidSimulation(2,42);sim.Start();foreach(var p in sim.Players)p.Hp=0;sim.NetworkTick(Array.Empty<PlayerCommand>(),-1);
  Check(sim.Mode==EncounterMode.Fighting&&sim.WipeDeadlineTick==601,"wipe with charges opens ten second revive window");
  for(int i=0;i<600;i++)sim.NetworkTick(Array.Empty<PlayerCommand>(),-1);
  Check(sim.Mode==EncounterMode.Defeat,"unused wipe window cannot keep encounter alive indefinitely");
  sim=new RaidSimulation(2,42);sim.Start();sim.Players[0].Hp=0;sim.Players[0].Disconnected=true;sim.NetworkTick(new[]{new PlayerCommand{PlayerId=0,Revive=true}},-1);
  Check(sim.ReviveCharges==3&&sim.Players[0].Hp==0,"disconnected actor cannot spend revival pool");
  sim=new RaidSimulation(2,42){RoomsEnabled=true,NetworkPatternGate=true};sim.Start();sim.Boss.StaggerTime=100;
  foreach(var p in sim.Players)p.Hp=0;
  for(int i=0;i<350;i++)sim.NetworkTick(Array.Empty<PlayerCommand>(),-1);
  Check(sim.Mode==EncounterMode.Fighting&&sim.PendingRoomMarkTick==0&&!sim.Events.Any(e=>e.Kind=="RoomPatternMissing"),"all-dead revive window never requests an impossible Lua mark batch");
  sim.NetworkTick(new[]{new PlayerCommand{PlayerId=0,Revive=true}},-1);
  Check(sim.PendingRoomMarkTick==sim.TickIndex+30&&sim.RoomSelectionAliveMask==1,"revival resumes targeted pattern with full proposal lead time");
 }
 static void Match(Peer[] pair,bool reverse=false){
  var counts=pair.Select(p=>p.Messages.OfType<S_SceneMove>().Count()).ToArray();
  foreach(var p in reverse?pair.Reverse():pair){p.Send(new C_CreateRoom{PartySize=2});Pump(50);}
  Until(()=>pair.Select((p,i)=>p.Messages.OfType<S_SceneMove>().Count()>counts[i]).All(x=>x),"match");
  foreach(var p in pair)p.Ready();Until(()=>pair.All(p=>p.Client.Start!=null),"raid start");
 }
 static void Town(Peer[] pair){
  Until(()=>pair.All(p=>p.Client.Start==null&&p.Messages.OfType<S_SceneMove>().Last().TargetRoomType==RoomType.Town),"return transfer");
  foreach(var p in pair)p.Ready();Until(()=>pair.All(p=>p.Session.MyPlayer?.Room?.RoomType==RoomType.Town),"town ready");
 }
 static void Reentry(){
  ConfigManager.LoadConfig();DataManager.LoadData();DatabaseStartup.Verify(false);RaidRewardOutbox.Start();RoomManager.Instance.Add(RoomType.Town);
  string prefix="stability_"+Guid.NewGuid().ToString("N").Substring(0,8);var pair=new[]{Login(prefix+"_1"),Login(prefix+"_2")};
  Until(()=>pair.All(p=>p.Session.MyPlayer?.Room?.RoomType==RoomType.Town),"initial town");Match(pair);Pump(1000);
  var oldHost=pair.Single(p=>p.Client.IsHost);var oldRoom=oldHost.Session.MyPlayer.Room;long oldEncounter=oldHost.Client.Start.Encounter;var oldInput=oldHost.Client.Controls(new PlayerCommand());
  var engine=Field<Server.Game.RaidCombatSession>(oldRoom,"_raid").Engine;
  // Fixture only: drive real defeat/transfer/rematch handlers without waiting for boss damage.
  var state=Field<CombatState>(engine,"_final");state.Content.Simulation.ReviveCharges=0;foreach(var p in state.Content.Simulation.Players)p.Hp=0;
  Town(pair);Check(pair.All(p=>p.Messages.OfType<S_RaidEnded>().Last().Reason=="Defeat"),"two-player defeat uses real return path");
  Check(RoomManager.Instance.Find(oldRoom.RoomId)==null,"failed raid instance removed");
  // Force the other participant to queue first.
  Match(new[]{pair.Single(p=>p!=oldHost),oldHost});var host=pair.Single(p=>p.Client.IsHost);var newRoom=host.Session.MyPlayer.Room;long current=host.Client.Start.Encounter;
  Check(current!=oldEncounter&&newRoom.RoomId!=oldRoom.RoomId&&host!=oldHost,"rematch creates fresh encounter and host");
  Check(pair.All(p=>p.Client.Start.Fighters.Where(f=>!f.IsBoss).All(f=>f.Hp==100)&&RaidSnapshotCodec.Decode(p.Client.Start.Content.ToByteArray()).ReviveCharges==3),"fresh raid starts with living metadata and reset revival pool");
  RoomTransferService.Instance.StartRaidReturnToTownTransfer(oldRoom,pair.Select(p=>p.Session).ToArray());Pump(50);
  Check(pair.All(p=>p.Session.MyPlayer.Room==newRoom&&!p.Session.IsTransferring),"old room return callback cannot evict new raid players");
  oldHost.Send(oldInput);Pump(100);
  Check(oldHost.Messages.OfType<S_RaidInput>().Any(r=>r.Input?.Key?.Encounter==oldEncounter&&r.Result==(int)Admission.WrongEncounter),"stale input rejected by encounter identity");
  host.DropControls=true;Pump(160);host.DropControls=false;Pump(3000);
  Check(pair.All(p=>p.Client.Start?.Encounter==current),"temporary host proposal burst loss does not abort rematched raid");
  host.DropControls=true;Until(()=>host.Client.Start==null,"sustained missing host");
  Check(host.Messages.OfType<S_RaidEnded>().Last().Reason=="HostProposalOmissions","sustained unvalidated actions still terminate safely");
  host.DropControls=false;Town(pair);Match(pair);Pump(2500);
  Check(pair.All(p=>p.Client.Start!=null&&p.Client.Start.Encounter!=current),"third encounter remains playable after previous abort");
  host=pair.Single(p=>p.Client.IsHost);host.Send(new C_RaidLeave{Encounter=host.Client.Start.Encounter});Town(pair);
 }
 static int Main(){try{HealthAndClock();Revive();Reentry();Console.WriteLine("[STABILITY_VALIDATED] "+checks);return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}finally{foreach(var p in peers)p.Dispose();}}
 sealed class Peer:IDisposable {
  readonly TcpClient tcp;readonly List<byte> data=new List<byte>();bool closed;public bool DropControls;long lastControl;public ClientSession Session;public RaidNetworkClient Client=new RaidNetworkClient();public List<IMessage> Messages=new List<IMessage>();
  static readonly Dictionary<MsgId,MessageParser> parsers=ProtocolReflection.Descriptor.MessageTypes.Concat(RaidReflection.Descriptor.MessageTypes).Where(x=>Enum.TryParse<MsgId>(x.Name.Replace("_",""),out _)).ToDictionary(x=>(MsgId)Enum.Parse(typeof(MsgId),x.Name.Replace("_","")),x=>x.Parser);
  public Peer(){var l=new TcpListener(IPAddress.Loopback,0);l.Start();tcp=new TcpClient("127.0.0.1",((IPEndPoint)l.LocalEndpoint).Port);var socket=l.AcceptSocket();l.Stop();tcp.NoDelay=true;socket.NoDelay=true;Session=SessionManager.Instance.Generate();Session.Start(socket);Session.OnConnected(socket.RemoteEndPoint);Client.Send=Send;}
  public void Send(IMessage m){if(closed || DropControls && m is C_RaidProposal proposal && proposal.Origin!=null)return;byte[] b=m.ToByteArray(),p=new byte[b.Length+4];BitConverter.GetBytes((ushort)p.Length).CopyTo(p,0);BitConverter.GetBytes((ushort)(MsgId)Enum.Parse(typeof(MsgId),m.Descriptor.Name.Replace("_",""))).CopyTo(p,2);b.CopyTo(p,4);tcp.GetStream().Write(p);}
  public void Ready(){var m=Messages.OfType<S_SceneMove>().Last();Send(new C_SceneReady{TargetRoomId=m.TargetRoomId,TargetRoomType=m.TargetRoomType,TransferId=m.TransferId,SceneType=m.SceneType});}
  public void Poll(){if(closed)return;while(tcp.Available>0){var b=new byte[Math.Min(tcp.Available,65536)];int n=tcp.GetStream().Read(b);data.AddRange(b.Take(n));}while(data.Count>=4){int n=data[0]|data[1]<<8;if(data.Count<n)break;var id=(MsgId)(data[2]|data[3]<<8);var b=data.GetRange(4,n-4).ToArray();data.RemoveRange(0,n);if(parsers.TryGetValue(id,out var parser)){var m=parser.ParseFrom(b);Messages.Add(m);Client.Handle(m);}}if(Client.Start!=null&&Client.NowUs-lastControl>=50000){lastControl=Client.NowUs;Client.Controls(new PlayerCommand());}}
  public void Dispose(){if(closed)return;closed=true;tcp.Dispose();}
 }
}
