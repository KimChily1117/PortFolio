using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Google.Protobuf.Protocol;
using ProjectDawn.Combat;

namespace Server.Game.Room
{
    public partial class GameRoom
    {
        private static long _nextEncounter = DateTime.UtcNow.Ticks;
        private ClientSession[] _raidRoster;
        private readonly Dictionary<int, SessionBinding> _raidBindings = new Dictionary<int, SessionBinding>();
        private RaidCombatSession _raid;
        private bool _raidEnded;
        private long _lastRaidFrameTick = -1;
        private long _raidWaitStarted;
        private readonly HashSet<InputKey> _awaitingHost = new HashSet<InputKey>();
        private readonly HostProposalHealth _hostHealth=new HostProposalHealth();
        private int _raidRejectLogs;
        private void LogRaidRejection(string message)
        { if(_raidRejectLogs<32){_raidRejectLogs++;Console.WriteLine(message);} }
        public bool UsesRaidCore { get; private set; }

        public bool ConfigureMatchedRaid(IReadOnlyList<ClientSession> roster)
        {
            lock (_updateGate)
            {
                if (roster == null || roster.Count < 1 || roster.Count > 4 || roster.Any(s => s?.MyPlayer == null || s.RaidProtocolVersion < 1) || roster.Select(s=>s.RaidProtocolVersion).Distinct().Count()!=1 ||
                    roster.Select(s => s.MyPlayer.Id).Distinct().Count() != roster.Count)
                    throw new InvalidOperationException("Raid requires one to four distinct negotiated participants");
                _raidRoster = roster.ToArray();
                if(!InitializeParticipation())return false;
                UsesRaidCore = true;
                _raidWaitStarted = Environment.TickCount64;
                return true;
            }
        }

        private bool IsRaidMember(Server.Game.Object.Player player) => !_raidEnded && player != null &&
            _raidRoster != null && _raidRoster.Any(s => ReferenceEquals(s.MyPlayer, player) && ReferenceEquals(player.Session, s));

