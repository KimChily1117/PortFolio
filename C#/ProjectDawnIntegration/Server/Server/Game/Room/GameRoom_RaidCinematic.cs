using System;
using System.Linq;
using Google.Protobuf.Protocol;
using ProjectDawn.Combat;

namespace Server.Game.Room
{
    public partial class GameRoom
    {
        RaidClearCinematic _clearCinema;
        long _clearCinemaBroadcastAt;
        bool _clearCinemaReleased;
        int[] ClearViewers()=>Enumerable.Range(0,_raidRoster.Length).Where(i=>!_raidRoster[i].ConnectionClosed &&
            !_raidResultsReturned[i] && _raidReward.Recipients[i].Eligible).ToArray();
        void BeginClearCinematic()
        {
            _clearCinema=new RaidClearCinematic(_raid.Engine.EncounterId,ClearViewers(),Environment.TickCount64+500);
            State="RaidCinematic";PublishClearCinematic(Environment.TickCount64);
        }
        void UpdateClearCinematic(long now)
        {
            if(_clearCinema==null)return;
            bool changed=_clearCinema.Advance(now,ClearViewers());
            if(_clearCinema.Complete&&!_clearCinemaReleased){
                _clearCinemaReleased=true;
                if(_raidRewardSaved){State="RaidCleared";_raidResultsUntil=now+RaidRewardPresentation.ResultMs(_raidReward.Recipients.Count);}
                Console.WriteLine("[RAID_CINEMATIC_END] encounter="+_clearCinema.Encounter+" reason="+_clearCinema.Reason+" votes="+_clearCinema.Votes+" required="+_clearCinema.Required);
                changed=true;
            }
            if(changed || (!_clearCinema.Complete&&now>=_clearCinemaBroadcastAt))PublishClearCinematic(now);
        }
        void PublishClearCinematic(long now)
        {
            var c=_clearCinema;
            var packet=new S_RaidCinematic{Encounter=c.Encounter,Revision=c.Revision,ElapsedMs=(int)(now-c.StartMs),
                FadeMs=RaidClearCinematic.FadeMs,DurationMs=RaidClearCinematic.VideoMs,Viewers=c.Viewers,Required=c.Required,
                Complete=c.Complete,Reason=c.Reason};
            packet.VotedSlots.AddRange(c.VotedSlots);
            foreach(int i in ClearViewers())_raidRoster[i].Send(packet);
            _clearCinemaBroadcastAt=now+1000;
        }
        public void RequestCinematicSkip(ClientSession session,long encounter)
        {
            if(_raidEnded||!_raidClearWaiting||_clearCinema==null||session.ConnectionClosed||session.MyPlayer?.Room!=this)return;
            int slot=Array.IndexOf(_raidRoster,session);if(slot<0)return;
            long now=Environment.TickCount64;UpdateClearCinematic(now);
            if(_clearCinema.Vote(encounter,slot,now)){
                Console.WriteLine("[RAID_CINEMATIC_VOTE] encounter="+encounter+" slot="+slot+" votes="+_clearCinema.Votes+" required="+_clearCinema.Required);
                UpdateClearCinematic(now);PublishClearCinematic(now);
            }
        }
    }
}
