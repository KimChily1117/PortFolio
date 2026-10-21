using UnityEngine;
namespace DnfRaidDemo {
 public sealed partial class RaidDemoApp {
  bool raidRevivePressed;
  void DrawRaidRevive() {
   if(!flowMode||flowStage!="Raid"||flowBusy||sim==null||net?.Dawn==null||sim.Mode!=EncounterMode.Fighting)return;
   Label(new Rect(1050,380,220,24),"파티 공용 부활  "+sim.ReviveCharges+" / 3",16,gold);
   var p=sim.Players[net.LocalPlayerId];if(p.Hp>0||p.Departed)return;
   Fill(new Rect(430,280,420,108),new Color(.04f,.03f,.05f,.92f));
   string text=sim.ReviveCharges>0?"F5 · HP 50%로 부활 (공용 1회 사용)":"공용 부활을 모두 사용했습니다.";
   Label(new Rect(445,292,390,27),text,18,Color.white);
   if(sim.WipeDeadlineTick>0)Label(new Rect(445,322,390,24),"전멸까지 "+Mathf.Max(0,(sim.WipeDeadlineTick-sim.TickIndex)/60f).ToString("F1")+"초",16,gold);
   if(sim.ReviveCharges>0&&!ChatBlocksInput&&GUI.Button(new Rect(550,351,180,28),"부활 · F5"))raidRevivePressed=true;
  }
 }
}
