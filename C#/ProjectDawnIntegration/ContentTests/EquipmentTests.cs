using System;
using System.Linq;
using DnfRaidDemo;
using ProjectDawn.Combat;
internal static partial class Program
{
 static RaidSimulation EquipmentScene(float rate=1) {
  var s=new RaidSimulation(4);s.Start();s.Boss.StaggerTime=30;s.Boss.Hp=s.Boss.MaxHp=100000;
  s.Players[0].Position=new RaidVector(2.5f,0);s.Players[0].Mp=s.Players[0].MaxMp=10000;s.Players[0].AttackSpeedScale=rate;return s;
 }
 static void TestEquipmentTiming() {
  var normal=EquipmentScene();var fast=EquipmentScene(1.5f);
  var move=new[]{new PlayerCommand{PlayerId=0,Move=new RaidVector(-1,0)}};fast.Players[0].MoveSpeedScale=1.2f;
  float start=normal.Players[0].Position.x;for(int i=0;i<30;i++){normal.Tick(RaidSimulation.FixedStep,move);fast.Tick(RaidSimulation.FixedStep,move);}
  Check(Math.Abs((start-fast.Players[0].Position.x)/(start-normal.Players[0].Position.x)-1.2f)<.001f,"equipment move speed changes authoritative distance");
  var hitTicks=new int[2];var damage=new float[2];
  for(int i=0;i<2;i++) {
   var s=EquipmentScene(i==0?1:1.5f);s.Tick(RaidSimulation.FixedStep,new[]{new PlayerCommand{PlayerId=0,Skills=1<<(int)SkillId.BloodSword}});
   for(int t=1;t<160;t++){s.Tick(RaidSimulation.FixedStep,Array.Empty<PlayerCommand>());if(hitTicks[i]==0&&s.Boss.Hp<100000)hitTicks[i]=t;}
   damage[i]=100000-s.Boss.Hp;
  }
  Check(hitTicks[1]>0&&hitTicks[1]<hitTicks[0]&&Math.Abs(hitTicks[0]/1.5f-hitTicks[1])<3,"attack speed retimes actual first impact, not just sprite playback");
  Check(damage[0]>0&&Math.Abs(damage[0]-damage[1])<.01f,"attack speed preserves per-cast physical damage");
  foreach(float rate in new[]{.5f,2.5f}) {
   var s=EquipmentScene(rate);s.Tick(RaidSimulation.FixedStep,new[]{new PlayerCommand{PlayerId=0,Skills=1<<(int)SkillId.BloodyRave}});
   var ids=s.Zones.Where(z=>z.FromPlayer).Select(z=>z.Id).ToArray();var hit=new System.Collections.Generic.HashSet<int>();
   for(int t=0;t<800;t++) {
    foreach(var z in s.Zones.Where(z=>z.FromPlayer)){z.Center=s.Boss.Position;z.Follow=false;z.Velocity=RaidVector.zero;}
    s.Tick(RaidSimulation.FixedStep,Array.Empty<PlayerCommand>());
    foreach(var e in s.Events.Where(e=>e.Kind=="Damage"&&e.SourceId==0&&e.Age==0))hit.Add(int.Parse(e.FeedbackKey.Split('/').Last()));
   }
   Check(hit.Count==ids.Length,"all repeated hit windows remain effective at attack rate "+rate);
  }
  var snap=EquipmentScene(1.4f);snap.Players[0].MoveSpeedScale=1.15f;snap.Tick(RaidSimulation.FixedStep,new[]{new PlayerCommand{PlayerId=0,Basic=true}});
  var restored=RaidSnapshotCodec.Decode(RaidSnapshotCodec.Encode(snap));
  Check(restored.Players[0].ActionRate==1.4f*1.12f&&restored.Players[0].MoveSpeedScale==1.15f&&restored.Zones.All(z=>z.PlaybackRate==1.4f*1.12f),"snapshot preserves equipment times base tempo for action and hit clocks");
  var cinematic=EquipmentScene(2);cinematic.Tick(RaidSimulation.FixedStep,new[]{new PlayerCommand{PlayerId=0,Skills=1<<(int)SkillId.Destruction}});
  Check(cinematic.Players[0].ActionRate==1&&cinematic.Zones.All(z=>z.PlaybackRate==1),"awakening cutscene and skill keep their shared authored timeline");
  var basic=EquipmentScene(2);basic.Tick(RaidSimulation.FixedStep,new[]{new PlayerCommand{PlayerId=0,Basic=true}});
  Check(Math.Abs(basic.Players[0].Cooldowns[0]-SkillBook.Get(SkillId.Basic).Cooldown/(2*1.12f))<.001f,"basic combo recovery and repeat cooldown both follow attack speed");
 }
}