        private void UpdateRaid()
        {
            if (!UsesRaidCore || _raidEnded) return;
            if (UpdateRaidLifecycle()) return;
            if (_raid == null)
            {
                if (_raidRoster.Any(s => s.MyPlayer?.Room != this))
                {
                    if (Environment.TickCount64 - _raidWaitStarted > 30000) EndRaid("SceneReadyTimeout");
                    return;
                }
                if (_enemys.Count != 1) { EndRaid("BossRosterInvalid"); return; }
                bool content = _raidRoster[0].RaidProtocolVersion == 3;
                var timing = content ? RaidContentRules.Timing() : IntegrationRules.Timing();
                var initial = new CombatState();
                foreach (var player in _players.Values)
                    initial.Fighters.Add(player.Id, new FighterState { Id = player.Id, X = 0,
                        Hp = (int)player.HP, MaxHp = (int)player.MaxHP });
                var boss = _enemys.Values.Single();
                initial.Fighters.Add(boss.Id, new FighterState { Id = boss.Id, IsBoss = true, X = -2000,
                    Hp = (int)boss.HP, MaxHp = (int)boss.MaxHP });
                if (content) initial.Content = new RaidContentState(_raidRoster.Select(s=>s.MyPlayer.Id).ToArray(),boss.Id,playIntro:true);
                if(content) for(int slot=0;slot<_raidRoster.Length;slot++) {
                    var owner=_raidRoster[slot].MyPlayer;owner.RefreshAdditionalStat();
                    var fighter=initial.Content.Simulation.Players[slot];
                    fighter.Look=owner.Info.Appearance;
                    fighter.Name=owner.Info.Name;
                    fighter.PhysicalAttack=owner.PhysicalAttack;
                    fighter.MoveSpeedScale=owner.MoveSpeedScale;fighter.AttackSpeedScale=owner.AttackSpeedScale;
                    fighter.EquipmentDefence=owner.ArmorDefence;
                    // A fresh encounter must not advertise dead HP from the previous raid.
                    initial.Fighters[owner.Id].Hp=(int)fighter.Hp;
                    initial.Fighters[owner.Id].MaxHp=(int)fighter.MaxHp;
                }
                if(content){initial.Fighters[boss.Id].Hp=(int)initial.Content.Simulation.Boss.Hp;initial.Fighters[boss.Id].MaxHp=(int)initial.Content.Simulation.Boss.MaxHp;}

                long encounter = Interlocked.Increment(ref _nextEncounter);
                _raid = new RaidCombatSession(encounter, _raidRoster[0].MyPlayer.Id, 1, timing, content ? RaidContentRules.Create() : IntegrationRules.Create(), initial);
                foreach (var session in _raidRoster)
                {
                    // Start with receive-time fallback; only server-issued measured leases enable backdating.
                    var binding = new SessionBinding(session.MyPlayer.Id, 1, 1, 0, false);
                    _raidBindings.Add(binding.Player, binding);
                    _raid.BindAuthenticatedParticipant(binding);
                }
                foreach (var session in _raidRoster)
                {
                    session.RaidRoute = new RaidIngress(this, _raid, _raidBindings[session.MyPlayer.Id],session.RaidClockSync);
                    var start = new S_RaidStart { Version = content ? 3 : 1, Encounter = encounter, Self = session.MyPlayer.Id,
                        Host = _raid.Engine.HostPlayer, HostEpoch = 1, ClockVersion = 1, ReceiveTimeOnly = true, ClockSync=session.RaidClockSync,
                        TickUs = timing.TickUs, InputGraceUs = timing.InputGraceUs,
                        ProposalGraceUs = timing.ProposalGraceUs, FinalizationDelayUs = timing.FinalizationDelayUs,
                        Ruleset = content ? RaidContentRules.Id : IntegrationRules.Id };
                    if (content) { start.Content = Google.Protobuf.ByteString.CopyFrom(RaidSnapshotCodec.Encode(initial.Content.Simulation)); start.PlayerSlots.AddRange(initial.Content.PlayerIds); }
                    start.Fighters.AddRange(initial.Fighters.Values.OrderBy(f => f.Id).Select(ToWire));
                    session.Send(start);
                }
                Console.WriteLine($"[RAID_CORE] Started Encounter={encounter} Room={RoomId} Host={_raid.Engine.HostPlayer} Players={_raidRoster.Length}");
            }
            foreach(var session in _raidRoster) session.RaidRoute?.PollClock(session);
            _raid.UpdateOnRoomThread();
            if (_raid.Engine.IsAborted) { EndRaid(_raid.Engine.AbortReason); return; }
            foreach (var receipt in _raid.Engine.DrainInputReceipts())
            {
                if(receipt.Result!=Admission.Admitted && receipt.Result!=Admission.Duplicate) {
                    var mapping=_raidBindings.TryGetValue(receipt.Input.Key.Player,out var binding)?binding.CaptureClock(receipt.Input.ClockVersion):null;
                    LogRaidRejection($"[RAID_INPUT_REJECT] key={receipt.Input.Key} result={receipt.Result} receivedUs={receipt.ReceivedUs} clientUs={receipt.Input.ClientTimeUs} tick={receipt.EffectiveTick} clock={receipt.Input.ClockVersion} fallback={receipt.UsedReceiveTime} mappedAgeUs={(mapping==null?-1:receipt.ReceivedUs-receipt.Input.ClientTimeUs-mapping.OffsetUs)} allowanceUs={mapping?.MaxBackdateUs??0}");
                }
                var i = receipt.Input;
                if (receipt.Result == Admission.Admitted && (i.Kind == InputKind.Attack || i.Kind == InputKind.Controls)) _awaitingHost.Add(i.Key);
                var wire = new S_RaidInput { Input = new C_RaidInput { Key = ToWire(i.Key), ClientTimeUs = i.ClientTimeUs,
                    ClockVersion = i.ClockVersion, Kind = (int)i.Kind, Value = i.Value, MoveX = i.MoveX, MoveY = i.MoveY, Buttons = i.Buttons }, Result = (int)receipt.Result,
                    OriginalResult = (int)receipt.OriginalResult, ReceivedUs = receipt.ReceivedUs,
                    EffectiveTick = receipt.EffectiveTick, UsedReceiveTime = receipt.UsedReceiveTime };
                // Only admitted input is relayed to the host. Rejections are also observable for diagnostics.
                Broadcast(wire);
            }
            foreach (var receipt in _raid.Engine.DrainProposalReceipts())
            {
                if(receipt.Result!=Admission.Admitted && receipt.Result!=Admission.Duplicate)
                    LogRaidRejection($"[RAID_PROPOSAL_REJECT] id={receipt.Proposal.Id} result={receipt.Result} receivedUs={receipt.ReceivedUs} tick={receipt.Proposal.StartTick} actor={receipt.Proposal.Actor} definition={receipt.Proposal.Definition} origin={receipt.Proposal.Origin}");
                Broadcast(new S_RaidProposalResult { Encounter = _raid.Engine.EncounterId,
                    Sequence = receipt.Proposal.Sequence, Result = (int)receipt.Result });
            }
            var events = _raid.DrainFinalEventsOnRoomThread();
            foreach (var e in events)
            {
                if (!e.Origin.HasValue) continue;
                if (e.Kind == "ActionCommitted" && _awaitingHost.Remove(e.Origin.Value)) _hostHealth.Progress(e.EvaluatedTick*(_raidRoster[0].RaidProtocolVersion==3?RaidContentRules.Timing().TickUs:IntegrationRules.Timing().TickUs));
                else if ((e.Kind == "InputRejected"||e.Kind=="ActionRejected") && _awaitingHost.Remove(e.Origin.Value)) _hostHealth.Omitted();
            }
            var state = _raid.Engine.FinalSnapshot;
            if (state.Tick == _lastRaidFrameTick && events.Length == 0) return;
            _lastRaidFrameTick = state.Tick;
            var frame = new S_RaidFrame { Encounter = _raid.Engine.EncounterId, FinalizedTick = state.Tick,
                ServerTimeUs = _raid.NowUs };
            foreach (var fighter in state.Fighters.Values.OrderBy(f => f.Id))
            {
                frame.Fighters.Add(ToWire(fighter));
                if (_players.TryGetValue(fighter.Id, out var player)) player.ApplyRaidState(fighter.Hp, fighter.MaxHp, fighter.X / 1000f);
                if (_enemys.TryGetValue(fighter.Id, out var enemy)) enemy.ApplyRaidState(fighter.Hp, fighter.MaxHp, fighter.X / 1000f);
            }
            foreach (var e in events)
                frame.Events.Add(new RaidEvent { Sequence = e.Sequence, EvaluatedTick = e.EvaluatedTick,
                    Kind = e.Kind, Key = e.Key, Origin = e.Origin.HasValue ? ToWire(e.Origin.Value) : null,
                    Target = e.Target, Damage = e.Damage, HpAfter = e.HpAfter });
            if (state.Content != null) frame.Content = Google.Protobuf.ByteString.CopyFrom(RaidSnapshotCodec.Encode(state.Content.Simulation));
            // Keep packets inside the transport's ushort frame limit; fail closed before an oversized send.
            if (frame.CalculateSize() > 60000) { EndRaid("FrameCapacityExceeded"); return; }
            Broadcast(frame);
            if (state.Content != null)
            {
                var preview = _raid.Engine.PreviewSnapshot.Content.Simulation;
                var plan = new S_RaidFrame { Encounter=frame.Encounter, FinalizedTick=frame.FinalizedTick, ServerTimeUs=frame.ServerTimeUs,
                    Preview=Google.Protobuf.ByteString.CopyFrom(RaidSnapshotCodec.Encode(preview)) };
                if (plan.CalculateSize() > 60000) { EndRaid("PreviewCapacityExceeded");return; }
                _raidRoster[0].Send(plan);
                if (events.Any(e=>e.Kind=="ContentPatternMissing"||e.Kind=="ContentRoomPatternMissing")) { EndRaid("HostPatternProposalMissing");return; }
                if (state.Content.Simulation.Mode==DnfRaidDemo.EncounterMode.Defeat) { EndRaid("Defeat");return; }
            }
            if (_hostHealth.Failed(state.Tick*(state.Content!=null?RaidContentRules.Timing().TickUs:IntegrationRules.Timing().TickUs))) { EndRaid("HostProposalOmissions"); return; }
            if (state.Fighters.Values.Where(f => f.IsBoss).All(f => f.Hp <= 0)) BeginRaidClear();
        }

