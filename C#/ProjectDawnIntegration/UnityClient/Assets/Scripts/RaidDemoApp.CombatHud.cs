using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using ProjectDawn.Network;
namespace DnfRaidDemo {
 public sealed partial class RaidDemoApp {
  readonly PersonalCombo personalCombo=new PersonalCombo();
  readonly Dictionary<string,Sheet> comboArt=new Dictionary<string,Sheet>();
  readonly HealthTrail[] playerTrails={new HealthTrail(),new HealthTrail(),new HealthTrail(),new HealthTrail()};
  readonly HealthTrail bossTrail=new HealthTrail();
  float comboPopAt=-100,rankPopAt=-100;
  int lastHudTick=-1;
  bool potionPressed;
  void ResetCombatHud(){personalCombo.Reset();foreach(var t in playerTrails)t.Reset();bossTrail.Reset();lastHudTick=-1;comboPopAt=rankPopAt=-100;potionPressed=false;if(dawnHud!=null)dawnHud.ResetHealth();}
  void UpdateCombatHud() {
   if(sim==null||net==null||flowMode&&(flowBusy||flowStage!="Raid"))return;
   int tick=net.IsDawn?(int)net.Dawn.ConfirmedTick:sim.TickIndex;
   if(tick<lastHudTick)ResetCombatHud();lastHudTick=tick;
   foreach(var e in sim.Events) {
    int rank=personalCombo.Rank;
    if(personalCombo.Accept(e,net.LocalPlayerId,tick-Mathf.RoundToInt(e.Age*60))) {
     comboPopAt=Time.unscaledTime;if(rank!=personalCombo.Rank)rankPopAt=Time.unscaledTime;
     if(flowMode||skillGallery||combatHudReview)Debug.Log("[PERSONAL_COMBO] local="+net.LocalPlayerId+" source="+e.SourceId+" count="+personalCombo.Count+" rank="+personalCombo.RankName+" key="+e.FeedbackKey);
    }
   }
   personalCombo.Advance(tick);
   foreach(var p in sim.Players)playerTrails[p.Id].Update(p.Hp/Mathf.Max(1,p.MaxHp),Time.unscaledTime,Time.unscaledDeltaTime);
   bossTrail.Update(sim.Boss.Hp/Mathf.Max(1,sim.Boss.MaxHp),Time.unscaledTime,Time.unscaledDeltaTime);
  }
  void HealthBar(Rect rect,float ratio,float trail,Color color) {
   OriginalHud("partyTrail",rect,new Color(.15f,.15f,.15f));
   OriginalFill("bossWhite",rect,trail,Color.white);
   OriginalFill("partyHp",rect,ratio,Color.white);
  }
  void HudSprite(string name,int frame,Vector2 center,float scale,Color tint) {
   Sheet sheet;int row;if(!comboArt.TryGetValue(name,out sheet)||!sheet.ByFrame.TryGetValue(frame,out row))return;
   DrawRow(sheet,row,center,sheet.Rows[row+5]+sheet.Rows[row+3]*.5f,sheet.Rows[row+6]+sheet.Rows[row+4]*.5f,scale,tint);
  }
  float HudNumber(string name,string number,float right,float y,float scale,Color tint) {
   Sheet sheet;if(!comboArt.TryGetValue(name,out sheet))return right;
   float width=0;foreach(char c in number){int row;if(sheet.ByFrame.TryGetValue(c-'0',out row))width+=(sheet.Rows[row+3]+1)*scale;}
   float x=right-width;
   foreach(char c in number){int row;if(!sheet.ByFrame.TryGetValue(c-'0',out row))continue;DrawRow(sheet,row,new Vector2(x,y),sheet.Rows[row+5],sheet.Rows[row+6],scale,tint);x+=(sheet.Rows[row+3]+1)*scale;}
   return right-width;
  }
  void DrawPersonalCombo() {
   if(personalCombo.Count==0)return;
   float pop=1+.65f*Mathf.Pow(Mathf.Clamp01(1-(Time.unscaledTime-comboPopAt)/.18f),2);
   float rankPop=1+.15f*Mathf.Clamp01(1-(Time.unscaledTime-rankPopAt)/.28f);
   float fade=Mathf.Clamp01((PersonalCombo.TimeoutTicks-(lastHudTick-personalCombo.LastHitTick))/35f);
   Color tint=new Color(1,1,1,fade);
   // Right-side stack leaves the arena centre and bottom skill bar unobstructed.
   int frame=(7-personalCombo.Rank)*2;
   HudSprite("dungeon_rank",frame,new Vector2(1214,484),rankPop,tint);
   HudSprite("dungeon_rank",frame+1,new Vector2(1214,483),rankPop,tint);
   HudNumber("dungeon_score",Mathf.RoundToInt((float)personalCombo.Damage).ToString(CultureInfo.InvariantCulture),1236,512,1.35f,tint);
   Label(new Rect(1020,544,216,19),"누적 피해",12,new Color(1,.8f,.35f,fade),false,TextAnchor.MiddleRight);
   HudSprite("newcombo_bonus",5,new Vector2(1191,597),1.8f,tint);
   HudNumber("combo_bonus_num",personalCombo.Count.ToString(CultureInfo.InvariantCulture),1126,579-(pop-1)*10,2.3f*pop,tint);
   Bar(new Rect(1080,628,156,3),Mathf.Clamp01(1-(lastHudTick-personalCombo.LastHitTick)/(float)PersonalCombo.TimeoutTicks),gold);
  }
  void DrawPotionSlot(PlayerState p) {
   float remaining=Mathf.Max(0,p.NextPotionTick-lastHudTick)/60f;
   Rect box=new Rect(1040,655,48,48);
   bool usable=p.Hp>0&&p.Hp<p.MaxHp&&p.PotionCharges>0&&remaining<=0&&sim.Mode==EncounterMode.Fighting;
   OriginalHud("slot",new Rect(box.x,box.y,48,48),usable?Color.white:Color.gray);
   OriginalHud("potionHp",new Rect(box.x+6,box.y+6,36,36));
   Label(new Rect(box.x+2,box.y+1,18,18),"1",13,Color.white,true);
   Label(new Rect(box.x+30,box.y+26,16,20),p.PotionCharges.ToString(),14,Color.white,true,TextAnchor.MiddleRight);
   if(remaining>0){Fill(box,new Color(0,0,0,.65f));Label(box,remaining.ToString("0.0"),18,Color.white,true,TextAnchor.MiddleCenter);}
   if(usable&&!ChatBlocksInput&&!pauseMenu&&!showHelp&&!AnyWindow&&GUI.Button(box,GUIContent.none,GUIStyle.none))potionPressed=true;
   Label(new Rect(1100,654,170,25),"회복 포션  ·  1",16,Color.white,true);
   Label(new Rect(1100,678,170,22),p.Hp<=0?"전투 불능 · F5 부활":"HP 40%  /  15초",13,new Color(.6f,1,.72f));
  }
 }
}
