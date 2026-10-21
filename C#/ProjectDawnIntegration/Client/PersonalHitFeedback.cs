using System;
using System.Collections.Generic;
using DnfRaidDemo;

namespace ProjectDawn.Network
{
    // Presentation only. No network transport, simulation mutation or global time scaling.
    public sealed class PersonalHitFeedback
    {
        public readonly struct Profile
        {
            public readonly string Name;
            public readonly float Hold, Shake, Scale, Volume, Life;
            public Profile(string name,float hold,float shake,float scale,float volume,float life=.2f)
            {Name=name;Hold=hold;Shake=shake;Scale=scale;Volume=volume;Life=life;}
        }
        public static Profile Select(CombatEvent e)
        {
            if(e.Skill==(int)SkillId.Basic) {
                bool finish=e.Combo>=(e.Frenzy?3:2);
                return new Profile(e.Frenzy?"frenzy":"basic",finish?.065f:e.Frenzy?.042f:.05f,finish?4.5f:2.5f,finish?1.2f:.85f,.72f);
            }
            if(e.Skill==(int)SkillId.BloodSword)return new Profile("bloodsword",.09f,6f,1.65f,.85f,.26f);
            if(e.Skill==(int)SkillId.ExtremeOverkill) {
                if(e.HitIndex==0)return new Profile("overkill-impact",.105f,8f,2f,.9f,.3f);
                if(e.HitIndex==9)return new Profile("overkill-last",.06f,5f,1.45f,.75f,.24f);
                return new Profile("overkill-pulse",.008f,1.1f,.6f,.32f,.12f);
            }
            return new Profile("other",.012f,1.4f,.65f,.55f,.14f);
        }
        readonly HashSet<string> seen=new HashSet<string>();
        readonly Queue<string> keys=new Queue<string>();
        readonly Queue<(float Time,float Hold)> budget=new Queue<(float,float)>();
        float used,holdUntil=-1,releaseUntil=-1,contactAge,lastHold=-1;
        int action=-1;
        public float LastHold {get;private set;}
        public bool Accept(CombatEvent e,int localSlot,float now,out Profile profile)
        {
            profile=Select(e);LastHold=0;
            if(e.Kind!="Damage" || e.TargetId>=0 || e.SourceId!=localSlot || string.IsNullOrEmpty(e.FeedbackKey) || !seen.Add(e.FeedbackKey))return false;
            keys.Enqueue(e.FeedbackKey);if(keys.Count>2048)seen.Remove(keys.Dequeue());
            while(budget.Count>0 && now-budget.Peek().Time>=1f)used-=budget.Dequeue().Hold;
            // At most 240ms of presentation stop per second; rapid multi-hit cannot pin the actor.
            float hold=Math.Min(profile.Hold,Math.Max(0,.24f-used));
            if(now-lastHold<.08f)hold=0;
            if(hold>0) {
                LastHold=hold;used+=hold;budget.Enqueue((now,hold));lastHold=now;
                action=e.SourceAction;contactAge=e.ActionTime;holdUntil=now+hold;releaseUntil=holdUntil+.075f;
            }
            return true;
        }
        public float VisualAge(int sequence,float current,float now)
        {
            if(sequence!=action || now>=releaseUntil)return current;
            float held=Math.Min(current,contactAge);
            if(now<=holdUntil)return held;
            float t=(now-holdUntil)/.075f;t=t*t*(3-2*t);
            return held+(current-held)*t;
        }
        public void CancelHold(){action=-1;holdUntil=releaseUntil=-1;}
        public void Reset(){seen.Clear();keys.Clear();budget.Clear();used=0;lastHold=-1;LastHold=0;CancelHold();}
    }
}
