using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ProjectDawn.Network;
using UnityEngine;
namespace DnfRaidDemo {
 public sealed partial class RaidDemoApp {
  [Serializable] class MemberEndpoint {public int version=1;public string server="";public int port=28443;}
  [Serializable] class MemberTestCredential {public string account,password,character;}
  bool flowMemberMode,flowSignup;
  string flowPassword="",flowPasswordConfirm="",flowMemberError;
  void FlowLoadMemberEndpoint() {
   string path=Arg("--member-endpoint",Path.GetFullPath(Path.Combine(Application.dataPath,"..","playtest.server.json")));
   flowMemberMode=Array.IndexOf(Environment.GetCommandLineArgs(),"--member")>=0||Array.IndexOf(Environment.GetCommandLineArgs(),"--member-endpoint")>=0||File.Exists(path);
   if(!flowMemberMode)return;
   flowFriendMode=false;flowInvite="";flowAccount=PlayerPrefs.GetString("DawnMemberAccount","");flowCharacter="";
   try {
    if(!File.Exists(path)||new FileInfo(path).Length>4096)flowMemberError="서버 접속 파일을 확인해 주세요.";
    else {var endpoint=JsonUtility.FromJson<MemberEndpoint>(File.ReadAllText(path));
     if(endpoint==null||endpoint.version!=1)flowMemberError="접속 파일 버전이 맞지 않습니다.";
     else if(string.IsNullOrEmpty(endpoint.server))flowMemberError="테스트 서버를 준비 중입니다. 준비가 끝나면 안내해 드립니다.";
     else if(!endpoint.server.StartsWith("tls://",StringComparison.OrdinalIgnoreCase)||Uri.CheckHostName(endpoint.server.Substring(6))==UriHostNameType.Unknown||endpoint.port<1||endpoint.port>65535)flowMemberError="서버 접속 주소가 올바르지 않습니다.";
     else {hostAddress=endpoint.server;portText=endpoint.port.ToString();}
    }
   }catch{flowMemberError="서버 접속 파일을 읽지 못했습니다.";}
   // Explicit QA launch input; credentials are never included in the public package or command line.
   string testFile=Arg("--member-credential-file","");
   if(flowAuto&&!string.IsNullOrEmpty(testFile)) {
    try {if(new FileInfo(testFile).Length>4096)throw new InvalidDataException();var credential=JsonUtility.FromJson<MemberTestCredential>(File.ReadAllText(testFile));flowAccount=credential.account;flowPassword=credential.password;flowCharacter=credential.character;}
    catch{flowMemberError="테스트 계정 파일을 읽지 못했습니다.";}
   }
   flowStatus=flowMemberError??"게임 계정으로 로그인하세요. 처음이라면 회원가입을 선택하세요.";
  }
  bool FlowMemberInputs(bool register) {
   flowAccount=flowAccount.Trim().ToLowerInvariant();
   if(!Regex.IsMatch(flowAccount,"\\A[a-z0-9_-]{3,32}\\z")){flowStatus="아이디는 영문·숫자·_- 3~32자로 입력해 주세요.";return false;}
   if(flowPassword.Length<8||flowPassword.Length>128||string.IsNullOrWhiteSpace(flowPassword)){flowStatus="비밀번호는 8~128자로 입력해 주세요.";return false;}
   if(register&&flowPassword!=flowPasswordConfirm){flowStatus="비밀번호 확인이 일치하지 않습니다.";return false;}
   return true;
  }
  async void FlowRegisterMember() {
   if(flowBusy||flowMemberError!=null||!FlowMemberInputs(true))return;
   string account=flowAccount,password=flowPassword,address=hostAddress;int port=int.Parse(portText);
   flowPassword=flowPasswordConfirm="";FlowState("Registering");flowStatus="회원가입을 처리하고 있습니다…";
   try {await Task.Run(()=>CloudTransport.Register(address,port,account,password));if(this==null)return;flowSignup=false;flowStatus="가입되었습니다. 비밀번호를 입력하고 로그인하세요.";PlayerPrefs.SetString("DawnMemberAccount",account);}
   catch(Exception){if(this==null)return;flowStatus="가입할 수 없습니다. 입력 내용을 확인하거나 잠시 후 다시 시도해 주세요.";}
   finally {password="";if(this!=null)FlowState("Login");}
  }
  void DrawMemberLogin() {
   bool canEdit=!flowBusy&&flowMemberError==null&&flowStage!="Connecting"&&flowStage!="Registering";
   GUI.enabled=canEdit;
   Label(new Rect(410,352,115,30),"아이디",18,Color.white);flowAccount=GUI.TextField(new Rect(530,350,330,36),flowAccount,32);
   Label(new Rect(410,404,115,30),"비밀번호",18,Color.white);flowPassword=GUI.PasswordField(new Rect(530,402,330,36),flowPassword,'*',128);
   if(flowSignup){Label(new Rect(410,456,115,30),"비밀번호 확인",16,Color.white);flowPasswordConfirm=GUI.PasswordField(new Rect(530,454,330,36),flowPasswordConfirm,'*',128);}
   if(GUI.Button(new Rect(420,510,440,44),flowStage=="Registering"||flowStage=="Connecting"?"처리 중…":flowSignup?"회원가입":"로그인")){if(flowSignup)FlowRegisterMember();else FlowLogin();}
   if(GUI.Button(new Rect(420,566,440,28),flowSignup?"이미 계정이 있어요 · 로그인":"처음이신가요? · 회원가입")){flowSignup=!flowSignup;flowPassword=flowPasswordConfirm="";flowStatus=flowSignup?"아이디 3~32자 · 비밀번호 8~128자":"게임 계정으로 로그인하세요.";}
   GUI.enabled=true;
  }
 }
}
