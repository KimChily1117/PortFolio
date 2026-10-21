using System;
using System.Linq;
using DnfRaidDemo;
using ProjectDawn.Combat;
using ProjectDawn.Network;
class Program {
 static int count;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);count++;}
 static void Step(RaidSimulation s,params PlayerCommand[] commands)=>s.NetworkTick(commands,-1);
 static void Wait(RaidSimulation s,int ticks){for(int i=0;i<ticks;i++)Step(s);}
 static void Intro(){
  var s=new RaidContentState(new[]{10,20,30,40},100,true).Simulation;
  var p=s.Players[0];var initial=p.Position;var bossHp=s.Boss.Hp;
  Check(s.IntroActive&&s.NextRoomMarkTick==RaidSimulation.EncounterIntroTicks+300,"entry sets protected shared intro and delays room schedule");
  var hostile=new PlayerCommand{PlayerId=0,Move=new RaidVector(1,1),Basic=true,Jump=true,Dash=true,Potion=true,Skills=PlayerCommand.Bit(SkillId.Destruction)|PlayerCommand.Bit(SkillId.Frenzy)};
  for(int tick=1;tick<=600;tick++)Step(s,hostile);
  Check(s.TickIndex==600&&s.Elapsed==0&&p.Position.x==initial.x&&p.Position.y==initial.y&&p.Hp==100&&p.Mp==100&&p.JumpHeight==0,"intro advances network time but suppresses combat input and timer");
  Check(s.Zones.Count==0&&s.MarkTargets.Length==0&&s.PendingPattern<0&&p.ActionSequence==0&&s.Boss.Hp==bossHp,"no action, attack or meteor can run behind intro");
  var restored=RaidSnapshotCodec.Decode(RaidSnapshotCodec.Encode(s));
  Check(restored.IntroEnabled&&restored.IntroActive&&restored.TickIndex==600,"snapshot carries intro policy and exact position in cinematic");
  var projected=new RaidPresentationTimeline(0).Project(s,630);
  Check(projected.IntroActive&&projected.TickIndex==630&&projected.Elapsed==0,"display projection cannot advance combat timer during intro");
  Wait(s,RaidSimulation.EncounterLandingTick-s.TickIndex);Check(s.Events.Count(e=>e.Kind=="EncounterLanding"&&e.Age==0)==1,"single landing cue after recorded leap");
  Wait(s,132);Check(!s.IntroActive&&s.Elapsed==0&&s.TickIndex==RaidSimulation.EncounterIntroTicks,"intro ends exactly at server boundary");
  Step(s);Check(s.Elapsed>0&&s.Zones.Count==0&&p.ActionSequence==0,"discarded intro presses never fire after release");
  Step(s,new PlayerCommand{PlayerId=0,Move=new RaidVector(1,0)});Check(p.Position.x>initial.x,"control resumes after intro");
  Wait(restored,RaidSimulation.EncounterIntroTicks+2-restored.TickIndex);Check(restored.TickIndex==RaidSimulation.EncounterIntroTicks+2&&restored.Elapsed==s.Elapsed,"reconnected snapshot follows same end boundary");
 }
 static void Frenzy(){
  var s=new RaidSimulation(1){AutoBuffs=true,NetworkPatternGate=true};s.Start();s.Boss.StaggerTime=9999;
  var p=s.Players[0];var toggle=new PlayerCommand{PlayerId=0,Skills=PlayerCommand.Bit(SkillId.Frenzy)};
  Check(RaidFrenzy.SwordVisible(p),"initial ON stance has second sword");
  Step(s,toggle);Check(p.FrenzyTime==0&&!RaidFrenzy.SwordVisible(p),"ordinary skill command toggles OFF and removes blood sword");
  Check(s.Events.Any(e=>e.Kind=="BuffOff")&&!s.Events.Any(e=>e.Age==0&&e.Kind=="Skill"),"OFF does not replay activation FX");
  Wait(s,40);Step(s,new PlayerCommand{PlayerId=0,Basic=true});
  Check(!p.ActionFrenzy&&p.BasicComboStep==0,"OFF begins normal first basic attack");
  Wait(s,50);Step(s,toggle);Check(p.FrenzyTime>0&&RaidFrenzy.SwordVisible(p),"same command toggles ON");
  float enabled=p.FrenzyTime;Wait(s,120);Check(p.FrenzyTime==enabled,"toggle stays ON without countdown expiry");
  Step(s,new PlayerCommand{PlayerId=0,Basic=true});Check(p.ActionFrenzy&&p.BasicComboStep==0,"ON resets to first dual-blade attack");
  var restored=RaidSnapshotCodec.Decode(RaidSnapshotCodec.Encode(s));Check(restored.Players[0].FrenzyTime==enabled&&restored.Players[0].ActionFrenzy,"toggle survives checkpoint/reconnect");
  Wait(s,50);p.Hp=0;Step(s,toggle);Check(p.FrenzyTime==enabled,"dead player cannot toggle");
 }
 static void Authority(bool propose){
  long now=0;var state=CombatState.FourPlayers();state.Content=new RaidContentState(new[]{1,2,3,4},100);
  var policy=RaidContentRules.Timing();var e=new EncounterEngine(902,1,1,policy,RaidContentRules.Create(),state,()=>now);
  var host=new SessionBinding(1,1,1,0,false);var peer=new SessionBinding(2,1,1,0,false);e.Bind(host);e.Bind(peer);
  var input=new PlayerInput(new InputKey(902,2,1,1),0,1,InputKind.Controls,PlayerCommand.Bit(SkillId.Frenzy));e.ReceiveInput(peer,input);
  if(propose){var proposal=new HostProposal(902,1,1,1,2,100,input.Key);e.ReceiveProposal(host,proposal);e.ReceiveProposal(host,proposal);}
  for(int i=0;i<100;i++){now=i*policy.TickUs;e.Pump();}
  var p=e.FinalSnapshot.Content.Simulation.Players[1];
  Check(propose?p.FrenzyTime==0:p.FrenzyTime>0,propose?"host proposed toggle finalized once despite duplicate proposal":"server cannot toggle without host proposal");
 }
 static RaidSimulation PhaseScene(){
  var s=new RaidContentState(new[]{10,20,30,40},100,true).Simulation;
  Wait(s,RaidSimulation.EncounterIntroTicks+1);s.NextRoomMarkTick=s.TickIndex+1000;return s;
 }
 static void PhaseTwo(){
  var s=PhaseScene();var p=s.Players[0];var startPos=p.Position;
  p.RoomId=0;p.NextPotionTick=s.TickIndex+400;p.NextChargeBurnTick=s.TickIndex+30;p.Cooldowns[1]=5;
  s.MarkTargets=new[]{0,1};s.MarkTarget=0;s.MarkDeadline=s.TickIndex+200;
  s.RoomFires.Add(new RoomFire{RoomId=0,StartTick=s.TickIndex-20,EndTick=s.TickIndex+300,NextPulse=s.TickIndex+10});
  s.Zones.Add(new AttackZone{Id=900,Tag="RoomMeteorImpact",RoomId=0,Age=.1f,Telegraph=.5f,ActiveTime=.2f,Recovery=1,Damage=95});
  s.Zones.Add(new AttackZone{Id=901,FromPlayer=true,OwnerId=0,Age=0,Telegraph=1,ActiveTime=1,Damage=100});
  s.Boss.Hp=s.Boss.MaxHp*.5f;float elapsed=s.Elapsed;Step(s);
  int start=s.TickIndex,mark=s.MarkDeadline,fire=s.RoomFires[0].EndTick,potion=p.NextPotionTick,charge=p.NextChargeBurnTick;
  Check(s.PhaseTransitionActive&&s.CinematicActive&&s.Boss.Phase==2&&!s.PhaseTwoBattlefieldActive,"phase2 starts protected shared cinematic at 50 percent");
  Check(s.Elapsed==elapsed&&s.Zones.Count==1&&s.Zones[0].Tag=="RoomMeteorImpact"&&p.RoomId==0,"transition cancels attacks but preserves room location and persistent hazards");
  var hostile=new PlayerCommand{PlayerId=0,Move=new RaidVector(1,1),Basic=true,Jump=true,Dash=true,Potion=true,Skills=PlayerCommand.Bit(SkillId.Destruction)};
  for(int i=0;i<479;i++)Step(s,hostile);
  Check(!s.PhaseTwoBattlefieldActive&&s.Elapsed==elapsed&&p.Hp==100&&p.Cooldowns[1]==5&&p.Position.x==startPos.x&&p.ActionSequence==0,"479 paused ticks reject gameplay and keep HP cooldowns and combat time stable");
  Check(s.MarkDeadline==mark+479&&s.RoomFires[0].EndTick==fire+479&&p.NextPotionTick==potion+479&&p.NextChargeBurnTick==charge+479&&s.Zones[0].Age==.1f,"all persistent absolute deadlines and hazard ages remain paused");
  var restored=RaidSnapshotCodec.Decode(RaidSnapshotCodec.Encode(s));
  Check(restored.PhaseTransitionStartTick==start&&restored.PhaseTransitionActive,"reconnect carries exact phase2 timeline");
  var display=new RaidPresentationTimeline(0).Project(s,s.TickIndex+30);
  Check(display.PhaseTwoBattlefieldActive&&display.Elapsed==elapsed&&display.MarkDeadline==s.MarkDeadline+30&&display.Zones[0].Age==.1f,"projected battlefield switch preserves hazard and combat clocks");
  Step(s);Check(s.PhaseTwoBattlefieldActive&&s.Events.Count(e=>e.Kind=="PhaseBattlefield"&&e.Age==0)==1,"battlefield switches at tick480");
  Wait(s,156);Check(s.Events.Count(e=>e.Kind=="PhaseLanding"&&e.Age==0)==1,"dragon lands at tick636");
  Wait(s,84);Check(!s.CinematicActive&&s.Boss.TransitionTime==0&&s.Elapsed==elapsed&&s.Events.Count(e=>e.Kind=="PhaseReady"&&e.Age==0)==1,"ready at tick720 with no combat advancement");
  Step(s);Check(s.Elapsed>elapsed&&p.ActionSequence==0&&p.Position.x==startPos.x&&p.JumpHeight==0,"discarded cinematic input cannot leak into first combat tick");
  Wait(restored,242);Check(Convert.ToBase64String(RaidSnapshotCodec.Encode(s))==Convert.ToBase64String(RaidSnapshotCodec.Encode(restored)),"reconnect replay is bit-identical through transition exit");
  s.Start();Check(s.PhaseTransitionStartTick==-1&&!s.PhaseTwoBattlefieldActive,"new encounter clears old transition identity");
 }
 static void PhaseGate(){
  var s=PhaseScene();var p=s.Players[0];p.Position=s.Boss.Position;p.PhysicalAttack=1000000;
  Step(s,new PlayerCommand{PlayerId=0,Basic=true});Wait(s,50);
  Check(s.Mode==EncounterMode.Fighting&&s.Boss.Hp==s.Boss.MaxHp*.5f&&s.PhaseTransitionActive,"lethal awakening-sized damage cannot skip transformation");
  s=PhaseScene();s.Boss.Hp=s.Boss.MaxHp*.5f;
  s.Zones.Add(new AttackZone{Id=999,Tag="DawnMeteorShockwave",Telegraph=.05f,ActiveTime=.05f,Recovery=.05f,RoomId=4});
  Step(s);Check(s.Boss.Phase==1&&!s.PhaseTransitionActive,"phase gate waits for protected volley to finish");
  Wait(s,20);Check(s.PhaseTransitionActive&&s.Boss.Phase==2,"phase gate starts immediately after protected volley ends");
  s=PhaseScene();s.Boss.Phase=2;s.Boss.Hp=0;Step(s);Check(s.Mode==EncounterMode.Victory,"phase2 can still die normally");
 }
 static void PhaseAuthority(){
  long now=0;var state=CombatState.FourPlayers();state.Content=new RaidContentState(new[]{1,2,3,4},100,true);
  state.Content.Simulation.Boss.Hp=state.Content.Simulation.Boss.MaxHp*.5f;
  var policy=RaidContentRules.Timing();var engine=new EncounterEngine(903,1,1,policy,RaidContentRules.Create(),state,()=>now);
  var bindings=Enumerable.Range(1,4).Select(id=>new SessionBinding(id,1,1,0,false)).ToArray();foreach(var binding in bindings)engine.Bind(binding);
  var events=new System.Collections.Generic.List<ProjectDawn.Combat.CombatEvent>();
  for(int tick=0;tick<1600;tick++) {
   now=tick*policy.TickUs;
   if(tick==1556)foreach(var binding in bindings){
    var input=new PlayerInput(new InputKey(903,binding.Player,1,1),now,1,InputKind.Controls,PlayerCommand.Bit(SkillId.Destruction),1000,0,1);
    engine.ReceiveInput(binding,input);engine.ReceiveProposal(bindings[0],new HostProposal(903,1,binding.Player,policy.TickAt(now),binding.Player,100,input.Key));
   }
   engine.Pump();events.AddRange(engine.DrainCommittedEvents());
  }
  var final=engine.FinalSnapshot.Content;
  Check(events.Count(e=>e.Kind=="ActionCommitted")==4,"all four late-cinematic controls are admitted and resolved through host proposals");
  Check(!engine.IsAborted&&final.Simulation.Boss.Phase==2&&final.Simulation.PhaseTransitionStartTick==RaidSimulation.EncounterIntroTicks+1,"four-slot authoritative engine finalizes the same phase start");
  Check(final.Simulation.Players.All(p=>p.ActionSequence==0)&&final.Held.Count==0,"late cinematic held controls cannot fire after server confirmation boundary");
  Check(events.Count(e=>e.Kind=="ContentPhaseTransition")==1&&events.Count(e=>e.Kind=="ContentPhaseBattlefield")==1&&events.Count(e=>e.Kind=="ContentPhaseLanding")==1&&events.Count(e=>e.Kind=="ContentPhaseReady")==1,"server commits each shared cinematic event once across replay");
 }
 static void StompAndBalance(){
  var standing=new RaidSimulation(4){NetworkPatternGate=true};standing.Start();standing.ForcePattern(5);
  var jumping=standing.Clone();var jumped=new System.Collections.Generic.HashSet<int>();int impacts=0;
  for(int tick=0;tick<260;tick++) {
   var zone=jumping.Zones.FirstOrDefault(z=>RaidSimulation.IsStompZone(z)&&z.Age>=z.Telegraph*.85f&&z.Age<z.Telegraph&&!jumped.Contains(z.Id));
   bool jump=zone!=null;if(jump)jumped.Add(zone.Id);
   Step(jumping,Enumerable.Range(0,4).Select(id=>new PlayerCommand{PlayerId=id,Jump=jump}).ToArray());Step(standing);
   impacts+=jumping.Events.Count(e=>e.Kind=="StompImpact"&&e.Age==0);
  }
  Check(jumped.Count==3&&jumping.Players.All(p=>p.Hp==100),"all four players jumping at 85 percent avoid all three stomp waves");
  Check(standing.Players.All(p=>p.Hp==52)&&impacts==3,"standing players take three hits and each stomp emits one shared impact cue");
  var s=new RaidSimulation(1){NetworkPatternGate=true};s.Start();var p=s.Players[0];p.Position=s.Boss.Position-new RaidVector(1,0);
  Step(s,new PlayerCommand{PlayerId=0,Skills=PlayerCommand.Bit(SkillId.BloodSword)});
  Check(Math.Abs(s.Zones.First(z=>z.FromPlayer).Damage-330)<.001f,"player attack tuning is half the previous 660 blood-sword contact damage");
  s=new RaidSimulation(1){NetworkPatternGate=true};s.Start();float hp=s.Boss.Hp;
  s.Zones.Add(new AttackZone{Id=990,FromPlayer=true,OwnerId=0,RoomId=4,Shape=ZoneShape.Box,Center=s.Boss.Position+new RaidVector(0,1),HalfSize=new RaidVector(.1f,.25f),ActiveTime=.1f,Damage=100});
  Step(s);Check(s.Boss.Hp==hp-100,"expanded boss Y hurtbox accepts an offset hit outside the former lane");
  s=new RaidSimulation(1){NetworkPatternGate=true};s.Start();hp=s.Boss.Hp;
  s.Zones.Add(new AttackZone{Id=991,FromPlayer=true,OwnerId=0,RoomId=4,Shape=ZoneShape.Box,Center=s.Boss.Position+new RaidVector(0,1.1f),HalfSize=new RaidVector(.1f,.25f),ActiveTime=.1f,Damage=100});
  Step(s);Check(s.Boss.Hp==hp,"expanded boss Y hurtbox still rejects a hit beyond its new edge");
 }
 static void MeteorEllipse(){
  var s=new RaidSimulation(4){NetworkPatternGate=true,RoomsEnabled=true};s.Start();s.NextRoomMarkTick=10000;s.ForcePattern(6);
  var impacts=s.Zones.Where(z=>z.Tag=="DawnMeteorImpact").ToArray();var z=impacts[0];
  Check(impacts.Length==4&&impacts.All(x=>x.Shape==ZoneShape.Ellipse&&x.HalfSize.x==DawnPatternRules.HalfWidth&&x.HalfSize.y==DawnPatternRules.HalfDepth),"four meteor impacts use matching elliptical warning extents");
  Check(RaidSimulation.Intersects(z,z.Center+new RaidVector(3.29f,0),RaidVector.zero)&&RaidSimulation.Intersects(z,z.Center+new RaidVector(0,1.19f),RaidVector.zero),"ellipse retains horizontal and vertical axis reach");
  Check(!RaidSimulation.Intersects(z,z.Center+new RaidVector(3,1),RaidSimulation.PlayerHurtboxHalfSize)&&!RaidSimulation.Intersects(z,z.Center+new RaidVector(3.31f,0),RaidVector.zero),"Dawn impact misses rounded corners and outside axis using ground anchor");
  var room=z.Clone();room.Tag="RoomMeteorImpact";
  Check(RaidSimulation.Intersects(room,room.Center+new RaidVector(3.4f,0),new RaidVector(.2f,.1f))&&!RaidSimulation.Intersects(room,room.Center+new RaidVector(3.5f,1.3f),new RaidVector(.2f,.1f)),"room meteor uses nearest-point ellipse versus player footprint");
  var restored=RaidSnapshotCodec.Decode(RaidSnapshotCodec.Encode(s));Check(restored.Zones.Where(x=>x.Tag=="DawnMeteorImpact").All(x=>x.Shape==ZoneShape.Ellipse),"appended ellipse enum survives snapshot without changing old enum values");
  var marked=new RaidSimulation(1){NetworkPatternGate=true,RoomsEnabled=true};marked.Start();marked.NextRoomMarkTick=10000;marked.MarkTargets=new[]{0};marked.MarkTarget=0;marked.MarkDeadline=1;Step(marked);
  Check(marked.Zones.Any(x=>x.Tag=="RoomMeteorImpact"&&x.Shape==ZoneShape.Ellipse)&&marked.RoomFires.Count==1,"marked player meteor generates ellipse while retaining room fire mechanic");
  s.Zones.RemoveAll(x=>x.Tag=="DawnMeteorImpact");var jumped=new System.Collections.Generic.HashSet<int>();
  for(int tick=0;tick<420;tick++) {
   var wave=s.Zones.FirstOrDefault(x=>x.Tag=="DawnMeteorShockwave"&&x.Age>=x.Telegraph*.85f&&x.Age<x.Telegraph&&!jumped.Contains(x.Id));
   bool jump=wave!=null;if(jump)jumped.Add(wave.Id);
   Step(s,Enumerable.Range(0,4).Select(id=>new PlayerCommand{PlayerId=id,Jump=jump}).ToArray());
  }
  Check(jumped.Count==4&&s.Players.All(p=>p.Hp==100),"all four players can jump four full-room meteor shockwaves at 85 percent");
 }
 static int Main(){try{Intro();Frenzy();Authority(true);Authority(false);PhaseTwo();PhaseGate();PhaseAuthority();StompAndBalance();MeteorEllipse();Console.WriteLine("[ENCOUNTER_VALIDATED] "+count);return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
