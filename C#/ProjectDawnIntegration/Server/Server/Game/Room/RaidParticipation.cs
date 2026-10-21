using System;
using System.Linq;
namespace Server.Game.Room
{
    // Room-thread policy. Time is monotonic; the deadline itself is exclusive for reconnect/rewards.
    public sealed class RaidParticipation
    {
        public const long ReconnectMs = 180000;
        public enum Status { Connected, Waiting, Expired, Left }
        public sealed class Member { public Status State; public long Deadline; }
        public readonly Member[] Members;
        public RaidParticipation(int count=4) {
            if(count<1||count>4)throw new ArgumentOutOfRangeException(nameof(count));
            Members=Enumerable.Range(0,count).Select(_=>new Member()).ToArray();
        }
        public bool Ended { get; private set; }
        public void Disconnect(int slot,long now) { var m=Members[slot];if(!Ended && m.State==Status.Connected){m.State=Status.Waiting;m.Deadline=checked(now+ReconnectMs);} }
        public bool CanResume(int slot,long now) => !Ended && Members[slot].State==Status.Waiting && now<Members[slot].Deadline;
        public bool Resume(int slot,long now) {if(!CanResume(slot,now))return false;Members[slot].State=Status.Connected;return true;}
        public bool Leave(int slot) {if(Ended||Members[slot].State==Status.Left||Members[slot].State==Status.Expired)return false;Members[slot].State=Status.Left;return true;}
        public int[] Expire(long now) {if(Ended)return Array.Empty<int>();var slots=Enumerable.Range(0,Members.Length).Where(i=>Members[i].State==Status.Waiting && now>=Members[i].Deadline).ToArray();foreach(int i in slots)Members[i].State=Status.Expired;return slots;}
        public bool[] Finish(long now) {if(Ended)throw new InvalidOperationException("Encounter already ended");Expire(now);Ended=true;return Members.Select(m=>m.State==Status.Connected||m.State==Status.Waiting).ToArray();}
    }
}
