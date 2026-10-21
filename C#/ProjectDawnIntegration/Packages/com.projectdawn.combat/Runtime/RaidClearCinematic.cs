using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectDawn.Combat
{
    // Server-owned presentation barrier. It never grants rewards or changes combat outcomes.
    public sealed class RaidClearCinematic
    {
        public const int FadeMs=2000, VideoMs=40720;
        public long Encounter {get;}
        public long StartMs {get;}
        public long EndMs=>StartMs+FadeMs+VideoMs;
        readonly HashSet<int> original,active,votes=new HashSet<int>();
        public int Viewers=>active.Count;
        public int Required=>Viewers/2+1;
        public int Votes=>votes.Count(s=>active.Contains(s));
        public int[] VotedSlots=>votes.Where(s=>active.Contains(s)).OrderBy(s=>s).ToArray();
        public bool Complete {get;private set;}
        public string Reason {get;private set;}="";
        public int Revision {get;private set;}=1;
        public RaidClearCinematic(long encounter,IEnumerable<int> slots,long startMs)
        {Encounter=encounter;StartMs=startMs;original=new HashSet<int>(slots);active=new HashSet<int>(original);}
        public bool Advance(long now,IEnumerable<int> connected)
        {
            if(Complete)return false;
            int prior=Revision;var next=new HashSet<int>(connected.Where(original.Contains));
            if(!active.SetEquals(next)){active.Clear();active.UnionWith(next);Revision++;}
            if(active.Count==0)Finish("NoViewers");
            else if(now>=EndMs)Finish("Finished");
            else if(now>=StartMs+FadeMs && Votes>=Required)Finish("MajoritySkip");
            return Revision!=prior;
        }
        public bool Vote(long encounter,int slot,long now)
        {
            if(Complete||encounter!=Encounter||!active.Contains(slot)||now<StartMs+FadeMs||now>=EndMs||!votes.Add(slot))return false;
            Revision++;if(Votes>=Required)Finish("MajoritySkip");return true;
        }
        void Finish(string reason){Complete=true;Reason=reason;Revision++;}
    }
}
