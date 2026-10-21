using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Google.Protobuf;
using Google.Protobuf.Protocol;
using ProjectDawn.Cloud;
class Program {
 static int checks;static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
 static byte[] Packet(IMessage message,MsgId id){var body=message.ToByteArray();var p=new byte[body.Length+4];BitConverter.GetBytes((ushort)p.Length).CopyTo(p,0);BitConverter.GetBytes((ushort)id).CopyTo(p,2);body.CopyTo(p,4);return p;}
 static async Task<(TcpClient,SslStream)> Connect(int port,X509Certificate2 root,string account,string token,string hostname="localhost") {
  var tcp=new TcpClient();await tcp.ConnectAsync(IPAddress.Loopback,port);var tls=new SslStream(tcp.GetStream(),false);
  try {var chain=new X509ChainPolicy {TrustMode=X509ChainTrustMode.CustomRootTrust,RevocationMode=X509RevocationMode.NoCheck};chain.CustomTrustStore.Add(root);
   await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions {TargetHost=hostname,CertificateChainPolicy=chain,EnabledSslProtocols=SslProtocols.Tls12});
   var auth=JsonSerializer.SerializeToUtf8Bytes(new{account,token});await tls.WriteAsync(new[]{(byte)auth.Length,(byte)(auth.Length>>8)});await tls.WriteAsync(auth);
   using var timeout=new CancellationTokenSource(3000);var ack=new byte[1];await tls.ReadExactlyAsync(ack,timeout.Token);if(ack[0]!=1)throw new IOException("Authentication rejected");return(tcp,tls);
  }catch{tcp.Dispose();throw;}
 }
 static async Task<bool> Closed(Stream stream){try{using var ct=new CancellationTokenSource(3000);return await stream.ReadAsync(new byte[1],ct.Token)==0;}catch(IOException){return true;}}
 static async Task<int> Main(){try {
  string token=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));string hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
  var profile=new ProjectDawn.Network.FriendPlaytestProfile{server="tls://203.0.113.10",account="friend_1",invite=token};
  Check(profile.Validate()==null,"personal profile supports TLS IP address without nickname as identity");
  profile.server="203.0.113.10";Check(profile.Validate()!=null,"friend profile cannot downgrade to plaintext");
  profile.server="tls://example.com/path";Check(profile.Validate()!=null,"friend profile rejects path in server address");
  profile.server="tls://example.com";profile.invite="wrong-secret";Check(profile.Validate()!=null&&!profile.Validate()!.Contains(profile.invite),"profile errors never echo invitation");
  Check(ProjectDawn.Network.FriendPlaytestProfile.ValidNickname("버서커01")&&!ProjectDawn.Network.FriendPlaytestProfile.ValidNickname(" ")&&!ProjectDawn.Network.FriendPlaytestProfile.ValidNickname("<script>"),"nickname accepts Korean and rejects blank or markup");
  var registry=new InviteRegistry(JsonSerializer.Serialize(new Dictionary<string,string>{{"friend_1",hash},{"friend_2",hash}}));
  Check(registry.Verify("friend_1",token)&&!registry.Verify("friend_3",token)&&!registry.Verify("friend_1",new string('x',44)),"per-account hashed invites reject unknown accounts and wrong keys");
  using var rsa=RSA.Create(2048);var req=new CertificateRequest("CN=localhost",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
  req.CertificateExtensions.Add(new X509BasicConstraintsExtension(true,false,0,true));var san=new SubjectAlternativeNameBuilder();san.AddDnsName("localhost");req.CertificateExtensions.Add(san.Build());
  using var generated=req.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddHours(1));
  using var cert=X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pfx),null,X509KeyStorageFlags.DefaultKeySet);
  var backend=new TcpListener(IPAddress.Loopback,0);backend.Start();int backendPort=((IPEndPoint)backend.LocalEndpoint).Port;
  await using var gateway=new Gateway(IPAddress.Loopback,0,backendPort,cert,registry);gateway.Start();var running=gateway.RunAsync();
  var (client,tls)=await Connect(gateway.Port,cert,"friend_1",token);using(client)using(tls)using(var raw=await backend.AcceptTcpClientAsync()){
   var login=Packet(new C_Login {UniqueId="friend_1"},MsgId.CLogin);await tls.WriteAsync(login);var received=new byte[login.Length];await raw.GetStream().ReadExactlyAsync(received);
   Check(received.SequenceEqual(login),"TLS authenticated login reaches private backend byte-exactly");
   var reply=Packet(new S_Login {LoginOK=1},MsgId.SLogin);await raw.GetStream().WriteAsync(reply);var response=new byte[reply.Length];await tls.ReadExactlyAsync(response);
   Check(response.SequenceEqual(reply),"backend response remains framed across TLS");
   bool duplicate=false;try{var second=await Connect(gateway.Port,cert,"FRIEND_1",token);second.Item1.Dispose();}catch(IOException){duplicate=true;}
   Check(duplicate,"case-variant duplicate account rejected while original is active");
   await tls.WriteAsync(login);Check(await Closed(tls),"repeated login cannot change authenticated identity");
  }
  var (other,otherTls)=await Connect(gateway.Port,cert,"friend_2",token);using(other)using(otherTls)using(var raw=await backend.AcceptTcpClientAsync()){
   await otherTls.WriteAsync(Packet(new C_Login {UniqueId="friend_1"},MsgId.CLogin));Check(await Closed(otherTls)&&!raw.GetStream().DataAvailable,"valid invite cannot impersonate another account");
  }
  bool wrong=false;try{var bad=await Connect(gateway.Port,cert,"friend_2",new string('z',44));bad.Item1.Dispose();}catch(IOException){wrong=true;}
  Check(wrong&&!backend.Pending(),"wrong key never opens a backend session");
  bool wrongName=false;try{var bad=await Connect(gateway.Port,cert,"friend_2",token,"wrong.example");bad.Item1.Dispose();}catch(AuthenticationException){wrongName=true;}
  Check(wrongName,"TLS validates hostname even for trusted test certificate");
  bool untrusted=false;try{using var stream=ProjectDawn.Network.CloudTransport.Connect("tls://localhost",gateway.Port,"friend_2",token,out var raw);raw.Dispose();}catch(AuthenticationException){untrusted=true;}
  Check(untrusted,"shipped client rejects untrusted certificate without trust bypass");
  bool plain=false;try{using var stream=ProjectDawn.Network.CloudTransport.Connect("127.0.0.1",backendPort,"friend_2",token,out var raw);raw.Dispose();}catch(InvalidOperationException){plain=true;}
  Check(plain,"client cannot send invite over plaintext transport");
  bool plainMember=false;try{using var stream=ProjectDawn.Network.CloudTransport.ConnectMember("127.0.0.1",backendPort,"member_test","test-password-123",out var raw);raw.Dispose();}catch(InvalidOperationException){plainMember=true;}
  Check(plainMember,"client cannot send member password over plaintext transport");
  bool plainSignup=false;try{ProjectDawn.Network.CloudTransport.Register("127.0.0.1",backendPort,"member_test","test-password-123");}catch(InvalidOperationException){plainSignup=true;}
  Check(plainSignup,"registration requires authenticated TLS even on LAN");
  backend.Stop();Console.WriteLine("[DAWN_CLOUD_VALIDATED] "+checks);return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
