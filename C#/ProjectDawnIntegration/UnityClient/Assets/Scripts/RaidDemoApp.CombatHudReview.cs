using System;
using System.IO;
using UnityEngine;
namespace DnfRaidDemo {
 public sealed partial class RaidDemoApp {
  bool combatHudReview,reviewDamage,reviewPotion,reviewTrail,reviewHealed,reviewRank;
  float reviewEnd=-1;
  bool StartCombatHudReview(string[] args){
   if(Array.IndexOf(args,"--combat-hud-review")<0)return false;
   combatHudReview=true;Begin(NetworkRole.Offline);sim.PracticeInvulnerable=false;
   sim.Boss.Position=Vector2.zero;sim.Boss.Hp=sim.Boss.MaxHp=1000000;sim.Boss.StaggerTime=999;
   sim.Players[0].Position=new Vector2(-1.2f,0);sim.Players[0].FrenzyTime=999;
   return true;
  }
  PlayerCommand CombatHudReviewCommand(int id){
   var c=new PlayerCommand{PlayerId=id,Basic=sim.Elapsed<23};
   if(sim.Elapsed>=4&&!reviewPotion){c.Potion=true;reviewPotion=true;}
   return c;
  }
  void UpdateCombatHudReview(){
   if(sim==null)return;
   if(sim.Elapsed>=3&&!reviewDamage){reviewDamage=true;sim.Zones.Add(new AttackZone{Id=990001,RoomId=4,Center=sim.Players[0].Position,Radius=2,Shape=ZoneShape.Circle,Damage=40,ActiveTime=.1f,Tag="HudReviewDamage"});}
   if(sim.Elapsed>=3.12f&&!reviewTrail){reviewTrail=true;Capture("health-damage-trail.png");}
   if(sim.Elapsed>=4.3f&&!reviewHealed){reviewHealed=true;Capture("potion-healed.png");}
   if(personalCombo.RankName=="SSS"&&!reviewRank){reviewRank=true;Capture("combo-sss.png");}
   if(sim.Elapsed>=29&&reviewEnd<0){Capture("combo-expired.png");Directory.CreateDirectory(smokeDirectory);
    File.WriteAllText(Path.Combine(smokeDirectory,"combat-hud-review.json"),JsonUtility.ToJson(new CombatHudReviewResult{errors=errors,sss=reviewRank,hp=sim.Players[0].Hp,potions=sim.Players[0].PotionCharges,combo=personalCombo.Count,font=font.name},true));reviewEnd=Time.unscaledTime+1;
   }
   if(reviewEnd>0&&Time.unscaledTime>reviewEnd)Application.Quit(errors==0&&reviewRank&&sim.Players[0].Hp==100&&sim.Players[0].PotionCharges==4&&personalCombo.Count==0?0:1);
  }
  [Serializable]class CombatHudReviewResult{public int errors,potions,combo;public bool sss;public float hp;public string font;}
 }
}
