using System;
using System.Collections.Generic;
using System.Linq;
using DnfRaidDemo;
using ProjectDawn.Combat;
internal static partial class Program
{
    static int checks;
    static void Check(bool condition,string name){if(!condition)throw new Exception(name);checks++;Console.WriteLine("PASS: "+name);}
    static int Main()
    {
        try
        {
            for(int skill=0;skill<SkillBook.Count;skill++)
            {
                var sim=new RaidSimulation(4);sim.Start();sim.Boss.StaggerTime=20;
                var p=sim.Players[0];p.Position=new RaidVector(2.5f,0);p.Mp=p.MaxMp=1000;
                sim.Tick(RaidSimulation.FixedStep,new[]{new PlayerCommand {PlayerId=0,Skills=1<<skill,Basic=skill==0,Dash=skill==3}});
                Check(skill==3?p.DashTime>0:p.ActionSequence==1 && p.ActiveSkill==(SkillId)skill,"shared skill "+skill+" "+SkillBook.Get((SkillId)skill).Key);
            }
            var original=new RaidSimulation(4,42){AutoBuffs=true};original.Start();
            for(int t=0;t<83;t++)original.Tick(RaidSimulation.FixedStep,new[]{new PlayerCommand {PlayerId=0,Move=new RaidVector(.3f,.2f),Basic=true}});
            var clone=original.Clone();clone.Players[0].Cooldowns[0]=99;clone.Zones.Clear();
            Check(original.Players[0].Cooldowns[0]!=99 && original.Zones.Count>0,"checkpoint clone isolates mutable arrays and zones");
            var decoded=RaidSnapshotCodec.Decode(RaidSnapshotCodec.Encode(original));
            for(int t=0;t<180;t++) {var commands=new[]{new PlayerCommand{PlayerId=1,Move=new RaidVector(.7f,0),Jump=t==5,Basic=true}};
                original.Tick(RaidSimulation.FixedStep,commands);decoded.Tick(RaidSimulation.FixedStep,commands);}
            Check(RaidSnapshotCodec.Encode(original).SequenceEqual(RaidSnapshotCodec.Encode(decoded)),"binary checkpoint resumes the same RNG/skill/attack state");
            TestEquipment();TestEquipmentTiming();TestActionTempo();TestSkillReference();TestSkillPhaseIntegrity();TestEngine(false);TestEngine(true);
            Console.WriteLine("[DAWN_CONTENT_VALIDATED] "+checks+" checks");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    static void TestEquipment()
    {
        var plain=new RaidSimulation(4);plain.Start();plain.Boss.StaggerTime=10;
        plain.Players[0].Position=new RaidVector(2.5f,0);
        var equipped=plain.Clone();equipped.Players[0].PhysicalAttack=125f;
        var cmd=new[]{new PlayerCommand{PlayerId=0,Basic=true}};
        plain.Tick(RaidSimulation.FixedStep,cmd);equipped.Tick(RaidSimulation.FixedStep,cmd);
        Check(Math.Abs(equipped.Zones.First(z=>z.FromPlayer).Damage/plain.Zones.First(z=>z.FromPlayer).Damage-1.25f)<.0001f,"server loadout scales actual spawned attack zones");
        var saved=RaidSnapshotCodec.Decode(RaidSnapshotCodec.Encode(equipped));
        Check(saved.Players[0].PhysicalAttack==125f,"equipment modifier survives server snapshot and host replay");
        var naked=new RaidSimulation(4);naked.Start();naked.Boss.AttackTime=100;
        var armor=naked.Clone();armor.Players[0].EquipmentDefence=100;
        foreach(var sim in new[]{naked,armor}) {
            sim.Zones.Add(new AttackZone{Id=999,Center=sim.Players[0].Position,HalfSize=new RaidVector(1,1),Shape=ZoneShape.Box,Damage=20,ActiveTime=1,Tag="EquipmentTest"});
            sim.Tick(RaidSimulation.FixedStep,Array.Empty<PlayerCommand>());
        }
        Check(Math.Abs((100-armor.Players[0].Hp)*2-(100-naked.Players[0].Hp))<.0001f && naked.Players[0].Hp<100,"server defence reduces actual incoming damage");
        Check(Math.Abs(armor.Events.Last(e=>e.Kind=="Damage"&&e.TargetId==0).Value-(100-armor.Players[0].Hp))<.001f,"damage feedback displays the post-defence amount actually lost");
        var immutable=equipped.Clone();saved.Players[0].PhysicalAttack=9900;
        Check(immutable.Players[0].PhysicalAttack==125f,"client snapshot mutation cannot alter server loadout");
    }
    static void TestSkillPhaseIntegrity()
    {
        // Retiming and adding missing intermediate visuals must not silently rebalance each cast.
        var baseline = new Dictionary<SkillId, float> {
            [SkillId.GoreCross]=350, [SkillId.Bloodlust]=300, [SkillId.BloodScratch]=360,
            [SkillId.RagingFury]=600, [SkillId.CruelEngage]=580, [SkillId.Rage]=970,
            [SkillId.BloodyRave]=660, [SkillId.BloodSword]=220, [SkillId.OutrageBreak]=480,
            [SkillId.ExtremeOverkill]=1100, [SkillId.BloodSnatch]=850, [SkillId.BrutalCrusher]=700,
            [SkillId.BloodBoom]=800, [SkillId.FatalBlood]=1300, [SkillId.BloodRiven]=2400,
            [SkillId.RampantMadness]=1500, [SkillId.Destruction]=5400 };
        Check(baseline.All(x=>Math.Abs(SkillBook.Get(x.Key).Hits.Sum(h=>h.Damage*h.Repeat)-x.Value)<.001f),
            "reference phase corrections preserve total demo damage for all 17 damaging skills");
        Check(SkillBook.All.All(s=>s.Duration>=0 && s.Lock>=0 && s.Lock<=s.Duration &&
            s.Hits.All(h=>h.Repeat>=1 && h.Delay>=0 && h.Telegraph>=0 && h.Active>0 && (h.Repeat==1 || h.Interval>0)) &&
            s.Fx.All(f=>!string.IsNullOrWhiteSpace(f.Effect) && f.Delay>=0 && f.Duration>=0 && f.Loop>=0)),
            "all skill locks, repeat intervals and visual cue clocks are valid");
        var cruel=SkillBook.Get(SkillId.CruelEngage);
        Check(cruel.Hits.Length==3 && cruel.Hits[0].Delay<cruel.Hits[1].Delay && cruel.Hits[1].Delay<cruel.Hits[2].Delay,
            "Cruel Engages three distinct ordered sword sweeps");
        var rave=SkillBook.Get(SkillId.BloodyRave);
        var suction=rave.Hits[0];
        Check(suction.Delay+(suction.Repeat-1)*suction.Interval+suction.Active<rave.Hits[1].Delay,
            "Bloody Rave suction finishes before its heavy finishing swing");
        var outrage=SkillBook.Get(SkillId.OutrageBreak);
        Check(outrage.Hits.Length==2 && outrage.Hits[1].Repeat==4 && outrage.Hits[0].Delay<outrage.Hits[1].Delay &&
            Math.Abs(outrage.Hits[0].Offset.x-outrage.Advance*outrage.AdvanceUntil)<.001f &&
            outrage.Hits[0].Offset.x==outrage.Hits[1].Offset.x,
            "Outrage slam and delayed eruptions share the projected landing point");
        var overkill=SkillBook.Get(SkillId.ExtremeOverkill);
        Check(overkill.Hits[0].Delay+(overkill.Hits[0].Repeat-1)*overkill.Hits[0].Interval>overkill.Duration &&
            overkill.Fx.Any(f=>f.Grounded && f.Delay+f.Duration>overkill.Duration),
            "Overkill pillar remains active and grounded after the caster recovers");
        var destruction=SkillBook.Get(SkillId.Destruction);
        Check(destruction.Hits[0].Repeat==4 && destruction.Hits[0].Delay+3*destruction.Hits[0].Interval<destruction.Hits[1].Delay &&
            destruction.Hits[1].Delay<destruction.Hits[2].Delay && destruction.BodyUntil>=destruction.Hits[2].Delay &&
            destruction.Lock>=destruction.Hits[2].Delay,
            "Destruction keeps its transformed body through four cuts, slam and delayed final eruption");
    }
    static void TestEngine(bool forge)
    {
        long now=0,sequence=0;var initial=CombatState.FourPlayers();initial.Content=new RaidContentState(new[]{1,2,3,4},100);
        var policy=RaidContentRules.Timing();var engine=new EncounterEngine(901,1,1,policy,RaidContentRules.Create(),initial,()=>now);
        var sessions=Enumerable.Range(1,4).Select(p=>new SessionBinding(p,1,1,0,false)).ToArray();foreach(var s in sessions)engine.Bind(s);
        var proposed=new HashSet<int>();var events=new List<ProjectDawn.Combat.CombatEvent>();
        for(int t=1;t<=200;t++)
        {
            now=(t-1)*policy.TickUs;
            if(t%3==1)foreach(var s in sessions){var input=new PlayerInput(new InputKey(901,s.Player,1,t),0,1,InputKind.Controls,0,1000,0,1);
                engine.ReceiveInput(s,input);engine.ReceiveProposal(sessions[0],new HostProposal(901,1,++sequence,t,s.Player,100,input.Key));}
            engine.Pump();
            var preview=engine.PreviewSnapshot.Content.Simulation;
            if(preview.PendingPattern>=0 && proposed.Add(preview.PendingPatternTick))
                engine.ReceiveProposal(sessions[0],new HostProposal(901,1,++sequence,preview.PendingPatternTick,100,201+(preview.PendingPattern+(forge?1:0))%7,program:BossPatternProgram.Default(preview.PendingPattern,preview.SelectionOrdinal,roll:preview.SelectionRoll,aliveMask:preview.SelectionAliveMask,previousMarkMask:preview.SelectionPreviousMarkMask)));
            events.AddRange(engine.DrainCommittedEvents());engine.DrainInputReceipts();engine.DrainProposalReceipts();
            CheckTick(!engine.IsAborted,"content engine aborted");
        }
        Check(events.Any(e=>e.Kind==(forge?"ContentPatternMissing":"ContentPattern")),forge?"forged boss selection cannot execute":"host-calculated scheduled pattern passes server replay");
        Check(events.Any(e=>e.Kind=="ContentSkill"),"four-player content accepts host-proposed controls");
        if(forge)Check(!events.Any(e=>e.Kind=="ContentPattern"),"wrong pattern creates no boss attack");
    }
    static void CheckTick(bool b,string name){if(!b)throw new Exception(name);}
}
