using System;
using System.Linq;
using DnfRaidDemo;
using ProjectDawn.Combat;
using ProjectDawn.Network;
using CombatEvent = DnfRaidDemo.CombatEvent;
class Program
{
    static int checks;
    static void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);checks++;}
    static CombatEvent Hit(int index=0,int skill=0)=>new CombatEvent{Kind="Damage",SourceId=2,TargetId=-1,Skill=skill,SourceAction=7,HitIndex=index,FeedbackKey="hit/2/7/"+index,ActionTime=.4f};
    static RaidSimulation Setup(){var s=new RaidSimulation(4){NetworkPatternGate=true};s.Start();s.Boss.Position=RaidVector.zero;s.Boss.Hp=s.Boss.MaxHp=100000;s.Boss.StaggerTime=100;foreach(var p in s.Players){p.Position=new RaidVector(-1.2f,0);p.Facing=1;}return s;}
    static int Main()
    {
        try {
            var f=new PersonalHitFeedback();var e=Hit();
            Check(!f.Accept(e,0,1,out _),"remote hits cannot freeze local player");
            Check(f.Accept(e,2,1,out var profile),"actual local slot accepted");
            Check(!f.Accept(e.Clone(),2,1.01f,out _),"confirmed replay of predicted hit does not replay feedback");
            Check(Math.Abs(f.VisualAge(7,.43f,1.02f)-.4f)<.0001,"contact frame held");
            Check(f.VisualAge(8,.02f,1.02f)==.02f,"next action is never held by previous contact");
            Check(f.VisualAge(7,.55f,1.09f)>.4f && f.VisualAge(7,.55f,1.09f)<.55f,"release catches up gradually");
            Check(f.VisualAge(7,.7f,1.2f)==.7f,"clock fully catches up");
            f.CancelHold();Check(f.VisualAge(7,.45f,1.02f)==.45f,"jump/hurt/room transition can cancel only presentation hold");
            f.Reset();float total=0;
            for(int i=0;i<100;i++){f.Accept(Hit(i,(int)SkillId.BloodSword),2,2+i*.009f,out _);total+=f.LastHold;}
            Check(total<=.24001f,"rapid hits cannot exceed 240ms stop per second");
            f.Reset();Check(f.Accept(e,2,0,out _),"new encounter permits reused action key");
            var basic=PersonalHitFeedback.Select(e);e.Frenzy=true;e.Combo=3;
            Check(PersonalHitFeedback.Select(e).Hold>basic.Hold,"frenzy final hit has stronger emphasis");
            Check(PersonalHitFeedback.Select(Hit(0,4)).Hold>PersonalHitFeedback.Select(Hit(1,4)).Hold,"overkill pulses do not repeat first-impact stop");
            var s=Setup();var p=s.Players[2];
            s.NetworkTick(new[]{new PlayerCommand{PlayerId=2,Skills=PlayerCommand.Bit(SkillId.BloodSword)}},-1);
            for(int i=0;i<27;i++)s.NetworkTick(Array.Empty<PlayerCommand>(),-1);
            var contact=s.Events.First(x=>x.Kind=="Damage"&&x.SourceId==2);
            Check(contact.Skill==1 && contact.SourceAction==1 && contact.HitIndex==0 && contact.Facing==1 && contact.ActionTime>=.4f-RaidSimulation.FixedStep-.0001f,"real hit records immutable skill/action/timing metadata");
            p.ActiveSkill=SkillId.Basic;p.ActionSequence=99;
            var restored=RaidSnapshotCodec.Decode(RaidSnapshotCodec.Encode(s));
            var restoredHit=restored.Events.First(x=>x.FeedbackKey==contact.FeedbackKey);
            Check(restoredHit.Skill==1 && restoredHit.SourceAction==1 && restoredHit.ActionTime==contact.ActionTime,"snapshot round-trip preserves old hit after new action");
            var bytes=RaidSnapshotCodec.Encode(s);bytes[0]=12;
            bool rejected=false;try{RaidSnapshotCodec.Decode(bytes);}catch(System.IO.InvalidDataException){rejected=true;}
            Check(rejected,"old snapshot schema rejected instead of silently misreading fields");
            var final=Setup();var frozen=RaidSnapshotCodec.Encode(final);var timeline=new RaidPresentationTimeline(2);int count=0;
            timeline.Feedback+=h=>{if(h.Kind=="Damage"){Check(h.Skill==1&&h.SourceId==2,"predicted hit carries contact metadata");count++;f.Accept(h,2,5,out _);}};
            timeline.Track(1,1,new PlayerCommand{PlayerId=2,Skills=PlayerCommand.Bit(SkillId.BloodSword)});
            var display=timeline.Project(final,30);timeline.Project(final,30);
            Check(count==1,"repeated projection does not duplicate personal contact");
            Check(RaidSnapshotCodec.Encode(final).SequenceEqual(frozen),"feedback/prediction cannot mutate authoritative state");
            Check(display.Boss.Hp==final.Boss.Hp && display.Players[2].Hp==final.Players[2].Hp,"visible feedback does not publish predicted HP");
            Console.WriteLine("[PERSONAL_FEEDBACK_VALIDATED] "+checks+" checks");return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
