using System.Net.Sockets;
using System.Net.Security;
using System.Security.Cryptography;
using System.Text.Json;
using ProjectDawn.Cloud;
using ProjectDawn.Network;
int checks=0;
void Check(bool ok,string title){if(!ok)throw new Exception(title);checks++;Console.WriteLine("PASS "+title);}
Check(!MemberPasswords.ValidPassword("1234567"),"seven characters rejected");
Check(MemberPasswords.ValidPassword("12345678"),"eight characters accepted");
Check(MemberPasswords.ValidPassword(new string('x',12)),"existing twelve-character policy remains accepted");
Check(MemberPasswords.ValidPassword(new string('x',128))&&!MemberPasswords.ValidPassword(new string('x',129)),"upper boundary unchanged");
Check(!MemberPasswords.ValidPassword("        ")&&!MemberPasswords.ValidPassword("1234567\n"),"whitespace and control characters rejected");
Check(MemberPasswords.Iterations==600000&&MemberPasswords.Hash("12345678",RandomNumberGenerator.GetBytes(32)).Length==32,"hash work factor and size unchanged");
if(args.Length==2&&args[0]=="--aws"){
 string address=args[1],host=address.Substring("tls://".Length),account="p8_"+Guid.NewGuid().ToString("N").Substring(0,12);
 string password=Convert.ToHexString(RandomNumberGenerator.GetBytes(4));
 // Bypass client validation only in this test to prove the real TLS server rejects length seven.
 using(var tcp=new TcpClient()){
  await tcp.ConnectAsync(host,28443);using var tls=new SslStream(tcp.GetStream());await tls.AuthenticateAsClientAsync(host);
  var body=JsonSerializer.SerializeToUtf8Bytes(new{account=account+"_bad",password=password.Substring(0,7),action="register"});
  await tls.WriteAsync(new[]{(byte)body.Length,(byte)(body.Length>>8)});await tls.WriteAsync(body);
  using var timeout=new CancellationTokenSource(8000);var ack=new byte[1];await tls.ReadExactlyAsync(ack,timeout.Token);
  Check(ack[0]==0,"AWS TLS server rejects seven-character signup");
 }
 CloudTransport.Register(address,28443,account,password);Check(true,"AWS TLS registers eight-character password");
 using(var stream=CloudTransport.ConnectMember(address,28443,account,password,out var tcp))using(tcp)Check(true,"AWS TLS authenticates eight-character account");
 Console.WriteLine("Random test account retained without publishing password: "+account);
}
Console.WriteLine("[PASSWORD_POLICY_VALIDATED] "+checks);
