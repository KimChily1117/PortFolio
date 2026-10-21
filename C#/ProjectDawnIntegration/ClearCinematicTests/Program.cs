using System;
using System.Linq;
using ProjectDawn.Combat;
class Program {
 static int checks;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);checks++;}
 static void Main(){
  for(int count=1;count<=4;count++){
   Check(RaidRewardPresentation.ResultMs(count)-RaidRewardPresentation.RevealMs(count)==15000,"full return hold after final card "+count);
   Check(RaidRewardPresentation.RevealMs(count)==5200+1800*count,"countdown plus actual participant reveal slots "+count);
   var slots=Enumerable.Range(0,count).ToArray();var c=new RaidClearCinematic(77,slots,1000);
   Check(c.Required==count/2+1,"strict majority for "+count);
   Check(!c.Vote(77,0,2999)&&!c.Complete,"no skip before fade ends "+count);
   Check(!c.Vote(76,0,3000)&&!c.Vote(77,9,3000),"wrong encounter and outsider rejected "+count);
   for(int i=0;i<c.Required;i++){
    Check(c.Vote(77,i,3000),"unique vote "+count+"/"+i);
    Check(!c.Vote(77,i,3000),"duplicate vote ignored "+count+"/"+i);
    Check(c.Complete==(i+1>=c.Required),"only majority releases "+count+"/"+i);
   }
   Check(c.Reason=="MajoritySkip"&&!c.Advance(999999,slots),"completion immutable "+count);
  }
  var full=new RaidClearCinematic(1,new[]{0,1,2,3},500);
  Check(!full.Advance(full.EndMs-1,new[]{0,1,2,3})&&!full.Complete,"natural end not early");
  Check(full.Advance(full.EndMs,new[]{0,1,2,3})&&full.Reason=="Finished","natural end at exact deadline");
  var disconnect=new RaidClearCinematic(2,new[]{0,1,2,3},0);
  disconnect.Vote(2,0,3000);disconnect.Vote(2,1,3000);
  Check(!disconnect.Complete,"two of four is not majority");
  disconnect.Advance(3000,new[]{0,1,2,99});
  Check(disconnect.Complete&&disconnect.Viewers==3&&disconnect.Required==2,"disconnect adjusts viewers; outsider cannot join");
  var absent=new RaidClearCinematic(3,new[]{0,1,2,3},0);absent.Vote(3,0,3000);
  absent.Advance(3000,new[]{1,2,3});Check(absent.Votes==0&&!absent.Complete,"departed voter not counted");
  absent.Advance(3001,Array.Empty<int>());Check(absent.Reason=="NoViewers","empty room releases without reward mutation");
  Console.WriteLine("[CLEAR_CINEMATIC_POLICY_PASSED] "+checks);
 }
}
