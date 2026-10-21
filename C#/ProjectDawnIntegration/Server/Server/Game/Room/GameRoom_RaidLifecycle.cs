using System;
using System.Linq;
using Google.Protobuf;
using Google.Protobuf.Protocol;
using ProjectDawn.Combat;
using Server.DB;
using Server.Game.Object;

namespace Server.Game.Room
{
    public partial class GameRoom
    {
        private RaidParticipation _participation;
        private Player[] _raidPlayers;
        private ClientSession[] _raidResuming=Array.Empty<ClientSession>();
        private bool[] _raidResultsReturned=Array.Empty<bool>();
        private bool _raidClearWaiting,_raidRewardSaved,_raidRewardQueued;
        private long _raidResultsUntil,_raidRewardRetryAt;
        private RaidRewardRecord _raidReward;
        private static int _resumeTransfer;
        private bool InitializeParticipation()
        {
            _participation=new RaidParticipation(_raidRoster.Length);_raidPlayers=_raidRoster.Select(s=>s.MyPlayer).ToArray();
            _raidResuming=new ClientSession[_raidRoster.Length];_raidResultsReturned=new bool[_raidRoster.Length];
            return RaidResumeRegistry.RegisterParty(_raidPlayers.Select(player=>player.PlayerDbId),this);
        }
        private bool UpdateRaidLifecycle()
        {
            long now=Environment.TickCount64;
            if(_raidClearWaiting){
                UpdateClearCinematic(now);
                if(!_raidRewardQueued && now>=_raidRewardRetryAt)QueueRaidReward();
                if(_raidRewardSaved && _clearCinemaReleased && now>=_raidResultsUntil)EndRaid("Clear");
                return true;
            }
            foreach(var session in _raidRoster)if(session.RaidRoute!=null && _raid.NowUs-session.RaidRoute.LastActivityUs>15000000)session.Disconnect();
            // A socket close is visible immediately, even if its room-queue notification was delayed.
            for(int i=0;i<_raidRoster.Length;i++)if(_raidRoster[i].ConnectionClosed && _participation.Members[i].State==RaidParticipation.Status.Connected)OnRaidDisconnected(_raidRoster[i]);
            if(_raidEnded)return true;
            foreach(int slot in _participation.Expire(now))RemoveRaidParticipant(slot);
            return false;
        }
        private void RemoveRaidParticipant(int slot)
        {
            var session=_raidRoster[slot];session.RaidRoute=null;
            _raid?.Engine.Revoke(_raidPlayers[slot].Id);
            _raid?.Engine.SetPresence(_raidPlayers[slot].Id,false,true);
            RaidResumeRegistry.Remove(_raidPlayers[slot].PlayerDbId,this);
            if(_raidResuming[slot]!=null){var pending=_raidResuming[slot];_raidResuming[slot]=null;pending.RaidResumePending=false;pending.ClearPendingTransfer();pending.Send(new S_RaidResume{Status="Expired"});}
            // Voluntary departure uses the original transfer path. Expired offline actors have no scene to load.
            if(_participation.Members[slot].State==RaidParticipation.Status.Expired){LeaveRoom(_raidPlayers[slot].Id,false);ObjectManager.Instance.Remove(_raidPlayers[slot].Id);}
        }
        public void BeginRaidResume(ClientSession session,int character)
        {
            int slot=Array.FindIndex(_raidPlayers,p=>p.PlayerDbId==character);
            Action<string> reject=reason=>{session.RaidResumePending=false;session.Send(new S_RaidResume{Status=reason});};
            if(session.ConnectionClosed){reject("Disconnected");return;}
            if(slot<0 || _raidRoster[slot].AccountDbId!=session.AccountDbId || session.RaidProtocolVersion!=_raidRoster[slot].RaidProtocolVersion){reject("NotOriginalParticipant");return;}
            if(_raidRoster[slot].ConnectionClosed && _participation.Members[slot].State==RaidParticipation.Status.Connected)OnRaidDisconnected(_raidRoster[slot]);
            if(_raidEnded||_raidClearWaiting){reject("EncounterEnded");return;}
            if(!_participation.CanResume(slot,Environment.TickCount64)||_raidResuming[slot]!=null){reject(_participation.Members[slot].State==RaidParticipation.Status.Connected?"AlreadyConnected":"ExpiredOrResuming");return;}
            _raidResuming[slot]=session;session.MyPlayer=_raidPlayers[slot];
            session.IsTransferring=true;session.PendingRoomId=RoomId;session.PendingRoomType=RoomType;
            session.PendingTransferId=System.Threading.Interlocked.Increment(ref _resumeTransfer);
            session.PendingSceneType=SceneType.SceneBakal;
            session.Send(new S_RaidResume{Status="Loading",RemainingSeconds=(int)Math.Max(0,(_participation.Members[slot].Deadline-Environment.TickCount64)/1000)});
            var items=new S_ItemList();items.Items.AddRange(_raidPlayers[slot].Inven.Items.Values.Select(i=>i.Info.Clone()));session.Send(items);
            session.Send(new S_SceneMove{TargetRoomId=RoomId,TargetRoomType=RoomType,TransferId=session.PendingTransferId,SceneType=session.PendingSceneType});
        }
        internal void CompleteRaidResume(ClientSession session)
        {
            int slot=Array.IndexOf(_raidResuming,session);
            if(slot<0||!session.RaidResumePending)return;
            if(session.ConnectionClosed){_raidResuming[slot]=null;return;}
            if(_raidEnded||_raidClearWaiting||!_participation.Resume(slot,Environment.TickCount64)){
                _raidResuming[slot]=null;session.RaidResumePending=false;session.ClearPendingTransfer();session.Send(new S_RaidResume{Status="Expired"});return;
            }
            _raidResuming[slot]=null;_raidRoster[slot]=session;_raidPlayers[slot].Session=session;
            session.RaidResumePending=false;session.ClearPendingTransfer();session.ResumeIngame();
            session.BindChat(this);
            int player=_raidPlayers[slot].Id;
            var binding=new SessionBinding(player,_raidBindings[player].Epoch+1,1,0,false);
            _raidBindings[player]=binding;_raid.BindAuthenticatedParticipant(binding);
            _raid.Engine.SetPresence(player,true,false);
            var state=_raid.Engine.FinalSnapshot;var timing=state.Content!=null?RaidContentRules.Timing():IntegrationRules.Timing();
            var start=new S_RaidStart{Version=session.RaidProtocolVersion,Encounter=_raid.Engine.EncounterId,Self=player,
                Host=_raid.Engine.HostPlayer,HostEpoch=_raid.Engine.HostEpoch,ClockVersion=1,ReceiveTimeOnly=true,ClockSync=session.RaidClockSync,
                TickUs=timing.TickUs,InputGraceUs=timing.InputGraceUs,ProposalGraceUs=timing.ProposalGraceUs,FinalizationDelayUs=timing.FinalizationDelayUs,
                Ruleset=state.Content!=null?RaidContentRules.Id:IntegrationRules.Id,EventSequence=_raid.Engine.EventSequence,ServerTimeUs=_raid.NowUs,Resumed=true};
            start.Fighters.AddRange(state.Fighters.Values.OrderBy(f=>f.Id).Select(ToWire));
            if(state.Content!=null){start.Content=ByteString.CopyFrom(RaidSnapshotCodec.Encode(state.Content.Simulation));start.PlayerSlots.AddRange(state.Content.PlayerIds);}
            session.Send(new S_EnterGame{Player=_raidPlayers[slot].Info.Clone()});session.Send(start);
            session.RaidRoute=new RaidIngress(this,_raid,binding,session.RaidClockSync);
            Console.WriteLine("[RAID_RESUMED] encounter="+start.Encounter+" slot="+slot+" epoch="+binding.Epoch);
        }
        private void BeginRaidClear()
        {
            if(_raidClearWaiting||_raidEnded)return;
            var eligible=_participation.Finish(Environment.TickCount64);
            _raidClearWaiting=true;State="RaidRewardPending";
            _raidReward=new RaidRewardRecord{Encounter=_raid.Engine.EncounterId,CompletedUtc=DateTime.UtcNow};
            for(int i=0;i<_raidRoster.Length;i++){
                _raidRoster[i].RaidRoute=null;
                int equipment=eligible[i]?RaidRewardStore.RollEquipment():0;
                var equipmentData=equipment==0?null:Server.Data.DataManager.ItemDict[equipment];
                _raidReward.Recipients.Add(new RaidRewardRecipient{Slot=i,AccountId=_raidRoster[i].AccountDbId,CharacterId=_raidPlayers[i].PlayerDbId,
                    Name=_raidPlayers[i].Info.Name,Eligible=eligible[i],Amount=eligible[i]?RaidRewardStore.ClearTokens:0,
                    EquipmentTemplate=equipment,EquipmentName=equipmentData?.name,EquipmentIcon=equipmentData?.iconPath,
                    Reason=eligible[i]?"Clear":_participation.Members[i].State.ToString()});
            }
            BeginClearCinematic();
            Broadcast(RaidRewardStore.Packet(_raidReward,"Pending"));QueueRaidReward();
        }
        private void ReturnClearedParticipant(int slot)
        {
            if(_raidResultsReturned[slot])return;
            _raidResultsReturned[slot]=true;var session=_raidRoster[slot];
            RaidResumeRegistry.Remove(_raidPlayers[slot].PlayerDbId,this);
            session.Send(new S_RaidEnded{Encounter=_raid.Engine.EncounterId,Reason="Clear"});
            RoomTransferService.Instance.StartRaidReturnToTownTransfer(this,new[]{session});
        }
        private void QueueRaidReward()
        {
            try{
                RaidRewardOutbox.Enqueue(_raidReward,ok=>{if(ok)Push(CompleteRaidRewards);});_raidRewardQueued=true;
            }catch(Exception e){_raidRewardRetryAt=Environment.TickCount64+5000;Console.WriteLine("[RAID_REWARD_JOURNAL_RETRY] "+e.GetType().Name);}
        }
        private void CompleteRaidRewards()
        {
            if(_raidEnded||_raidRewardSaved)return;
            try{
                    // Reload SQL-confirmed rewards into the existing character, including offline participants.
                    using(var db=new AppDbContext())foreach(var player in _raidPlayers){
                        var rows=db.Items.Where(item=>item.OwnerDbId==player.PlayerDbId).ToList();
                        foreach(var row in rows)if(player.Inven.Get(row.ItemDbId)==null){var item=Server.Game.Item.MakeItem(row);if(item!=null)player.Inven.Add(item);}
                    }
                    _raidRewardSaved=true;State=_clearCinemaReleased?"RaidCleared":"RaidCinematic";
                    _raidResultsUntil=_clearCinemaReleased?Environment.TickCount64+RaidRewardPresentation.ResultMs(_raidReward.Recipients.Count):long.MaxValue;
                    foreach(var session in _raidRoster.Where((s,i)=>_participation.Members[i].State!=RaidParticipation.Status.Left&&_participation.Members[i].State!=RaidParticipation.Status.Expired)){
                        var inventory=new S_ItemList();inventory.Items.AddRange(session.MyPlayer.Inven.Items.Values.Select(item=>item.Info.Clone()));session.Send(inventory);
                        var packet=RaidRewardStore.Packet(_raidReward,"Saved");session.Send(packet);
                    }
            }catch(Exception e){Console.WriteLine("[RAID_REWARD_INVENTORY_RETRY] "+e.GetType().Name);PushAfter(5000,CompleteRaidRewards);}
        }
    }
}
