using System;
using System.Linq;
using DnfRaidDemo;
using ProjectDawn.Combat;
internal static partial class Program {
 static void TestActionTempo(){
  foreach(bool run in new[]{false,true}){
   var s=EquipmentScene();var p=s.Players[0];p.Position=RaidVector.zero;
   for(int t=0;t<30;t++)s.Tick(RaidSimulation.FixedStep,new[]{new PlayerCommand{PlayerId=0,Move=new RaidVector(1,0),Run=run}});
   Check(Math.Abs(p.Position.x-(run?4f:2.75f))<.001f,"v20 authoritative half-second "+(run?"run":"walk")+" displacement");
  }
  var normal=EquipmentScene();var oldTempo=EquipmentScene(1f/1.12f);
  int[] first=new int[2];var scenes=new[]{normal,oldTempo};
  for(int i=0;i<2;i++){
   var s=scenes[i];s.Tick(RaidSimulation.FixedStep,new[]{new PlayerCommand{PlayerId=0,Skills=PlayerCommand.Bit(SkillId.BloodSword)}});
   for(int t=1;t<=90;t++){
    s.Tick(RaidSimulation.FixedStep,Array.Empty<PlayerCommand>());
    if(first[i]==0&&s.Boss.Hp<s.Boss.MaxHp)first[i]=t;
   }
  }
  Check(first[0]>0&&first[0]<first[1],"v20 Blood Sword impact is earlier in actual simulation");
  Check(Math.Abs(normal.Boss.Hp-oldTempo.Boss.Hp)<.001f,"tempo retains per-cast damage");
  foreach(var s in scenes)s.Tick(RaidSimulation.FixedStep,new[]{new PlayerCommand{PlayerId=0,Jump=true}});
  bool same=true;
  for(int t=0;t<50;t++){
   foreach(var s in scenes)s.Tick(RaidSimulation.FixedStep,Array.Empty<PlayerCommand>());
   same &= Math.Abs(scenes[0].Players[0].JumpHeight-scenes[1].Players[0].JumpHeight)<.00001f;
  }
  Check(same,"tempo does not accelerate ordinary jump or change avoidance height");
 }
}
