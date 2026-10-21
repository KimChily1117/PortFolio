using System;
using System.Text.RegularExpressions;
namespace ProjectDawn.Network {
 [Serializable] public sealed class FriendPlaytestProfile {
  public int version=1;
  public string server="",account="",invite="";
  public int port=28443;
  public string Validate() {
   if(version!=1)return "접속 파일 버전이 맞지 않습니다. 새 파일을 받아 주세요.";
   if(string.IsNullOrWhiteSpace(server))return "테스트 서버를 준비 중입니다. 접속 파일을 받은 뒤 다시 실행해 주세요.";
   if(!server.StartsWith("tls://",StringComparison.OrdinalIgnoreCase)||Uri.CheckHostName(server.Substring(6))==UriHostNameType.Unknown||port<1||port>65535)
    return "접속 주소 설정이 올바르지 않습니다. 운영자에게 알려 주세요.";
   if(!Regex.IsMatch(account??"","\\A[A-Za-z0-9_-]{3,64}\\z")||!Regex.IsMatch(invite??"","\\A[A-Za-z0-9_+/=-]{40,256}\\z"))
    return "개인 접속 파일이 올바르지 않습니다. 운영자에게 새 파일을 받아 주세요.";
   return null;
  }
  public static bool ValidNickname(string value) {
   return Regex.IsMatch(value??"","\\A[\\p{L}\\p{N}_-]{2,20}\\z");
  }
 }
}
