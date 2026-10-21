using System;
using System.Linq;
using DnfRaidDemo;
using ProjectDawn.Combat;
using ProjectDawn.Network;
class Program {
 static int checks;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);checks++;}
 static void Tick(RaidSimulation s,int n=1){for(int i=0;i<n;i++)s.NetworkTick(Array.Empty<PlayerCommand>(),-1);}
 static RaidSimulation Charge() {
  var s=new RaidSimulation(4){NetworkPatternGate=true};s.Start();
  s.Boss.Position=new RaidVector(-4,0);foreach(var p in s.Players){p.RoomId=0;p.Position=new RaidVector(2,0);}
  s.Players[2].RoomId=4;s.PendingPattern=8;s.PendingPatternTick=1;s.SelectionOrdinal=2;
  s.NetworkTick(Array.Empty<PlayerCommand>(),8,BossPatternProgram.Default(8,2));Tick(s,36);return s;
 }
 static int Main(){try {
  var s=Charge();var z=s.Zones.Single(x=>x.Tag=="BossCharge");
  Check(s.Boss.Position.x>-4&&s.Boss.Position.x<2,"approach closes distance before charge");
  Check(z.Telegraph>=.8f&&z.Damage==38&&z.Velocity.magnitude<=14.001f,"server-owned charge warning speed damage");
  Check(BossPatternProgram.Valid(BossPatternProgram.Default(8,2),8,2)&&!BossPatternProgram.Valid("0,0,2;4,0,2",8,2)&&!BossPatternProgram.Valid("0,0,2;4,36,1",8,2),"forged direction target and timing rejected");
  var before=s.Boss.Position;Tick(s,20);Check((s.Boss.Position-before).sqrMagnitude<.00001f&&s.Players[2].Hp==100,"warning is stationary and harmless");
  var replay=RaidSnapshotCodec.Decode(RaidSnapshotCodec.Encode(s));Tick(s,130);Tick(replay,130);
  Check(RaidSnapshotCodec.Encode(s).SequenceEqual(RaidSnapshotCodec.Encode(replay)),"charge snapshot restores exact future state");
  Check(s.Players[2].Hp<=62&&s.Players[2].Hp>=50,"charge direct hit plus weak non-stacking trail remain bounded");
  Check(s.Players[0].Hp==100,"charge cannot hit a player in another room");
  Check(s.Boss.Position.x<=7.501f&&Math.Abs(s.Boss.Position.y)<=2.301f,"charge stops inside arena");
  var dodge=Charge();var velocity=dodge.Zones.Single().Velocity;dodge.Players[2].Position=new RaidVector(2,2);Tick(dodge,100);
  Check(dodge.Players[2].Hp==100&&Math.Abs(dodge.Boss.Position.y)<.001f,"sidestep avoids locked direction; boss does not home");
  var delayed=Charge();Tick(delayed,40);var finalBytes=RaidSnapshotCodec.Encode(delayed);
  var timeline=new RaidPresentationTimeline(0);var screen=timeline.Project(delayed,delayed.TickIndex+18);var expected=delayed.Clone();Tick(expected,18);
  Check((screen.Boss.Position-expected.Boss.Position).sqrMagnitude<.00001f,"delayed presentation projects charge boss to authoritative clock");
  Check(screen.Zones.Any(x=>x.Tag=="ChargeTrail"),"delayed screen shows trail at the projected charge position");
  Check(finalBytes.SequenceEqual(RaidSnapshotCodec.Encode(delayed)),"presentation cannot change authoritative damage or positions");
  var volley=Charge();volley.PracticeInvulnerable=true;foreach(var player in volley.Players)player.RoomId=4;
  volley.Boss.Stagger=99;volley.Zones.Add(new AttackZone{Id=99999,FromPlayer=true,RoomId=4,OwnerId=0,Center=volley.Boss.Position,Radius=1,ActiveTime=.2f,Damage=10,Tag="stagger-test"});Tick(volley);
  Check(volley.Boss.StaggerTime==0&&volley.Boss.Stagger>=100,"stagger waits for three-charge sequence to finish");
  var targets=new System.Collections.Generic.List<int>{2};var ticks=new System.Collections.Generic.List<int>{37};
  for(int i=0;i<500;i++){Tick(volley);foreach(var e in volley.Events.Where(e=>e.Kind=="ChargeLocked"&&e.Age==0)){targets.Add((int)e.Value);ticks.Add(volley.TickIndex);}}
  Check(targets.SequenceEqual(new[]{2,3,0})&&ticks.Zip(ticks.Skip(1),(a,b)=>b-a).All(n=>n==162),"three marked targets charge at exact 162-tick intervals");
  Check(volley.Boss.StaggerTime>0&&!volley.Zones.Any(x=>x.Tag=="BossCharge"),"reserved stagger starts after third charge recovery");
  var burn=new RaidSimulation(4){NetworkPatternGate=true};burn.Start();burn.Boss.StaggerTime=100;
  foreach(var player in burn.Players){player.RoomId=player.Id==0?4:0;player.Position=RaidVector.zero;}
  for(int i=0;i<5;i++)burn.Zones.Add(new AttackZone{Id=10000+i,RoomId=4,Center=RaidVector.zero,Radius=.6f,ActiveTime=2.5f,Damage=3,Tag="ChargeTrail",GroundOnly=true});
  Tick(burn,160);
  Check(burn.Players[0].Hp==91&&burn.Players[0].StunTime==0&&burn.Players[0].Position.sqrMagnitude==0,"five overlapping weak fires deal only 3 HP per second without stun or knockback");
  Check(burn.Zones.Count==0&&burn.Players[1].Hp==100,"short fire expires after 2.5 seconds and cannot damage another room");
  var trailing=Charge();Tick(trailing,60);Check(trailing.Zones.Any(x=>x.Tag=="ChargeTrail"),"moving charge leaves server-simulated fire patches");
  var tr=RaidSnapshotCodec.Decode(RaidSnapshotCodec.Encode(trailing));Tick(trailing,90);Tick(tr,90);
  Check(RaidSnapshotCodec.Encode(trailing).SequenceEqual(RaidSnapshotCodec.Encode(tr)),"burn cooldown and remaining charges restore deterministically");
  var lua=new LuaPatternPlanner();s=new RaidSimulation(4){SelectionOrdinal=2,SelectionPhase=1,PendingPatternTick=100};var choice=lua.Request(s);
  Check(choice.Pattern==8&&choice.Program.Contains("4,36,2"),"actual host Lua proposes charge callback");
  foreach(bool forged in new[]{false,true}) {
   long now=0;var state=CombatState.FourPlayers();state.Content=new RaidContentState(new[]{1,2,3,4},100);
   var content=state.Content.Simulation;content.PendingPattern=8;content.PendingPatternTick=30;content.SelectionOrdinal=2;
   var policy=RaidContentRules.Timing();var engine=new EncounterEngine(90,1,1,policy,RaidContentRules.Create(),state,()=>now);var host=new SessionBinding(1,1,1,0,false);engine.Bind(host);
   engine.ReceiveProposal(host,new HostProposal(90,1,1,30,100,209,program:forged?"0,0,2;4,36,1;4,198,3;4,360,0":BossPatternProgram.Default(8,2)));
   var events=new System.Collections.Generic.List<ProjectDawn.Combat.CombatEvent>();
   for(int t=1;t<100;t++){now=t*policy.TickUs;engine.Pump();events.AddRange(engine.DrainCommittedEvents());}
   Check(events.Any(e=>e.Kind==(forged?"ActionRejected":"ActionCommitted"))&&events.Any(e=>e.Kind=="ContentChargeLocked")==!forged,forged?"authoritative engine rejects forged Lua charge target":"authoritative engine commits Lua charge and emits locked target");
  }
  Console.WriteLine("[DAWN_AI_VALIDATED] "+checks);return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
