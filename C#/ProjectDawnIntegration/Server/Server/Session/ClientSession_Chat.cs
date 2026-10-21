using System;
using System.Globalization;
using System.Text;
using Google.Protobuf.Protocol;
using Server.Game.Room;

namespace Server {
 public partial class ClientSession {
  readonly object chatGate=new object();
  long chatContext,chatRefill=Environment.TickCount64;
  double chatTokens=3;
  GameRoom chatRoom;
  internal void BindChat(GameRoom room) {
   lock(chatGate){chatRoom=room;chatContext++;Send(new S_ChatContext{Scope=room.RoomType==RoomType.Town?ChatScope.ChatTown:ChatScope.ChatDungeon,RoomId=room.RoomId,Context=chatContext});}
  }
  internal bool ChatContextMatches(GameRoom room,long context) {lock(chatGate)return chatRoom==room&&chatContext==context;}
  public void HandleChat(C_Chat packet) {
   var player=MyPlayer;var room=player?.Room;
   if(packet==null||ConnectionClosed||AccountDbId<=0||room==null||IsTransferring||player.Session!=this)return;
   string text;
   lock(chatGate) {
    long now=Environment.TickCount64;chatTokens=Math.Min(3,chatTokens+Math.Max(0,now-chatRefill)/1000.0);chatRefill=now;
    // Budget malformed requests too; do not queue unbounded room work or flood error replies.
    if(chatTokens<1)return;chatTokens--;
    string error=chatRoom!=room||packet.Context!=chatContext?"ContextChanged":null;
    text=packet.Text;
    if(error==null&&!ValidChatText(text))error="InvalidText";
    if(error!=null){Send(new S_Chat{Scope=room.RoomType==RoomType.Town?ChatScope.ChatTown:ChatScope.ChatDungeon,RoomId=room.RoomId,Error=error});return;}
    text=text.Trim().Normalize(NormalizationForm.FormC);
   }
   room.Push(()=>room.DeliverChat(this,player,packet.Context,text));
  }
  static bool ValidChatText(string text) {
   if(string.IsNullOrWhiteSpace(text)||text.Length>120||Encoding.UTF8.GetByteCount(text)>360)return false;
   for(int i=0;i<text.Length;i++) {
    char c=text[i];
    if(char.IsControl(c)||char.GetUnicodeCategory(c)==UnicodeCategory.Format)return false;
    if(char.IsHighSurrogate(c)){if(i+1>=text.Length||!char.IsLowSurrogate(text[++i]))return false;}
    else if(char.IsLowSurrogate(c))return false;
   }
   return true;
  }
 }
}
