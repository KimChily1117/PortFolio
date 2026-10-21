using System;
using System.Linq;
using DnfRaidDemo;
using ProjectDawn.Combat;
using ProjectDawn.Network;
using Hit=DnfRaidDemo.CombatEvent;
class Program {
 static int n;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);n++;}
 static RaidSimulation Setup(){var s=new RaidSimulation(4){NetworkPatternGate=true};s.Start();s.Boss.StaggerTime=999;s.Boss.Hp=s.Boss.MaxHp=100000;return s;}
 static void Step(RaidSimulation s,params PlayerCommand[] inputs)=>s.NetworkTick(inputs,-1);
 static void ServerPotions(bool propose,int buttons){
  long now=0;var state=CombatState.FourPlayers();state.Content=new RaidContentState(new[]{1,2,3,4},100);
  state.Content.Simulation.Players[1].Hp=20;state.Content.Simulation.Boss.StaggerTime=999;
  var policy=RaidContentRules.Timing();var engine=new EncounterEngine(901,1,1,policy,RaidContentRules.Create(),state,()=>now);
  var host=new SessionBinding(1,1,1,0,false);var peer=new SessionBinding(2,1,1,0,false);engine.Bind(host);engine.Bind(peer);
  var input=new PlayerInput(new InputKey(901,2,1,1),0,1,InputKind.Controls,buttons:buttons);
  engine.ReceiveInput(peer,input);
  if(propose)engine.ReceiveProposal(host,new HostProposal(901,1,1,1,2,100,input.Key));
  for(int i=0;i<100;i++){now=i*policy.TickUs;engine.Pump();}
  var actual=engine.FinalSnapshot.Content.Simulation.Players[1];
  bool allowed=propose&&buttons==32;
  Check(actual.Hp==(allowed?60:20)&&actual.PotionCharges==(allowed?4:5),allowed?"server finalizes host-proposed heal once":propose?"server rejects unknown control bits":"server cannot heal without host proposal");
 }
 static int Main(){try{
  var s=Setup();var p=s.Players[2];var use=new PlayerCommand{PlayerId=2,Potion=true};
  Step(s,use);Check(p.PotionCharges==5&&p.NextPotionTick==0,"full HP cannot consume potion");
  p.Hp=20;Step(s,use);Check(p.Hp==60&&p.PotionCharges==4&&p.NextPotionTick==902,"own slot heals 40 percent, consumes one and starts cooldown");
  Check(s.Players[0].PotionCharges==5&&s.Players[0].Hp==100,"other players unchanged");
  Check(s.Events.Last(e=>e.Kind=="Heal").Value==40,"healing event contains actual amount");
  Step(s,use);Check(p.Hp==60&&p.PotionCharges==4,"held or duplicate request cannot bypass cooldown");
  s.TickIndex=p.NextPotionTick-2;Step(s,use);Check(p.PotionCharges==4,"one tick before cooldown rejected");
  p.Hp=85;Step(s,use);Check(p.Hp==100&&p.PotionCharges==3&&s.Events.Last(e=>e.Kind=="Heal").Value==15,"exact boundary accepted and heal capped at max HP");
  var restored=RaidSnapshotCodec.Decode(RaidSnapshotCodec.Encode(s));Check(restored.Players[2].NextPotionTick==p.NextPotionTick&&restored.Players[2].PotionCharges==3,"snapshot and rollback retain consumable state");
  var clone=s.Clone();clone.Players[2].PotionCharges=0;Check(p.PotionCharges==3,"clone cannot mutate server stock");
  p.NextPotionTick=0;p.Hp=0;Step(s,use);Check(p.Hp==0&&p.PotionCharges==3,"potion cannot revive dead player");
  Step(s,new PlayerCommand{PlayerId=2,Revive=true});Check(p.Hp==50&&p.PotionCharges==3,"shared revive does not replenish potion stock");
  p.Disconnected=true;Step(s,use);Check(p.PotionCharges==3,"disconnected character cannot use potion");
  p.Disconnected=false;p.Departed=true;Step(s,use);Check(p.PotionCharges==3,"departed character cannot use potion");p.Departed=false;
  p.PotionCharges=0;Step(s,use);Check(p.Hp==50,"empty stock cannot heal");
  s.Mode=EncounterMode.Defeat;p.PotionCharges=1;Step(s,use);Check(p.Hp==50&&p.PotionCharges==1,"ended encounter rejects healing");
  s.Start();Check(s.Players.All(x=>x.PotionCharges==5&&x.NextPotionTick==0),"new encounter resets personal allowance");
  var projected=new RaidPresentationTimeline(2);s.Players[2].Hp=20;projected.Track(1,1,use);var display=projected.Project(s,5);
  Check(display.Players[2].Hp==20&&display.Players[2].PotionCharges==5,"prediction cannot publish healing or inventory consumption");
  s.Players[2].Facing=1;s.Players[2].Position=s.Boss.Position-new RaidVector(1,0);Step(s,new PlayerCommand{PlayerId=2,Skills=PlayerCommand.Bit(SkillId.BloodSword)});
  Check(Math.Abs(s.Zones.First(z=>z.FromPlayer).Damage-330)<.001f,"player damage buff applied to authoritative attack zone");
  var combo=new PersonalCombo();Hit Hit(int i,int source=2)=>new Hit{Kind="Damage",SourceId=source,TargetId=-1,Value=10,FeedbackKey="hit/"+i};
  Check(!combo.Accept(Hit(0,1),2,1)&&combo.Count==0,"remote player cannot increment personal combo");
  Check(combo.Accept(Hit(0),2,1)&&!combo.Accept(Hit(0),2,2)&&combo.Count==1,"repeated confirmed snapshots counted once");
  for(int i=1;i<50;i++)combo.Accept(Hit(i),2,i*3);
  Check(combo.Count==50&&combo.RankName=="SSS"&&combo.Damage==500,"50 actual hits earn demo SSS and real cumulative damage");
  combo.Advance(446);Check(combo.Count==50,"combo remains before five-second boundary");combo.Advance(447);Check(combo.Count==0&&combo.RankName=="F","five-second inactivity expires combo");
  Check(!combo.Accept(Hit(49),2,447),"expired combo cannot replay stale confirmed hit");
  combo.Reset();Check(combo.Accept(Hit(0),2,1),"new encounter accepts reused action identities");
  var trail=new HealthTrail();trail.Update(1,0,.01f);Check(trail.Update(.6f,1,.01f)==1,"health loss leaves white previous-health segment");
  Check(trail.Update(.6f,1.2f,.1f)==1,"damage trail holds briefly");Check(trail.Update(.6f,1.5f,.2f)<1,"damage trail then animates downward");
  Check(trail.Update(1,1.6f,.1f)==1,"healing snaps trail to new health without false damage");
  ServerPotions(true,32);ServerPotions(false,32);ServerPotions(true,64);
  var key=new InputKey(901,2,1,1);var potionInput=new PlayerInput(key,0,1,InputKind.Controls,buttons:32);
  Check(RaidContentState.Command(potionInput,1).Potion&&!RaidContentState.Command(potionInput,1,false).Potion,"potion press transmitted as edge, never repeated held input");
  Console.WriteLine("[QOL_VALIDATED] "+n);return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
