#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
namespace DnfRaidDemo
{
    public partial class RaidSimulation
    {
        // A shared deterministic schedule is checked against the host's future proposal.
        public bool NetworkPatternGate;
        public int PendingPattern = -1, PendingPatternTick;
        public int SelectionAliveMask=15,SelectionPreviousMarkMask;
        public uint SelectionRoll;
        public int SelectionPhase, SelectionPrevious, SelectionOrdinal=-1;
        private int _approvedPattern = -1;
        public void NetworkTick(IReadOnlyList<PlayerCommand> commands, int approvedPattern,string program="",string roomProgram="")
        {
            _approvedPattern = approvedPattern;_approvedProgram=program;_approvedRoomProgram=roomProgram;
            Tick(FixedStep, commands);
            _approvedPattern = -1;_approvedProgram=null;_approvedRoomProgram=null;
        }
        private void UpdateNetworkPattern()
        {
            if (ProgramPattern < 0 && PendingPattern < 0 && _patternRemaining <= 0f)
            {
                SelectionAliveMask=Players.Where(p=>p.Hp>0).Aggregate(0,(mask,p)=>mask|(1<<p.Id));SelectionPreviousMarkMask=PreviousMarkMask;
                SelectionRoll=NextRandom();SelectionPhase=Boss.Phase;SelectionPrevious=_lastPattern;SelectionOrdinal++;
                PendingPattern = DawnPatternRules.Select(SelectionRoll,SelectionPhase,SelectionPrevious,SelectionOrdinal);
                PendingPatternTick = TickIndex + 30; // Lead time for host proposal / server validation.
            }
            if (PendingPattern >= 0 && TickIndex == PendingPatternTick)
            {
                if (_approvedPattern == PendingPattern) BeginBossProgram(PendingPattern,_approvedProgram);
                else Emit("PatternMissing", Boss.Position, PendingPattern, -1);
                PendingPattern = -1; PendingPatternTick = 0;
            }
        }
        public RaidSimulation Clone()
        {
            var c = (RaidSimulation)MemberwiseClone();
            c.Players = Players.Select(p => p.Clone()).ToList(); c.Boss = Boss.Clone();
            c.Zones = Zones.Select(z => z.Clone()).ToList(); c.Events = Events.Select(e => e.Clone()).ToList();
            c.ProgramSteps=ProgramSteps.Select(p=>new BossPatternStep{Kind=p.Kind,Tick=p.Tick,Target=p.Target}).ToArray();
            c.MarkTargets=(int[])MarkTargets.Clone();
            c.RoomFires=RoomFires.Select(f=>f.Clone()).ToList();
            c.Looks = (string[])Looks.Clone();
            c._hitTargets = _hitTargets.ToDictionary(p=>p.Key,p=>new HashSet<int>(p.Value));
            c._inputBuffers = _inputBuffers.ToDictionary(p=>p.Key,p=>(float[])p.Value.Clone());
            c._lastBasicTimes = new Dictionary<int,float>(_lastBasicTimes);
            c._lastBasicFrenzy = new Dictionary<int,bool>(_lastBasicFrenzy);
            c._runBlocked = new Dictionary<int,bool>(_runBlocked);
            return c;
        }
        public RaidCheckpoint Checkpoint() => new RaidCheckpoint {
            NextRoomMarkTick=NextRoomMarkTick,PendingRoomMarkTick=PendingRoomMarkTick,RoomSelectionRoll=RoomSelectionRoll,RoomSelectionAliveMask=RoomSelectionAliveMask,RoomSelectionPreviousMarkMask=RoomSelectionPreviousMarkMask,
            RoomsEnabled=RoomsEnabled,MarkTargets=(int[])MarkTargets.Clone(),PreviousMarkMask=PreviousMarkMask,SelectionAliveMask=SelectionAliveMask,SelectionPreviousMarkMask=SelectionPreviousMarkMask,MarkTarget=MarkTarget,MarkDeadline=MarkDeadline,RoomFires=RoomFires.ToArray(),
            ProgramPattern=ProgramPattern,ProgramStart=ProgramStart,ProgramIndex=ProgramIndex,ProgramSteps=ProgramSteps,BossMoveTarget=BossMoveTarget,BossMoveUntil=BossMoveUntil,
            SelectionRoll=SelectionRoll,SelectionPhase=SelectionPhase,SelectionPrevious=SelectionPrevious,SelectionOrdinal=SelectionOrdinal,
            Players = Players.ToArray(), Boss = Boss, Zones = Zones.ToArray(), Events = Events.ToArray(),
            Mode = Mode, Elapsed = Elapsed, TimeLimit = TimeLimit, Tick = TickIndex, ReviveCharges=ReviveCharges,WipeDeadlineTick=WipeDeadlineTick,
            PhaseTransitionStartTick = PhaseTransitionStartTick, AutoBuffs = AutoBuffs, IntroEnabled = IntroEnabled, NetworkPatternGate = NetworkPatternGate, PendingPattern = PendingPattern,
            PendingPatternTick = PendingPatternTick, InitialSeed = _initialSeed, Random = _random,
            Accumulator = _accumulator, PatternRemaining = _patternRemaining, NextZoneId = _nextZoneId, LastPattern = _lastPattern,
            HitRecords = _hitTargets.OrderBy(p=>p.Key).Select(p=>new RaidHitRecord { Zone=p.Key,Targets=p.Value.OrderBy(i=>i).ToArray() }).ToArray(),
            Buffers = Players.Select(p=>(float[])_inputBuffers[p.Id].Clone()).ToArray(),
            LastBasic = Players.Select(p=>_lastBasicTimes[p.Id]).ToArray(),
            LastFrenzy = Players.Select(p=>_lastBasicFrenzy[p.Id]).ToArray(), RunBlocked = Players.Select(p=>_runBlocked[p.Id]).ToArray()
        };
        public static RaidSimulation Restore(RaidCheckpoint c)
        {
            var s = new RaidSimulation(c.Players.Length, unchecked((int)c.InitialSeed));
            s.Players = c.Players.ToList(); s.Boss = c.Boss; s.Zones = c.Zones.ToList(); s.Events = c.Events.ToList();
            s.NextRoomMarkTick=c.NextRoomMarkTick;s.PendingRoomMarkTick=c.PendingRoomMarkTick;s.RoomSelectionRoll=c.RoomSelectionRoll;s.RoomSelectionAliveMask=c.RoomSelectionAliveMask;s.RoomSelectionPreviousMarkMask=c.RoomSelectionPreviousMarkMask;
            s.MarkTargets=c.MarkTargets;s.PreviousMarkMask=c.PreviousMarkMask;s.SelectionAliveMask=c.SelectionAliveMask;s.SelectionPreviousMarkMask=c.SelectionPreviousMarkMask;
            s.RoomsEnabled=c.RoomsEnabled;s.MarkTarget=c.MarkTarget;s.MarkDeadline=c.MarkDeadline;s.RoomFires=c.RoomFires.ToList();
            s.ProgramPattern=c.ProgramPattern;s.ProgramStart=c.ProgramStart;s.ProgramIndex=c.ProgramIndex;s.ProgramSteps=c.ProgramSteps;s.BossMoveTarget=c.BossMoveTarget;s.BossMoveUntil=c.BossMoveUntil;
            s.Mode=c.Mode;s.Elapsed=c.Elapsed;s.TimeLimit=c.TimeLimit;s.TickIndex=c.Tick;s.AutoBuffs=c.AutoBuffs;s.IntroEnabled=c.IntroEnabled;s.PhaseTransitionStartTick=c.PhaseTransitionStartTick;s.ReviveCharges=c.ReviveCharges;s.WipeDeadlineTick=c.WipeDeadlineTick;
            s.NetworkPatternGate=c.NetworkPatternGate;s.PendingPattern=c.PendingPattern;s.PendingPatternTick=c.PendingPatternTick;
            s.SelectionRoll=c.SelectionRoll;s.SelectionPhase=c.SelectionPhase;s.SelectionPrevious=c.SelectionPrevious;s.SelectionOrdinal=c.SelectionOrdinal;
            s._random=c.Random;s._accumulator=c.Accumulator;s._patternRemaining=c.PatternRemaining;
            s._nextZoneId=c.NextZoneId;s._lastPattern=c.LastPattern;
            s._hitTargets=c.HitRecords.ToDictionary(p=>p.Zone,p=>new HashSet<int>(p.Targets));
            for(int i=0;i<s.Players.Count;i++) { s._inputBuffers[i]=c.Buffers[i]; s._lastBasicTimes[i]=c.LastBasic[i];
                s._lastBasicFrenzy[i]=c.LastFrenzy[i];s._runBlocked[i]=c.RunBlocked[i];s.Looks[i]=c.Players[i].Look; }
            return s;
        }
    }
    [Serializable] public sealed class RaidHitRecord { public int Zone; public int[] Targets = Array.Empty<int>(); }
    [Serializable] public sealed class RaidCheckpoint
    {
        public int NextRoomMarkTick,PendingRoomMarkTick,RoomSelectionAliveMask,RoomSelectionPreviousMarkMask; public uint RoomSelectionRoll;
        public int[] MarkTargets=Array.Empty<int>();public int PreviousMarkMask,SelectionAliveMask=15,SelectionPreviousMarkMask;
        public bool RoomsEnabled; public int MarkTarget=-1,MarkDeadline; public RoomFire[] RoomFires=Array.Empty<RoomFire>();
        public PlayerState[] Players = Array.Empty<PlayerState>(); public BossState Boss = new BossState();
        public AttackZone[] Zones = Array.Empty<AttackZone>(); public CombatEvent[] Events = Array.Empty<CombatEvent>();
        public EncounterMode Mode; public float Elapsed, TimeLimit, Accumulator, PatternRemaining;
        public int ProgramPattern,ProgramStart,ProgramIndex,BossMoveUntil; public BossPatternStep[] ProgramSteps=Array.Empty<BossPatternStep>(); public ProjectDawn.Combat.RaidVector BossMoveTarget;
        public int PhaseTransitionStartTick = -1;
        public int Tick, PendingPattern, PendingPatternTick, NextZoneId, LastPattern,ReviveCharges,WipeDeadlineTick;
        public uint SelectionRoll;public int SelectionPhase,SelectionPrevious,SelectionOrdinal;
        public uint InitialSeed, Random; public bool AutoBuffs, IntroEnabled, NetworkPatternGate;
        public RaidHitRecord[] HitRecords=Array.Empty<RaidHitRecord>(); public float[][] Buffers=Array.Empty<float[]>();
        public float[] LastBasic=Array.Empty<float>(); public bool[] LastFrenzy=Array.Empty<bool>(),RunBlocked=Array.Empty<bool>();
    }
}
