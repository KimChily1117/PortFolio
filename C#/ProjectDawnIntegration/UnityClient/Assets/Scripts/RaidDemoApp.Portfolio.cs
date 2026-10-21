using System;
using System.Collections;
using System.IO;
using System.Linq;
using Google.Protobuf.Protocol;
using UnityEngine;

namespace DnfRaidDemo {
 public sealed partial class RaidDemoApp {
  // Explicit recording-only input driver. Never writes simulation/authority state.
  bool portfolioDemo,portfolioEquipSent,portfolioChannelSent,portfolioTownChat,portfolioRaidChat,portfolioPhaseChat;
  int portfolioSlot=1;
  float portfolioPhaseTwo=-1,portfolioDbAt=-1,portfolioFinishAt=-1;
  PortfolioDbRow portfolioDb;
  long portfolioEpoch;
  float PortfolioAge => portfolioEpoch>0 ? (float)(DateTime.UtcNow.Ticks-portfolioEpoch)/10000000f : Time.realtimeSinceStartup;
  void UpdatePortfolioChat() {
   if(!portfolioDemo||!flowMode||flowBusy||net?.Dawn==null)return;
   // Returning accounts receive the last receipt. Use the same local close action
   // as the history panel before demonstrating a fresh match; never alter the grant.
   if(flowStage=="Town"&&RaidRewardsVisible&&raidRewards?.Status=="History"&&Time.realtimeSinceStartup-flowAt>1.5f){HideRaidRewards();FlowRecord("PortfolioHistoryClosed");}
   if(flowStage=="Town"&&flowPublic&&!flowReturned&&chatContext!=null&&!portfolioChannelSent) {
    portfolioChannelSent=true;if(chatContext.RoomId!=1){MoveChannel(1);return;}
   }
   if(!ChatAvailable||chatContext==null||chatQaKeys.Count>0||chatOpen)return;
   if(flowStage=="Town"&&flowPublic&&!flowReturned&&!portfolioTownChat&&PortfolioAge>34+(portfolioSlot-1)*4) {
    portfolioTownChat=true;QueueChatQa(new[]{"다들 준비됐나요? 바칼 토벌 출발!","마을 채팅 잘 보여요. 준비 완료!","말풍선도 보여요! 함께 갑시다.","4명 모두 준비 완료! 출발해요."}[portfolioSlot-1]);
    FlowRecord("PortfolioChatQueued Town");
   }
   if(flowStage=="Raid"&&!sim.CinematicActive&&!portfolioRaidChat&&sim.Elapsed>4+(portfolioSlot-1)*4) {
    portfolioRaidChat=true;QueueChatQa(new[]{"운석 표식은 빈 방으로 유도해요!","던전 채팅 수신 확인! 점프 준비!","다른 방에서도 파티 채팅 보여요!","확인! 표식 빼고 다시 합류할게요."}[portfolioSlot-1]);
    FlowRecord("PortfolioChatQueued Raid");
   }
   if(flowStage=="Raid"&&portfolioSlot==1&&!sim.CinematicActive&&portfolioPhaseTwo>0&&sim.Elapsed-portfolioPhaseTwo>8&&!portfolioPhaseChat) {
    portfolioPhaseChat=true;QueueChatQa("2페이즈 진입! 끝까지 함께 가요!");FlowRecord("PortfolioChatQueued PhaseTwo");
   }
  }
  [Serializable] sealed class PortfolioDbRow {
   public long encounter;public int player,item,owner,template,equipped;public string name,equipment;
  }
  [Serializable] sealed class PortfolioFrame {
   public string file,stage;public long utcTicks;public int local,tick,phase,room,selected;
   public float seconds,combatBossHp,maxBossHp;public bool clearCinematic,rewards,phaseTransition,equipped;
  }
  PlayerCommand PortfolioCommand() {
   int id=net.LocalPlayerId;var p=sim.Players[id];
   var c=RaidShowcase.Command(sim,id);c.Skills=0;c.Basic=false;
   if(sim.Mode!=EncounterMode.Fighting||sim.CinematicActive||p.Hp<=0||ChatBlocksInput)return new PlayerCommand{PlayerId=id};
   if(sim.Boss.Phase>=2&&portfolioPhaseTwo<0)portfolioPhaseTwo=sim.Elapsed;
   bool danger=false,ground=false;
   foreach(var z in sim.Zones) {
    if(z.FromPlayer||z.RoomId!=p.RoomId||z.Age<0||z.Age>=z.Telegraph+z.ActiveTime)continue;
    float remaining=z.Telegraph-z.Age;
    if(z.GroundOnly&&remaining<.65f)ground=true;
    if(z.GroundOnly&&remaining<.26f&&remaining>.15f&&p.JumpHeight<=.001f)c.Jump=true;
    if(!z.GroundOnly&&RaidSimulation.Intersects(z,p.Position,RaidSimulation.PlayerHurtboxHalfSize))danger=true;
   }
   bool opening=sim.Boss.Phase==1?sim.Elapsed<35:sim.Elapsed-portfolioPhaseTwo<32;
   bool pace=opening&&sim.Boss.Hp<sim.Boss.MaxHp*(sim.Boss.Phase==1?.8f:.4f);
   bool ready=!danger&&!ground&&!pace&&p.RoomId==RaidSimulation.BossRoom&&p.JumpHeight<=.001f&&p.AttackTime<=0&&
    Mathf.Abs(p.Position.y-sim.Boss.Position.y)<.86f&&Mathf.Abs(p.Position.x-sim.Boss.Position.x)<2.5f;
   if(ready) {
    int facing=p.Position.x<sim.Boss.Position.x?1:-1;
    c.Move=p.Facing!=facing?new Vector2(facing,0):Vector2.zero;
    if(p.Facing==facing) {
     c.Basic=true;
     if(p.OverkillTime>0&&p.OverkillAge>1.2f)c.Skills=PlayerCommand.Bit(SkillId.ExtremeOverkill);
     else if(!opening)for(int i=SkillBook.Count-1;i>=1;i--) {
      if(i==3||i%4!=id||sim.Elapsed<6+i*.65f)continue;
      // Save awakenings for phase two so their lingering contacts do not shorten it.
      if(sim.Boss.Phase==1&&(i>11||i==4))continue;
      var s=SkillBook.All[i];
      if(s.Id==SkillId.Frenzy&&p.FrenzyTime>0||s.Id==SkillId.Berserk&&p.BerserkTime>0)continue;
      if(p.Cooldowns[i]<=0&&p.Mp>=s.Mp){c.Skills=PlayerCommand.Bit(s.Id);break;}
     }
    }
   }
   return EvadeCharge(RoomAutoCommand(c));
  }
  void PortfolioReturned() {
   if(Time.realtimeSinceStartup-flowAt<3)return;
   string path=Path.Combine(smokeDirectory,"db-before-equip.json");
   if(portfolioDb==null&&File.Exists(path)) {
    var row=JsonUtility.FromJson<PortfolioDbRow>(File.ReadAllText(path));
    // Only a received owned inventory item may be sent to the ordinary equip handler.
    if(row.name!=flowCharacter||row.player!=row.owner||!flowItems.Any(i=>i.ItemDbId==row.item&&i.TemplateId==row.template))return;
    portfolioDb=row;portfolioDbAt=Time.realtimeSinceStartup;
    inventoryWindow=true;flowWardrobe=false;originalSelectedItem=row.item;
    FlowRecord("PortfolioSqlReceipt item="+row.item+" owner="+row.owner+" encounter="+row.encounter);
   }
   if(portfolioDb==null)return;
   if(!portfolioEquipSent&&Time.realtimeSinceStartup-portfolioDbAt>9) {
    portfolioEquipSent=true;net.Dawn.SendFlow(new C_EquipItem{ItemDbId=portfolioDb.item,Equipped=true});
    FlowRecord("PortfolioEquipRequested "+portfolioDb.item);
   }
   if(portfolioEquipSent&&portfolioFinishAt<0&&File.Exists(Path.Combine(smokeDirectory,"db-after-equip.json"))) {
    var row=JsonUtility.FromJson<PortfolioDbRow>(File.ReadAllText(Path.Combine(smokeDirectory,"db-after-equip.json")));
    if(row.item!=portfolioDb.item||row.owner!=portfolioDb.owner||row.encounter!=portfolioDb.encounter||row.equipped!=1||
     !flowItems.Any(i=>i.ItemDbId==row.item&&i.Equipped))return;
    portfolioFinishAt=Time.realtimeSinceStartup;FlowRecord("PortfolioSqlEquipConfirmed "+row.item);
    WriteFlowAudit();Capture("db-equipped-inventory.png");
   }
   if(portfolioFinishAt>0&&Time.realtimeSinceStartup-portfolioFinishAt>10){WriteFlowAudit();Application.Quit(errors==0?0:1);}
  }
  void DrawPortfolioEvidence() {
   if(!portfolioDemo||portfolioDb==null||flowStage!="Town")return;
   Fill(new Rect(12,100,280,182),new Color(.025f,.04f,.065f,.96f));
   Label(new Rect(24,112,258,25),"AWS MySQL · 실제 조회",18,gold);
   Label(new Rect(24,144,258,22),"아이템 #"+portfolioDb.item+" / 소유 캐릭터 #"+portfolioDb.owner,14,Color.white);
   Label(new Rect(24,173,258,22),"보상 → 아이템 소유권 일치",14,Color.white);
   Label(new Rect(24,207,258,22),portfolioFinishAt>0?"장착 응답 · DB 재조회 완료":portfolioEquipSent?"일반 장착 요청 · DB 재조회 중":"DB 저장 확인 · 획득 장비 선택",14,cyan);
   Label(new Rect(24,240,258,22),portfolioFinishAt>0?"Equipped = 1  ✓":"Equipped = 0 → 장착 예정",14,Color.white);
  }
  IEnumerator RecordPortfolioScreens() {
   string directory=Path.Combine(smokeDirectory,"recording");Directory.CreateDirectory(directory);
   string index=Path.Combine(directory,"frames.jsonl");
   if(File.Exists(index)){Debug.LogError("Portfolio recording requires fresh directory");yield break;}
   bool begun=false;float next=0;int number=0;
   while(true) {
    yield return new WaitForEndOfFrame();
    if(!begun&&flowStage=="Town"&&!flowBusy){begun=true;if(portfolioSlot==1&&fourAudio!=null){fourAudioStarted=true;fourAudio.Begin(directory);}}
    if(!begun||Time.realtimeSinceStartup<next)continue;
    int id=net!=null&&net.IsDawn&&inGame?net.LocalPlayerId:portfolioSlot-1;
    var row=new PortfolioFrame{file="frame-"+number++.ToString("D5")+".jpg",utcTicks=DateTime.UtcNow.Ticks,
     local=portfolioSlot-1,stage=flowStage,tick=sim?.TickIndex??0,seconds=sim?.Elapsed??0,phase=sim?.Boss.Phase??0,
     combatBossHp=sim?.Boss.Hp??0,maxBossHp=sim?.Boss.MaxHp??0,phaseTransition=sim!=null&&sim.PhaseTransitionActive,
     room=sim!=null&&id>=0&&id<sim.Players.Count?sim.Players[id].RoomId:-1,
     clearCinematic=ClearCinematicActive,rewards=RaidRewardsVisible,selected=originalSelectedItem,
     equipped=portfolioDb!=null&&flowItems.Any(i=>i.ItemDbId==portfolioDb.item&&i.Equipped)};
    var texture=ScreenCapture.CaptureScreenshotAsTexture();
    try{File.WriteAllBytes(Path.Combine(directory,row.file),texture.EncodeToJPG(88));}finally{Destroy(texture);}
    File.AppendAllText(index,JsonUtility.ToJson(row)+Environment.NewLine);next=Time.realtimeSinceStartup+1f/12f;
   }
  }
 }
}
