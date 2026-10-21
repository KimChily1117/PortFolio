using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using Google.Protobuf;
using Google.Protobuf.Protocol;
using ProjectDawn.Combat;
using DnfRaidDemo;

namespace ProjectDawn.Network
{
    // Runs on the client's dispatch thread; Send must write to the existing ordered TCP transport.
    // A Unity adapter can bind Frame/Committed to rendering without giving it HP authority.
    public sealed class RaidNetworkClient
    {
        private readonly Stopwatch monotonic=Stopwatch.StartNew();
        public Func<long> ReadClockUs {get;set;}
        public long NowUs => ReadClockUs!=null?ReadClockUs():(long)(monotonic.ElapsedTicks*(1000000.0/Stopwatch.Frequency));
        public bool EnableClockSync {get;set;}=true;
        public S_RaidClockState ClockState {get;private set;}
        public bool ClockTrusted => ClockState!=null && ClockState.Trusted && NowUs+ClockState.OffsetUs<ClockState.ExpiresUs;
        public event Action<S_RaidClockState> ClockChanged;
        public event Action<S_RaidInput> InputReceived;
        public Action<IMessage> Send { get; set; }
        public event Action<S_RaidStart> Started;
        public event Action<S_RaidFrame> Frame;
        public event Action<RaidEvent> Committed;
        public event Action<S_RaidEnded> Ended;
        public S_RaidStart Start { get; private set; }
        public S_RaidFrame Latest { get; private set; }
        public bool IsHost => Start != null && Start.Host == Start.Self;
        public LuaPatternPlanner PatternPlanner {get;set;}
        public bool AutoPropose { get; set; } = true;
        private long _inputSequence, _proposalSequence, _eventSequence;
        private readonly HashSet<string> _proposed = new HashSet<string>();
        public void Negotiate(int version=1) => Send(new C_RaidHello { Version = version, ClockSync=EnableClockSync });
        public C_RaidInput Controls(PlayerCommand command)
        {
            if(Start==null || Start.Version!=3) throw new InvalidOperationException("Content encounter not started");
            var self=Start.Fighters.Single(f=>f.Id==Start.Self);
            var packet=new C_RaidInput {Key=new RaidInputKey {Encounter=Start.Encounter,Player=Start.Self,Epoch=self.Epoch,Sequence=++_inputSequence},
                ClientTimeUs=NowUs,ClockVersion=ClockState?.Version??Start.ClockVersion,Kind=(int)InputKind.Controls,Value=command.Skills,
                MoveX=(int)Math.Max(-1000,Math.Min(1000,command.Move.x*1000)),MoveY=(int)Math.Max(-1000,Math.Min(1000,command.Move.y*1000)),
                Buttons=(command.Basic?1:0)|(command.Run?2:0)|(command.Jump?4:0)|(command.Dash?8:0)|(command.Revive?16:0)|(command.Potion?32:0)};
            Send(packet);return packet;
        }
        public C_RaidInput Input(InputKind kind, int value = 0)
        {
            if (Start == null) throw new InvalidOperationException("Raid not started");
            var self = Start.Fighters.Single(f => f.Id == Start.Self);
            var packet = new C_RaidInput {
                Key = new RaidInputKey { Encounter = Start.Encounter, Player = Start.Self,
                    Epoch = self.Epoch, Sequence = ++_inputSequence },
                ClientTimeUs=NowUs, ClockVersion = ClockState?.Version??Start.ClockVersion, Kind = (int)kind, Value = value
            };
            Send(packet);
            return packet;
        }
        public bool Handle(IMessage message)
        {
            if(message is S_RaidClockChallenge challenge)
            {
                if(Start==null || !Start.ClockSync || challenge.Encounter!=Start.Encounter ||
                    challenge.Epoch!=Start.Fighters.Single(f=>f.Id==Start.Self).Epoch || challenge.Token.Length!=32) return true;
                long received=NowUs;
                Send(new C_RaidClockResponse {Encounter=Start.Encounter,Epoch=challenge.Epoch,Token=challenge.Token,
                    ClientReceivedUs=received,ClientSentUs=NowUs});return true;
            }
            if(message is S_RaidClockState clock)
            {
                if(Start!=null && Start.ClockSync && clock.Encounter==Start.Encounter && clock.Version>(ClockState?.Version??Start.ClockVersion))
                {ClockState=clock.Clone();ClockChanged?.Invoke(ClockState.Clone());}
                return true;
            }
            if (message is S_RaidStart start)
            {
                if (Start != null && Start.Encounter == start.Encounter && Start.Fighters.Single(f=>f.Id==Start.Self).Epoch >= start.Fighters.Single(f=>f.Id==start.Self).Epoch) return true;
                if (((start.Version != 1 || start.Ruleset != IntegrationRules.Id) && (start.Version != 3 || start.Ruleset != RaidContentRules.Id)) || start.Fighters.Count(f => !f.IsBoss) < 1 || start.Fighters.Count(f => !f.IsBoss) > 4)
                    throw new InvalidOperationException("Unsupported raid contract");
                if(start.Version==3 && (start.PlayerSlots.Count!=start.Fighters.Count(f=>!f.IsBoss)||
                    start.PlayerSlots.Distinct().Count()!=start.PlayerSlots.Count||!start.PlayerSlots.Contains(start.Self)||!start.PlayerSlots.Contains(start.Host)||
                    start.PlayerSlots.Any(id=>!start.Fighters.Any(f=>f.Id==id&&!f.IsBoss))||
                    RaidSnapshotCodec.Decode(start.Content.ToByteArray()).Players.Count!=start.PlayerSlots.Count))
                    throw new InvalidOperationException("Invalid raid slot mapping");
                Start = start.Clone(); Latest = null; ClockState=null; _inputSequence = _proposalSequence = 0; _eventSequence = start.EventSequence;
                _proposed.Clear(); Started?.Invoke(Start.Clone()); return true;
            }
            if (message is S_RaidInput receipt)
            {
                if(Start!=null && receipt.Input?.Key?.Encounter==Start.Encounter) InputReceived?.Invoke(receipt.Clone());
                if (Start == null || receipt.Input?.Key?.Encounter != Start.Encounter || !IsHost || !AutoPropose ||
                    receipt.Result != (int)Admission.Admitted || (receipt.Input.Kind != (int)InputKind.Attack && receipt.Input.Kind != (int)InputKind.Controls)) return true;
                var key = receipt.Input.Key;
                if (_proposed.Count >= 65536) throw new InvalidOperationException("Host action ledger full");
                if (!_proposed.Add($"{key.Player}/{key.Epoch}/{key.Sequence}")) return true;
                Send(new C_RaidProposal { Encounter = Start.Encounter, HostEpoch = Start.HostEpoch,
                    Sequence = ++_proposalSequence, StartTick = receipt.EffectiveTick,
                    Actor = key.Player, Definition = receipt.Input.Kind==(int)InputKind.Controls?100:receipt.Input.Value, Origin = key.Clone() });
                return true;
            }
            if (message is S_RaidFrame frame)
            {
                if (Start == null || frame.Encounter != Start.Encounter) return true;
                if(frame.Preview.Length>0)
                {
                    if(IsHost && AutoPropose) {
                        var preview=RaidSnapshotCodec.Decode(frame.Preview.ToByteArray());
                        ProposeRoomMarks(preview.Clone());ProposePatterns(preview);
                    }
                    return true;
                }
                if (Latest != null && frame.FinalizedTick < Latest.FinalizedTick) return true;
                foreach (var ev in frame.Events)
                {
                    if (ev.Sequence <= _eventSequence) continue;
                    if (ev.Sequence != _eventSequence + 1) throw new InvalidOperationException("Final event gap: reconnect required");
                    _eventSequence = ev.Sequence;
                    if(IsHost && Start.Version==3 && (ev.Kind=="ContentPattern"||ev.Kind=="ContentRoomPattern")) PatternPlanner?.Confirm(ev.Damage,ev.EvaluatedTick);
                    Committed?.Invoke(ev.Clone());
                }
                Latest = frame.Clone(); Frame?.Invoke(frame.Clone()); return true;
            }
            if (message is S_RaidEnded ended)
            {
                if (Start?.Encounter == ended.Encounter) { Start = null; ClockState=null; _proposed.Clear(); Ended?.Invoke(ended.Clone()); }
                return true;
            }
            return message is S_RaidHello || message is S_RaidProposalResult;
        }
        private void ProposeRoomMarks(RaidSimulation simulation)
        {
            if(!simulation.RoomsEnabled)return;
            for(int i=0;i<13;i++) {
                if(simulation.PendingRoomMarkTick>0) {
                    string key=$"room/{simulation.PendingRoomMarkTick}/{simulation.RoomSelectionRoll}/{simulation.RoomSelectionAliveMask}/{simulation.RoomSelectionPreviousMarkMask}";
                    if(_proposed.Count>=65536)throw new InvalidOperationException("Host action ledger full");
                    if(_proposed.Add(key)) {
                        if(PatternPlanner==null)PatternPlanner=new LuaPatternPlanner();
                        var plan=PatternPlanner.RequestRoom(simulation);
                        Send(new C_RaidProposal {Encounter=Start.Encounter,HostEpoch=Start.HostEpoch,Sequence=++_proposalSequence,
                            Actor=Start.Fighters.Single(f=>f.IsBoss).Id,Definition=201+plan.Pattern,StartTick=plan.Tick,Program=plan.Program});
                    }
                    return;
                }
                simulation.NetworkTick(Array.Empty<PlayerCommand>(),-1);
            }
        }
        private void ProposePatterns(RaidSimulation simulation)
        {
            // Calculate the schedule ahead using the same rules/seed; the server independently checks it at the proposed tick.
            for(int i=0;i<13;i++)
            {
                if(simulation.PendingPattern>=0)
                {
                    // Preview is rollbackable: a late admitted input can change the selection
                    // context without changing its start tick/pattern. Resend that new context
                    // under a fresh sequence; C# rejects the obsolete program and accepts the match.
                    string key=$"boss/{simulation.PendingPatternTick}/{simulation.PendingPattern}/{simulation.SelectionOrdinal}/{simulation.SelectionRoll}/{simulation.SelectionPhase}/{simulation.SelectionPrevious}/{simulation.SelectionAliveMask}/{simulation.SelectionPreviousMarkMask}";
                    if(_proposed.Count>=65536)throw new InvalidOperationException("Host action ledger full");
                    if(_proposed.Add(key)) {
                        if(PatternPlanner==null)PatternPlanner=new LuaPatternPlanner();
                        var plan=PatternPlanner.Request(simulation);
                        Send(new C_RaidProposal {Encounter=Start.Encounter,HostEpoch=Start.HostEpoch,
                        Sequence=++_proposalSequence,Actor=Start.Fighters.Single(f=>f.IsBoss).Id,
                        Definition=201+plan.Pattern,StartTick=plan.Tick,Program=plan.Program}); }
                    return;
                }
                simulation.NetworkTick(Array.Empty<PlayerCommand>(),-1);
            }
        }
    }
}
