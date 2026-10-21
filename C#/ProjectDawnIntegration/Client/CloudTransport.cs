using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Text.RegularExpressions;
namespace ProjectDawn.Network {
 public static class CloudTransport {
  public static Stream Connect(string address,int port,string account,string invite,out TcpClient tcp) => ConnectCore(address,port,account,invite,null,false,out tcp);
  public static Stream ConnectMember(string address,int port,string account,string password,out TcpClient tcp) => ConnectCore(address,port,account,null,password,false,out tcp);
  public static void Register(string address,int port,string account,string password) {
   TcpClient tcp;using(var stream=ConnectCore(address,port,account,null,password,true,out tcp))using(tcp){}
  }
  static string JsonString(string value) {
   var result=new StringBuilder("\"");foreach(char c in value){if(c=='"'||c=='\\')result.Append('\\').Append(c);else if(c<32)result.Append("\\u").Append(((int)c).ToString("x4"));else result.Append(c);}return result.Append('"').ToString();
  }
  static Stream ConnectCore(string address,int port,string account,string invite,string password,bool register,out TcpClient tcp) {
   bool secure=address.StartsWith("tls://",StringComparison.OrdinalIgnoreCase);
   if(password!=null&&!secure)throw new InvalidOperationException("회원 로그인과 가입에는 인증된 TLS 서버가 필요합니다.");
   string host=secure?address.Substring(6):address;
   if(!secure&&!string.IsNullOrEmpty(invite))throw new InvalidOperationException("초대 키는 tls:// 서버에서만 사용할 수 있습니다.");
   var ips=Dns.GetHostAddresses(host);
   if(!secure&&(ips.Length==0||ips.Any(ip=>!PrivateAddress(ip))))throw new InvalidOperationException("외부 서버는 tls:// 주소와 계정별 초대 키가 필요합니다.");
   tcp=new TcpClient{NoDelay=true,SendTimeout=5000};
   try {
    var connecting=tcp.ConnectAsync(host,port);if(!connecting.Wait(5000))throw new TimeoutException("서버 연결 시간 초과");connecting.GetAwaiter().GetResult();
    Stream stream=tcp.GetStream();stream.WriteTimeout=5000;stream.ReadTimeout=8000;
    if(secure) {
     if(password!=null){if(!Regex.IsMatch(account??"","\\A[A-Za-z0-9_-]{3,32}\\z")||password.Length<8||password.Length>128||string.IsNullOrWhiteSpace(password)||password.Any(char.IsControl))throw new InvalidOperationException("아이디는 영문·숫자·_- 3~32자, 비밀번호는 8~128자로 입력해 주세요.");}
     else if(string.IsNullOrEmpty(account)||!Regex.IsMatch(account,"\\A[A-Za-z0-9_-]{3,64}\\z")||string.IsNullOrEmpty(invite)||!Regex.IsMatch(invite,"\\A[A-Za-z0-9_+/=-]{40,256}\\z"))throw new InvalidOperationException("클라우드 계정 ID와 초대 키를 확인하세요.");
     var tls=new SslStream(stream,false);tls.AuthenticateAsClient(host,null,SslProtocols.Tls12,true);stream=tls;
     var auth=Encoding.UTF8.GetBytes(password==null?"{\"account\":\""+account+"\",\"token\":\""+invite+"\"}":"{\"account\":"+JsonString(account.ToLowerInvariant())+",\"password\":"+JsonString(password)+",\"action\":\""+(register?"register":"login")+"\"}");
     stream.Write(new[]{(byte)auth.Length,(byte)(auth.Length>>8)},0,2);stream.Write(auth,0,auth.Length);
     Array.Clear(auth,0,auth.Length);int ack=stream.ReadByte();
     if(ack!=(register?2:1))throw new IOException(ack==4?"요청이 많습니다. 잠시 후 다시 시도해 주세요.":ack==3?"이 계정은 이미 접속 중입니다.":password==null?"초대 키 인증에 실패했습니다.":register?"가입할 수 없습니다. 입력 내용을 확인하거나 운영자에게 문의해 주세요.":"로그인할 수 없습니다. 아이디와 비밀번호를 확인해 주세요.");
    }
    stream.ReadTimeout=120000;return stream;
   }catch{tcp.Dispose();throw;}
  }
  static bool PrivateAddress(IPAddress ip) {
   if(IPAddress.IsLoopback(ip))return true;if(ip.IsIPv4MappedToIPv6)ip=ip.MapToIPv4();var b=ip.GetAddressBytes();
   return b.Length==4&&(b[0]==10||b[0]==192&&b[1]==168||b[0]==172&&b[1]>=16&&b[1]<=31);
  }
 }
}
