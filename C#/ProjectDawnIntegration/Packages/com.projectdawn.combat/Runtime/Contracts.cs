#nullable enable
using System;
using System.Collections.Generic;

namespace ProjectDawn.Combat
{
    // All times are monotonic microseconds relative to encounter creation, never UTC.
    public sealed class TimingPolicy
    {
        public long TickUs { get; }
        public long InputGraceUs { get; }
        public long ProposalGraceUs { get; }
        public long FinalizationDelayUs { get; }
        public long FutureToleranceUs { get; }
        public int MaxReplayTicks { get; }
        public int MaxFutureTicks { get; }
        public int MaxRecords { get; }
        public int MaxPending { get; }
        public TimingPolicy(long tickUs, long inputGraceUs, long proposalGraceUs,
            long finalizationDelayUs, long futureToleranceUs, int maxReplayTicks = 128,
            int maxFutureTicks = 128, int maxRecords = 65536, int maxPending = 4096)
        {
            if (tickUs <= 0 || inputGraceUs < 0 || proposalGraceUs < 0 ||
                finalizationDelayUs < checked(inputGraceUs + proposalGraceUs) ||
                futureToleranceUs < 0 || maxReplayTicks < 2 || maxFutureTicks < 1 ||
                maxRecords < 1 || maxPending < 1 ||
                finalizationDelayUs / tickUs + 2 >= maxReplayTicks)
                throw new ArgumentOutOfRangeException(nameof(tickUs), "Invalid bounded timing policy.");
            TickUs = tickUs; InputGraceUs = inputGraceUs; ProposalGraceUs = proposalGraceUs;
            FinalizationDelayUs = finalizationDelayUs; FutureToleranceUs = futureToleranceUs;
            MaxReplayTicks = maxReplayTicks; MaxFutureTicks = maxFutureTicks;
            MaxRecords = maxRecords; MaxPending = maxPending;
        }
        public long TickAt(long timeUs) => checked(timeUs / TickUs + 1);
        public long End(long tick) => checked(tick * TickUs);
        public long InputDeadline(long tick) => checked(End(tick) + InputGraceUs);
        public long ProposalDeadline(long tick) => checked(InputDeadline(tick) + ProposalGraceUs);
        public long FinalDeadline(long tick) => checked(End(tick) + FinalizationDelayUs);
    }

