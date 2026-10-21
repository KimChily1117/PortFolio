using System;
using System.IO;
using Google.Protobuf.Protocol;
using ProjectDawn.Network;
using UnityEngine;
namespace DnfRaidDemo {
 public sealed partial class RaidDemoApp {
  void FlowLoadFriendProfile() {
   var args=Environment.GetCommandLineArgs();
   string path=Arg("--friend-profile",Path.GetFullPath(Path.Combine(Application.dataPath,"..","playtest.connection.json")));
   flowFriendMode=Array.IndexOf(args,"--friend")>=0||Array.IndexOf(args,"--friend-profile")>=0||File.Exists(path);
   if(!flowFriendMode)return;
   // Private profile supplies authentication; the nickname is never an account credential.
   flowAccount="";flowInvite="";flowCharacter="";
   try {
    if(!File.Exists(path)){flowFriendError="개인 접속 파일이 없습니다. 받은 폴더 전체를 압축 해제해 주세요.";}
    else if(new FileInfo(path).Length>4096){flowFriendError="개인 접속 파일이 올바르지 않습니다.";}
    else {
     var profile=JsonUtility.FromJson<FriendPlaytestProfile>(File.ReadAllText(path));
     flowFriendError=profile==null?"개인 접속 파일이 올바르지 않습니다.":profile.Validate();
     if(flowFriendError==null){hostAddress=profile.server;portText=profile.port.ToString();flowAccount=profile.account;flowInvite=profile.invite;flowCharacter=PlayerPrefs.GetString("DawnFriendName/"+flowAccount,"");}
    }
   }catch{flowFriendError="개인 접속 파일을 읽지 못했습니다. 운영자에게 새 파일을 받아 주세요.";}
   flowStatus=flowFriendError??"닉네임을 입력하고 모험을 시작하세요.";
  }
  bool FlowFriendNickname() {
   flowCharacter=flowCharacter.Trim();
   if(FriendPlaytestProfile.ValidNickname(flowCharacter))return true;
   flowStatus="닉네임은 문자·숫자·밑줄·하이픈 2~20자로 입력해 주세요.";return false;
  }
  void FlowFriendCreate() {
   if(!FlowFriendNickname())return;
   flowStatus="캐릭터를 만들고 있습니다…";FlowState("Creating");
   net.Dawn.SendFlow(new C_CreatePlayer{Name=flowCharacter});
  }
  void DrawFriendLogin() {
   bool canEdit=flowStage!="Connecting"&&flowStage!="Creating"&&!flowBusy&&flowFriendError==null;
   Label(new Rect(420,363,110,32),"닉네임",20,Color.white);
   GUI.enabled=canEdit;
   GUI.SetNextControlName("FriendNickname");
   flowCharacter=GUI.TextField(new Rect(530,360,330,40),flowCharacter,20);
   bool enter=Event.current.type==EventType.KeyDown&&(Event.current.keyCode==KeyCode.Return||Event.current.keyCode==KeyCode.KeypadEnter)&&GUI.GetNameOfFocusedControl()=="FriendNickname";
   bool clicked=GUI.Button(new Rect(420,435,440,52),flowStage=="Connecting"||flowStage=="Creating"?"접속 중…":"모험 시작");
   if(canEdit&&(clicked||enter)){if(enter)Event.current.Use();if(flowStage=="Characters")FlowFriendCreate();else FlowLogin();}
   GUI.enabled=true;
   Label(new Rect(410,510,460,54),"첫 접속은 닉네임으로 시작합니다.\n다음 접속에는 저장된 캐릭터를 이어서 플레이합니다.",15,Color.gray,false,TextAnchor.MiddleCenter);
  }
 }
}
