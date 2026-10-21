using System;
using System.Collections;
using System.Linq;
using Google.Protobuf.Protocol;
using UnityEngine;
using UnityEngine.Networking;

namespace DnfRaidDemo {
 public sealed partial class RaidDemoApp {
  [Serializable] class ChannelPlayer {public int objectId;public string name;}
  [Serializable] class ChannelRoom {public int roomId,playerCount;public string roomType;public ChannelPlayer[] players;}
  [Serializable] class ChannelRooms {public ChannelRoom[] rooms;}
  ChannelRoom[] flowChannels=Array.Empty<ChannelRoom>();
  bool channelOpen,channelLoading,channelMoving,channelSucceeded;
  int channelCurrent;
  string channelStatus="채널 목록을 확인합니다.";
  void OpenChannels(){channelOpen=true;StartCoroutine(RefreshChannels());}
  IEnumerator RefreshChannels(){
   if(channelLoading)yield break;channelLoading=true;channelStatus="채널 목록을 불러오는 중…";
   net.Dawn.SendFlow(new C_ChannelList());
   float until=Time.realtimeSinceStartup+5;
   while(channelLoading && Time.realtimeSinceStartup<until)yield return null;
   if(channelLoading){channelStatus="채널 목록 응답이 지연되고 있습니다.";channelLoading=false;}
  }
  void ChannelListResult(S_ChannelList result){
   channelCurrent=result.CurrentRoomId;
   flowChannels=result.Rooms.OrderBy(r=>r.RoomId).Select(r=>new ChannelRoom{roomId=r.RoomId,playerCount=r.PlayerCount,roomType="Town"}).ToArray();
   channelLoading=false;channelStatus="채널을 선택하면 현재 위치를 유지하며 이동합니다.";
   FlowRecord("ChannelList "+string.Join(",",flowChannels.Select(r=>r.roomId+":"+r.playerCount)));
  }
  void MoveChannel(int room){if(channelMoving||flowBusy||flowStage!="Town"||flowMe==null)return;StartCoroutine(ChannelTransfer(room));}
  IEnumerator ChannelTransfer(int room){
   channelMoving=true;channelSucceeded=false;flowBusy=true;channelStatus="채널 이동 중…";inventoryWindow=skillWindow=false;
   float t=0;while(t<.2f){t+=Time.unscaledDeltaTime;flowFade=Mathf.Clamp01(t/.2f);yield return null;}
   net.Dawn.SendFlow(new C_ChannelMove{TargetRoomId=room});FlowRecord("ChannelMoveRequested "+room);
   float start=Time.realtimeSinceStartup;while(channelMoving&&Time.realtimeSinceStartup-start<5)yield return null;
   if(channelMoving){channelMoving=false;channelStatus="채널 이동 응답이 지연되고 있습니다. 목록을 다시 확인하세요.";}
   t=0;while(t<.2f){t+=Time.unscaledDeltaTime;flowFade=1-Mathf.Clamp01(t/.2f);yield return null;}
   flowBusy=false;if(channelSucceeded)StartCoroutine(RefreshChannels());
  }
  void ChannelResult(S_ChannelMove result){
   channelMoving=false;channelSucceeded=result.Success;channelCurrent=result.CurrentRoomId;
   if(result.Success){flowPeople.Clear();flowMe=null;channelOpen=false;flowStatus="채널 "+result.CurrentRoomId+"로 이동했습니다.";}
   else channelStatus=result.Reason=="ChannelFull"?"채널이 가득 찼습니다.":result.Reason=="AlreadyInChannel"?"현재 접속 중인 채널입니다.":"채널을 이동할 수 없습니다: "+result.Reason;
   FlowRecord("ChannelMoveResult "+result.Success+" "+result.CurrentRoomId+" "+result.Reason);
  }
  void DrawChannels(){
   if(!channelOpen)return;
   Fill(new Rect(355,150,570,455),ink);Label(new Rect(380,174,440,35),"마을 채널 선택",26,gold,true);
   if(GUI.Button(new Rect(866,173,34,30),"×"))channelOpen=false;
   Label(new Rect(380,219,515,42),channelStatus,14,Color.white);
   int row=0;foreach(var room in flowChannels.Take(6)){
    float y=275+row++*42;bool here=room.roomId==channelCurrent;
    Label(new Rect(382,y,330,32),"채널 "+room.roomId+"    "+room.playerCount+" / 50"+(here?"   [현재 채널]":""),18,here?cyan:Color.white);
    GUI.enabled=!here&&room.playerCount<50&&!channelMoving&&!channelLoading&&!flowBusy;
    if(GUI.Button(new Rect(762,y,130,32),"이동"))MoveChannel(room.roomId);GUI.enabled=true;
   }
   GUI.enabled=!channelLoading&&!channelMoving;if(GUI.Button(new Rect(760,552,132,32),"새로고침"))StartCoroutine(RefreshChannels());GUI.enabled=true;
  }
 }
}
