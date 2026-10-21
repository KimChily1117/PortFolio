using System.Net;
using System.Security.Cryptography.X509Certificates;
using ProjectDawn.Cloud;
class Program {
 static async Task<int> Main() {
  try {
   string Need(string name)=>Environment.GetEnvironmentVariable(name) is string value && !string.IsNullOrWhiteSpace(value)?value:throw new InvalidOperationException("Required configuration: "+name);
   var cert=X509CertificateLoader.LoadPkcs12FromFile(Need("DAWN_TLS_PFX"),Environment.GetEnvironmentVariable("DAWN_TLS_PASSWORD"),OperatingSystem.IsWindows()?X509KeyStorageFlags.DefaultKeySet:X509KeyStorageFlags.EphemeralKeySet);
   if(!cert.HasPrivateKey||cert.NotAfter.ToUniversalTime()<=DateTime.UtcNow)throw new InvalidOperationException("TLS certificate unavailable or expired");
   string? authConnection=Environment.GetEnvironmentVariable("DAWN_AUTH_DB_CONNECTION_STRING");
   IMemberAccounts? members=string.IsNullOrWhiteSpace(authConnection)?null:new MySqlMemberAccounts(authConnection,int.Parse(Environment.GetEnvironmentVariable("DAWN_MAX_ACCOUNTS")??"64"));
   var invites=members==null?new InviteRegistry(File.ReadAllText(Need("DAWN_INVITES_FILE"))):null;
   int port=int.Parse(Environment.GetEnvironmentVariable("DAWN_PUBLIC_PORT")??"28443");int backend=int.Parse(Environment.GetEnvironmentVariable("DAWN_BACKEND_PORT")??"28182");
   await using var gateway=new Gateway(IPAddress.Any,port,backend,cert,invites,members:members);gateway.Start();Console.WriteLine("[DAWN_TLS_READY] port="+gateway.Port+" auth="+(members==null?"invites":"members"));await gateway.RunAsync();return 0;
  }catch(Exception e){Console.Error.WriteLine("[DAWN_GATEWAY_FAILED] "+e.GetType().Name);return 1;}
 }
}