    public enum InputKind { Move, Jump, Attack, Controls }
    public readonly struct InputKey : IEquatable<InputKey>, IComparable<InputKey>
    {
        public readonly long Encounter, Epoch, Sequence;
        public readonly int Player;
        public InputKey(long encounter, int player, long epoch, long sequence)
        { Encounter = encounter; Player = player; Epoch = epoch; Sequence = sequence; }
        public bool Equals(InputKey x) => Encounter == x.Encounter && Player == x.Player && Epoch == x.Epoch && Sequence == x.Sequence;
        public override bool Equals(object? x) => x is InputKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Encounter, Player, Epoch, Sequence);
        public int CompareTo(InputKey x)
        {
            int c = Encounter.CompareTo(x.Encounter); if (c != 0) return c;
            c = Player.CompareTo(x.Player); if (c != 0) return c;
            c = Epoch.CompareTo(x.Epoch); return c != 0 ? c : Sequence.CompareTo(x.Sequence);
        }
        public override string ToString() => $"{Encounter}/{Player}/{Epoch}/{Sequence}";
    }

    public readonly struct PlayerInput : IEquatable<PlayerInput>
    {
        public readonly InputKey Key;
        public readonly long ClientTimeUs, ClockVersion;
        public readonly InputKind Kind;
        public readonly int Value, MoveX, MoveY, Buttons;
        public PlayerInput(InputKey key, long clientTimeUs, long clockVersion, InputKind kind, int value = 0, int moveX = 0, int moveY = 0, int buttons = 0)
        { Key = key; ClientTimeUs = clientTimeUs; ClockVersion = clockVersion; Kind = kind; Value = value; MoveX = moveX; MoveY = moveY; Buttons = buttons; }
        public bool Equals(PlayerInput x) => Key.Equals(x.Key) && ClientTimeUs == x.ClientTimeUs &&
            ClockVersion == x.ClockVersion && Kind == x.Kind && Value == x.Value && MoveX == x.MoveX && MoveY == x.MoveY && Buttons == x.Buttons;
    }

    // Minted by the authenticated server session adapter; never deserialize this from a packet.
    public sealed class SessionBinding
    {
        private readonly object _clockGate = new object();
        private readonly SortedDictionary<long, ClockMapping> _clocks = new SortedDictionary<long, ClockMapping>();
        public int Player { get; }
        public long Epoch { get; }
        public long ClockVersion { get; }
        public long ClockOffsetUs { get; }
        public bool ClockTrusted { get; }
        public SessionBinding(int player, long epoch, long clockVersion, long clockOffsetUs, bool clockTrusted)
        {
            Player = player; Epoch = epoch; ClockVersion = clockVersion; ClockOffsetUs = clockOffsetUs; ClockTrusted = clockTrusted;
            _clocks.Add(clockVersion, new ClockMapping(clockVersion, clockOffsetUs, clockTrusted, long.MaxValue, long.MaxValue, 0));
        }
        // Server-owned estimates. An ingress envelope captures one immutable estimate.
        public void InstallClock(ClockMapping mapping)
        {
            lock (_clockGate)
            {
                if (_clocks.ContainsKey(mapping.Version) || mapping.Version <= ClockVersion) throw new ArgumentException("Clock version must increase");
                long latest = ClockVersion; foreach (var version in _clocks.Keys) latest = version;
                if (mapping.Version <= latest) throw new ArgumentException("Clock version must increase");
                _clocks.Add(mapping.Version, mapping);
                while (_clocks.Count > 4) { long first = 0; foreach (var key in _clocks.Keys) { first=key; break; } _clocks.Remove(first); }
            }
        }
        public ClockMapping? CaptureClock(long version)
        { lock (_clockGate) return _clocks.TryGetValue(version, out var mapping) ? mapping : null; }
    }

    public enum Admission
    {
        Admitted, Duplicate, Conflict, WrongEncounter, WrongSession, Invalid,
        TooLate, FutureTimestamp, ClockMismatch, SequenceTimeConflict,
        WrongHost, CapacityExceeded, EncounterAborted
    }

    public sealed class InputReceipt
    {
        public PlayerInput Input { get; }
        public Admission Result { get; }
        public Admission OriginalResult { get; }
        public long ReceivedUs { get; }
        public long EffectiveTick { get; }
        public bool UsedReceiveTime { get; }
        internal InputReceipt(PlayerInput input, Admission result, long received, long tick, bool fallback = false,
            Admission? originalResult = null)
        { Input = input; Result = result; OriginalResult = originalResult ?? result;
            ReceivedUs = received; EffectiveTick = tick; UsedReceiveTime = fallback; }
    }

    public readonly struct HostProposal : IEquatable<HostProposal>
    {
        public readonly long Encounter, HostEpoch, Sequence, StartTick;
        public readonly int Actor, Definition;
        public readonly InputKey? Origin;
        public readonly string Program;
        public HostProposal(long encounter, long hostEpoch, long sequence, long startTick,
            int actor, int definition, InputKey? origin = null, string program = "")
        { Encounter = encounter; HostEpoch = hostEpoch; Sequence = sequence; StartTick = startTick;
            Actor = actor; Definition = definition; Origin = origin; Program = program ?? ""; }
        public string Id => $"{Encounter}/h{HostEpoch}/a{Sequence}";
        public bool Equals(HostProposal x) => Encounter == x.Encounter && HostEpoch == x.HostEpoch &&
            Sequence == x.Sequence && StartTick == x.StartTick && Actor == x.Actor && Definition == x.Definition &&
            Nullable.Equals(Origin, x.Origin) && Program == x.Program;
    }

    // Immutable server-owned definitions. Values in tests are fixtures, not final game balance.
    public sealed class AttackDefinition
    {
        public int Id { get; }
        public bool FromBoss { get; }
        public bool GroundOnly { get; }
        public bool SurvivesOwnerDeath { get; }
        public int Damage { get; }
        public int Range { get; }
        public int DelayTicks { get; }
        public int Repeat { get; }
        public int IntervalTicks { get; }
        public int StunTicks { get; }
        public int Knockback { get; }
        public int CooldownTicks { get; }
        public AttackDefinition(int id, bool fromBoss, bool groundOnly, int damage, int range,
            int delayTicks = 1, int repeat = 1, int intervalTicks = 1, int stunTicks = 0,
            int knockback = 0, int cooldownTicks = 0, bool survivesOwnerDeath = false)
        {
            if (id < 1 || damage < 0 || range < 0 || delayTicks < 0 || repeat < 1 || intervalTicks < 1 ||
                stunTicks < 0 || cooldownTicks < 0) throw new ArgumentOutOfRangeException(nameof(id));
            Id = id; FromBoss = fromBoss; GroundOnly = groundOnly; Damage = damage; Range = range;
            DelayTicks = delayTicks; Repeat = repeat; IntervalTicks = intervalTicks; StunTicks = stunTicks;
            Knockback = knockback; CooldownTicks = cooldownTicks; SurvivesOwnerDeath = survivesOwnerDeath;
        }
    }

    public sealed class CombatRules
    {
        private readonly Dictionary<int, AttackDefinition> _attacks = new Dictionary<int, AttackDefinition>();
        public int MovePerTick { get; }
        public int MoveLeaseTicks { get; }
        public int JumpDurationTicks { get; }
        public int JumpRisePerTick { get; }
        public int DodgeHeight { get; }
        public int WorldLimit { get; }
        public CombatRules(IEnumerable<AttackDefinition> attacks, int movePerTick = 100, int moveLeaseTicks = 3,
            int jumpDurationTicks = 6, int jumpRisePerTick = 300, int dodgeHeight = 300, int worldLimit = 100000)
        {
            if (movePerTick < 0 || moveLeaseTicks < 1 || jumpDurationTicks < 2 || jumpRisePerTick < 1 ||
                dodgeHeight < 0 || worldLimit < 1) throw new ArgumentOutOfRangeException(nameof(movePerTick));
            MovePerTick = movePerTick; MoveLeaseTicks = moveLeaseTicks; JumpDurationTicks = jumpDurationTicks;
            JumpRisePerTick = jumpRisePerTick; DodgeHeight = dodgeHeight; WorldLimit = worldLimit;
            foreach (var attack in attacks) _attacks.Add(attack.Id, attack);
        }
        public bool TryAttack(int id, out AttackDefinition definition) => _attacks.TryGetValue(id, out definition!);
    }
}
