using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using Google.Protobuf;
using Google.Protobuf.Protocol;
using ProjectDawn.Network;

static class PublicMembership {
 sealed class Credential {public string account{get;set;}="";public string password{get;set;}="";public string character{get;set;}="";public bool registered{get;set;}}
 sealed class Peer:IDisposable {
  readonly TcpClient tcp;readonly Stream stream;
  public Peer(string server,Credential credential){stream=CloudTransport.ConnectMember(server,28443,credential.account,credential.password,out tcp);}
  public void Dispose(){stream.Dispose();tcp.Dispose();}
  public void Send(IMessage m,MsgId id){byte[] body=m.ToByteArray(),p=new byte[body.Length+4];BitConverter.GetBytes((ushort)p.Length).CopyTo(p,0);BitConverter.GetBytes((ushort)id).CopyTo(p,2);body.CopyTo(p,4);stream.Write(p);}
  public byte[] Until(MsgId id){while(true){var h=new byte[4];stream.ReadExactly(h);var b=new byte[(h[0]|h[1]<<8)-4];stream.ReadExactly(b);if((MsgId)(h[2]|h[3]<<8)==id)return b;}}
 }
 public static int Run(string server,string privateDirectory) {
  Directory.CreateDirectory(privateDirectory);int checks=0;void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);checks++;}
  string prefix="qa_"+Guid.NewGuid().ToString("N").Substring(0,8);
  var credentials=new List<Credential>();var peers=new List<Peer>();
  try {
   for(int i=1;i<=4;i++) {
    string path=Path.Combine(privateDirectory,"p"+i+".json");
    var credential=File.Exists(path)?JsonSerializer.Deserialize<Credential>(File.ReadAllText(path))!:new Credential{account=prefix+"_"+i,password=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),character="AWS_"+prefix+"_"+i};
    if(!File.Exists(path))File.WriteAllText(path,JsonSerializer.Serialize(credential));
    if(!credential.registered){CloudTransport.Register(server,28443,credential.account,credential.password);credential.registered=true;File.WriteAllText(path,JsonSerializer.Serialize(credential));Check(true,"public TLS membership registered");}
    credentials.Add(credential);var peer=new Peer(server,credential);peers.Add(peer);peer.Send(new C_RaidHello{Version=3},MsgId.CRaidHello);peer.Send(new C_Login{UniqueId=credential.account},MsgId.CLogin);
    var login=S_Login.Parser.ParseFrom(peer.Until(MsgId.SLogin));Check(login.LoginOK==1,"public member login reaches Linux MySQL game server");
    if(login.Players.Count==0){peer.Send(new C_CreatePlayer{Name=credential.character},MsgId.CCreatePlayer);Check(S_CreatePlayer.Parser.ParseFrom(peer.Until(MsgId.SCreatePlayer)).Player?.Name==credential.character,"public member creates own character");}
   }
   for(int i=0;i<4;i++){peers[i].Send(new C_EnterGame{Name=credentials[i].character},MsgId.CEnterGame);Check(S_ItemList.Parser.ParseFrom(peers[i].Until(MsgId.SItemList)).Items.Count==7,"public member loads seven starter items");Check(S_EnterGame.Parser.ParseFrom(peers[i].Until(MsgId.SEnterGame)).Player.Name==credentials[i].character,"four public TLS sessions enter town");}
   peers[0].Send(new C_ChannelList(),MsgId.CChannelList);Check(S_ChannelList.Parser.ParseFrom(peers[0].Until(MsgId.SChannelList)).Rooms.Count>=2,"channel list uses public authenticated game protocol");
  }finally{foreach(var p in peers)p.Dispose();}
  Thread.Sleep(500);
  using(var returning=new Peer(server,credentials[0])){returning.Send(new C_Login{UniqueId=credentials[0].account},MsgId.CLogin);Check(S_Login.Parser.ParseFrom(returning.Until(MsgId.SLogin)).Players.Any(p=>p.Name==credentials[0].character),"public reconnect restores account character from MySQL");}
  Console.WriteLine("[DAWN_PUBLIC_MEMBERS_VALIDATED] "+checks);return 0;
 }
}