        private RaidFighter ToWire(FighterState f) => new RaidFighter { Id = f.Id, X = f.X, Hp = f.Hp,
            MaxHp = f.MaxHp, JumpHeight = f.JumpHeight, IsBoss = f.IsBoss,
            Epoch = _raidBindings.TryGetValue(f.Id, out var b) ? b.Epoch : 0 };
        private static RaidInputKey ToWire(InputKey k) => new RaidInputKey { Encounter = k.Encounter,
            Player = k.Player, Epoch = k.Epoch, Sequence = k.Sequence };

        // Called on the room queue; an interrupted validation run never grants rewards.
        internal void OnRaidDisconnected(ClientSession session)
        {
            session.RaidRoute = null;
            int slot=Array.IndexOf(_raidRoster,session);
            if(_raidEnded)return;
            for(int i=0;i<_raidResuming.Length;i++)if(_raidResuming[i]==session){_raidResuming[i]=null;session.ClearPendingTransfer();session.RaidResumePending=false;return;}
            if(slot<0)return;
            if(_raidClearWaiting)return; // Eligibility was frozen when the server finalized the clear.
            if(_raid==null || slot==0){EndRaid("HostDisconnected");return;}
            _participation.Disconnect(slot,session.DisconnectedAtMs);
            _raid.Engine.Revoke(_raidPlayers[slot].Id);
            _raid.Engine.SetPresence(_raidPlayers[slot].Id,false,false);
        }
        public void RequestRaidReturn(ClientSession session, long encounter)
        {
            int slot=Array.IndexOf(_raidRoster,session);
            if(_raidEnded||_raid==null||slot<0||session.MyPlayer?.Room!=this||_raid.Engine.EncounterId!=encounter)return;
            if(_raidClearWaiting){if(_raidRewardSaved && _clearCinemaReleased)ReturnClearedParticipant(slot);return;}
            if(slot==0){EndRaid("PartyReturn_NoRewards");return;}
            if(_participation.Leave(slot)){
                RemoveRaidParticipant(slot);
                session.Send(new S_RaidEnded{Encounter=encounter,Reason="VoluntaryLeave"});
                RoomTransferService.Instance.StartRaidReturnToTownTransfer(this,new[]{session});
            }
        }
        private void EndRaid(string reason)
        {
            if (_raidEnded) return;
            _raidEnded = true;
            var endedState=_raid?.Engine.FinalSnapshot;
            Console.WriteLine($"[RAID_ENDED] encounter={_raid?.Engine.EncounterId??0} room={RoomId} reason={reason} tick={endedState?.Tick??0} hostMissing={_hostHealth.Missing} lastHostProgressUs={_hostHealth.LastProgressUs}");
            _raid?.AbortOnRoomThread(reason);
            State = "RaidEnded";
            foreach (var session in _raidRoster.Where((s,i)=>!_raidResultsReturned[i] && _participation.Members[i].State!=RaidParticipation.Status.Left && _participation.Members[i].State!=RaidParticipation.Status.Expired))
            {
                RaidResumeRegistry.Remove(session.MyPlayer.PlayerDbId,this);
                session.RaidRoute = null;
                session.Send(new S_RaidEnded { Encounter = _raid?.Engine.EncounterId ?? 0, Reason = reason });
            }
            foreach(var pending in _raidResuming.Where(s=>s!=null)){pending.RaidResumePending=false;pending.ClearPendingTransfer();pending.Send(new S_RaidResume{Status="EncounterEnded"});}
            RoomTransferService.Instance.StartRaidReturnToTownTransfer(this, _raidRoster.Where((s,i)=>!_raidResultsReturned[i] && _participation.Members[i].State!=RaidParticipation.Status.Left && _participation.Members[i].State!=RaidParticipation.Status.Expired).ToArray());
        }
    }
}
