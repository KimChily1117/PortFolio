#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectDawn.Combat
{
    // Receive methods may be called by network threads. Pump and binding changes belong to the room thread.
    // Time is stamped inside the same lock that enqueues; draining that lock yields a trustworthy watermark.
    public sealed class EncounterEngine
    {
        private sealed class Envelope
        {
            public long Received;
            public SessionBinding Session = null!;
            public ClockMapping? Clock;
            public PlayerInput? Input;
            public HostProposal? Proposal;
        }
        private readonly object _inboxLock = new object();
        private readonly Queue<Envelope> _inbox = new Queue<Envelope>();
        private readonly Func<long> _clock;
        private readonly TimingPolicy _timing;
        private readonly CombatRules _rules;
        private readonly Dictionary<int, SessionBinding> _sessions = new Dictionary<int, SessionBinding>();
        private readonly Dictionary<InputKey, InputReceipt> _records = new Dictionary<InputKey, InputReceipt>();
        private readonly Dictionary<string, (HostProposal Proposal, Admission Result)> _proposals = new Dictionary<string, (HostProposal, Admission)>();
        private readonly Queue<InputReceipt> _receipts = new Queue<InputReceipt>();
        private readonly Queue<(HostProposal Proposal, Admission Result, long ReceivedUs)> _proposalReceipts = new Queue<(HostProposal, Admission, long)>();
        private readonly Queue<CombatEvent> _committed = new Queue<CombatEvent>();
        private CombatState _final, _preview;
        private long _lastReceived, _lastPump, _eventSequence;
        private bool _overflow;
        public long EncounterId { get; }
        public int HostPlayer { get; }
        public long HostEpoch { get; }
        public long FinalizedThroughTick => _final.Tick;
        public long CurrentTick => _preview.Tick;
        public long EventSequence => _eventSequence;
        private readonly SortedDictionary<long, Dictionary<int, (bool Connected, bool Departed)>> _presence = new SortedDictionary<long, Dictionary<int, (bool, bool)>>();
        public void Revoke(int player) => _sessions.Remove(player);
        // Server lifecycle changes enter the same replay timeline as combat, never rewrite finalized ticks.
        public void SetPresence(int player, bool connected, bool departed)
        {
            if (IsAborted || !_final.Fighters.ContainsKey(player)) return;
            long tick = Math.Max(CurrentTick, _clock() / _timing.TickUs) + 1;
            if (!_presence.TryGetValue(tick, out var changes)) _presence[tick] = changes = new Dictionary<int, (bool, bool)>();
            changes[player] = (connected, departed);
        }
        private void ApplyPresence(CombatState work, long tick)
        {
            if (!_presence.TryGetValue(tick, out var changes)) return;
            foreach (var pair in changes)
            {
                if (work.Content != null)
                {
                    var c = work.Content; var p = c.Simulation.Players[c.Slot(pair.Key)];
                    p.Disconnected = !pair.Value.Connected; p.Departed = pair.Value.Departed;
                    c.Held.Remove(pair.Key); c.Lease.Remove(pair.Key);
                    if (p.Departed) { p.Hp = 0; p.AttackTime = p.ActionDuration = p.DashTime = 0;
                        c.Simulation.Zones.RemoveAll(z => z.FromPlayer && z.OwnerId == p.Id); }
                }
                if (pair.Value.Departed) work.Fighters[pair.Key].Hp = 0;
            }
        }
        public string? AbortReason { get; private set; }
        public bool IsAborted => AbortReason != null;
        public int ReplayPasses { get; private set; }
        public int ReplayTicksLastPump { get; private set; }
        public CombatState FinalSnapshot => _final.Clone();
        public CombatState PreviewSnapshot => _preview.Clone();

        public EncounterEngine(long encounterId, int hostPlayer, long hostEpoch, TimingPolicy timing,
            CombatRules rules, CombatState initial, Func<long> monotonicClockUs)
        {
            if (encounterId <= 0 || hostEpoch <= 0 || initial.Tick != 0 ||
                !initial.Fighters.TryGetValue(hostPlayer, out var host) || host.IsBoss ||
                initial.Fighters.Values.Count(x => !x.IsBoss) < 1 || initial.Fighters.Values.Count(x => !x.IsBoss) > 4)
                throw new ArgumentException("A new one-to-four-player encounter and a roster host are required.");
            EncounterId = encounterId; HostPlayer = hostPlayer; HostEpoch = hostEpoch;
            _timing = timing; _rules = rules; _clock = monotonicClockUs;
            _final = initial.Clone(); _preview = initial.Clone();
        }

        public void Bind(SessionBinding session)
        {
            if (!_final.Fighters.TryGetValue(session.Player, out var p) || p.IsBoss || session.Epoch < 1 ||
                session.ClockVersion < 1) throw new ArgumentException("Session must belong to the starting roster.");
            if (_sessions.TryGetValue(session.Player, out var previous) && session.Epoch <= previous.Epoch)
                throw new InvalidOperationException("Rebinding must revoke the previous connection epoch.");
            _sessions[session.Player] = session;
        }

        public bool ReceiveInput(SessionBinding authenticatedSession, PlayerInput input) =>
            Enqueue(new Envelope { Session = authenticatedSession, Input = input });
        public bool ReceiveProposal(SessionBinding authenticatedSession, HostProposal proposal) =>
            Enqueue(new Envelope { Session = authenticatedSession, Proposal = proposal });

        private bool Enqueue(Envelope envelope)
        {
            lock (_inboxLock)
            {
                long now = _clock();
                if (now < 0 || now < _lastReceived) { _overflow = true; return false; }
                _lastReceived = now;
                if (_inbox.Count >= _timing.MaxPending) { _overflow = true; return false; }
                envelope.Received = now;
                if(envelope.Input.HasValue) envelope.Clock=envelope.Session.CaptureClock(envelope.Input.Value.ClockVersion);
                _inbox.Enqueue(envelope);
                return true;
            }
        }

        public void Abort(string reason)
        {
            if (IsAborted) return;
            AbortReason = reason;
            _preview = _final.Clone();
        }

        private bool Active(SessionBinding binding) => _sessions.TryGetValue(binding.Player, out var active) &&
            ReferenceEquals(binding, active);

        public void Pump()
        {
            List<Envelope> incoming;
            long watermark;
            lock (_inboxLock)
            {
                watermark = _clock();
                if (_overflow || watermark < _lastReceived || watermark < _lastPump)
                    Abort("Ingress capacity or monotonic-clock violation");
                incoming = _inbox.ToList(); _inbox.Clear();
            }
            if (IsAborted) return;
            _lastPump = watermark;
            foreach (var envelope in incoming)
            {
                if (_records.Count + _proposals.Count >= _timing.MaxRecords)
                { Abort("Encounter command ledger capacity exceeded"); return; }
                if (envelope.Input.HasValue) AdmitInput(envelope);
                else AdmitProposal(envelope);
                if (_receipts.Count + _proposalReceipts.Count >= _timing.MaxRecords)
                { Abort("Undrained response capacity exceeded"); return; }
            }

            long current = watermark / _timing.TickUs;
            long span = current - _final.Tick;
            if (span < 0 || span > _timing.MaxReplayTicks)
            { Abort("Bounded replay budget exceeded"); return; }

            var inputs = _records.Values.Where(x => x.Result == Admission.Admitted && x.EffectiveTick > _final.Tick)
                .GroupBy(x => x.EffectiveTick).ToDictionary(x => x.Key, x => (IReadOnlyList<InputReceipt>)x.ToArray());
            var proposals = _proposals.Values.Where(x => x.Result == Admission.Admitted && x.Proposal.StartTick > _final.Tick)
                .Select(x => x.Proposal).GroupBy(x => x.StartTick).ToDictionary(x => x.Key, x => (IReadOnlyList<HostProposal>)x.ToArray());
            var work = _final.Clone();
            ReplayTicksLastPump = checked((int)span);
            ReplayPasses++;
            for (long tick = _final.Tick + 1; tick <= current; tick++)
            {
                ApplyPresence(work, tick);
                inputs.TryGetValue(tick, out var tickInputs);
                proposals.TryGetValue(tick, out var tickProposals);
                var events = CombatStep.Run(work, tick, tickInputs ?? Array.Empty<InputReceipt>(),
                    tickProposals ?? Array.Empty<HostProposal>(), _rules);
                // The inbox watermark covers both input and proposal deadlines, including all queued envelopes.
                if (watermark >= _timing.FinalDeadline(tick))
                {
                    _final = work.Clone();
                    foreach (var ev in events) _committed.Enqueue(ev.Commit(++_eventSequence));
                    if (_committed.Count >= _timing.MaxRecords)
                    { Abort("Undrained final event capacity exceeded"); return; }
                }
            }
            _preview = work;
        }

        private void AdmitInput(Envelope envelope)
        {
            PlayerInput input = envelope.Input!.Value;
            Admission result = Admission.Admitted;
            long tick = 0;
            bool fallback = false;
            if (input.Key.Encounter != EncounterId) result = Admission.WrongEncounter;
            else if (!Active(envelope.Session) || input.Key.Player != envelope.Session.Player || input.Key.Epoch != envelope.Session.Epoch)
                result = Admission.WrongSession;
            else if (_records.TryGetValue(input.Key, out var original))
            {
                _receipts.Enqueue(new InputReceipt(input, input.Equals(original.Input) ? Admission.Duplicate : Admission.Conflict,
                    original.ReceivedUs, original.EffectiveTick, original.UsedReceiveTime, original.Result));
                return;
            }
            else if (input.Key.Sequence < 1 || input.ClientTimeUs < 0 || !Enum.IsDefined(typeof(InputKind), input.Kind) || (_final.Content != null && input.Kind != InputKind.Controls) ||
                (input.Kind == InputKind.Controls && (_final.Content == null || input.Value < 0 || input.Value >= (1 << 21) || input.MoveX < -1000 || input.MoveX > 1000 || input.MoveY < -1000 || input.MoveY > 1000 || input.Buttons < 0 || input.Buttons > 63)) ||
                (input.Kind == InputKind.Move && (input.Value < -1 || input.Value > 1)) ||
                (input.Kind == InputKind.Jump && input.Value != 0) ||
                (input.Kind == InputKind.Attack && (!_rules.TryAttack(input.Value, out var attack) || attack.FromBoss))) result = Admission.Invalid;
            else if (envelope.Clock == null) result = Admission.ClockMismatch;
            else
            {
                try
                {
                    var clock=envelope.Clock!;
                    fallback = !clock.Trusted || envelope.Received >= clock.ExpiresUs;
                    long mapped = fallback ? envelope.Received : checked(input.ClientTimeUs + clock.OffsetUs);
                    if (mapped < 0) result = Admission.Invalid;
                    else if (mapped > checked(envelope.Received + Math.Max(_timing.FutureToleranceUs, clock.UncertaintyUs))) result = Admission.FutureTimestamp;
                    else if (!fallback && envelope.Received - mapped > clock.MaxBackdateUs) result = Admission.TooLate;
                    else
                    {
                        mapped = Math.Min(mapped, envelope.Received);
                        tick = _timing.TickAt(mapped);
                        // Switching estimates may only move a later input forward, never behind an accepted input.
                        foreach(var prior in _records.Values.Where(x=>x.Result==Admission.Admitted && x.Input.Key.Player==input.Key.Player &&
                            x.Input.Key.Epoch==input.Key.Epoch && x.Input.Key.Sequence<input.Key.Sequence &&
                            x.Input.ClockVersion!=input.ClockVersion && x.Input.ClientTimeUs<=input.ClientTimeUs))
                            tick=Math.Max(tick,prior.EffectiveTick);
                        if (tick <= _final.Tick || envelope.Received >= _timing.InputDeadline(tick)) result = Admission.TooLate;
                        else
                        {
                            foreach (var other in _records.Values.Where(x => x.Result == Admission.Admitted &&
                                x.Input.Key.Player == input.Key.Player && x.Input.Key.Epoch == input.Key.Epoch))
                            {
                                bool earlier = other.Input.Key.Sequence < input.Key.Sequence;
                                if ((earlier && (other.Input.ClientTimeUs > input.ClientTimeUs || other.EffectiveTick > tick)) ||
                                    (!earlier && (other.Input.ClientTimeUs < input.ClientTimeUs || other.EffectiveTick < tick)))
                                { result = Admission.SequenceTimeConflict; break; }
                            }
                        }
                    }
                }
                catch (OverflowException) { result = Admission.Invalid; }
            }
            var receipt = new InputReceipt(input, result, envelope.Received, tick, fallback);
            // Unauthenticated keys cannot reserve another player's legitimate input identity.
            if (result != Admission.WrongEncounter && result != Admission.WrongSession) _records.Add(input.Key, receipt);
            _receipts.Enqueue(receipt);
        }

        private void AdmitProposal(Envelope envelope)
        {
            var p = envelope.Proposal!.Value;
            Admission result = Admission.Admitted;
            if (p.Encounter != EncounterId) result = Admission.WrongEncounter;
            else if (!Active(envelope.Session) || envelope.Session.Player != HostPlayer || p.HostEpoch != HostEpoch)
                result = Admission.WrongHost;
            else if (_proposals.TryGetValue(p.Id, out var original))
            { _proposalReceipts.Enqueue((p, p.Equals(original.Proposal) ? Admission.Duplicate : Admission.Conflict, envelope.Received)); return; }
            else if (p.Sequence < 1 || p.StartTick < 1 || !_final.Fighters.TryGetValue(p.Actor, out var actor) ||
                !_rules.TryAttack(p.Definition, out var def) || def.FromBoss != actor.IsBoss ||
                (actor.IsBoss && p.Origin.HasValue) || (!actor.IsBoss && (!p.Origin.HasValue ||
                    p.Origin.Value.Player != p.Actor || p.Origin.Value.Encounter != EncounterId))) result = Admission.Invalid;
            else
            {
                try
                {
                    long receiveTick = _timing.TickAt(envelope.Received);
                    if (p.StartTick > receiveTick + _timing.MaxFutureTicks) result = Admission.Invalid;
                    else if (p.StartTick <= _final.Tick || envelope.Received >= _timing.ProposalDeadline(p.StartTick)) result = Admission.TooLate;
                    // Boss telegraphs are scheduled in the future, never invented as past damage.
                    else if (actor.IsBoss && p.StartTick <= receiveTick) result = Admission.TooLate;
                }
                catch (OverflowException) { result = Admission.Invalid; }
            }
            if (result != Admission.WrongEncounter && result != Admission.WrongHost) _proposals.Add(p.Id, (p, result));
            _proposalReceipts.Enqueue((p, result, envelope.Received));
        }

        public InputReceipt[] DrainInputReceipts() { var result = _receipts.ToArray(); _receipts.Clear(); return result; }
        public (HostProposal Proposal, Admission Result, long ReceivedUs)[] DrainProposalReceipts()
        { var result = _proposalReceipts.ToArray(); _proposalReceipts.Clear(); return result; }
        public CombatEvent[] DrainCommittedEvents() { var result = _committed.ToArray(); _committed.Clear(); return result; }
    }
}
