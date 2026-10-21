using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Google.Protobuf;
using Google.Protobuf.Protocol;
using MySql.Data.MySqlClient;
using ProjectDawn.Cloud;

class Program {
 static int checks;static void Check(bool ok,string title){if(!ok)throw new Exception(title);checks++;Console.WriteLine("PASS "+title);}
 static string Connection=>Environment.GetEnvironmentVariable("PROJECT_DAWN_DB_CONNECTION_STRING")??throw new Exception("DB configuration required");
 static async Task<object?> Sql(string sql,params (string,object)[] args){using var c=new MySqlConnection(Connection);await c.OpenAsync();using var cmd=c.CreateCommand();cmd.CommandText=sql;foreach(var (k,v) in args)cmd.Parameters.AddWithValue(k,v);return await cmd.ExecuteScalarAsync();}
 sealed class Peer:IDisposable {
  public TcpClient Tcp;public SslStream Stream;
  public Peer(TcpClient tcp,SslStream stream){Tcp=tcp;Stream=stream;}
  public void Dispose(){Stream.Dispose();Tcp.Dispose();}
  public async Task Send(IMessage msg,MsgId id){byte[] body=msg.ToByteArray(),packet=new byte[body.Length+4];BitConverter.GetBytes((ushort)packet.Length).CopyTo(packet,0);BitConverter.GetBytes((ushort)id).CopyTo(packet,2);body.CopyTo(packet,4);await Stream.WriteAsync(packet);}
  public async Task<byte[]> Until(MsgId wanted){using var deadline=new CancellationTokenSource(10000);while(true){var h=new byte[4];await Stream.ReadExactlyAsync(h,deadline.Token);int size=h[0]|h[1]<<8;var b=new byte[size-4];await Stream.ReadExactlyAsync(b,deadline.Token);if((MsgId)(h[2]|h[3]<<8)==wanted)return b;}}
 }
 static async Task<(byte,Peer)> Auth(int port,X509Certificate2 root,string account,string password,string action) {
  var tcp=new TcpClient();await tcp.ConnectAsync(IPAddress.Loopback,port);var tls=new SslStream(tcp.GetStream());
  try {var chain=new X509ChainPolicy{TrustMode=X509ChainTrustMode.CustomRootTrust,RevocationMode=X509RevocationMode.NoCheck};chain.CustomTrustStore.Add(root);
   await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions{TargetHost="localhost",CertificateChainPolicy=chain,EnabledSslProtocols=SslProtocols.Tls12});
   var body=JsonSerializer.SerializeToUtf8Bytes(new{account,password,action});await tls.WriteAsync(new[]{(byte)body.Length,(byte)(body.Length>>8)});await tls.WriteAsync(body);using var timeout=new CancellationTokenSource(10000);var ack=new byte[1];await tls.ReadExactlyAsync(ack,timeout.Token);return(ack[0],new Peer(tcp,tls));
  }catch{tls.Dispose();tcp.Dispose();throw;}
 }
 static async Task<int> Main(string[] args) {
  try {
   if(args[0]=="--db-probe"){
    try {using var c=new MySqlConnection(Connection);c.Open();Console.WriteLine("PASS database TLS connection");return 0;}
    catch(Exception e){var secret=new MySqlConnectionStringBuilder(Connection).Password;for(Exception? current=e;current!=null;current=current.InnerException)Console.WriteLine(current.GetType().Name+": "+current.Message.Replace(secret,"[redacted]"));return 1;}
   }
   if(args[0]=="--public")return PublicMembership.Run(args[1],args[2]);
   if(args.Contains("--migrate")){foreach(var statement in File.ReadAllText("Server/Server/Migrations/MySql/002_member_auth.sql").Split(';'))if(!string.IsNullOrWhiteSpace(statement))await Sql(statement);Console.WriteLine("PASS additive member authentication schema applied");return 0;}
   int backend=int.Parse(args[0]);var store=new MySqlMemberAccounts(Connection);string prefix="mt_"+Guid.NewGuid().ToString("N").Substring(0,12);var ids=Enumerable.Range(1,4).Select(i=>prefix+"_"+i).ToArray();string password=Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))+"\"\\한글";
   using var rsa=RSA.Create(2048);var req=new CertificateRequest("CN=localhost",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);req.CertificateExtensions.Add(new X509BasicConstraintsExtension(true,false,0,true));var san=new SubjectAlternativeNameBuilder();san.AddDnsName("localhost");req.CertificateExtensions.Add(san.Build());using var generated=req.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddHours(1));using var cert=X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pfx),null,X509KeyStorageFlags.DefaultKeySet);
   await using var gateway=new Gateway(IPAddress.Loopback,0,backend,cert,null,members:store);gateway.Start();var running=gateway.RunAsync();
   foreach(string id in ids){var registered=await Auth(gateway.Port,cert,id,password,"register");using(registered.Item2)Check(registered.Item1==2,"TLS registration succeeds without opening game session");}
   Check(!await store.Register(ids[0].ToUpperInvariant(),password,CancellationToken.None),"case-insensitive duplicate registration denied");
   Check(await store.Verify(ids[0].ToUpperInvariant(),password,CancellationToken.None),"fresh store verifies salted password and canonical account");
   Check(!await store.Verify(ids[0],"wrong-password-123",CancellationToken.None)&&!await store.Verify(prefix+"_missing",password,CancellationToken.None),"wrong password and unknown account rejected");
   Check(!await store.Register(prefix+"_weak","short",CancellationToken.None),"weak registration password denied");
   Check(!await new MySqlMemberAccounts(Connection,1).Register(prefix+"_quota",password,CancellationToken.None),"persistent account quota denies further registration");
   var limitedSignup=await Auth(gateway.Port,cert,prefix+"_fifth",password,"register");using(limitedSignup.Item2)Check(limitedSignup.Item1==4,"fifth signup from same IP is throttled");
   var bypass=await Auth(gateway.Port,cert,ids[0],password,"invite");using(bypass.Item2)Check(bypass.Item1==0,"member gateway refuses legacy invitation action");
   string legacy=prefix+"_legacy";await Sql("INSERT INTO account(AccountName) VALUES(@id)",("@id",legacy));Check(!await store.Register(legacy,password,CancellationToken.None),"existing legacy account cannot be claimed through signup");
   Check(Convert.ToInt64(await Sql("SELECT COUNT(DISTINCT HEX(PasswordSalt)) FROM accountcredential WHERE LoginId IN (@a,@b,@c,@d)",("@a",ids[0]),("@b",ids[1]),("@c",ids[2]),("@d",ids[3])))==4,"identical passwords receive four unique salts");
   Check(Convert.ToInt64(await Sql("SELECT COUNT(*) FROM accountcredential c JOIN account a ON a.AccountDbId=c.AccountDbId WHERE c.LoginId=@id AND a.AccountName=@id AND OCTET_LENGTH(c.PasswordHash)=32 AND c.Iterations=600000",("@id",ids[0])))==1,"credential record is linked to the existing game account");
   await Sql("UPDATE accountcredential SET Disabled=1 WHERE LoginId=@id",("@id",ids[3]));Check(!await store.Verify(ids[3],password,CancellationToken.None),"disabled member cannot authenticate");await Sql("UPDATE accountcredential SET Disabled=0 WHERE LoginId=@id",("@id",ids[3]));
   var peers=new List<Peer>();var names=new List<string>();
   try {
    foreach(string id in ids){var auth=await Auth(gateway.Port,cert,id,password,"login");Check(auth.Item1==1,"member authenticates through TLS gateway");peers.Add(auth.Item2);await auth.Item2.Send(new C_RaidHello{Version=3},MsgId.CRaidHello);await auth.Item2.Send(new C_Login{UniqueId=id},MsgId.CLogin);var login=S_Login.Parser.ParseFrom(await auth.Item2.Until(MsgId.SLogin));Check(login.LoginOK==1&&login.Players.Count==0,"new account has its own empty character list");string name="모험_"+id;names.Add(name);await auth.Item2.Send(new C_CreatePlayer{Name=name},MsgId.CCreatePlayer);var created=S_CreatePlayer.Parser.ParseFrom(await auth.Item2.Until(MsgId.SCreatePlayer));Check(created.Player?.Name==name,"character creation belongs to authenticated account");}
    var duplicate=await Auth(gateway.Port,cert,ids[0],password,"login");using(duplicate.Item2)Check(duplicate.Item1==3,"same account cannot open concurrent sessions");
    for(int i=2;i<=4;i++){await peers[0].Send(new C_CreatePlayer{Name=names[0]+"_"+i},MsgId.CCreatePlayer);Check(S_CreatePlayer.Parser.ParseFrom(await peers[0].Until(MsgId.SCreatePlayer)).Player!=null,"additional character created within account");}
    await peers[0].Send(new C_CreatePlayer{Name=names[0]+"_5"},MsgId.CCreatePlayer);Check(S_CreatePlayer.Parser.ParseFrom(await peers[0].Until(MsgId.SCreatePlayer)).Player==null,"server enforces four character slots");
    await peers[0].Send(new C_EnterGame{Name=names[1]},MsgId.CEnterGame);
    for(int i=0;i<4;i++){await peers[i].Send(new C_EnterGame{Name=names[i]},MsgId.CEnterGame);var inventory=S_ItemList.Parser.ParseFrom(await peers[i].Until(MsgId.SItemList));Check(inventory.Items.Count>0,"new character receives existing equipment inventory");var entered=S_EnterGame.Parser.ParseFrom(await peers[i].Until(MsgId.SEnterGame));Check(entered.Player.Name==names[i],"own town entry succeeds; foreign character is not selected");}
   }finally{foreach(var p in peers)p.Dispose();}
   await Task.Delay(300);
   var returning=await Auth(gateway.Port,cert,ids[0],password,"login");using(returning.Item2){Check(returning.Item1==1,"new client connection authenticates existing account");await returning.Item2.Send(new C_Login{UniqueId=ids[0]},MsgId.CLogin);var saved=S_Login.Parser.ParseFrom(await returning.Item2.Until(MsgId.SLogin));Check(saved.Players.Count==4&&saved.Players.Any(p=>p.Name==names[0]),"characters persist across client reconnect");}
   var bad=await Auth(gateway.Port,cert,ids[2],"wrong-password-123","login");using(bad.Item2)Check(bad.Item1==0,"gateway denies wrong password before backend access");
   var rate=new AuthRateLimits();Check(rate.Allow("a",2,60000,0)&&rate.Allow("a",2,60000,1)&&!rate.Allow("a",2,60000,2)&&rate.Allow("a",2,60000,60000),"login throttling blocks bursts and recovers after window");
   Console.WriteLine("[DAWN_MEMBERS_VALIDATED] "+checks);Console.WriteLine("Test accounts retained with random unknown passwords; prefix="+prefix);return 0;
  }catch(Exception e){Console.Error.WriteLine("[MEMBER_TEST_FAILED] "+e.GetType().Name+" "+(e is MySqlException m?m.Number.ToString():e.Message));return 1;}
 }
}
