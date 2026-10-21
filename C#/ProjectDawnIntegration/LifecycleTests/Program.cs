using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.Protocol;
using Microsoft.EntityFrameworkCore;
using ProjectDawn.Combat;
using ProjectDawn.Network;
using Server;
using Server.DB;
using Server.Data;
using Server.Game.Room;
using DnfRaidDemo;
class Program
{
 static int checks;static List<Peer> peers=new List<Peer>();
 static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;Console.WriteLine("PASS: "+name);}
 static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(target);
 static void Pump(int ms){var sw=Stopwatch.StartNew();while(sw.ElapsedMilliseconds<ms){RoomManager.Instance.UpdateRooms();DbTransaction.Instance.Flush();foreach(var p in peers.ToArray())p.Poll();Thread.Sleep(2);}}
 static void Until(Func<bool> done,string name){var sw=Stopwatch.StartNew();while(!done()){Pump(5);if(sw.ElapsedMilliseconds>15000)throw new Exception("Timeout: "+name);}}
 static void Policy()
 {
  var p=new RaidParticipation();p.Disconnect(1,1000);p.Disconnect(1,9000);
  Check(p.Members[1].Deadline==181000,"repeated disconnect cannot extend the 3 minute deadline");
  Check(p.CanResume(1,180999)&&!p.CanResume(1,181000),"reconnect allowed before, rejected at exact deadline");
  Check(p.Resume(1,180999)&&p.Members[1].State==RaidParticipation.Status.Connected,"original participant resumes before expiry");
  p.Disconnect(1,200000);p.Leave(2);p.Disconnect(3,1000);
  var eligible=p.Finish(200001);
  Check(eligible.SequenceEqual(new[]{true,true,false,false}),"clear includes connected/waiting, excludes voluntary/expired");
  Check(!p.Resume(1,200002)&&!p.Leave(0),"clear freezes eligibility against later reconnect and leave");
  var q=new RaidParticipation();q.Disconnect(1,0);Check(q.Expire(179999).Length==0&&q.Expire(180000).SequenceEqual(new[]{1})&&q.Expire(180001).Length==0,"expiration is one-shot at server deadline");
  var a=new GameRoom();var b=new GameRoom();int key=int.MaxValue-5;
  Check(RaidResumeRegistry.RegisterParty(new[]{key},a)&&!RaidResumeRegistry.RegisterParty(new[]{key+1,key},b)&&RaidResumeRegistry.Find(key+1)==null,"conflicting party reservation is atomic and leaves no leaked characters");
  Check(!RaidResumeRegistry.RegisterParty(new[]{key+2,key+2},b)&&RaidResumeRegistry.Find(key+2)==null,"same SQL character cannot occupy two raid slots");RaidResumeRegistry.Remove(key,a);
 }
 static void Core()
 {
  long now=0;var state=new CombatState();for(int i=1;i<=4;i++)state.Fighters[i]=new FighterState{Id=i,Hp=100,MaxHp=100};state.Fighters[9]=new FighterState{Id=9,IsBoss=true,Hp=1000,MaxHp=1000};state.Content=new RaidContentState(new[]{1,2,3,4},9);
  state.Content.Simulation.Zones.Add(new AttackZone{Id=500,OwnerId=-1,Tag="TestHazard",Center=state.Content.Simulation.Players[1].Position,Radius=.15f,Damage=10,ActiveTime=.3f,Shape=ZoneShape.Circle});
  var e=new EncounterEngine(1,1,1,RaidContentRules.Timing(),RaidContentRules.Create(),state,()=>now);
  // Observe confirmed state after the configured finality boundary, not the pre-v13 400 ms assumption.
  var old=new SessionBinding(2,1,1,0,false);e.Bind(old);e.Revoke(2);e.SetPresence(2,false,false);now=RaidContentRules.Timing().FinalizationDelayUs+400000;e.Pump();
  Check(e.FinalSnapshot.Content.Simulation.Players[1].Disconnected&&e.FinalSnapshot.Content.Simulation.Players[1].Hp<100&&e.FinalSnapshot.Content.Simulation.Players[1].Hp>0,"disconnected actor still takes authoritative hazard damage");
  var fresh=new SessionBinding(2,2,1,0,false);e.Bind(fresh);e.SetPresence(2,true,false);
  e.ReceiveInput(old,new PlayerInput(new InputKey(1,2,1,1),0,1,InputKind.Controls,0));e.ReceiveInput(fresh,new PlayerInput(new InputKey(1,2,2,1),0,1,InputKind.Controls,0));e.Pump();
  Check(e.DrainInputReceipts().Select(r=>r.Result).SequenceEqual(new[]{Admission.WrongSession,Admission.Admitted}),"old connection revoked and fresh epoch accepted");
  e.SetPresence(2,false,true);now+=RaidContentRules.Timing().FinalizationDelayUs+400000;e.Pump();var final=e.FinalSnapshot;
  Check(final.Content.Simulation.Players[1].Departed&&final.Fighters[2].Hp==0,"expiry removes actor through replay without rewriting committed ticks");
  Check(RaidSnapshotCodec.Decode(RaidSnapshotCodec.Encode(final.Content.Simulation)).Players[1].Departed,"departure included in authoritative snapshot");
 }
 static Peer Login(string account,string character=null)
 {
  var p=new Peer();peers.Add(p);p.Client.Negotiate(3);p.Send(new C_Login{UniqueId=account});Until(()=>p.Messages.OfType<S_Login>().Any(),"login");
  var login=p.Messages.OfType<S_Login>().Last();Check(login.LoginOK==1,"SQL account authenticated "+account);
  if(login.Players.Count==0){p.Send(new C_CreatePlayer{Name=account});Until(()=>p.Messages.OfType<S_CreatePlayer>().Any(),"create character");character=account;}
  else character=character??login.Players[0].Name;
  p.Account=account;p.Character=character;p.Send(new C_EnterGame{Name=character});return p;
 }
 static void Ready(Peer p){var m=p.Messages.OfType<S_SceneMove>().Last();p.Send(new C_SceneReady{TargetRoomId=m.TargetRoomId,TransferId=m.TransferId,SceneType=m.SceneType,TargetRoomType=m.TargetRoomType});}
 static void NetworkAndSql()
 {
  ConfigManager.LoadConfig();DataManager.LoadData();DatabaseStartup.Verify(false);RaidRewardOutbox.Start();RoomManager.Instance.Add(RoomType.Town);
  string prefix="PD_Life_"+DateTime.UtcNow.ToString("MMddHHmmss");var party=Enumerable.Range(0,4).Select(i=>Login(prefix+"_"+i)).ToArray();
  Until(()=>party.All(p=>p.Messages.OfType<S_EnterGame>().Any()),"town entries");foreach(var p in party)p.Send(new C_CreateRoom());
  Until(()=>party.All(p=>p.Messages.OfType<S_SceneMove>().Any()),"4 player match");foreach(var p in party)Ready(p);Until(()=>party.All(p=>p.Client.Start!=null),"raid start");
  var host=party.Single(p=>p.Client.IsHost);var member=party.First(p=>p!=host);var room=host.Session.MyPlayer.Room;long encounter=host.Client.Start.Encounter;
  int id=member.Client.Start.Self, slot=member.Client.Start.PlayerSlots.IndexOf(id), characterId=member.Session.MyPlayer.PlayerDbId;
  var duplicate=Login(member.Account);Until(()=>duplicate.Messages.OfType<S_RaidResume>().Any(),"duplicate character rejection");Check(duplicate.Messages.OfType<S_RaidResume>().Last().Status=="AlreadyConnected","second login cannot take connected original actor");duplicate.Dispose();
  Until(()=>Field<Server.Game.RaidCombatSession>(room,"_raid").Engine.EventSequence>0,"committed history before disconnect");member.Dispose();Until(()=>member.Session.ConnectionClosed,"disconnect observed");Pump((int)(RaidContentRules.Timing().FinalizationDelayUs/1000)+100);
  var combat=Field<Server.Game.RaidCombatSession>(room,"_raid");var before=combat.Engine.FinalSnapshot.Content.Simulation.Players[slot];
  Check(before.Disconnected&&!before.Departed,"disconnected participant remains in battle");
  // Fixture damage uses the actual server simulation hit path, not a production packet or API.
  var simulation=Field<CombatState>(combat.Engine,"_final").Content.Simulation;simulation.Players[slot].Invulnerable=0;simulation.Players[slot].Hp=37;simulation.Players[slot].RoomId=2;
  var resumed=Login(member.Account);Until(()=>resumed.Messages.OfType<S_SceneMove>().Any(),"resume scene");Ready(resumed);Until(()=>resumed.Client.Start!=null,"resume start");
  var restored=RaidSnapshotCodec.Decode(resumed.Client.Start.Content.ToByteArray()).Players[slot];
  Check(resumed.Client.Start.Resumed&&resumed.Client.Start.Self==id&&resumed.Client.Start.Fighters.Single(f=>f.Id==id).Epoch==2,"original actor and slot rebound to epoch two");
  Check(restored.Hp==37&&restored.RoomId==2,"reconnect preserves current HP and room instead of spawn reset");
  Until(()=>resumed.Client.Latest!=null&&resumed.Client.Latest.Events.Count>0,"resumed event stream");Check(resumed.Client.Start.EventSequence>0,"resume establishes committed event baseline without gap");
  var spoof=resumed.Client.Controls(new PlayerCommand());spoof.Key.Epoch=1;spoof.Key.Sequence=99999;resumed.Send(spoof);Until(()=>resumed.Messages.OfType<S_RaidInput>().Any(r=>r.Result==(int)Admission.WrongSession),"stale epoch rejection");Check(true,"old epoch rejected over actual TCP");
  var leaver=party.First(p=>p!=host&&p!=member);int leaveSlot=host.Client.Start.PlayerSlots.IndexOf(leaver.Client.Start.Self);
  leaver.Send(new C_RaidLeave{Encounter=encounter});Until(()=>leaver.Messages.OfType<S_RaidEnded>().Any(),"voluntary leave");Ready(leaver);Until(()=>leaver.Session.MyPlayer.Room?.RoomType==RoomType.Town,"solo town return");Pump((int)(RaidContentRules.Timing().FinalizationDelayUs/1000)+100);
  Until(()=>combat.Engine.FinalSnapshot.Content.Simulation.Players[leaveSlot].Departed,"departure finalized after server input grace");
  Check(host.Client.Start!=null,"nonhost leaves alone, remaining raid continues");
  var waiter=party.First(p=>p!=host&&p!=member&&p!=leaver);int waiterSlot=host.Client.Start.PlayerSlots.IndexOf(waiter.Client.Start.Self);waiter.Dispose();Until(()=>waiter.Session.ConnectionClosed,"waiting disconnect");Pump(30);
  // Freeze a winning fixture at the core boundary; actual clear detection, journal, SQL and result packets are production paths.
  var final=Field<CombatState>(combat.Engine,"_final");final.Content.Simulation.Players[slot].Hp=0;final.Content.Simulation.Boss.Hp=0;final.Content.Simulation.Mode=EncounterMode.Victory;final.Fighters[final.Content.BossId].Hp=0;
  Until(()=>host.Messages.OfType<S_RaidRewards>().Any(r=>r.Status=="Saved"),"durable clear reward");
  var saved=host.Messages.OfType<S_RaidRewards>().Last(r=>r.Status=="Saved");Check(saved.Entries.Count(r=>r.Eligible)==3&&!saved.Entries.Single(r=>r.Slot==leaveSlot).Eligible&&saved.Entries.Single(r=>r.Slot==waiterSlot).Eligible,"clear pays dead connected and offline waiting, excludes voluntary leaver");
  var record=Field<RaidRewardRecord>(room,"_raidReward");Parallel.Invoke(()=>RaidRewardStore.Save(record),()=>RaidRewardStore.Save(record));
  var history=RaidRewardStore.History(host.Session.AccountDbId,host.Session.MyPlayer.PlayerDbId,encounter);
  Check(history.TotalTokens==100&&history.Entries.Count==4,"concurrent retries cannot duplicate SQL tokens or receipts");
  using(var db=new AppDbContext()){
   int[] eligibleIds=record.Recipients.Where(r=>r.Eligible).Select(r=>r.CharacterId).ToArray();
   Check(history.Entries.Where(r=>r.Eligible).All(r=>r.EquipmentTemplate>=92001&&r.EquipmentTemplate<=92005&&!string.IsNullOrEmpty(r.EquipmentName)),"saved reward cards carry server-selected equipment");
   Check(db.Items.Count(i=>eligibleIds.Contains(i.OwnerDbId.Value)&&i.TemplateId>=92001&&i.TemplateId<=92005)==3,"concurrent SQL retries grant exactly one owned equipment per eligible player");
   Check(!db.Items.Any(i=>i.OwnerDbId==leaver.Session.MyPlayer.PlayerDbId&&i.TemplateId>=92001),"voluntary leaver receives no equipment");
  }
  var invalid=Newtonsoft.Json.JsonConvert.DeserializeObject<RaidRewardRecord>(Newtonsoft.Json.JsonConvert.SerializeObject(record));invalid.Encounter+=10000;invalid.Recipients[3].AccountId=int.MaxValue;
  bool rolledBack=false;try{RaidRewardStore.Save(invalid);}catch(System.IO.InvalidDataException){rolledBack=true;}
  Check(rolledBack && RaidRewardStore.History(host.Session.AccountDbId,host.Session.MyPlayer.PlayerDbId,invalid.Encounter)==null,"invalid recipient rolls back entire SQL receipt transaction");
  var altered=Newtonsoft.Json.JsonConvert.DeserializeObject<RaidRewardRecord>(Newtonsoft.Json.JsonConvert.SerializeObject(record));altered.Recipients[0].Name+="conflict";bool refused=false;try{RaidRewardStore.Save(altered);}catch(System.IO.InvalidDataException){refused=true;}Check(refused,"same encounter with changed receipt is rejected");
  Until(()=>host.Messages.OfType<S_RaidCinematic>().Any(),"server cinematic after clear");
  host.Send(new C_RaidLeave{Encounter=encounter});Pump(100);
  Check(!host.Messages.OfType<S_RaidEnded>().Any(),"saved reward cannot bypass cinematic via leave");
  Pump(2600);
  host.Send(new C_RaidCinematicSkip{Encounter=encounter-1});Pump(80);
  Check(Field<RaidClearCinematic>(room,"_clearCinema").Votes==0,"stale encounter skip rejected over TCP");
  host.Send(new C_RaidCinematicSkip{Encounter=encounter});host.Send(new C_RaidCinematicSkip{Encounter=encounter});Pump(100);
  Check(Field<RaidClearCinematic>(room,"_clearCinema").Votes==1&&!Field<RaidClearCinematic>(room,"_clearCinema").Complete,"two viewers need two distinct votes; duplicate ignored");
  resumed.Send(new C_RaidCinematicSkip{Encounter=encounter});
  Until(()=>host.Messages.OfType<S_RaidCinematic>().Any(c=>c.Complete)&&resumed.Messages.OfType<S_RaidCinematic>().Any(c=>c.Complete),"shared cinematic release");
  Check(host.Messages.OfType<S_RaidCinematic>().Last().Reason=="MajoritySkip","server confirms shared majority skip");
  host.Send(new C_RaidLeave{Encounter=encounter});Until(()=>host.Messages.OfType<S_RaidEnded>().Any(),"clear return");resumed.Send(new C_RaidLeave{Encounter=encounter});Until(()=>resumed.Messages.OfType<S_RaidEnded>().Any(),"member clear return");foreach(var p in new[]{host,resumed})Ready(p);Pump(200);
  room.Push(()=>typeof(GameRoom).GetMethod("EndRaid",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(room,new object[]{"Clear"}));Pump(20);
  var offline=Login(waiter.Account);Until(()=>offline.Messages.OfType<S_RaidRewards>().Any(r=>r.Status=="History"),"offline login receipt");Check(offline.Messages.OfType<S_RaidRewards>().Last().TotalTokens==100,"offline reward survives fresh SQL login");
  Check(offline.Messages.OfType<S_ItemList>().Last().Items.Any(i=>i.TemplateId>=92001&&i.PhysicalAttack>0&&i.AttackSpeedPercent!=0),"fresh login receives owned reward equipment and server stat definitions");
  var gear=offline.Messages.OfType<S_ItemList>().Last().Items.First(i=>i.TemplateId>=92001);offline.Send(new C_EquipItem{ItemDbId=gear.ItemDbId,Equipped=true});
  Until(()=>offline.Messages.OfType<S_EquipItem>().Any(x=>x.ItemDbId==gear.ItemDbId&&x.Equipped),"reward equipment equipped");
  Check(Math.Abs(offline.Messages.OfType<S_EquipmentStats>().Last().PhysicalAttack-(100+gear.PhysicalAttack))<.001f,"SQL-owned reward equipment updates authoritative physical attack");
  // A separate process recovers the pending journal; the original worker never sees this entry.
  var durable=Newtonsoft.Json.JsonConvert.DeserializeObject<RaidRewardRecord>(Newtonsoft.Json.JsonConvert.SerializeObject(record));durable.Encounter++;
  System.IO.Directory.CreateDirectory(RaidRewardOutbox.DirectoryPath);
  System.IO.File.WriteAllText(System.IO.Path.Combine(RaidRewardOutbox.DirectoryPath,durable.Encounter+".pending.json"),Newtonsoft.Json.JsonConvert.SerializeObject(durable));
  var launch=new ProcessStartInfo(Environment.ProcessPath){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
  if(System.IO.Path.GetFileNameWithoutExtension(Environment.ProcessPath).Equals("dotnet",StringComparison.OrdinalIgnoreCase))launch.ArgumentList.Add(typeof(Program).Assembly.Location);
  launch.ArgumentList.Add("--recover-only");using(var child=Process.Start(launch)){
   var stdout=child.StandardOutput.ReadToEndAsync();var stderr=child.StandardError.ReadToEndAsync();
   Check(child.WaitForExit(15000)&&child.ExitCode==0,"fresh server worker process replays durable SQL journal");
   System.IO.File.WriteAllText(System.IO.Path.Combine(RaidRewardOutbox.DirectoryPath,"recovery-process.log"),stdout.Result+stderr.Result);
  }
  Until(()=>RaidRewardStore.History(host.Session.AccountDbId,host.Session.MyPlayer.PlayerDbId).TotalTokens==200,"outbox recovery");Check(true,"write-ahead receipt is recovered and SQL applied exactly once");
  var again=new[]{host,resumed,leaver,offline};var sceneCounts=again.ToDictionary(p=>p,p=>p.Messages.OfType<S_SceneMove>().Count());
  foreach(var p in again)p.Send(new C_CreateRoom());Until(()=>again.All(p=>p.Messages.OfType<S_SceneMove>().Count()>sceneCounts[p]),"second match");foreach(var p in again)Ready(p);Until(()=>again.All(p=>p.Client.Start!=null),"second raid");
  var newHost=again.Single(p=>p.Client.IsHost);var gone=again.First(p=>p!=newHost);var newRoom=newHost.Session.MyPlayer.Room;
  int goneSlot=gone.Client.Start.PlayerSlots.IndexOf(gone.Client.Start.Self);int goneCharacter=gone.Session.MyPlayer.PlayerDbId;
  gone.Dispose();Until(()=>gone.Session.ConnectionClosed,"expiry disconnect");Pump(30);
  var policy=Field<RaidParticipation>(newRoom,"_participation");long deadline=policy.Members[goneSlot].Deadline;
  var loading=Login(gone.Account);Until(()=>loading.Messages.OfType<S_SceneMove>().Any(),"reconnect loading");loading.Dispose();Until(()=>loading.Session.ConnectionClosed,"loading disconnect");Pump(30);
  Check(policy.Members[goneSlot].Deadline==deadline&&policy.Members[goneSlot].State==RaidParticipation.Status.Waiting,"interrupted reconnect loading keeps original deadline");
  policy.Members[goneSlot].Deadline=Environment.TickCount64;Pump((int)(RaidContentRules.Timing().FinalizationDelayUs/1000)+100); // Accelerated server-deadline fixture, production timeout remains 180s.
  var newCombat=Field<Server.Game.RaidCombatSession>(newRoom,"_raid");
  Check(policy.Members[goneSlot].State==RaidParticipation.Status.Expired&&newCombat.Engine.FinalSnapshot.Content.Simulation.Players[goneSlot].Departed,"deadline removes expired actor from authoritative field");
  Check(RaidResumeRegistry.Find(goneCharacter)==null&&newRoom.PlayerCount==3,"expiry releases original character reservation and room object");
  var late=Login(gone.Account);Until(()=>late.Messages.OfType<S_EnterGame>().Any(),"late login town");Check(late.Session.MyPlayer.Room.RoomType==RoomType.Town&&!late.Messages.OfType<S_RaidStart>().Any(),"expired participant returns to town, cannot rejoin same raid");
  int priorRewards=newHost.Messages.OfType<S_RaidRewards>().Count();newHost.Dispose();Until(()=>newHost.Session.ConnectionClosed,"host exit");Pump(200);
  Check(again.Where(p=>p!=gone&&p!=newHost).All(p=>p.Messages.OfType<S_RaidEnded>().Last().Reason=="HostDisconnected"),"host socket loss terminates surviving raid and returns members");
  Check(!Field<bool>(newRoom,"_raidClearWaiting"),"host-aborted encounter never creates clear rewards");

 }
 static int Main(string[] args){try{if(args.Contains("--recover-only")){ConfigManager.LoadConfig();RaidRewardOutbox.Start();RaidRewardOutbox.Flush();return System.IO.Directory.GetFiles(RaidRewardOutbox.DirectoryPath,"*.pending.json").Length==0?0:1;}Policy();Core();NetworkAndSql();Console.WriteLine("[LIFECYCLE_VALIDATED] "+checks+" checks");return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}finally{foreach(var p in peers)p.Dispose();}}
 sealed class Peer:IDisposable
 {
  TcpClient tcp;List<byte> data=new List<byte>();bool closed;public string Account,Character;public ClientSession Session;public RaidNetworkClient Client=new RaidNetworkClient();public List<IMessage> Messages=new List<IMessage>();
  static Dictionary<MsgId,MessageParser> parsers=ProtocolReflection.Descriptor.MessageTypes.Concat(RaidReflection.Descriptor.MessageTypes).Where(x=>Enum.TryParse<MsgId>(x.Name.Replace("_",""),out _)).ToDictionary(x=>(MsgId)Enum.Parse(typeof(MsgId),x.Name.Replace("_","")),x=>x.Parser);
  public Peer(){var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();tcp=new TcpClient("127.0.0.1",((IPEndPoint)listener.LocalEndpoint).Port);var socket=listener.AcceptSocket();listener.Stop();tcp.NoDelay=true;socket.NoDelay=true;Session=SessionManager.Instance.Generate();Session.Start(socket);Session.OnConnected(socket.RemoteEndPoint);Client.Send=Send;}
  public void Send(IMessage m){if(closed)return;byte[] b=m.ToByteArray();var id=(ushort)(MsgId)Enum.Parse(typeof(MsgId),m.Descriptor.Name.Replace("_",""));var packet=new byte[b.Length+4];Array.Copy(BitConverter.GetBytes((ushort)packet.Length),packet,2);Array.Copy(BitConverter.GetBytes(id),0,packet,2,2);Array.Copy(b,0,packet,4,b.Length);tcp.GetStream().Write(packet,0,packet.Length);}
  public void Poll(){if(closed)return;while(tcp.Available>0){var b=new byte[Math.Min(tcp.Available,65536)];int n=tcp.GetStream().Read(b,0,b.Length);data.AddRange(b.Take(n));}while(data.Count>=4){int n=data[0]|data[1]<<8;if(data.Count<n)break;var id=(MsgId)(data[2]|data[3]<<8);var b=data.GetRange(4,n-4).ToArray();data.RemoveRange(0,n);if(parsers.TryGetValue(id,out var parser)){var m=parser.ParseFrom(b);Messages.Add(m);Client.Handle(m);}}}
  public void Dispose(){if(closed)return;closed=true;tcp.Close();}
 }
}
