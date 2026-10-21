using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using Google.Protobuf;
using Google.Protobuf.Protocol;
using ProjectDawn.Network;
class Program {
 static readonly List<Peer> peers=new List<Peer>();static int checks;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
 static void Pump(int ms){var t=Stopwatch.StartNew();while(t.ElapsedMilliseconds<ms){foreach(var p in peers)p.Poll();Thread.Sleep(2);}}
 static void Until(Func<bool> f){var t=Stopwatch.StartNew();while(!f()){Pump(10);if(t.ElapsedMilliseconds>12000)throw new Exception("Timed out");}}
 static void Chat(Peer p,string text,long? context=null)=>p.Send(new C_Chat{Text=text,Context=context??p.Context.Context});
 static int Count(Peer p,string text)=>p.Messages.OfType<S_Chat>().Count(m=>m.Text==text);
 static int Main(string[] args){try{
  int port=int.Parse(args[0]);string prefix="chat_"+Guid.NewGuid().ToString("N").Substring(0,10);
  for(int i=0;i<9;i++){var p=new Peer(port);peers.Add(p);p.Send(new C_Chat{Text="unauthenticated",Context=1});p.Client.Negotiate(3);p.Send(new C_Login{UniqueId=prefix+"_"+i});}
  Until(()=>peers.All(p=>p.Messages.OfType<S_Login>().Any()));
  Check(peers.All(p=>!p.Messages.OfType<S_Chat>().Any()),"unauthenticated chat rejected");
  for(int i=0;i<9;i++)peers[i].Send(new C_CreatePlayer{Name=prefix+"_"+i});
  Until(()=>peers.All(p=>p.Messages.OfType<S_CreatePlayer>().Any()));
  for(int i=0;i<9;i++)peers[i].Send(new C_EnterGame{Name=prefix+"_"+i});
  Until(()=>peers.All(p=>p.Context!=null));Check(peers.All(p=>p.Context.Scope==ChatScope.ChatTown),"server assigns town contexts");
  var town=peers[0].Context.Clone();
  foreach(var p in peers.Where(p=>p.Context.RoomId!=town.RoomId))p.Send(new C_ChannelMove{TargetRoomId=town.RoomId});
  Until(()=>peers.All(p=>p.Context.RoomId==town.RoomId));
  Chat(peers[0],"한글 채팅 <b>글자</b> 😀");Pump(200);
  Check(peers.All(p=>Count(p,"한글 채팅 <b>글자</b> 😀")==1),"Korean emoji and literal markup delivered once to same town including sender");
  var message=peers[1].Messages.OfType<S_Chat>().Last();Check(message.SenderName==prefix+"_0"&&message.SenderId==peers[0].Messages.OfType<S_EnterGame>().Last().Player.ObjectId,"server owns sender identity");
  peers[8].Send(new C_ChannelList());Until(()=>peers[8].Messages.OfType<S_ChannelList>().Any());
  int other=peers[8].Messages.OfType<S_ChannelList>().Last().Rooms.First(r=>r.RoomId!=town.RoomId).RoomId;
  peers[8].Send(new C_ChannelMove{TargetRoomId=other});Until(()=>peers[8].Context.RoomId==other);
  Chat(peers[1],"channel-only");Pump(200);Check(peers.Take(8).All(p=>Count(p,"channel-only")==1)&&Count(peers[8],"channel-only")==0,"other town channel is isolated");
  Chat(peers[8],"stale-context",town.Context);Pump(100);Check(peers.All(p=>Count(p,"stale-context")==0),"old context cannot target a new channel");
  Chat(peers[2],"line\nbreak");Chat(peers[2],new string('가',121));Chat(peers[2],"\u202eformat");Pump(200);
  Check(peers.All(p=>!p.Messages.OfType<S_Chat>().Any(m=>m.Error==""&&(m.Text.Contains('\n')||m.Text.Length>120||m.Text.Contains('\u202e')))),"newline oversize and bidi controls rejected");
  for(int n=0;n<20;n++)Chat(peers[3],"burst");Pump(200);Check(Count(peers[0],"burst")<=3&&Count(peers[0],"burst")>0,"server limits burst to three messages");
  foreach(var p in peers.Take(4))p.Send(new C_CreateRoom());
  Until(()=>peers.Take(4).All(p=>p.Messages.OfType<S_SceneMove>().Any()));
  Chat(peers[0],"during-transfer");Pump(100);Check(peers.All(p=>Count(p,"during-transfer")==0),"transfer cannot broadcast stale town chat");
  foreach(var p in peers.Take(4))p.Ready();Until(()=>peers.Take(4).All(p=>p.Client.Start!=null&&p.Context.Scope==ChatScope.ChatDungeon));
  foreach(var p in peers.Skip(4).Take(4))p.Send(new C_CreateRoom());
  Until(()=>peers.Skip(4).Take(4).All(p=>p.Messages.OfType<S_SceneMove>().Any()));
  foreach(var p in peers.Skip(4).Take(4))p.Ready();Until(()=>peers.Skip(4).Take(4).All(p=>p.Client.Start!=null&&p.Context.Scope==ChatScope.ChatDungeon));
  Check(peers[0].Context.RoomId!=peers[4].Context.RoomId,"two distinct raid encounters created");
  Pump(1100);Chat(peers[0],"party-A");Chat(peers[4],"party-B");Chat(peers[8],"town-only");Pump(200);
  Check(peers.Take(4).All(p=>Count(p,"party-A")==1)&&peers.Skip(4).All(p=>Count(p,"party-A")==0),"raid A excludes raid B and town");
  Check(peers.Skip(4).Take(4).All(p=>Count(p,"party-B")==1)&&peers.Take(4).All(p=>Count(p,"party-B")==0)&&Count(peers[8],"party-B")==0,"raid B excludes raid A and town");
  Check(Count(peers[8],"town-only")==1&&peers.Take(8).All(p=>Count(p,"town-only")==0),"town excludes dungeon participants");
  long old=peers[0].Context.Context;var host=peers.Take(4).Single(p=>p.Client.IsHost);host.Send(new C_RaidLeave{Encounter=host.Client.Start.Encounter});
  Until(()=>peers.Take(4).All(p=>p.Messages.OfType<S_SceneMove>().Count()==2));foreach(var p in peers.Take(4))p.Ready();
  Until(()=>peers.Take(4).All(p=>p.Context.Scope==ChatScope.ChatTown));Pump(1100);
  Chat(peers[0],"old-raid-message",old);Chat(peers[0],"returned-town");Pump(200);
  Check(peers.All(p=>Count(p,"old-raid-message")==0),"return invalidates dungeon context");
  Check(peers.Take(4).All(p=>Count(p,"returned-town")==1)&&peers.Skip(4).Take(4).All(p=>Count(p,"returned-town")==0),"return switches audience back to town");
  Console.WriteLine("[DAWN_CHAT_VALIDATED] "+checks);return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}finally{foreach(var p in peers)p.Dispose();}}
 class Peer:IDisposable {
  readonly TcpClient tcp;readonly List<byte> data=new List<byte>();public readonly List<IMessage> Messages=new List<IMessage>();public readonly RaidNetworkClient Client=new RaidNetworkClient();
  public S_ChatContext Context=>Messages.OfType<S_ChatContext>().LastOrDefault();
  static readonly Dictionary<MsgId,MessageParser> parsers=ProtocolReflection.Descriptor.MessageTypes.Concat(RaidReflection.Descriptor.MessageTypes).Where(x=>Enum.TryParse<MsgId>(x.Name.Replace("_",""),out _)).ToDictionary(x=>(MsgId)Enum.Parse(typeof(MsgId),x.Name.Replace("_","")),x=>x.Parser);
  public Peer(int port){tcp=new TcpClient("127.0.0.1",port){NoDelay=true};Client.Send=Send;}
  public void Send(IMessage m){byte[] b=m.ToByteArray(),p=new byte[b.Length+4];BitConverter.GetBytes((ushort)p.Length).CopyTo(p,0);BitConverter.GetBytes((ushort)(MsgId)Enum.Parse(typeof(MsgId),m.Descriptor.Name.Replace("_",""))).CopyTo(p,2);b.CopyTo(p,4);tcp.GetStream().Write(p);}
  public void Ready(){var m=Messages.OfType<S_SceneMove>().Last();Send(new C_SceneReady{TargetRoomId=m.TargetRoomId,TargetRoomType=m.TargetRoomType,TransferId=m.TransferId,SceneType=m.SceneType});}
  public void Poll(){while(tcp.Available>0){var b=new byte[Math.Min(tcp.Available,65536)];int n=tcp.GetStream().Read(b);data.AddRange(b.Take(n));}while(data.Count>=4){int n=data[0]|data[1]<<8;if(data.Count<n)break;var id=(MsgId)(data[2]|data[3]<<8);var b=data.GetRange(4,n-4).ToArray();data.RemoveRange(0,n);if(parsers.TryGetValue(id,out var parser)){var m=parser.ParseFrom(b);Messages.Add(m);Client.Handle(m);}}}
  public void Dispose()=>tcp.Dispose();
 }
}
