using System;
using System.Collections.Generic;
using System.Linq;
using DnfRaidDemo;
using ProjectDawn.Combat;
using ProjectDawn.Network;
internal static class Program
{
    static int checks;
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;Console.WriteLine("PASS: "+name);}
    static RaidSimulation Scenario(){var s=new RaidSimulation(4){NetworkPatternGate=true};s.Start();s.ForcePattern(6);foreach(var p in s.Players)p.Position+=new RaidVector(0,1.3f);return s;}
    static int Main()
    {
        try {
            foreach(int delay in new[]{16,18,22,30,34,38})foreach(int stride in new[]{1,2}) Alignment(delay,stride);
            var s=Scenario();var frozen=RaidSnapshotCodec.Encode(s);var t=new RaidPresentationTimeline(0);
            var display=t.Project(s,19);Check(s.TickIndex==0 && RaidSnapshotCodec.Encode(s).SequenceEqual(frozen),"projection cannot mutate server snapshot");
            Check(display.Players[0].Hp==s.Players[0].Hp && display.Boss.Hp==s.Boss.Hp,"HP stays finalized");
            t.Track(1,5,new PlayerCommand{PlayerId=0,Jump=true});display=t.Project(s,8);
            Check(display.Players[0].JumpHeight>0,"pending local jump uses input timeline");
            t.Acknowledge(1,10,true);display=t.Project(s,8);Check(display.Players[0].JumpHeight==0,"ACK corrects jump to server input tick");
            display=t.Project(s,13);Check(display.Players[0].JumpHeight>0,"ACK jump replays when corrected tick arrives");
            t.Acknowledge(1,9,true);display=t.Project(s,8);Check(display.Players[0].JumpHeight==0,"jump pose never advances beyond the displayed hazard tick");
            t.Reject(1);display=t.Project(s,13);Check(display.Players[0].JumpHeight==0,"rejected jump does not survive replay");
            display=t.Project(s,1000);Check(t.Clamped && display.TickIndex==60,"stale snapshot projection is bounded to one second");
            var empty=Scenario();empty.Zones.Clear();display=t.Project(empty,19);Check(display.Zones.All(z=>z.FromPlayer),"cancelled zones disappear on replacement snapshot");
            var beforeHit=Scenario();for(int i=0;i<55;i++)beforeHit.NetworkTick(Array.Empty<PlayerCommand>(),-1);
            var projectedHit=new RaidPresentationTimeline(0).Project(beforeHit,80);
            Check(projectedHit.Players[0].Hp==100 && beforeHit.Players[0].Hp==100,"projected hazard cannot apply unconfirmed HP damage");
            foreach(int delay in new[]{19,30,34,38})VisibleJump(delay);
            EveryPattern();DelayedImpactImmunity();RemoteActionClock();
            Console.WriteLine("[DAWN_TIMELINE_VALIDATED] "+checks+" checks");return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    static void Alignment(int delay,int stride)
    {
        var sim=Scenario();sim.PracticeInvulnerable=true;
        var states=new List<RaidSimulation>{sim.Clone()};var actual=new Dictionary<int,int>();
        var timeline=new RaidPresentationTimeline(0);var seen=new Dictionary<int,(long Tick,long Seen)>();int sounds=0;
        timeline.Activated+=(z,t,o,p)=>{if(z.Tag=="DawnMeteorImpact")seen.Add(z.Id,(t,o));};
        timeline.Feedback+=e=>{if(e.Kind=="MeteorImpact")sounds++;};
        for(int tick=1;tick<=450;tick++) {
            sim.NetworkTick(Array.Empty<PlayerCommand>(),-1);states.Add(sim.Clone());
            foreach(var e in sim.Events.Where(e=>e.Kind=="MeteorImpact" && e.Age==0))actual[(int)e.Value]=tick;
            if(tick<delay || tick%stride!=0)continue;
            var view=timeline.Project(states[tick-delay],tick);
            // A second draw in the same tick must not replay landing sounds.
            timeline.Project(states[tick-delay],tick);
            foreach(var zone in view.Zones.Where(z=>!z.FromPlayer)) {
                var truth=sim.Zones.FirstOrDefault(z=>z.Id==zone.Id);
                if(truth==null || Math.Abs(truth.Age-zone.Age)>0.00001f)throw new Exception("zone clock mismatch");
            }
        }
        Check(actual.Count==4 && seen.Count==4 && sounds==4 && actual.All(p=>seen[p.Key].Tick==p.Value && seen[p.Key].Seen-p.Value<stride),
            "meteor display equals server activation ticks, no replay; delay="+delay+" ticks fps="+(60/stride));
    }
    static void EveryPattern()
    {
        for(int pattern=0;pattern<=6;pattern++) {
            var sim=Scenario();sim.PracticeInvulnerable=true;sim.ForcePattern(pattern);
            var history=new List<RaidSimulation>{sim.Clone()};var timeline=new RaidPresentationTimeline(0);
            for(int tick=1;tick<=180;tick++) {
                sim.NetworkTick(Array.Empty<PlayerCommand>(),-1);history.Add(sim.Clone());if(tick<19)continue;
                var view=timeline.Project(history[tick-19],tick);
                foreach(var z in view.Zones.Where(z=>!z.FromPlayer)) {
                    var truth=sim.Zones.Single(x=>x.Id==z.Id);
                    if(Math.Abs(z.Age-truth.Age)>0.00001f || (z.Center-truth.Center).sqrMagnitude>0.00001f)throw new Exception("pattern schedule mismatch "+pattern);
                }
            }
            Check(true,"boss pattern "+pattern+" warning/active/recovery ages match current server state");
        }
    }
    static void VisibleJump(int delay)
    {
        var sim=Scenario();var frames=new List<RaidSimulation>{sim.Clone()};var t=new RaidPresentationTimeline(0);long sequence=0;
        PlayerCommand next=null;bool visibleSafe=false,serverSafe=false;
        t.Activated+=(z,tick,shown,p)=>{if(z.Tag=="DawnMeteorShockwave" && tick<100)visibleSafe=p.JumpHeight>=RaidSimulation.JumpDodgeHeight;};
        for(int tick=1;tick<=100;tick++) {
            sim.NetworkTick(next==null?Array.Empty<PlayerCommand>():new[]{next},-1);next=null;frames.Add(sim.Clone());
            serverSafe|=sim.Events.Any(e=>e.Kind=="MeteorJumpAvoid" && e.TargetId==0);
            if(tick<delay)continue;
            var view=t.Project(frames[tick-delay],tick);
            if(view.Players[0].JumpHeight<=0 && view.Zones.Any(z=>z.GroundOnly && z.Age>=0 && z.Telegraph-z.Age>0.15f && z.Telegraph-z.Age<.26f)) {
                next=new PlayerCommand{PlayerId=0,Jump=true};t.Track(++sequence,tick+1,next);
            }
        }
        Check(visibleSafe && serverSafe && sim.Players[0].Hp==100,"jump from displayed warning agrees with server ground-shock decision; snapshot age="+delay);
    }
    static void DelayedImpactImmunity()
    {
        var sim=Scenario();for(int i=0;i<60;i++)sim.NetworkTick(Array.Empty<PlayerCommand>(),-1);
        sim.Players[0].Invulnerable=13.5f*RaidSimulation.FixedStep;sim.Players[0].JumpHeight=1;
        var timeline=new RaidPresentationTimeline(0);float immunity=-1;long activation=0,observed=0;
        timeline.Activated+=(z,t,o,p)=>{if(z.Tag=="DawnMeteorShockwave"){immunity=p.Invulnerable;activation=t;observed=o;}};
        var display=timeline.Project(sim,74);
        Check(activation==73 && observed==74 && immunity>0 && display.Players[0].Invulnerable==0,
            "late rendered impact audits immunity at activation, not after immunity expires: "+activation+"/"+observed+"/"+immunity+"/"+display.Players[0].Invulnerable);
    }
    static void RemoteActionClock()
    {
        foreach(int delay in new[]{0,16,18,22,60}) {
            var sim=Scenario();sim.Zones.Clear();sim.Boss.StaggerTime=10;
            sim.Players[1].Position=new RaidVector(-3,1);
            sim.NetworkTick(new[]{new PlayerCommand{PlayerId=1,Skills=PlayerCommand.Bit(SkillId.Destruction)}},-1);
            for(int i=0;i<30;i++)sim.NetworkTick(Array.Empty<PlayerCommand>(),-1);
            var frozen=RaidSnapshotCodec.Encode(sim);var remote=sim.Players[1].Clone();
            var timeline=new RaidPresentationTimeline(0);var displayed=timeline.Project(sim,sim.TickIndex+delay);
            var truth=sim.Clone();for(int i=0;i<delay;i++)truth.NetworkTick(Array.Empty<PlayerCommand>(),-1);
            Check(Math.Abs(displayed.Players[1].ActionElapsed-truth.Players[1].ActionElapsed)<.00001f,
                "remote awakening matches current authoritative cast age at "+delay+" tick delay");
            Check(displayed.Players[1].Hp==remote.Hp && displayed.Players[1].Mp==remote.Mp && displayed.Players[1].Position==remote.Position
                && displayed.Players[1].Cooldowns.SequenceEqual(remote.Cooldowns) && RaidSnapshotCodec.Encode(sim).SequenceEqual(frozen),
                "remote presentation never changes authoritative state at "+delay+" ticks");
            var again=timeline.Project(sim,sim.TickIndex+delay);
            Check(again.Players[1].ActionElapsed==displayed.Players[1].ActionElapsed,"repeated render cannot advance remote clock twice");
            Check(Math.Abs(displayed.Players[1].ActionElapsed-displayed.Players[1].ActionPresentationLead-remote.ActionElapsed)<.00001f,
                "confirmed lunge origin remains recoverable from source action age");
        }
        var end=Scenario();end.Zones.Clear();var actor=end.Players[1];actor.ActiveSkill=SkillId.Destruction;
        actor.ActionDuration=6.7f;actor.ActionElapsed=6.65f;actor.ActionSequence=10;
        var t=new RaidPresentationTimeline(0);var view=t.Project(end,20);
        Check(view.Players[1].ActionElapsed==6.7f,"completed remote action clamps at its end");
        actor.ActionElapsed=actor.ActionDuration=0;
        view=t.Project(end,20);Check(view.Players[1].ActionDuration==0 && view.Players[1].ActionPresentationLead==0,"confirmed cancellation is not resurrected");
        actor.ActionDuration=6.7f;actor.ActionElapsed=1;actor.Hp=0;
        view=t.Project(end,20);Check(view.Players[1].ActionElapsed==1,"dead actor does not advance an old action");
        actor.Hp=100;actor.ActionSequence=11;actor.ActiveSkill=SkillId.BloodSword;actor.ActionDuration=1.2f;actor.ActionElapsed=.1f;
        view=t.Project(end,18);Check(view.Players[1].ActionSequence==11 && view.Players[1].ActiveSkill==SkillId.BloodSword
            && Math.Abs(view.Players[1].ActionElapsed-.4f)<.00001f,"replacement cast uses its own confirmed clock");
        view=t.Project(end,1000);Check(t.Clamped && Math.Abs(view.Players[1].ActionPresentationLead-1)<.00001f,"remote extrapolation obeys stale snapshot cap");
        Check(RaidSnapshotCodec.Decode(RaidSnapshotCodec.Encode(view)).Players[1].ActionPresentationLead==0,"cosmetic clock offset is absent from network snapshots");
        var zones=Scenario();zones.Zones.Clear();zones.Zones.Add(new AttackZone{Id=990,FromPlayer=true,OwnerId=1,Age=.2f,Telegraph=.3f,ActiveTime=.1f,Recovery=.2f});
        var zview=new RaidPresentationTimeline(0).Project(zones,12);
        Check(Math.Abs(zview.Zones.Single(z=>z.Id==990).Age-.4f)<.00001f && zones.Zones[0].Age==.2f,"remote cosmetic skill area advances without changing source damage schedule");
        zview=new RaidPresentationTimeline(0).Project(zones,30);
        Check(zview.Zones.All(z=>z.Id!=990),"expired remote skill area is not replayed late");
        actor.ActionElapsed=.1f;actor.ActionRate=1.5f;
        view=t.Project(end,18);Check(Math.Abs(view.Players[1].ActionElapsed-.55f)<.00001f,"remote cast presentation uses server-latched equipment attack speed");
        zones.Zones[0].PlaybackRate=1.5f;
        zview=new RaidPresentationTimeline(0).Project(zones,12);
        Check(Math.Abs(zview.Zones.Single(z=>z.Id==990).Age-.5f)<.00001f,"remote skill effects share the accelerated hit clock");
    }

}
