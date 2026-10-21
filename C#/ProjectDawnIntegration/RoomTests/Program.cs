using System;
using System.Linq;
using DnfRaidDemo;
using ProjectDawn.Combat;
internal static class Program
{
    static int checks;
    static readonly PlayerCommand[] None=Array.Empty<PlayerCommand>();
    static void Check(bool value,string label){if(!value)throw new Exception(label);checks++;Console.WriteLine("PASS: "+label);}
    static RaidSimulation New()
    {
        var sim=new RaidSimulation(4){RoomsEnabled=true,NetworkPatternGate=true};sim.Start();sim.Boss.StaggerTime=100;
        return sim;
    }
    static void Tick(RaidSimulation s,params PlayerCommand[] commands)=>s.NetworkTick(commands,-1);
    static void Until(RaidSimulation s,int tick){while(s.TickIndex<tick)Tick(s);}
    static void Move(RaidSimulation s,int id,float x,float y)=>Tick(s,new PlayerCommand{PlayerId=id,Move=new RaidVector(x,y)});
    static void Door(RaidSimulation s,int id,int dx,int dy)
    {
        var p=s.Players[id];p.Position=dx!=0?new RaidVector(dx*8.7f,0):new RaidVector(0,-dy*2.8f);
        p.RoomChangedTick=-1000;Move(s,id,dx,-dy);
    }
    static RaidSimulation Marked(int candidate=0,bool dead=false)
    {
        var s=New();s.Boss.StaggerTime=0;
        if(dead)s.Players[candidate].Hp=0;
        ApproveMark(s,(uint)candidate,s.Players.Where(x=>x.Hp>0).Aggregate(0,(m,x)=>m|(1<<x.Id)));
        return s;
    }
    static void ApproveMark(RaidSimulation s,uint roll=0,int alive=15,int previous=-1)
    {
        s.RoomSelectionRoll=roll;s.RoomSelectionAliveMask=alive;s.RoomSelectionPreviousMarkMask=previous<0?s.PreviousMarkMask:previous;
        s.PendingRoomMarkTick=s.TickIndex+1;s.NextRoomMarkTick=s.PendingRoomMarkTick;
        s.NetworkTick(None,-1,roomProgram:BossPatternProgram.RoomMarks(roll,alive,s.RoomSelectionPreviousMarkMask));
        s.Boss.StaggerTime=100;
    }
    static void SchedulerTick(RaidSimulation s)
    {
        string program=s.PendingRoomMarkTick==s.TickIndex+1?BossPatternProgram.RoomMarks(s.RoomSelectionRoll,s.RoomSelectionAliveMask,s.RoomSelectionPreviousMarkMask):"";
        s.NetworkTick(None,-1,roomProgram:program);
    }
    static void Topology()
    {
        int edges=0;
        for(int room=0;room<9;room++)foreach(var d in new[]{(-1,0),(1,0),(0,-1),(0,1)}){
            int expectedX=room%3+d.Item1,expectedY=room/3+d.Item2;
            int expected=expectedX<0||expectedX>2||expectedY<0||expectedY>2?-1:expectedY*3+expectedX;
            int next=RaidSimulation.NeighborRoom(room,d.Item1,d.Item2);
            if(next!=expected)throw new Exception("3x3 wrong edge "+room);
            if(next>=0){edges++;if(RaidSimulation.NeighborRoom(next,-d.Item1,-d.Item2)!=room)throw new Exception("nonreciprocal edge");}
        }
        Check(edges==24,"all 9 rooms expose exactly 24 reciprocal directed edges without row wrap");
        Check(RaidSimulation.NeighborRoom(-1,1,0)==-1&&RaidSimulation.NeighborRoom(9,-1,0)==-1&&RaidSimulation.NeighborRoom(4,1,1)==-1&&RaidSimulation.NeighborRoom(4,0,0)==-1,"invalid room, diagonal and zero direction rejected");
    }
    static void Travel()
    {
        foreach(var d in new[]{(-1,0),(1,0),(0,-1),(0,1)}){
            var s=New();var p=s.Players[0];p.Position=RaidVector.zero;
            for(int t=0;t<600&&p.RoomId==4;t++)Move(s,0,d.Item1,-d.Item2);
            Check(p.RoomId==RaidSimulation.NeighborRoom(4,d.Item1,d.Item2),"normal movement reaches cardinal doorway "+d);
            Check(p.Hp==100&&p.Invulnerable==0&&p.RoomChangedTick==s.TickIndex,"travel preserves HP and grants no immunity "+d);
            int room=p.RoomId;Door(s,0,-d.Item1,-d.Item2);Check(p.RoomId==4,"opposite doorway returns from room "+room);
        }
        foreach(string state in new[]{"center","off-door","dead","attack","stun","jump","dash","cooldown"}){
            var s=New();var p=s.Players[0];p.Position=new RaidVector(8.7f,0);
            switch(state){case "center":p.Position=RaidVector.zero;break;case "off-door":p.Position=new RaidVector(8.7f,1.5f);break;
                case "dead":p.Hp=0;break;case "attack":p.AttackTime=2;break;case "stun":p.StunTime=2;break;
                case "jump":p.JumpHeight=1;p.JumpVelocity=1;break;case "dash":p.DashTime=1;break;case "cooldown":p.RoomChangedTick=s.TickIndex;break;}
            Move(s,0,1,0);Check(p.RoomId==4,"door rejects "+state);
        }
        var corner=New();corner.Players[0].RoomId=2;Door(corner,0,1,0);Check(corner.Players[0].RoomId==2,"right edge of upper-right room cannot wrap");
    }
    static void MarkAndBurn()
    {
        var s=Marked();Check(s.MarkTarget==0&&s.MarkDeadline==s.TickIndex+RaidSimulation.MarkTicks,"approved Lua kind3 starts a seven-second seeded mark");
        int deadline=s.MarkDeadline;Door(s,0,1,0);Check(s.Players[0].RoomId==5&&s.MarkTarget==0&&s.MarkDeadline==deadline,"mark follows legitimate room travel without resetting deadline");
        Until(s,deadline-1);Check(s.RoomFires.Count==0,"no impact or fire before mark deadline");Tick(s);
        var fire=s.RoomFires.Single(f=>f.RoomId==5);var impact=s.Zones.Single(z=>z.Tag=="RoomMeteorImpact"&&z.RoomId==5);
        Check(s.MarkTarget==-1&&fire.RoomId==5&&impact.RoomId==5&&impact.Center.x==s.Players[0].Position.x,"deadline locks meteor to marked player current room and position");
        Check(fire.EndTick-fire.StartTick==960,"room fire has a sixteen-second authoritative lifetime");
        Door(s,0,-1,0);Check(s.Players[0].RoomId==4&&fire.RoomId==5&&impact.RoomId==5,"leaving after lock cannot drag impact or fire to another room");
        var victim=s.Players[1];victim.RoomId=5;victim.Position=new RaidVector(7,2);var safe=s.Players[2];safe.RoomId=8;safe.Position=victim.Position;
        Until(s,fire.StartTick+59);Check(victim.Hp==100,"burn waits one second after landing");Tick(s);Check(victim.Hp==80&&safe.Hp==100,"first burn pulse damages same-room actor only");
        Until(s,fire.StartTick+119);Check(victim.Hp==80,"persistent field cannot repeatedly hit within its one-second cadence");Tick(s);Check(victim.Hp==60,"next distinct burn pulse applies periodic damage");
        victim.RoomId=8;float hp=victim.Hp;Until(s,fire.StartTick+180);Check(victim.Hp==hp,"leaving a burning room stops room-local damage");
        Until(s,fire.StartTick+190);victim.RoomId=5;Until(s,fire.StartTick+240);Check(victim.Hp==hp-20,"reentering resumes the existing field instead of restarting its timer");
        victim.RoomId=8;Until(s,fire.EndTick);Check(s.RoomFires.Count==0,"empty room fire expires on server clock");
        victim.RoomId=5;hp=victim.Hp;Until(s,fire.EndTick+120);Check(victim.Hp==hp&&!s.Zones.Any(z=>z.Tag=="RoomBurn"),"entering expired room is safe and does not revive hazards");
        var timing=Marked();Until(timing,timing.MarkDeadline);var timedFire=timing.RoomFires.First();
        int firstDamage=-1;while(timing.TickIndex<timedFire.StartTick+2){Tick(timing);if(firstDamage<0&&timing.Players[0].Hp<100)firstDamage=timing.TickIndex;}
        Check(firstDamage==timedFire.StartTick,"room meteor damage and fire visual start share the same authoritative landing tick");
        var dead=Marked(1,true);Check(dead.MarkTarget==2,"seed target dead at selection uses deterministic next living slot");
        var dying=Marked(3);dying.Players[3].RoomId=8;dying.Players[3].Hp=0;Until(dying,dying.MarkDeadline);Check(dying.RoomFires.Count==2&&dying.RoomFires.Any(f=>f.RoomId==8),"death after mark retains its last room and cannot erase the drop");
    }
    static void PairRounds()
    {
        var sim=Marked();sim.PracticeInvulnerable=true;
        Check(sim.MarkTargets.SequenceEqual(new[]{0,1})&&sim.PreviousMarkMask==3,"first mark round assigns two distinct players and remembers the actual pair");
        int deadline=sim.MarkDeadline;Door(sim,0,-1,0);Door(sim,1,1,0);Until(sim,deadline);
        Check(sim.RoomFires.Select(f=>f.RoomId).OrderBy(x=>x).SequenceEqual(new[]{3,5})&&sim.RoomFires.Select(f=>f.StartTick).Distinct().Count()==1,"two simultaneous marked players drop independent meteors in separate rooms at the same tick");
        ApproveMark(sim);
        Check(sim.MarkTargets.SequenceEqual(new[]{2,3})&&sim.PreviousMarkMask==12,"next four-player mark round excludes both previous targets");
        Until(sim,sim.MarkDeadline);ApproveMark(sim,1);
        Check(sim.MarkTargets.OrderBy(x=>x).SequenceEqual(new[]{0,1}),"third round returns to the other pair without within-round duplication");
        var died=New();died.Players[1].Hp=0;ApproveMark(died,1,15);
        Check(died.MarkTargets.Length==2&&died.MarkTargets.Distinct().Count()==2&&died.MarkTargets.All(x=>died.Players[x].Hp>0),"death between selection and execution replaces targets without duplicating the second candidate");
        var one=New();one.PreviousMarkMask=1;foreach(var p in one.Players.Skip(1))p.Hp=0;ApproveMark(one,0,1,1);
        Check(one.MarkTargets.SequenceEqual(new[]{0}),"one remaining survivor receives one mark rather than duplicate marks");
    }
    static void IndependentCadence()
    {
        var s=New();s.PracticeInvulnerable=true;foreach(var p in s.Players){p.RoomId=new[]{0,2,6,8}[p.Id];}
        var starts=new System.Collections.Generic.List<int>();
        RaidSimulation restored=null;
        for(int t=1;t<=2100;t++) {
            SchedulerTick(s);
            if(t==270){Check(s.PendingRoomMarkTick==300&&s.MarkTargets.Length==0,"room callback requests its first mark thirty ticks before independent start");restored=RaidSnapshotCodec.Decode(RaidSnapshotCodec.Encode(s));restored.PracticeInvulnerable=true;}
            else if(restored!=null){SchedulerTick(restored);if(!RaidSnapshotCodec.Encode(s).SequenceEqual(RaidSnapshotCodec.Encode(restored)))throw new Exception("independent scheduler replay mismatch at "+t);}
            if(s.Events.Any(e=>e.Kind=="RoomPattern"&&e.Age==0))starts.Add(t);
            if(t==900)Check(s.MarkTargets.Length==2&&s.RoomFires.Any(f=>f.StartTick<=t&&f.EndTick>t),"next pair is marked while the previous generation is visibly burning");
            if(t==1400)Check(s.RoomFires.Count(f=>f.StartTick<=t&&f.EndTick>t)==4&&s.RoomFires.Select(f=>f.RoomId).Distinct().Count()==4,"two meteor generations burn in four different corner rooms concurrently");
        }
        Check(starts.SequenceEqual(new[]{300,900,1500,2100}),"independent ten-second Lua schedule ignores boss stagger throughout four rounds");
        Check(s.Boss.StaggerTime>0,"boss remains staggered during independent room rounds");
        Check(true,"pending scheduler checkpoint replays alternating targets and overlapping fire generations exactly");
        var paused=New();paused.Boss.TransitionTime=100;while(paused.TickIndex<300)SchedulerTick(paused);
        Check(paused.MarkTargets.Length==2&&paused.Boss.TransitionTime>0,"boss phase transition does not pause room mark scheduling");
        var missing=New();Until(missing,300);Check(missing.MarkTargets.Length==0&&missing.Events.Any(e=>e.Kind=="RoomPatternMissing"&&e.Age==0),"missing host Lua room proposal creates no automatic server marks");
    }
    static void PercentageDamage()
    {
        foreach(float maxHp in new[]{100f,250f})foreach(float defence in new[]{0f,900f}) {
            var s=New();var p=s.Players[0];p.Hp=p.MaxHp=maxHp;p.EquipmentDefence=defence;
            s.Zones.Add(new AttackZone{Id=900,RoomId=4,Tag="RoomMeteorImpact",Shape=ZoneShape.Box,Center=p.Position,HalfSize=new RaidVector(2,2),ActiveTime=.12f,Damage=1});
            Tick(s);Check(Math.Abs(p.Hp-maxHp*.05f)<.001f,"room direct meteor leaves five percent max HP regardless of armour / "+maxHp+" / "+defence);
            Until(s,40);s.Zones.Add(new AttackZone{Id=901,RoomId=4,Tag="RoomBurn",Shape=ZoneShape.Box,Center=RaidVector.zero,HalfSize=new RaidVector(9,3),ActiveTime=.12f,Damage=1});
            Tick(s);Check(p.Hp==0,"remaining in fire after a direct meteor is lethal / "+maxHp+" / "+defence);
        }
        var overlap=New();var victim=overlap.Players[0];victim.Hp=victim.MaxHp=250;victim.EquipmentDefence=900;
        for(int i=0;i<2;i++)overlap.Zones.Add(new AttackZone{Id=910+i,RoomId=4,Tag="RoomBurn",Shape=ZoneShape.Box,Center=RaidVector.zero,HalfSize=new RaidVector(9,3),ActiveTime=.12f,Damage=1});
        Tick(overlap);Check(victim.Hp==200,"simultaneous overlapping room fire costs twenty percent once through shared hit immunity");
        Until(overlap,61);overlap.Zones.Add(new AttackZone{Id=920,RoomId=4,Tag="RoomBurn",Shape=ZoneShape.Box,Center=RaidVector.zero,HalfSize=new RaidVector(9,3),ActiveTime=.12f,Damage=1});
        Tick(overlap);Check(victim.Hp==150,"next fire second costs another twenty percent even with high defence");
        var normal=New();var ordinary=normal.Players[0];ordinary.EquipmentDefence=100;
        normal.Zones.Add(new AttackZone{Id=930,RoomId=4,Tag="DawnMeteorImpact",Shape=ZoneShape.Box,Center=ordinary.Position,HalfSize=new RaidVector(2,2),ActiveTime=.12f,Damage=25});
        Tick(normal);Check(ordinary.Hp==87.5f,"ordinary four-wave meteor retains its fixed damage and normal armour calculation");
    }
    static void RoomCombatIsolation()
    {
        var s=New();var p=s.Players[0];p.RoomId=5;p.Position=s.Boss.Position;
        Tick(s,new PlayerCommand{PlayerId=0,Basic=true});
        Check(s.Zones.Any(z=>z.FromPlayer)&&s.Zones.Where(z=>z.FromPlayer).All(z=>z.RoomId==5),"skill attacks retain the caster room in authoritative zones");
        float bossHp=s.Boss.Hp;Until(s,45);Check(s.Boss.Hp==bossHp,"player attacks in another room cannot damage boss at matching coordinates");
        var marked=Marked();int deadline=marked.MarkDeadline;Until(marked,deadline-1);Door(marked,0,-1,0);
        Check(marked.TickIndex==deadline&&marked.RoomFires.Count==2&&marked.RoomFires.Any(f=>f.RoomId==3),"validated doorway input on the lock tick determines destination room before meteor locks");
        var phase=Marked();Until(phase,phase.MarkDeadline);int end=phase.RoomFires[0].EndTick;
        phase.Boss.Hp=phase.Boss.MaxHp*.4f;Tick(phase);
        Check(phase.Boss.Phase==2&&phase.RoomFires.Count==2&&phase.RoomFires.All(f=>f.EndTick==end)&&phase.Zones.Any(z=>z.Tag=="RoomMeteorImpact"),"boss phase transition cannot clear already committed room hazards");
    }
    static void Replay()
    {
        var s=Marked(2);Door(s,2,0,-1);Until(s,120);var copy=RaidSnapshotCodec.Decode(RaidSnapshotCodec.Encode(s));
        for(int i=0;i<1000;i++){
            Tick(s);Tick(copy);
            if(!RaidSnapshotCodec.Encode(s).SequenceEqual(RaidSnapshotCodec.Encode(copy)))throw new Exception("room/mark/fire checkpoint diverged at "+s.TickIndex);
        }
        Check(true,"checkpoint during carried mark replays room lock, burn pulses and expiry identically");
        var burning=Marked();Until(burning,burning.MarkDeadline+100);var clone=burning.Clone();var wire=RaidSnapshotCodec.Decode(RaidSnapshotCodec.Encode(burning));
        clone.RoomFires[0].RoomId=8;Check(burning.RoomFires[0].RoomId==4,"room-fire clone owns independent mutable records");
        for(int i=0;i<500;i++){Tick(burning);Tick(wire);if(!RaidSnapshotCodec.Encode(burning).SequenceEqual(RaidSnapshotCodec.Encode(wire)))throw new Exception("midfire replay diverged");}
        Check(true,"mid-fire checkpoint preserves next pulse and remaining lifetime");
    }
    static int Main(){try{Topology();Travel();MarkAndBurn();PairRounds();IndependentCadence();PercentageDamage();RoomCombatIsolation();Replay();Console.WriteLine("[DAWN_ROOMS_VALIDATED] "+checks+" checks");return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
