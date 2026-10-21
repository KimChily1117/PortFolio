using System;
using System.Linq;
using DnfRaidDemo;
using ProjectDawn.Network;
using ProjectDawn.Combat;
using System.Collections.Generic;
internal static class Program
{
    static int checks;
    static void Check(bool pass,string name){if(!pass)throw new Exception(name);checks++;Console.WriteLine("PASS: "+name);}
    static void Reject(Action action,string name){bool rejected=false;try{action();}catch{rejected=true;}Check(rejected,name);}
    static int Main()
    {
        try
        {
            var lua=new LuaPatternPlanner();var sim=new RaidSimulation(4);
            for(int phase=1;phase<=2;phase++)for(int previous=-1;previous<=6;previous++)for(int ordinal=0;ordinal<20;ordinal++)
            {
                sim.SelectionRoll=unchecked((uint)(ordinal*1234567+previous*812+phase));sim.SelectionPhase=phase;
                sim.SelectionPrevious=previous;sim.SelectionOrdinal=ordinal;sim.PendingPatternTick=900+ordinal;
                var answer=lua.Request(sim);
                if(answer.Pattern!=DawnPatternRules.Select(sim.SelectionRoll,phase,previous,ordinal) || answer.Tick!=sim.PendingPatternTick || !BossPatternProgram.Valid(answer.Program,answer.Pattern,ordinal,sim.SelectionRoll))
                    throw new Exception("Lua/C# selection mismatch");
            }
            Check(lua.Requests==320,"320 Lua callbacks agree with independent C# selection");
            lua.Confirm(6,900);Check(lua.Confirmations==1,"final event invokes Lua confirmation callback");
            Reject(()=>new LuaPatternPlanner("while true do end"),"initialization instruction limit");
            Reject(()=>new LuaPatternPlanner("function on_pattern_requested(c) while true do end end").Request(sim),"callback instruction limit");
            Reject(()=>new LuaPatternPlanner("function on_pattern_requested(c) emit_pattern(6,c.start_tick);emit_pattern(6,c.start_tick) end").Request(sim),"duplicate emits rejected");
            Reject(()=>new LuaPatternPlanner("function on_pattern_requested(c) emit_pattern(9,c.start_tick) end").Request(sim),"unknown pattern rejected locally");
            Reject(()=>new LuaPatternPlanner("function on_pattern_requested(c) io.open('x','w') end").Request(sim),"Lua filesystem unavailable");
            Reject(()=>new LuaPatternPlanner("function on_pattern_requested(c) return 6 end").Request(sim),"missing callback does not fall back to C#");
            PreviewCorrection();RoomProposalGuards();MarkPairs();ActionPrograms();Meteor();ServerRejectsLua(false);ServerRejectsLua(true);
            Console.WriteLine("[DAWN_LUA_METEOR_VALIDATED] "+checks+" checks");return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    static void PreviewCorrection()
    {
        var sent=new List<Google.Protobuf.IMessage>();
        var client=new RaidNetworkClient{Send=m=>sent.Add(m)};
        var start=new Google.Protobuf.Protocol.S_RaidStart{Encounter=77,Host=1,Self=1,HostEpoch=1,Version=3,Ruleset=RaidContentRules.Id};
        for(int id=1;id<=4;id++)start.Fighters.Add(new Google.Protobuf.Protocol.RaidFighter{Id=id,Epoch=1});
        start.Fighters.Add(new Google.Protobuf.Protocol.RaidFighter{Id=100,IsBoss=true});
        start.PlayerSlots.AddRange(new[]{1,2,3,4});
        start.Content=Google.Protobuf.ByteString.CopyFrom(RaidSnapshotCodec.Encode(new RaidContentState(new[]{1,2,3,4},100).Simulation));
        client.Handle(start);
        var preview=new RaidSimulation(4){NetworkPatternGate=true,RoomsEnabled=true};preview.Start();
        preview.SelectionOrdinal=1;preview.SelectionRoll=0;preview.SelectionPhase=1;preview.SelectionAliveMask=15;preview.PendingPattern=0;preview.PendingPatternTick=30;preview.RoomSelectionRoll=0;preview.RoomSelectionAliveMask=15;preview.PendingRoomMarkTick=preview.NextRoomMarkTick=30;
        Action publish=()=>client.Handle(new Google.Protobuf.Protocol.S_RaidFrame{Encounter=77,Preview=Google.Protobuf.ByteString.CopyFrom(RaidSnapshotCodec.Encode(preview))});
        publish();publish();Check(sent.OfType<Google.Protobuf.Protocol.C_RaidProposal>().Count()==2,"concurrent boss and room previews each propose once without duplicate callbacks");
        // Model an admitted late input changing the pre-selection history: slot zero died.
        preview.Players[0].Hp=0;preview.RoomSelectionAliveMask=14;publish();publish();
        var proposals=sent.OfType<Google.Protobuf.Protocol.C_RaidProposal>().ToArray();var roomProposals=proposals.Where(p=>p.Definition==208).ToArray();
        Check(proposals.Length==3&&roomProposals.Length==2&&roomProposals[0].StartTick==roomProposals[1].StartTick&&roomProposals[0].Program!=roomProposals[1].Program,
            "changed rollback preview resends corrected mark program for unchanged tick and pattern");
        long now=0;var initial=CombatState.FourPlayers();initial.Content=new RaidContentState(new[]{1,2,3,4},100);initial.Content.Simulation=preview.Clone();
        var timing=RaidContentRules.Timing();var engine=new EncounterEngine(77,1,1,timing,RaidContentRules.Create(),initial,()=>now);var host=new SessionBinding(1,1,1,0,false);engine.Bind(host);
        foreach(var p in proposals)engine.ReceiveProposal(host,new HostProposal(77,1,p.Sequence,p.StartTick,100,p.Definition,program:p.Program));
        var events=new List<ProjectDawn.Combat.CombatEvent>();
        for(int tick=1;tick<=60;tick++){now=tick*timing.TickUs;engine.Pump();events.AddRange(engine.DrainCommittedEvents());}
        Check(!engine.IsAborted&&events.Count(e=>e.Kind=="ContentPattern")==1&&events.Count(e=>e.Kind=="ContentRoomPattern")==1&&!events.Any(e=>e.Kind=="ContentPatternMissing"||e.Kind=="ContentRoomPatternMissing")&&events.Any(e=>e.Kind=="ActionRejected")&&events.Any(e=>e.Kind=="ActionCommitted"),
            "server rejects obsolete program then executes corrected proposal once without missing-pattern failure");
        Check(engine.FinalSnapshot.Content.Simulation.MarkTargets.SequenceEqual(new[]{1,2}),"corrected final pair uses the replayed living roster");
    }
    static void RoomProposalGuards()
    {
        foreach(bool wrongTime in new[]{true,false}) {
            long now=0;var initial=CombatState.FourPlayers();initial.Content=new RaidContentState(new[]{1,2,3,4},100);
            var sim=initial.Content.Simulation;sim.Boss.StaggerTime=100;sim.RoomSelectionRoll=0;sim.RoomSelectionAliveMask=15;sim.PendingRoomMarkTick=sim.NextRoomMarkTick=30;
            var timing=RaidContentRules.Timing();var engine=new EncounterEngine(88,1,1,timing,RaidContentRules.Create(),initial,()=>now);var host=new SessionBinding(1,1,1,0,false);engine.Bind(host);
            var lua=new LuaPatternPlanner(wrongTime?
                "function on_room_mark_requested(c) emit_pattern(7,c.start_tick+1);emit_action(3,0,0);emit_action(3,0,1) end":
                "function on_room_mark_requested(c) emit_pattern(7,c.start_tick);emit_action(3,0,0);emit_action(3,0,0) end");
            var proposed=lua.RequestRoom(sim);engine.ReceiveProposal(host,new HostProposal(88,1,1,proposed.Tick,100,208,program:proposed.Program));
            var events=new List<ProjectDawn.Combat.CombatEvent>();
            for(int tick=1;tick<=60;tick++){now=tick*timing.TickUs;engine.Pump();events.AddRange(engine.DrainCommittedEvents());}
            Check(events.Any(e=>e.Kind=="ActionRejected")&&events.Any(e=>e.Kind=="ContentRoomPatternMissing")&&!events.Any(e=>e.Kind=="ContentRoomPattern")&&engine.FinalSnapshot.Content.Simulation.MarkTargets.Length==0,
                wrongTime?"server rejects independent Lua mark at wrong tick without inventing a replacement":"server rejects forged duplicate pair in independent Lua callback");
        }
        var missing=new RaidSimulation(4){PendingRoomMarkTick=30};
        Reject(()=>new LuaPatternPlanner("function on_pattern_requested(c) end").RequestRoom(missing),"missing independent Lua callback cannot fall back to server target selection");
    }
    static void MarkPairs()
    {
        var lua=new LuaPatternPlanner();var sim=new RaidSimulation(4);sim.PendingRoomMarkTick=100;
        int variations=0;
        for(int alive=0;alive<16;alive++)for(int previous=0;previous<16;previous++)for(uint roll=0;roll<4;roll++) {
            sim.RoomSelectionAliveMask=alive;sim.RoomSelectionPreviousMarkMask=previous;sim.RoomSelectionRoll=roll;
            var expected=BossPatternProgram.MarkTargets(roll,alive,previous);
            if(alive==0){if(expected.Length!=0)throw new Exception("dead roster marked");variations++;continue;}
            var program=lua.RequestRoom(sim);
            var actual=BossPatternProgram.Parse(program.Program).Where(x=>x.Kind==3).Select(x=>x.Target).ToArray();
            if(!actual.SequenceEqual(expected)||program.Pattern!=7||!BossPatternProgram.ValidRoomMarks(program.Program,roll,alive,previous))throw new Exception("Lua pair mismatch");
            int available=Enumerable.Range(0,4).Count(x=>(alive&(1<<x))!=0);
            if(expected.Length!=Math.Min(2,available)||expected.Distinct().Count()!=expected.Length||expected.Any(x=>(alive&(1<<x))==0))throw new Exception("pair membership");
            int fresh=Enumerable.Range(0,4).Count(x=>(alive&(1<<x))!=0&&(previous&(1<<x))==0);
            if(expected.Count(x=>(previous&(1<<x))==0)!=Math.Min(2,fresh))throw new Exception("previous-round priority");
            variations++;
        }
        Check(variations==1024,"1024 Lua/C# pair draws agree for all living/previous masks and four seed offsets");
        var first=BossPatternProgram.MarkTargets(1,15,0);int mask=first.Aggregate(0,(m,x)=>m|(1<<x));
        var second=BossPatternProgram.MarkTargets(3,15,mask);
        Check(first.Length==2&&second.Length==2&&!first.Intersect(second).Any(),"four living actors alternate disjoint pairs between rounds");
        Check(!BossPatternProgram.ValidRoomMarks("3,0,0;3,0,0",0,15,0),"server rejects duplicate target in one pair");
        Check(!BossPatternProgram.ValidRoomMarks(BossPatternProgram.RoomMarks(0,15,0),0,15,3),"server rejects immediately previous pair when fresh targets exist");
        Check(BossPatternProgram.MarkTargets(0,3,3).SequenceEqual(new[]{0,1})&&BossPatternProgram.MarkTargets(0,1,1).SequenceEqual(new[]{0}),"low-population fallback reuses survivors only when necessary without duplicates");
    }
    static void ActionPrograms()
    {
        var sim=new RaidSimulation(4);sim.Start();sim.PendingPatternTick=60;
        Reject(()=>new LuaPatternPlanner("function on_pattern_requested(c) emit_pattern(6,c.start_tick) end").Request(sim),"missing Lua action program does not use a C# fallback");
        Reject(()=>new LuaPatternPlanner("function on_pattern_requested(c) emit_pattern(6,c.start_tick); for i=1,8 do emit_action(0,0,0) end end").Request(sim),"Lua action count bounded");
        Reject(()=>new LuaPatternPlanner("function on_pattern_requested(c) emit_pattern(6,c.start_tick); emit_action(0,0,9) end").Request(sim),"Lua action target wire range bounded");
        Check(!BossPatternProgram.Valid("",6,0),"server rejects omitted program");
        Check(!BossPatternProgram.Valid("2,36,0;0,0,0",6,0),"server rejects reordered actions");
        Check(!BossPatternProgram.Valid("0,0,1;1,36,1",0,0),"server rejects manipulated approach target");
        Check(!BossPatternProgram.Valid("0,0,0;2,36,0;2,122,1;2,210,2;2,297,3",6,0),"server rejects altered meteor cadence");
        Check(!BossPatternProgram.Valid(BossPatternProgram.Default(6,0,23),6,0) && !BossPatternProgram.Valid(BossPatternProgram.Default(6,0,61),6,0),"server rejects unsafe preparation timing");
        Check(BossPatternProgram.ValidRoomMarks(BossPatternProgram.RoomMarks(7,15,0),7,15,0),"seeded carry-mark program accepts its server target");
        Check(!BossPatternProgram.ValidRoomMarks(BossPatternProgram.RoomMarks(6,15,0),7,15,0),"server rejects manipulated random mark target");
        Check(!BossPatternProgram.ValidRoomMarks("",7,15,0),"server rejects omitted mandatory carry mark");
        Check(!BossPatternProgram.Valid("0,0,0;3,0,0;1,36,0",0,0),"server rejects extra carry mark outside its cadence");
        foreach(int preparation in new[]{24,60}) {
            var active=new RaidSimulation(4){NetworkPatternGate=true};active.Start();
            while(active.PendingPattern<0)active.NetworkTick(Array.Empty<PlayerCommand>(),-1);
            int pattern=active.PendingPattern,ordinal=active.SelectionOrdinal,start=active.PendingPatternTick;
            var program=BossPatternProgram.Default(pattern,ordinal,preparation,active.SelectionRoll);
            while(active.TickIndex<start-1)active.NetworkTick(Array.Empty<PlayerCommand>(),-1);
            active.NetworkTick(Array.Empty<PlayerCommand>(),pattern,program);
            var position=active.Boss.Position;
            for(int i=0;i<10;i++)active.NetworkTick(Array.Empty<PlayerCommand>(),-1);
            Check((active.Boss.Position-position).sqrMagnitude>0.0001f,"Lua approach moves boss before attack / preparation "+preparation);
            var restored=RaidSnapshotCodec.Decode(RaidSnapshotCodec.Encode(active));
            int firstMeteorTick=-1;
            for(int i=0;i<360;i++) {
                active.NetworkTick(Array.Empty<PlayerCommand>(),-1);restored.NetworkTick(Array.Empty<PlayerCommand>(),-1);
                if(firstMeteorTick<0 && active.Zones.Any(z=>!z.FromPlayer && z.Tag=="DawnMeteorImpact"))firstMeteorTick=active.TickIndex;
                if(!RaidSnapshotCodec.Encode(active).SequenceEqual(RaidSnapshotCodec.Encode(restored)))throw new Exception("program replay mismatch");
            }
            Check(firstMeteorTick==start+preparation,"Lua-authored preparation controls actual attack tick / preparation "+preparation);
            Check(true,"active Lua program checkpoint resumes identically / preparation "+preparation);
        }
    }
    static void ServerRejectsLua(bool wrongTick)
    {
        var lua=new LuaPatternPlanner(wrongTick?
            "function on_pattern_requested(c) emit_pattern(6,c.start_tick+1); emit_action(0,0,c.ordinal%4); for w=0,3 do emit_action(2,36+w*87,(c.ordinal+w)%4) end end":
            "function on_pattern_requested(c) emit_pattern(0,c.start_tick); emit_action(0,0,c.ordinal%4); emit_action(1,36,c.ordinal%4) end");
        long now=0;var initial=CombatState.FourPlayers();initial.Content=new RaidContentState(new[]{1,2,3,4},100);
        var policy=RaidContentRules.Timing();var engine=new EncounterEngine(22,1,1,policy,RaidContentRules.Create(),initial,()=>now);
        var host=new SessionBinding(1,1,1,0,false);engine.Bind(host);bool sent=false;
        var events=new List<ProjectDawn.Combat.CombatEvent>();
        for(int t=1;t<180;t++) {
            now=t*policy.TickUs;engine.Pump();var preview=engine.PreviewSnapshot.Content.Simulation;
            if(!sent && preview.PendingPattern>=0) {var p=lua.Request(preview);sent=true;
                engine.ReceiveProposal(host,new HostProposal(22,1,1,p.Tick,100,201+p.Pattern,program:p.Program));}
            events.AddRange(engine.DrainCommittedEvents());
        }
        Check(sent && events.Any(e=>e.Kind=="ActionRejected") && events.Any(e=>e.Kind=="ContentPatternMissing") && !events.Any(e=>e.Kind=="ContentPattern"),
            wrongTick?"server rejects Lua-authored wrong schedule":"server rejects Lua-authored legal but manipulated pattern");
    }
    static RaidSimulation Scenario()
    {
        var sim=new RaidSimulation(4);sim.Start();
        for(int i=0;i<4;i++)sim.Players[i].Position=new ProjectDawn.Combat.RaidVector(i*4-6,0);
        sim.ForcePattern(6);return sim;
    }
    static void Meteor()
    {
        var sim=Scenario();
        Check(sim.Zones.Count==8 && sim.Zones.Count(z=>z.GroundOnly)==4,"four sequential locked meteors and four simultaneous landing shockwaves");
        var impacts=sim.Zones.Where(z=>!z.GroundOnly).ToArray();
        Check(impacts.Select((z,i)=>Math.Abs(z.Age+i*DawnPatternRules.MeteorInterval)<.00001f).All(x=>x),"four waves spaced 1.45 seconds, no simultaneous airborne meteors");
        Check(sim.Zones.Where(z=>z.GroundOnly).All(z=>z.Shape==ZoneShape.Box && z.Center.x==0 && z.Center.y==0 && z.HalfSize.x==9 && z.HalfSize.y==3),"each jumpable shockwave covers entire playable arena");
        var shockwaves=sim.Zones.Where(z=>z.GroundOnly).ToArray();
        Check(impacts.Select((z,i)=>z.Age==shockwaves[i].Age && z.Telegraph==shockwaves[i].Telegraph).All(x=>x),"whole-arena warning during fall, impact and shockwave share exact activation tick");
        var volley=Scenario();volley.PracticeInvulnerable=true;volley.Boss.Hp=volley.Boss.MaxHp*.4f;volley.Boss.Stagger=100;
        var ticks=new List<int>();var seen=new HashSet<float>();
        for(int t=0;t<450;t++) {
            volley.Tick(RaidSimulation.FixedStep,Array.Empty<PlayerCommand>());
            foreach(var e in volley.Events.Where(e=>e.Kind=="MeteorImpact"))if(seen.Add(e.Value))ticks.Add(volley.TickIndex);
            if(ticks.Count<4 && (volley.Boss.Phase!=1 || volley.Boss.StaggerTime>0))throw new Exception("volley interrupted");
        }
        Check(ticks.Count==4 && ticks.Zip(ticks.Skip(1),(a,b)=>b-a).All(d=>Math.Abs(d-87)<=1),"four impact events retain cadence through pending phase/stagger");
        Check(volley.Boss.Phase==2,"pending phase resumes after volley finishes");
        var zone=sim.Zones.First(z=>!z.GroundOnly);
        Check(Math.Abs(zone.Telegraph-6.5f/(2.6777f*2))<.00001f && zone.Damage==25 && Math.Abs(zone.HalfSize.x-3.3f)<.00001f && Math.Abs(zone.HalfSize.y-1.2f)<.00001f,"requested 2x speed / 3x footprint with original damage");
        Check(!RaidSimulation.Intersects(zone,zone.Center+new ProjectDawn.Combat.RaidVector(0,1.21f),RaidSimulation.PlayerHurtboxHalfSize),"ground-anchor depth limit preserved after resizing");
        int impact=(int)Math.Ceiling(zone.Telegraph*60);
        for(int t=0;t<impact+8;t++)sim.Tick(RaidSimulation.FixedStep,new[]{new PlayerCommand {PlayerId=0,Jump=t==impact-16}});
        Check(sim.Players[0].Hp==75,"jump cannot avoid direct meteor impact; one hit only");
        var evade=Scenario();var ground=Scenario();
        foreach(var s in new[]{evade,ground})foreach(var p in s.Players)p.Position+=new ProjectDawn.Combat.RaidVector(0,1.3f);
        int shock=(int)Math.Ceiling(zone.Telegraph*60);
        for(int t=0;t<shock+18;t++)
        {
            evade.Tick(RaidSimulation.FixedStep,new[]{new PlayerCommand {PlayerId=0,Jump=t==shock-16}});
            ground.Tick(RaidSimulation.FixedStep,Array.Empty<PlayerCommand>());
        }
        Check(evade.Players[0].Hp==100 && ground.Players[0].Hp==84,"move avoids impact; timed jump avoids landing shockwave");
    }
}
