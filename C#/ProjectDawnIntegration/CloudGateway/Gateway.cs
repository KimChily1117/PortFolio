using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.RegularExpressions;
using Google.Protobuf.Protocol;
namespace ProjectDawn.Cloud;

public sealed class InviteRegistry {
 readonly Dictionary<string,byte[]> accounts;
 public InviteRegistry(string json) {
  var values=JsonSerializer.Deserialize<Dictionary<string,string>>(json)??throw new InvalidDataException("Missing invites");
  accounts=new(StringComparer.OrdinalIgnoreCase);
  foreach(var pair in values){if(!ValidAccount(pair.Key))throw new InvalidDataException("Invalid account ID");var hash=Convert.FromHexString(pair.Value);if(hash.Length!=32)throw new InvalidDataException("Invalid invite hash");accounts.Add(pair.Key,hash);}
  if(accounts.Count==0||accounts.Count>128)throw new InvalidDataException("Invite roster must contain 1 to 128 accounts");
 }
 public static bool ValidAccount(string account)=>Regex.IsMatch(account,"\\A[A-Za-z0-9_-]{3,64}\\z");
 public bool Verify(string account,string token) {
  if(string.IsNullOrEmpty(account)||string.IsNullOrEmpty(token)||token.Length<40||token.Length>256)return false;
  var supplied=SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
  var known=accounts.TryGetValue(account,out var expected);
  return CryptographicOperations.FixedTimeEquals(supplied,expected??new byte[32])&&known;
 }
}
public sealed class Gateway : IAsyncDisposable {
 readonly TcpListener listener;readonly X509Certificate2 certificate;readonly InviteRegistry? invites;readonly int backendPort;
 readonly IMemberAccounts? members;readonly AuthRateLimits authLimits=new();readonly SemaphoreSlim passwordWorkers=new(2,2);
 readonly CancellationTokenSource stop=new();readonly SemaphoreSlim slots;
 readonly ConcurrentDictionary<string,byte> active=new(StringComparer.OrdinalIgnoreCase);
 readonly ConcurrentDictionary<long,Task> connections=new();long nextId;
 public int Port=>((IPEndPoint)listener.LocalEndpoint).Port;
 public Gateway(IPAddress bind,int port,int backendPort,X509Certificate2 certificate,InviteRegistry? invites,int capacity=32,IMemberAccounts? members=null) {
  if(capacity<1||capacity>128)throw new ArgumentOutOfRangeException(nameof(capacity));
  if(invites==null&&members==null)throw new ArgumentException("Authentication provider required");
  this.backendPort=backendPort;this.certificate=certificate;this.invites=invites;this.members=members;listener=new(bind,port);slots=new(capacity,capacity);
 }
 public void Start()=>listener.Start(64);
 public async Task RunAsync() {
  try {while(!stop.IsCancellationRequested){var client=await listener.AcceptTcpClientAsync(stop.Token);
   if(!slots.Wait(0)){client.Dispose();continue;}
   var id=Interlocked.Increment(ref nextId);var task=Serve(client);connections[id]=task;
   _=task.ContinueWith(t=>{connections.TryRemove(id,out _);slots.Release();},TaskScheduler.Default);
  }}catch(OperationCanceledException)when(stop.IsCancellationRequested){}
 }
 async Task Serve(TcpClient client) {
  string? account=null;bool registered=false;
  using(client)using(var life=CancellationTokenSource.CreateLinkedTokenSource(stop.Token))using(var tls=new SslStream(client.GetStream(),false)) {
   try {
    client.NoDelay=true;
    using(var auth=CancellationTokenSource.CreateLinkedTokenSource(life.Token)) {
     auth.CancelAfter(TimeSpan.FromSeconds(8));
     await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions{ServerCertificate=certificate,EnabledSslProtocols=SslProtocols.Tls12|SslProtocols.Tls13,ClientCertificateRequired=false},auth.Token);
     var prefix=new byte[2];await tls.ReadExactlyAsync(prefix,auth.Token);int count=prefix[0]|prefix[1]<<8;
     if(count<1||count>2048)throw new InvalidDataException("Auth size");var body=new byte[count];await tls.ReadExactlyAsync(body,auth.Token);
     var hello=JsonSerializer.Deserialize<Dictionary<string,string>>(body)??throw new InvalidDataException("Auth body");
     CryptographicOperations.ZeroMemory(body);
     if(!hello.TryGetValue("account",out account))throw new InvalidDataException("Auth rejected");
     if(members!=null) {
      if(!MemberPasswords.ValidId(account)||!hello.TryGetValue("password",out var password)||!hello.TryGetValue("action",out var action)||(action!="login"&&action!="register")||!MemberPasswords.ValidPassword(password)){await tls.WriteAsync(new byte[]{0},auth.Token);return;}
      account=account.ToLowerInvariant();string ip=((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString();long now=Environment.TickCount64;
      bool allowed=authLimits.Allow("all",60,60000,now)&&authLimits.Allow("ip/"+ip,20,60000,now)&&authLimits.Allow("id/"+account,6,60000,now);
      if(action=="register")allowed=allowed&&authLimits.Allow("signup/"+ip,4,3600000,now);
      if(!allowed||!passwordWorkers.Wait(0)){await tls.WriteAsync(new byte[]{4},auth.Token);return;}
      bool verified;
      try {verified=await Task.Run(()=>action=="register"?members.Register(account,password,auth.Token):members.Verify(account,password,auth.Token),auth.Token);}finally{passwordWorkers.Release();hello.Clear();password="";}
      if(!verified){await tls.WriteAsync(new byte[]{0},auth.Token);return;}
      if(action=="register"){await tls.WriteAsync(new byte[]{2},auth.Token);return;}
     } else if(!hello.TryGetValue("token",out var token)||invites==null||!invites.Verify(account,token))throw new InvalidDataException("Auth rejected");
     if(!active.TryAdd(account,0)){await tls.WriteAsync(new byte[]{3},auth.Token);return;}registered=true;
     await tls.WriteAsync(new byte[]{1},auth.Token);
    }
    using var backend=new TcpClient{NoDelay=true};await backend.ConnectAsync(IPAddress.Loopback,backendPort,life.Token);
    var raw=backend.GetStream();
    var inbound=ForwardClient(tls,raw,account!,life.Token);
    var outbound=raw.CopyToAsync(tls,65536,life.Token);
    await Task.WhenAny(inbound,outbound);life.Cancel();backend.Close();
    try{await Task.WhenAll(inbound,outbound);}catch(OperationCanceledException){}
   }catch(Exception e)when(e is IOException||e is SocketException||e is AuthenticationException||e is OperationCanceledException||e is JsonException||e is ArgumentException||e is System.Data.Common.DbException){Console.WriteLine("[GATEWAY_CLOSED] "+e.GetType().Name);}
   finally{life.Cancel();if(registered)active.TryRemove(account!,out _);}
  }
 }
 static async Task ForwardClient(Stream source,Stream destination,string account,CancellationToken token) {
  bool loggedIn=false;int helloCount=0,packetCount=0;long window=Environment.TickCount64;
  while(!token.IsCancellationRequested) {
   long now=Environment.TickCount64;if(now-window>=1000){window=now;packetCount=0;}
   if(++packetCount>600)throw new InvalidDataException("Packet rate exceeded");
   using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromMinutes(10));
   var header=new byte[4];await source.ReadExactlyAsync(header,deadline.Token);
   int size=header[0]|header[1]<<8;if(size<4)throw new InvalidDataException("Frame size");
   // A peer cannot keep a partial frame alive indefinitely or monopolize the backend.
   deadline.CancelAfter(TimeSpan.FromSeconds(10));var packet=new byte[size];header.CopyTo(packet,0);await source.ReadExactlyAsync(packet.AsMemory(4),deadline.Token);
   var id=(MsgId)(header[2]|header[3]<<8);
   if(id==MsgId.CLogin){var login=C_Login.Parser.ParseFrom(packet,4,size-4);if(loggedIn||!string.Equals(login.UniqueId,account,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Account binding rejected");loggedIn=true;}
   else if(!loggedIn&&(id!=MsgId.CRaidHello||++helloCount>2))throw new InvalidDataException("Login required");
   await destination.WriteAsync(packet,deadline.Token);
  }
 }
 public async ValueTask DisposeAsync(){stop.Cancel();listener.Stop();try{await Task.WhenAll(connections.Values);}catch{}stop.Dispose();}
}
