using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Google.Protobuf.Protocol;
using UnityEngine;
namespace DnfRaidDemo {
 public sealed partial class RaidDemoApp {
  S_ChatContext chatContext;
  readonly List<string> chatLines=new List<string>();
  bool chatOpen,chatFocus;
  int chatBlockFrame=-1;
  long chatLastSequence;
  string chatDraft="",chatNotice="";
  Vector2 chatScroll;
  float chatNextSend;
  bool chatQaTown,chatQaRaid,chatQaTownShot,chatQaRaidShot;
  readonly ChatEnterInput chatEnter=new ChatEnterInput();
  readonly Queue<Event> chatQaKeys=new Queue<Event>();
  bool chatQaPaint;
  bool chatClearFocus;
  sealed class ChatBubble {public string Text;public float Until;public bool Audited;}
  readonly Dictionary<int,ChatBubble> chatBubbles=new Dictionary<int,ChatBubble>();
  bool ChatBlocksInput=>chatOpen||chatBlockFrame==Time.frameCount;
  bool ChatAvailable=>flowMode&&!flowBusy&&!ClearCinematicActive&&net?.Dawn!=null&&(flowStage=="Town"||flowStage=="Matching"||flowStage=="Raid")&&!AnyWindow&&!channelOpen&&!pauseMenu&&!showHelp&&!RaidRewardsVisible;
  void ResetChat() {chatContext=null;chatLines.Clear();chatBubbles.Clear();chatLastSequence=0;CloseChat();}
  void CloseChat(){chatEnter.Cancel();chatOpen=false;chatDraft="";chatFocus=false;chatClearFocus=true;chatBlockFrame=Time.frameCount;dash=jump=false;raidRevivePressed=false;pressedSkills=0;runInput.Reset();Input.imeCompositionMode=IMECompositionMode.Auto;}
  bool ChatPacket(IMessage message) {
   if(message is S_ChatContext context) {
    if(chatContext==null||chatContext.Scope!=context.Scope||chatContext.RoomId!=context.RoomId){chatLines.Clear();chatBubbles.Clear();chatLastSequence=0;}
    chatContext=context.Clone();CloseChat();chatNotice="";
    if(flowAuto)FlowRecord("ChatContext "+context.Scope+" room="+context.RoomId);
    return true;
   }
   if(!(message is S_Chat received))return false;
   if(chatContext==null||received.Scope!=chatContext.Scope||received.RoomId!=chatContext.RoomId)return true;
   if(received.Error!=""){chatNotice=received.Error=="InvalidText"?"한 줄 120자 이내로 입력해 주세요.":"채널이 바뀌었습니다. 다시 입력해 주세요.";return true;}
   if(received.Sequence<=chatLastSequence)return true;
   chatLastSequence=received.Sequence;
   chatLines.Add(received.SenderName+" : "+received.Text);
   chatBubbles[received.SenderId]=new ChatBubble{Text=received.Text,Until=Time.unscaledTime+5};
   if(chatLines.Count>80)chatLines.RemoveAt(0);chatScroll.y=100000;
   if(flowAuto)FlowRecord("ChatReceived "+received.Scope+" sender="+received.SenderId+" seq="+received.Sequence);
   if(flowAuto&&Array.IndexOf(Environment.GetCommandLineArgs(),"--chat-auto")>=0)FlowRecord("ChatGuiTextMatch "+received.Scope+" "+(received.Text==(received.Scope==ChatScope.ChatTown?"마을 Enter 전송 확인":"던전 Enter 전송 확인")));
   return true;
  }
  void SendChat() {
   if(chatContext==null||!ChatAvailable)return;
   var text=chatDraft.Trim();if(text.Length==0){CloseChat();return;}
   if(Time.unscaledTime<chatNextSend){chatNotice="잠시 후 전송해 주세요. (초당 1회)";return;}
   net.Dawn.SendFlow(new C_Chat{Text=text,Context=chatContext.Context});chatNextSend=Time.unscaledTime+1;
   if(flowAuto)FlowRecord("ChatSubmitted "+chatContext.Scope);
   CloseChat();chatNotice="";
  }
  void ChatEnterDown() {
   if(!ChatAvailable||chatContext==null)return;
   if(chatEnter.Press(chatOpen,Time.frameCount)){chatOpen=chatFocus=true;chatBlockFrame=Time.frameCount;dash=jump=false;raidRevivePressed=false;pressedSkills=0;runInput.Reset();Input.imeCompositionMode=IMECompositionMode.On;if(flowAuto)FlowRecord("ChatInputOpened");}
  }
  void ChatGuiKey() {
   if(!ChatAvailable||chatContext==null)return;
   var e=Event.current;
   bool enter=e.keyCode==KeyCode.Return||e.keyCode==KeyCode.KeypadEnter;
   if(enter&&e.type==EventType.KeyUp){chatEnter.Release();e.Use();return;}
   if(e.type!=EventType.KeyDown)return;
   if(e.keyCode==KeyCode.Escape&&chatOpen){CloseChat();e.Use();return;}
   if(!enter)return;
   ChatEnterDown();
   if(flowAuto)FlowRecord("ChatGuiEnter "+chatContext.Scope);
   // Let TextField/IME commit the final syllable; never discard the submit intent.
   if(Input.compositionString.Length==0)e.Use();
  }
  void QueueChatQa(string text) {
   chatQaKeys.Enqueue(new Event{type=EventType.KeyDown,keyCode=KeyCode.Return});
   chatQaKeys.Enqueue(new Event{type=EventType.KeyUp,keyCode=KeyCode.Return});
   foreach(char c in text)chatQaKeys.Enqueue(new Event{type=EventType.KeyDown,character=c});
   chatQaKeys.Enqueue(new Event{type=EventType.KeyDown,keyCode=KeyCode.Return});
   chatQaKeys.Enqueue(new Event{type=EventType.KeyUp,keyCode=KeyCode.Return});
  }
  void UpdateChat() {
   foreach(int id in chatBubbles.Where(p=>p.Value.Until<=Time.unscaledTime).Select(p=>p.Key).ToArray())chatBubbles.Remove(id);
   if(Input.GetKeyUp(KeyCode.Return)||Input.GetKeyUp(KeyCode.KeypadEnter))chatEnter.Release();
   if(!ChatAvailable){if(chatOpen)CloseChat();chatEnter.Release();return;}
   bool composing=Input.compositionString.Length>0;
   if(Input.GetKeyDown(KeyCode.Escape)&&chatOpen){CloseChat();return;}
   if(Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.KeypadEnter))ChatEnterDown();
   if(chatEnter.TakeSubmit(composing,Time.frameCount)&&chatOpen)SendChat();
   // QA injects GUI key/character events through the actual focused TextField.
   if(flowAuto&&Array.IndexOf(Environment.GetCommandLineArgs(),"--chat-auto")>=0&&chatContext!=null) {
    float age=Time.realtimeSinceStartup-flowAt;
    bool town=flowStage=="Town"&&flowPublic&&!flowReturned;
    if(town&&age>5.2f&&!chatQaTown){chatQaTown=true;QueueChatQa("마을 Enter 전송 확인");}
    if(town&&age>6.5f&&!chatQaTownShot){chatQaTownShot=true;Capture("chat-town.png");}
    if(flowStage=="Raid"&&age>2.5f&&!chatQaRaid){chatQaRaid=true;QueueChatQa("던전 Enter 전송 확인");}
    if(flowStage=="Raid"&&age>4.5f&&!chatQaRaidShot){chatQaRaidShot=true;Capture("chat-dungeon.png");}
   }
  }
  void DrawChat() {
   if(!ChatAvailable||chatContext==null)return;
   if(chatClearFocus){GUI.FocusControl(null);chatClearFocus=false;}
   ChatGuiKey();
   DrawChatBubbles();
   float height=chatOpen?235:157,top=682-height;
   Fill(new Rect(18,top,448,height),new Color(.025f,.03f,.05f,chatOpen?.94f:.65f));
   Outline(new Rect(18,top,448,height),new Color(.32f,.28f,.22f),1);
   string title=chatContext.Scope==ChatScope.ChatTown?"마을 · 채널 "+chatContext.RoomId:"던전 · 파티 전체";
   var plain=new GUIStyle(Style(15,new Color(.91f,.91f,.88f))){richText=false,wordWrap=true};
   GUI.Label(new Rect(30,top+8,420,23),title+"  |  "+(chatOpen?"Enter 전송 · Esc 취소":"Enter 채팅"),new GUIStyle(plain){normal={textColor=gold}});
   float content=0;foreach(string line in chatLines)content+=Mathf.Max(23,plain.CalcHeight(new GUIContent(line),397))+3;
   var view=new Rect(30,top+37,424,height-(chatOpen?91:44));
   chatScroll=GUI.BeginScrollView(view,chatScroll,new Rect(0,0,400,Mathf.Max(view.height,content)));
   float y=0;foreach(string line in chatLines){float h=Mathf.Max(23,plain.CalcHeight(new GUIContent(line),397));GUI.Label(new Rect(0,y,397,h),line,plain);y+=h+3;}GUI.EndScrollView();
   if(chatOpen){GUI.SetNextControlName("DawnChatInput");chatDraft=GUI.TextField(new Rect(30,top+height-48,342,33),chatDraft,120,new GUIStyle(Style(16,Color.white,false,TextAnchor.MiddleLeft,true)){richText=false});
    // ScrollView allocates different control IDs for Used events. Focus only once
    // the regular repaint has registered the final input control ID.
    if(chatFocus&&Event.current.type==EventType.Repaint){GUI.FocusControl("DawnChatInput");chatFocus=GUI.GetNameOfFocusedControl()!="DawnChatInput";}
    Input.compositionCursorPos=new Vector2(30*Screen.width/1280f,(top+height-15)*Screen.height/800f);
    if(GUI.Button(new Rect(380,top+height-48,74,33),"전송"))SendChat();
   }
   if(chatNotice!="")GUI.Label(new Rect(24,top-25,440,23),chatNotice,new GUIStyle(plain){normal={textColor=gold}});
  }
  void DrawChatBubbles() {
   var placed=new List<Rect>();
   var style=new GUIStyle(Style(16,new Color(.12f,.1f,.08f),false,TextAnchor.MiddleCenter)){richText=false,wordWrap=true};
   foreach(var pair in chatBubbles.OrderBy(p=>p.Key)) {
    Vector2 anchor;
    if(flowStage=="Raid") {
     var start=net.Dawn.Client.Start;if(start==null)continue;int slot=start.PlayerSlots.IndexOf(pair.Key);
     var actor=sim.Players.FirstOrDefault(p=>p.Id==slot&&!p.Departed&&p.RoomId==ViewedRoom);if(actor==null)continue;
     Vector2 position;if(!displayPositions.TryGetValue(slot,out position))position=actor.Position;
     float height;if(!displayHeights.TryGetValue(slot,out height))height=actor.JumpHeight;
     anchor=ScreenPoint(position)-new Vector2(0,148+Mathf.Max(height,RaidSimulation.SkillElevation(actor,PresentationExtrapolation()+actor.ActionPresentationLead))*65);
    }else {
     if(flowCamera==null||!flowPeople.TryGetValue(pair.Key,out var actor)||actor.PosInfo==null||(!flowPublic&&actor.ObjectId!=flowMe?.ObjectId))continue;
     float scale=Mathf.Min(Screen.width/1280f,Screen.height/800f),ox=(Screen.width-1280*scale)*.5f,oy=(Screen.height-800*scale)*.5f;
     var world=flowCamera.WorldToScreenPoint(new Vector3(actor.PosInfo.PosX,actor.PosInfo.PosY,0));if(world.z<0)continue;
     anchor=new Vector2((world.x-ox)/scale,(Screen.height-world.y-oy)/scale-162);
    }
    if(anchor.x<-50||anchor.x>1330||anchor.y<0||anchor.y>780)continue;
    string text=pair.Value.Text;float width=Mathf.Clamp(style.CalcSize(new GUIContent(text)).x+28,100,280);
    float heightBox=style.CalcHeight(new GUIContent(text),width-24)+18;
    var rect=new Rect(Mathf.Clamp(anchor.x-width/2,8,1272-width),Mathf.Max(65,anchor.y-heightBox-10),width,heightBox);
    for(int attempt=0;attempt<placed.Count+1;attempt++){var overlap=placed.FirstOrDefault(r=>r.Overlaps(rect));if(overlap.width==0)break;rect.y=overlap.y-heightBox-6;}
    rect.y=Mathf.Max(65,rect.y);placed.Add(rect);
    float alpha=Mathf.Clamp01((pair.Value.Until-Time.unscaledTime)/.6f);Color old=GUI.color;GUI.color=new Color(old.r,old.g,old.b,old.a*alpha);
    Fill(rect,new Color(.98f,.96f,.87f));Outline(rect,new Color(.35f,.28f,.18f),1);
    float tail=Mathf.Clamp(anchor.x,rect.x+12,rect.xMax-12);
    for(int y=0;y<8;y++)Fill(new Rect(tail-7+y,rect.yMax+y,15-y*2,1),new Color(.98f,.96f,.87f));
    GUI.Label(new Rect(rect.x+12,rect.y+9,rect.width-24,rect.height-18),text,style);GUI.color=old;
    if(flowAuto&&!pair.Value.Audited&&Event.current.type==EventType.Repaint){pair.Value.Audited=true;FlowRecord("ChatBubbleShown "+chatContext.Scope+" sender="+pair.Key);}
   }
  }
 }
}
