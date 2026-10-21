using System;
using System.Linq;
using ProjectDawn.Combat;
internal static class Program
{
    static int count;
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS: "+name);count++;}
    static ClockMapping Samples(ClockSynchronizer sync,long start=0,long offset=-1000000,long oneWay=20000)
    {
        ClockMapping result=null;
        for(int i=0;i<3;i++) {long s=start+i*Math.Max(100000,oneWay*2+30000);var token=sync.Challenge(s);result=sync.Respond(token,s+oneWay-offset,s+oneWay-offset,s+2*oneWay);}
        return result;
    }
    static int Main()
    {
        try {
            var sync=new ClockSynchronizer(120000);var map=Samples(sync);
            Check(map.Trusted && map.OffsetUs==-1000000 && map.Version==2 && map.MaxBackdateUs==100000,"three measured samples issue bounded server mapping");
            Check(sync.Respond("replayed",0,0,300000)==null,"unknown or replayed challenge cannot install clock");
            var next=Samples(sync,5300000,-1000000);Check(next.Version==3 && next.OffsetUs==map.OffsetUs,"refresh freezes original offset and advances version");
            var drift=Samples(sync,10600000,-1500000);Check(!drift.Trusted,"clock jump removes retrospective authority");
            var slow=new ClockSynchronizer(120000);string token=slow.Challenge(0);
            Check(!slow.Respond(token,20000,20000,ClockSynchronizer.MaxRoundTripUs+1).Trusted,"excessive RTT falls back to receive time");
            HighLatencyClock();NoisyRefresh();
            var forged=new ClockSynchronizer(120000);token=forged.Challenge(0);
            Check(!forged.Respond(token,long.MaxValue,long.MaxValue,20000).Trusted,"extreme timestamp rejected without arithmetic overflow");
            foreach(var result in new[]{Admission.Admitted,Admission.TooLate,Admission.FutureTimestamp,Admission.ClockMismatch}) AdmissionCase(result);
            Expiration();ImmutableIngress();LateJump();LateMeteor();ThreeHopMeteor();
            Console.WriteLine("[DAWN_CLOCK_VALIDATED] "+count+" checks");return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    static TimingPolicy Policy()=>new TimingPolicy(10000,60000,20000,90000,1000);
    static void NoisyRefresh()
    {
        var sync=new ClockSynchronizer(160000);var original=Samples(sync,oneWay:100000);
        ClockMapping result=null;
        foreach(long start in new[]{6000000L,17000000L}) {
            for(int i=0;i<3;i++) {
                long sent=start+i*300000,offset=-1000000+(i-1)*15000;
                string token=sync.Challenge(sent);
                result=sync.Respond(token,sent+100000-offset,sent+100000-offset,sent+200000);
            }
            if(start==6000000)Check(result==null && ReferenceEquals(sync.Current,original),
                "jittered refresh retains only existing lease without changing expiry or backdate authority");
            else Check(result!=null && !result.Trusted,"noisy evidence cannot renew an expired clock lease");
        }
    }
    static void HighLatencyClock()
    {
        var sync=new ClockSynchronizer(RaidContentRules.Timing().InputGraceUs);
        ClockMapping map=null;
        // Deterministic asymmetric jitter and dispatch overhead, all above the old 160ms cap.
        long[] downstream={85000,100000,90000},upstream={95000,90000,105000};
        for(int i=0;i<3;i++) {
            long sent=i*300000;string token=sync.Challenge(sent);
            Check(sync.Challenge(sent+170000)==null,"outstanding high-latency challenge is not replaced "+i);
            map=sync.Respond(token,sent+downstream[i]+1000000,sent+downstream[i]+1000000,sent+downstream[i]+upstream[i]);
        }
        Check(map.Trusted && sync.RoundTripUs==180000 && map.MaxBackdateUs==160000,
            "180-195ms jittered RTT installs a measured, bounded clock");
        Check(map.MaxBackdateUs<=RaidContentRules.Timing().InputGraceUs,"high-latency mapping cannot exceed input deadline");
        var cap=Samples(new ClockSynchronizer(160000),oneWay:140000);
        Check(cap.Trusted && cap.MaxBackdateUs==160000,"RTT ceiling still clamps retrospective authority to input budget");
    }
    static void ThreeHopMeteor()
    {
        long now=0;var timing=RaidContentRules.Timing();
        var initial=CombatState.FourPlayers();initial.Content=new RaidContentState(new[]{1,2,3,4},100);
        initial.Content.Simulation.ForcePattern(6);
        foreach(var p in initial.Content.Simulation.Players)p.Position+=new RaidVector(0,1.3f);
        var map=Samples(new ClockSynchronizer(timing.InputGraceUs),oneWay:100000);
        var host=new SessionBinding(1,1,1,0,false);host.InstallClock(map);
        var guest=new SessionBinding(2,1,1,0,false);guest.InstallClock(map);
        var engine=new EncounterEngine(89,1,1,timing,RaidContentRules.Create(),initial,()=>now);
        engine.Bind(host);engine.Bind(guest);
        for(int tick=1;tick<=73;tick++){now=tick*timing.TickUs;engine.Pump();}
        var key=new InputKey(89,2,1,1);long pressed=68*timing.TickUs;
        now=pressed+100000;
        engine.ReceiveInput(guest,new PlayerInput(key,pressed-map.OffsetUs,map.Version,InputKind.Controls,0,0,0,4));engine.Pump();
        Check(engine.DrainInputReceipts().Single().Result==Admission.Admitted,"guest jump admitted after 100ms input hop");
        now=pressed+410000;engine.Pump();
        Check(engine.PreviewSnapshot.Content.Simulation.Players[1].Hp==84,"unapproved guest jump cannot independently change server damage");
        engine.ReceiveProposal(host,new HostProposal(89,1,1,69,2,100,key));engine.Pump();
        var receipt=engine.DrainProposalReceipts().Single();
        Check(receipt.Result==Admission.Admitted && receipt.ReceivedUs==now && engine.PreviewSnapshot.Content.Simulation.Players[1].Hp==100,
            "three-hop 410ms host proposal replays provisional meteor damage and preserves ingress time");
        now=timing.ProposalDeadline(69);
        engine.ReceiveProposal(host,new HostProposal(89,1,2,69,2,100,key));engine.Pump();
        Check(engine.DrainProposalReceipts().Single().Result==Admission.TooLate,
            "host proposal at exact high-latency deadline is rejected before finality");
        now=timing.FinalDeadline(74)+1;engine.Pump();
        Check(engine.FinalSnapshot.Content.Simulation.Players[1].Hp==100 && engine.DrainCommittedEvents().Any(e=>e.Kind=="ContentMeteorJumpAvoid"&&e.Target==2),
            "three-hop avoidance commits through server C# only");
        engine.ReceiveProposal(host,new HostProposal(89,1,3,69,2,100,key));engine.Pump();
        Check(engine.DrainProposalReceipts().Single().Result==Admission.TooLate,"high-latency policy never reopens finalized history");
    }
    static EncounterEngine Engine(Func<long> now,SessionBinding binding)
    {
        var rules=new CombatRules(new[]{new AttackDefinition(2,true,true,20,2000,delayTicks:1,stunTicks:3,knockback:700)});
        var e=new EncounterEngine(77,1,1,Policy(),rules,CombatState.FourPlayers(0),now);e.Bind(binding);return e;
    }
    static SessionBinding Binding(long expiry=1000000)
    {var b=new SessionBinding(1,1,1,0,false);b.InstallClock(new ClockMapping(2,-1000000,true,expiry,55000,1000));return b;}
    static PlayerInput Input(long time,long version=2,long sequence=1)=>new PlayerInput(new InputKey(77,1,1,sequence),time,version,InputKind.Jump);
    static void AdmissionCase(Admission expected)
    {
        long now=65000;var b=Binding();var e=Engine(()=>now,b);
        long time=expected==Admission.TooLate?1000000:expected==Admission.FutureTimestamp?1200000:1020000;
        e.ReceiveInput(b,Input(time,expected==Admission.ClockMismatch?999:2));e.Pump();
        var r=e.DrainInputReceipts().Single();Check(r.Result==expected,"server timestamp admission: "+expected);
    }
    static void Expiration()
    {
        long now=65000;var b=Binding(65000);var e=Engine(()=>now,b);e.ReceiveInput(b,Input(1000000));e.Pump();
        var r=e.DrainInputReceipts().Single();Check(r.Result==Admission.Admitted && r.UsedReceiveTime && r.EffectiveTick==7,"lease expiry at exact boundary uses receive time");
    }
    static void ImmutableIngress()
    {
        long now=65000;var b=Binding(70000);var e=Engine(()=>now,b);var input=Input(1020000);e.ReceiveInput(b,input);
        for(int v=3;v<9;v++)b.InstallClock(new ClockMapping(v,0,false,1000000,0,0));
        now=110000;e.Pump();var r=e.DrainInputReceipts().Single();
        Check(r.Result==Admission.Admitted && !r.UsedReceiveTime && r.EffectiveTick==3,"queued input retains frozen mapping despite lease expiry and version eviction");
        e.ReceiveInput(b,input);e.Pump();Check(e.DrainInputReceipts().Single().Result==Admission.Duplicate,"retransmit retains original decision after clock eviction");
    }
    static void LateMeteor()
    {
        long now=0;var timing=RaidContentRules.Timing();
        var initial=CombatState.FourPlayers();initial.Content=new RaidContentState(new[]{1,2,3,4},100);
        var sim=initial.Content.Simulation;sim.ForcePattern(6);
        foreach(var p in sim.Players)p.Position+=new RaidVector(0,1.3f);
        var synchronizer=new ClockSynchronizer(timing.InputGraceUs);var mapping=Samples(synchronizer,0,-1000000,80000);
        var binding=new SessionBinding(1,1,1,0,false);binding.InstallClock(mapping);
        var engine=new EncounterEngine(88,1,1,timing,RaidContentRules.Create(),initial,()=>now);engine.Bind(binding);
        for(int tick=1;tick<=74;tick++){now=tick*timing.TickUs;engine.Pump();}
        Check(engine.PreviewSnapshot.Content.Simulation.Players[0].Hp==84,"real meteor arena shockwave is provisional before delayed input");
        var key=new InputKey(88,1,1,1);
        engine.ReceiveInput(binding,new PlayerInput(key,68*timing.TickUs-mapping.OffsetUs,mapping.Version,InputKind.Controls,0,0,0,4));
        engine.ReceiveProposal(binding,new HostProposal(88,1,1,69,1,100,key));engine.Pump();
        Check(engine.DrainInputReceipts().Single().Result==Admission.Admitted && engine.PreviewSnapshot.Content.Simulation.Players[0].Hp==100,
            "100ms delayed real-content jump plus host proposal removes meteor shock damage");
        for(int tick=75;tick<=timing.TickAt(timing.FinalDeadline(74));tick++){now=tick*timing.TickUs;engine.Pump();}
        var events=engine.DrainCommittedEvents();
        Check(!engine.IsAborted && engine.FinalSnapshot.Content.Simulation.Players[0].Hp==100 &&
            events.Any(e=>e.Kind=="ContentMeteorJumpAvoid" && e.Target==1),"corrected meteor avoidance reaches committed content events");
    }
    static void LateJump()
    {
        long now=0;var b=Binding();var e=Engine(()=>now,b);
        e.ReceiveProposal(b,new HostProposal(77,1,1,3,100,2));
        now=40000;e.Pump();Check(e.PreviewSnapshot.Fighters[1].Hp==80,"boss hit is provisional before delayed jump");
        now=65000;e.ReceiveInput(b,Input(1020000));e.Pump();
        Check(e.PreviewSnapshot.Fighters[1].Hp==100 && e.PreviewSnapshot.Fighters[1].X==0,"45ms late mapped jump removes provisional hit and knockback");
        now=140000;e.Pump();Check(e.FinalSnapshot.Fighters[1].Hp==100 && e.DrainCommittedEvents().Count(x=>x.Kind=="Evaded" && x.Target==1)==1,"corrected jump finalizes exactly once");
        e.ReceiveInput(b,Input(1020000,2,2));e.Pump();Check(e.DrainInputReceipts().Last().Result==Admission.TooLate && e.FinalSnapshot.Fighters[1].Hp==100,"finalized history remains immutable");
    }
}
