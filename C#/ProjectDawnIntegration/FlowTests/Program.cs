using System;
using System.Linq;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Diagnostics;
using System.Threading;
using Google.Protobuf;
using Google.Protobuf.Protocol;
using ProjectDawn.Network;
using ProjectDawn.Combat;
class Program {
 static List<Peer> peers=new List<Peer>();static int checks;
 static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;Console.WriteLine("PASS: "+name);}
 static void Pump(int ms){var watch=Stopwatch.StartNew();while(watch.ElapsedMilliseconds<ms){foreach(var p in peers)p.Poll();Thread.Sleep(2);}}
 static void Until(Func<bool> done,string name){var watch=Stopwatch.StartNew();while(!done()){Pump(5);if(watch.ElapsedMilliseconds>12000)throw new Exception("Timeout: "+name);}}
 static int Main(string[] args){try{
  int port=int.Parse(args[0]);string prefix=args[1];
  for(int i=1;i<=4;i++){var p=new Peer(port);peers.Add(p);p.Client.Negotiate(3);p.Send(new C_Login{UniqueId=prefix+"_"+i});}
  Until(()=>peers.All(p=>p.Messages.OfType<S_Login>().Any()),"SQL login");
  Check(peers.All(p=>p.Messages.OfType<S_Login>().Single().LoginOK==1),"four accounts log in through SQL session path");
  var names=peers.Select(p=>p.Messages.OfType<S_Login>().Single().Players.Single().Name).ToArray();
  peers[0].Send(new C_EnterGame{Name=names[1]});Pump(150);
  Check(!peers[0].Messages.OfType<S_EnterGame>().Any(),"foreign character selection denied without terminating session");
  for(int i=0;i<4;i++)peers[i].Send(new C_EnterGame{Name=names[i]});
  Until(()=>peers.All(p=>p.Messages.OfType<S_EnterGame>().Any()&&p.Messages.OfType<S_ItemList>().Any()),"own town entry");
  // Exercise the existing channel-list and move path before the reconnect phase.
  peers[0].Send(new C_ChannelList());Until(()=>peers[0].Messages.OfType<S_ChannelList>().Any(),"authenticated channel list over game connection");
  Check(peers[0].Messages.OfType<S_ChannelList>().Last().Rooms.Count>=2&&peers[0].Messages.OfType<S_ChannelList>().Last().CurrentRoomId>0,"channel UI can list towns without public monitoring HTTP port");
  using(var http=new System.Net.Http.HttpClient()) {
   string json=http.GetStringAsync("http://127.0.0.1:8090/api/rooms").GetAwaiter().GetResult();
   using(var doc=System.Text.Json.JsonDocument.Parse(json)) {
    var towns=doc.RootElement.EnumerateArray().Where(r=>r.GetProperty("roomType").GetString()=="Town").ToArray();
    Check(towns.Length>=2,"existing monitor API lists two real town channels");
    int objectId=peers[0].Messages.OfType<S_EnterGame>().Last().Player.ObjectId;
    int current=towns.Single(r=>r.GetProperty("players").EnumerateArray().Any(p=>p.GetProperty("objectId").GetInt32()==objectId)).GetProperty("roomId").GetInt32();
    int destination=towns.First(r=>r.GetProperty("roomId").GetInt32()!=current).GetProperty("roomId").GetInt32();
    peers[0].Send(new C_ChannelMove{TargetRoomId=current});Until(()=>peers[0].Messages.OfType<S_ChannelMove>().Any(),"same channel response");
    Check(!peers[0].Messages.OfType<S_ChannelMove>().Last().Success,"already-current channel is rejected by original server handler");
    var before=peers[0].Messages.OfType<S_EnterGame>().Last().Player.Clone();
    peers[0].Send(new C_ChannelMove{TargetRoomId=destination});Until(()=>peers[0].Messages.OfType<S_ChannelMove>().Any(m=>m.Success)&&peers[0].Messages.OfType<S_EnterGame>().Count()==2,"channel move and self spawn");
    var after=peers[0].Messages.OfType<S_EnterGame>().Last().Player;
    Check(before.ObjectId==after.ObjectId&&before.Name==after.Name&&before.Appearance==after.Appearance&&before.PosInfo.PosX==after.PosInfo.PosX&&before.PosInfo.PosY==after.PosInfo.PosY,"channel transfer preserves authenticated character, position and saved avatar");
   }
  }
  string baseLook="skin:0000|face:|hair:10800|cap:|coat:18700|pants:0000|shoes:0000|belt:|neck:|weapon:lswd:0000";
  var looks=new[]{"21700","24105","2700","10800"}.Select(h=>baseLook.Replace("hair:10800","hair:"+h)).ToArray();
  if(args.Length>2 && args[2]=="--restore-only") {
   Check(peers.Select((p,i)=>p.Messages.OfType<S_EnterGame>().Last().Player.Appearance==looks[i]).All(x=>x),"server restart and fresh clients restore four SQL looks without appearance writes");
   Console.WriteLine("[DAWN_APPEARANCE_RESTART_VALIDATED]");return 0;
  }
  var inventory=peers.Select(p=>p.Messages.OfType<S_ItemList>().Last()).ToArray();
  Check(inventory.All(x=>x.Items.Count==7 && x.Items.Any(i=>i.TemplateId==91002&&i.Equipped)),"relogin restores seven SQL items and saved equipped weapon");
  peers[0].Send(new C_EquipItem{ItemDbId=inventory[1].Items.First().ItemDbId,Equipped=true});Pump(150);
  Check(!peers[0].Messages.OfType<S_EquipItem>().Any(),"other character item cannot be equipped");

  peers[0].Send(new C_Appearance{Look="skin:../../unknown"});Pump(100);
  Check(peers[0].Messages.OfType<S_Appearance>().Last().Error=="InvalidLook","unlisted appearance resource cannot be injected");
  for(int i=0;i<4;i++)peers[i].Send(new C_Appearance{Look=looks[i]});
  Until(()=>peers.Select((p,i)=>p.Messages.OfType<S_Appearance>().Any(x=>x.PlayerId==p.Messages.OfType<S_EnterGame>().Last().Player.ObjectId&&x.Look==looks[i]&&x.Error=="")).All(x=>x),"SQL appearance acceptance");
  foreach(var p in peers)p.Dispose();peers.Clear();Thread.Sleep(100);
  for(int i=1;i<=4;i++){var p=new Peer(port);peers.Add(p);p.Client.Negotiate(3);p.Send(new C_Login{UniqueId=prefix+"_"+i});}
  Until(()=>peers.All(p=>p.Messages.OfType<S_Login>().Any()),"fresh clients SQL login");
  for(int i=0;i<4;i++)peers[i].Send(new C_EnterGame{Name=names[i]});
  Until(()=>peers.All(p=>p.Messages.OfType<S_EnterGame>().Any()&&p.Messages.OfType<S_ItemList>().Any()),"fresh clients town entry");
  Check(peers.Select((p,i)=>p.Messages.OfType<S_EnterGame>().Last().Player.Appearance==looks[i]).All(x=>x),"four fresh clients restore character-specific SQL looks without local cache");
  for(int i=0;i<4;i++)peers[i].Send(new C_Appearance{Look=baseLook,InitializeOnly=true});
  Until(()=>peers.All(p=>p.Messages.OfType<S_Appearance>().Any(x=>x.PlayerId==p.Messages.OfType<S_EnterGame>().Last().Player.ObjectId)),"stale import replies");
  Check(peers.Select((p,i)=>p.Messages.OfType<S_Appearance>().Last(x=>x.PlayerId==p.Messages.OfType<S_EnterGame>().Last().Player.ObjectId).Look==looks[i]).All(x=>x),"stale local imports cannot overwrite saved SQL appearance");
  inventory=peers.Select(p=>p.Messages.OfType<S_ItemList>().Last()).ToArray();
  var expected=peers.Select((p,i)=>new{Id=p.Messages.OfType<S_EnterGame>().Last().Player.ObjectId,Look=looks[i]}).ToDictionary(x=>x.Id,x=>x.Look);
  var armor=inventory[0].Items.Single(i=>i.TemplateId==100);
  peers[0].Send(new C_EquipItem{ItemDbId=armor.ItemDbId,Equipped=false});Until(()=>peers[0].Messages.OfType<S_EquipItem>().Any(x=>!x.Equipped),"unequip armor");
  peers[0].Send(new C_CreateRoom());Until(()=>peers[0].Messages.OfType<S_CreateRoom>().Any(),"missing armor match");
  Check(peers[0].Messages.OfType<S_CreateRoom>().Last().ResponseCode==0,"SQL inventory still enforces required armor for matchmaking");
  peers[0].Send(new C_EquipItem{ItemDbId=armor.ItemDbId,Equipped=true});Until(()=>peers[0].Messages.OfType<S_EquipItem>().Any(x=>x.ItemDbId==armor.ItemDbId&&x.Equipped),"restore armor");
  foreach(var p in peers)p.Send(new C_CreateRoom());Until(()=>peers.All(p=>p.Messages.OfType<S_SceneMove>().Any()),"four match");
  var moves=peers.Select(p=>p.Messages.OfType<S_SceneMove>().Last()).ToArray();
  for(int i=0;i<3;i++)peers[i].Ready(moves[i]);Pump(150);
  Check(peers.All(p=>p.Client.Start==null),"actual session waits for fourth completed loading scene");
  var invalid=moves[3].Clone();invalid.TransferId++;peers[3].Ready(invalid);Pump(150);
  Check(peers.All(p=>p.Client.Start==null),"incorrect scene transfer ID rejected");peers[3].Ready(moves[3]);
  Until(()=>peers.All(p=>p.Client.Start!=null),"raid start");
  Check(peers.All(p=>RaidSnapshotCodec.Decode(p.Client.Start.Content.ToByteArray()).Players.All(x=>Math.Abs(x.PhysicalAttack-132f)<.001f&&x.EquipmentDefence==15)),"server snapshots freeze four SQL-verified equipment loadouts");
  Check(peers.All(p=>RaidSnapshotCodec.Decode(p.Client.Start.Content.ToByteArray()).Players.Select((x,i)=>x.Look==expected[p.Client.Start.PlayerSlots[i]]).All(x=>x)),"town appearance is identical in every raid snapshot");
  var host=peers.Single(p=>p.Client.IsHost);var member=peers.First(p=>p!=host);
  int confirms=peers[0].Messages.OfType<S_EquipItem>().Count();peers[0].Send(new C_EquipItem{ItemDbId=armor.ItemDbId,Equipped=false});Pump(150);
  Check(peers[0].Messages.OfType<S_EquipItem>().Count()==confirms,"raid rejects equipment changes");
  member.Send(new C_RaidLeave{Encounter=member.Client.Start.Encounter+1});host.Send(new C_RaidLeave{Encounter=host.Client.Start.Encounter+1});Pump(150);
  Check(peers.All(p=>!p.Messages.OfType<S_RaidEnded>().Any()),"incorrect encounter cannot trigger personal or party return");
  host.Send(new C_RaidLeave{Encounter=host.Client.Start.Encounter});Until(()=>peers.All(p=>p.Messages.OfType<S_SceneMove>().Count()==2),"return transfer");
  Check(peers.All(p=>p.Messages.OfType<S_RaidEnded>().Last().Reason=="PartyReturn_NoRewards"),"host return ends through server and does not grant clear rewards");
  foreach(var p in peers)p.Ready(p.Messages.OfType<S_SceneMove>().Last());
  Until(()=>peers.All(p=>p.Messages.OfType<S_EnterGame>().Count()==3),"town return");
  Check(true,"all four return to town on the original authenticated connection");
  Check(peers.Select((p,i)=>p.Messages.OfType<S_EnterGame>().Last().Player.Appearance==looks[i]).All(x=>x),"SQL appearance is unchanged after raid and town return");
  Console.WriteLine("[DAWN_FLOW_NETWORK_VALIDATED] "+checks+" checks");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}finally{foreach(var p in peers)p.Dispose();}}
 class Peer:IDisposable{
  TcpClient tcp;List<byte> data=new List<byte>();public List<IMessage> Messages=new List<IMessage>();public RaidNetworkClient Client=new RaidNetworkClient();
  static Dictionary<MsgId,MessageParser> parsers=ProtocolReflection.Descriptor.MessageTypes.Concat(RaidReflection.Descriptor.MessageTypes).Where(x=>Enum.TryParse<MsgId>(x.Name.Replace("_",""),out _)).ToDictionary(x=>(MsgId)Enum.Parse(typeof(MsgId),x.Name.Replace("_","")),x=>x.Parser);
  public Peer(int port){tcp=new TcpClient("127.0.0.1",port);tcp.NoDelay=true;Client.Send=Send;}
  public void Send(IMessage m){byte[] b=m.ToByteArray();var id=(ushort)(MsgId)Enum.Parse(typeof(MsgId),m.Descriptor.Name.Replace("_",""));var packet=new byte[b.Length+4];Array.Copy(BitConverter.GetBytes((ushort)packet.Length),packet,2);Array.Copy(BitConverter.GetBytes(id),0,packet,2,2);Array.Copy(b,0,packet,4,b.Length);tcp.GetStream().Write(packet,0,packet.Length);}
  public void Ready(S_SceneMove m)=>Send(new C_SceneReady{TargetRoomId=m.TargetRoomId,TargetRoomType=m.TargetRoomType,TransferId=m.TransferId,SceneType=m.SceneType});
  public void Poll(){while(tcp.Available>0){var b=new byte[Math.Min(tcp.Available,65536)];int n=tcp.GetStream().Read(b,0,b.Length);data.AddRange(b.Take(n));}while(data.Count>=4){int n=data[0]|data[1]<<8;if(data.Count<n)break;var id=(MsgId)(data[2]|data[3]<<8);var b=data.GetRange(4,n-4).ToArray();data.RemoveRange(0,n);if(parsers.TryGetValue(id,out var parser)){var m=parser.ParseFrom(b);Messages.Add(m);Client.Handle(m);}}}
  public void Dispose()=>tcp.Close();
 }
}
