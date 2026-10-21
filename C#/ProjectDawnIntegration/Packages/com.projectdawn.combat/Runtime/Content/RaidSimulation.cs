#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using Vector2 = ProjectDawn.Combat.RaidVector;
using Mathf = ProjectDawn.Combat.RaidMath;
#if UNITY_5_3_OR_NEWER
using UnityEngine;
#endif

namespace DnfRaidDemo
{
    // Indices into SkillBook.All. Basic and Dash keep their original slots 0 and 3.
    public enum SkillId
    {
        Basic, BloodSword, GoreCross, Dash, ExtremeOverkill, RagingFury, BloodyRave, OutrageBreak,
        BloodBoom, Bloodlust, Rage, BrutalCrusher, Frenzy, BloodRiven, BloodSnatch, RampantMadness,
        FatalBlood, Destruction, Berserk, CruelEngage, BloodScratch
    }
    public enum EncounterMode { Ready, Fighting, Victory, Defeat }
    public enum ZoneShape { Circle, Box, Donut, Ellipse }

    [Serializable]
    public class PlayerCommand
    {
        public int PlayerId;
        public Vector2 Move;
        public bool Basic, Dash, Jump, Run, Revive, Potion;
        // One bit per SkillBook index for skills pressed this tick (Basic and Dash use their own fields).
        public int Skills;
        public static int Bit(SkillId skill) { return 1 << (int)skill; }
        public bool Pressed(SkillId skill) { return (Skills & Bit(skill)) != 0; }
    }

    [Serializable]
    public class PlayerState
    {
        public int Id;
        public string Name = "";
        public bool Disconnected, Departed;
        public int RoomId = 4, RoomChangedTick = -1000, NextChargeBurnTick;
        public float PhysicalAttack = 100f, EquipmentDefence;
        public float MoveSpeedScale=1f, AttackSpeedScale=1f, ActionRate=1f;
        public Vector2 Position;
        public float Hp, MaxHp, Invulnerable, AttackTime, StunTime;
        public int PotionCharges = 5, NextPotionTick;
        public int Facing;
        public float[] Cooldowns = new float[SkillBook.Count];
        public float Mp, MaxMp, FrenzyTime, BerserkTime, LungeTime, LungeSpeed, LungeDelay;
        // Authoritative two-stage awakening state; included in checkpoints and server replay.
        public float OverkillTime, OverkillAge;
        public bool ActionOverkillFinish;
        // Physical skill leap clock is independent from presentation ActionElapsed/ActiveSkill.
        public float SkillLeapAge = -1f;
        public SkillId SkillLeapKind;
        // Appearance code (see RaidLook). Presentation only; never read by combat.
        public string Look = "";
        public float DashTime;
        public Vector2 DashDirection;
        public float JumpHeight, JumpVelocity;
        // Presentation state only. Combat locks still use AttackTime and StunTime.
        public SkillId ActiveSkill;
        public int ActionSequence, BasicComboStep, ActionFacing = 1;
        public bool ActionAirborne, ActionFrenzy, ActionDashAttack, Running;
        public float ActionElapsed, ActionDuration, LandingTime;
        // Display-only offset; excluded from authoritative snapshots and combat rules.
        public float ActionPresentationLead { get; set; }
        public PlayerState Clone() { var copy = (PlayerState)MemberwiseClone(); copy.Cooldowns = (float[])Cooldowns.Clone(); return copy; }

    }

    [Serializable]
    public class BossState
    {
        public Vector2 Position;
        public float Hp, MaxHp, Stagger, StaggerTime, AttackTime;
        public int Phase;
        public string PatternName;
        public float TransitionTime;
        public int ActionSequence;
        public float ActionElapsed, ActionDuration;
        public BossState Clone() { var copy = (BossState)MemberwiseClone(); return copy; }

    }

    [Serializable]
    public class AttackZone
    {
        public int Id, OwnerId, SourceAction, HitIndex;
        public int TargetId = -1;
        public int RoomId = 4;
        public Vector2 Center, HalfSize;
        public float Radius, InnerRadius, Age, Telegraph, ActiveTime, Recovery, Damage;
        public float PlaybackRate=1f;
        public bool FromPlayer;
        public ZoneShape Shape;
        public string Tag;
        public Vector2 Velocity;
        public bool GroundOnly;
        // Player skill zones: source skill, and owner-relative placement for zones that move with the caster.
        public int Skill = -1, Facing = 1;
        public bool Follow;
        public Vector2 Offset;
        public AttackZone Clone() { var copy = (AttackZone)MemberwiseClone(); return copy; }

    }

    [Serializable]
    public class CombatEvent
    {
        public string Kind;
        public int RoomId = 4;
        public Vector2 Position;
        public float Value, Age;
        public int TargetId, SourceId = -1;
        public string FeedbackKey = "";
        // Immutable contact identity. Consumers must not infer a delayed hit from the actor's next action.
        public int Skill = -1, SourceAction, HitIndex, Facing = 1, Combo;
        public bool Frenzy;
        public float ActionTime;
        public CombatEvent Clone() => (CombatEvent)MemberwiseClone();

    }

    // Independent demo mechanics and tuning, not recovered DNF game source or protocol.
    // The host alone owns this state machine. Remote clients display its public snapshots.
    public partial class RaidSimulation
    {
        public const float FixedStep = 1f / 60f;
        public const float CollisionEpsilon = .0001f;
        public const float InputBufferSeconds = .35f;
        public const float JumpDodgeHeight = .35f;
        public const float JumpLaunchVelocity = 5.8f;
        public const float JumpGravity = 18f;
        public const float MpRegen = 7f;
        public const float FrenzyDamage = 1.25f;
        public const float BerserkDamage = 1.15f;
        public const float PlayerDamageScale = 1.5f;
        public const string StompTag = "지면 충격파 · 점프로 회피";
        public static bool IsStompZone(AttackZone zone) => zone != null && !zone.FromPlayer && zone.GroundOnly && zone.Tag == StompTag;
        public const float PotionHealFraction = .4f;
        public const int PotionCooldownTicks = 900;
        // Demo client turns this on: 프렌지/폭주 start active like the in-game ON setting.
        public bool AutoBuffs;
        private static readonly int JumpBufferSlot = SkillBook.Count;
        public List<PlayerState> Players = new List<PlayerState>();
        public BossState Boss = new BossState();
        public List<AttackZone> Zones = new List<AttackZone>();
        public List<CombatEvent> Events = new List<CombatEvent>();
        public EncounterMode Mode;
        public float Elapsed, TimeLimit = 180f;
        public int TickIndex;
        public int ReviveCharges, WipeDeadlineTick;
        public bool PracticeInvulnerable;
        // Appearance per player id, copied into PlayerState.Look on every reset.
        public string[] Looks = new string[4];

        public static readonly Vector2 PlayerHurtboxHalfSize = new Vector2(.24f, .17f);
        public static readonly Vector2 BossHurtboxHalfSize = new Vector2(.75f, .8f);
        private readonly int _playerCount;
        private readonly uint _initialSeed;
        private uint _random;
        private float _accumulator, _patternRemaining;
        private int _nextZoneId, _lastPattern = -1;
        private Dictionary<int, HashSet<int>> _hitTargets = new Dictionary<int, HashSet<int>>();
        private Dictionary<int, float[]> _inputBuffers = new Dictionary<int, float[]>();
        private Dictionary<int, float> _lastBasicTimes = new Dictionary<int, float>();
        private Dictionary<int, bool> _runBlocked = new Dictionary<int, bool>();
        private Dictionary<int, bool> _lastBasicFrenzy = new Dictionary<int, bool>();

        public RaidSimulation(int playerCount = 1, int seed = 42)
        {
            _playerCount = Mathf.Clamp(playerCount, 1, 4);
            _initialSeed = seed == 0 ? 42u : unchecked((uint)seed);
            ResetState();
        }

        private void ResetState()
        {
            Players.Clear();
            _inputBuffers.Clear();
            _lastBasicTimes.Clear();
            _lastBasicFrenzy.Clear();
            _runBlocked.Clear();
            for (int i = 0; i < _playerCount; i++)
            {
                _inputBuffers[i] = new float[SkillBook.Count + 1];
                _lastBasicTimes[i] = float.NegativeInfinity;
                _lastBasicFrenzy[i] = false;
                _runBlocked[i] = false;
                Players.Add(new PlayerState
                {
                    Id = i, Position = new Vector2(-4f, _playerCount == 1 ? 0f : (_playerCount == 2 ? (i == 0 ? -.8f : .8f) : (i - 1.5f) * .8f)),
                    Hp = 100f, MaxHp = 100f, Mp = 100f, MaxMp = 100f, Facing = 1,
                    Look = Looks != null && i < Looks.Length && Looks[i] != null ? Looks[i] : ""
                });
            }
            Boss.Position = new Vector2(4f, 0f);
            Boss.Hp = Boss.MaxHp = 10000f * (1f + .75f * (_playerCount - 1));
            Boss.Stagger = Boss.StaggerTime = Boss.AttackTime = Boss.TransitionTime = 0f;
            Boss.ActionSequence = 0;
            CancelBossAction();
            Boss.Phase = 1;
            Boss.PatternName = "대기";
            Zones.Clear(); Events.Clear(); _hitTargets.Clear();
            _random = _initialSeed;
            _accumulator = 0f; _patternRemaining = 1.1f; _nextZoneId = 1; _lastPattern = -1;
            SelectionAliveMask=(1<<_playerCount)-1;SelectionPreviousMarkMask=0;SelectionOrdinal=-1;SelectionRoll=0;SelectionPhase=0;SelectionPrevious=-1;PendingPattern=-1;PendingPatternTick=0;
            CancelBossProgram(); ResetRooms();
            Elapsed = 0f; TickIndex = 0; PhaseTransitionStartTick = -1; Mode = EncounterMode.Ready;
            ReviveCharges=3;WipeDeadlineTick=0;
        }

        public void Start()
        {
            ResetState();
            Mode = EncounterMode.Fighting;
            if (IntroEnabled) NextRoomMarkTick += EncounterIntroTicks;
            if (AutoBuffs)
                foreach (PlayerState player in Players)
                {
                    player.FrenzyTime = SkillBook.Get(SkillId.Frenzy).BuffSeconds;
                    player.BerserkTime = SkillBook.Get(SkillId.Berserk).BuffSeconds;
                }
            Boss.PatternName = IntroEnabled ? "폭룡왕 조우" : "전투 시작";
            Emit("Start", Boss.Position, 0f, -1);
        }

        // Accept elapsed seconds, but advance only in fixed 60 Hz steps. The host normally
        // calls this once per fixed step; a bounded catch-up prevents unbounded stall work.
        public void Tick(float dt, IReadOnlyList<PlayerCommand> commands)
        {
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0f) return;
            _accumulator += Mathf.Min(dt, .25f);
            while (_accumulator + .000001f >= FixedStep)
            {
                _accumulator -= FixedStep;
                if (_accumulator < 0f) _accumulator = 0f;
                AgeEvents();
                if (Mode == EncounterMode.Fighting) Step(commands);
            }
        }

        private void Step(IReadOnlyList<PlayerCommand> commands)
        {
            TickIndex++;
            if (StepEncounterIntro() || StepPhaseTransition()) return;
            if (IntroEnabled && Boss.Phase == 1 && Boss.Hp > 0f && Boss.Hp <= Boss.MaxHp * .5f && !ProtectedVolleyActive)
            { BeginPhaseTwo(); return; }
            Elapsed += FixedStep;
            ApplyRevives(commands);
            if (CheckEnd()) return;
            ApplyPotions(commands);

            Boss.AttackTime = Down(Boss.AttackTime);
            Boss.StaggerTime = Down(Boss.StaggerTime);
            Boss.TransitionTime = Down(Boss.TransitionTime);
            Boss.ActionElapsed = AdvanceAction(Boss.ActionElapsed, Boss.ActionDuration);
            foreach (PlayerState player in Players)
            {
                player.Invulnerable = Down(player.Invulnerable);
                player.AttackTime = Down(player.AttackTime);
                player.StunTime = Down(player.StunTime);
                player.ActionElapsed = AdvanceAction(player.ActionElapsed, player.ActionDuration, player.ActionRate);
                player.LandingTime = Down(player.LandingTime);
                if (player.SkillLeapAge >= 0f) { player.SkillLeapAge += FixedStep * player.ActionRate; if (player.SkillLeapAge > 1.1f) player.SkillLeapAge = -1f; }
                // Frenzy is a stance toggle and remains active until switched off.
                player.BerserkTime = Down(player.BerserkTime);
                if (player.OverkillTime > 0f) {
                    player.OverkillAge += FixedStep;
                    // Keep the armed flag until the automatic finisher can legally begin.
                    player.OverkillTime = Mathf.Max(FixedStep, player.OverkillTime - FixedStep);
                }
                if (player.Hp > 0f) player.Mp = Mathf.Min(player.MaxMp, player.Mp + MpRegen * FixedStep);
                for (int i = 0; i < player.Cooldowns.Length; i++) player.Cooldowns[i] = Down(player.Cooldowns[i]);
                if (player.Hp <= 0f)
                {
                    player.DashTime = player.JumpHeight = player.JumpVelocity = player.LungeTime = 0f;
                    player.LandingTime = 0f; player.Running = false;
                    player.OverkillTime = player.OverkillAge = 0f;
                    CancelPlayerAction(player);
                    Array.Clear(_inputBuffers[player.Id], 0, _inputBuffers[player.Id].Length);
                    continue;
                }
                PlayerCommand command = FindCommand(commands, player.Id);
                UpdatePlayer(player, command);
                UpdateJump(player);
                UpdateRoomTravel(player, command);
            }

            if (Boss.Phase == 1 && Boss.Hp <= Boss.MaxHp * .5f && !ProtectedVolleyActive) { BeginPhaseTwo(); if (PhaseTransitionActive) return; }
            if (Boss.StaggerTime <= 0f && Boss.TransitionTime <= 0f)
            {
                _patternRemaining -= FixedStep;
                if (NetworkPatternGate) UpdateNetworkPattern();
                else if (_patternRemaining <= 0f) BeginPattern(ChoosePattern());
            }
            UpdateBossProgram();
            UpdateRoomScheduler();
            UpdateRoomHazards();
            UpdateZones();
            if (Boss.Hp > 0f && Boss.Stagger >= 100f && !ProtectedVolleyActive) BeginStagger();
            if (!CheckEnd() && Boss.Phase == 1 && Boss.Hp <= Boss.MaxHp * .5f && !ProtectedVolleyActive) BeginPhaseTwo();
        }

        private static PlayerCommand FindCommand(IReadOnlyList<PlayerCommand> commands, int id)
        {
            if (commands == null) return null;
            for (int i = commands.Count - 1; i >= 0; i--)
                if (commands[i] != null && commands[i].PlayerId == id) return commands[i];
            return null;
        }

        private void UpdatePlayer(PlayerState player, PlayerCommand command)
        {
            // A one-tick Q/E/R press must survive the recovery of held basic attacks.
            // Expiry is host simulation time; snapshots need not contain this private queue.
            float[] pending = _inputBuffers[player.Id];
            if (command != null)
            {
                for (int i = 1; i < SkillBook.Count; i++)
                    if (i != (int)SkillId.Dash && (command.Skills & (1 << i)) != 0) pending[i] = Elapsed + InputBufferSeconds;
                if (command.Dash) pending[(int)SkillId.Dash] = Elapsed + InputBufferSeconds;
                if (command.Jump) pending[JumpBufferSlot] = Elapsed + InputBufferSeconds;
            }
            bool wantsRun = command != null && command.Run;
            if (!wantsRun) _runBlocked[player.Id] = false;
            player.Running = false;
            Vector2 move = command == null ? Vector2.zero : command.Move;
            if (float.IsNaN(move.x) || float.IsInfinity(move.x) || float.IsNaN(move.y) || float.IsInfinity(move.y)) move = Vector2.zero;
            if (move.sqrMagnitude > 1f) move.Normalize();
            if (Mathf.Abs(move.x) > .01f) player.Facing = move.x < 0f ? -1 : 1;

            if (pending[JumpBufferSlot] > 0f && pending[JumpBufferSlot] >= Elapsed && player.JumpHeight <= 0f && player.JumpVelocity <= 0f && player.StunTime <= 0f)
            {
                pending[JumpBufferSlot] = 0f;
                _runBlocked[player.Id] = wantsRun;
                player.JumpVelocity = JumpLaunchVelocity;
                player.LandingTime = 0f;
                Emit("Jump", player.Position, JumpLaunchVelocity, player.Id);
            }

            if (pending[(int)SkillId.Dash] > 0f && pending[(int)SkillId.Dash] >= Elapsed && player.Cooldowns[(int)SkillId.Dash] <= 0f && player.StunTime <= 0f)
            {
                pending[(int)SkillId.Dash] = 0f;
                player.DashDirection = move.sqrMagnitude > .01f ? move.normalized : new Vector2(player.Facing, 0f);
                player.DashTime = .2f;
                _runBlocked[player.Id] = wantsRun;
                player.LungeTime = 0f;
                player.Invulnerable = Mathf.Max(player.Invulnerable, .32f);
                player.Cooldowns[(int)SkillId.Dash] = 1.6f;
                player.AttackTime = 0f;
                CancelPlayerAction(player);
                Emit("Dodge", player.Position, .32f, player.Id);
            }
            if (player.DashTime > 0f)
            {
                player.Position = ClampPosition(player.Position + player.DashDirection * (10f * FixedStep));
                player.DashTime = Down(player.DashTime);
                return;
            }
            if (player.StunTime > 0f) { _runBlocked[player.Id] = wantsRun; return; }
            player.Running = wantsRun && !_runBlocked[player.Id] && Mathf.Abs(move.x) > .01f
                && player.JumpHeight <= 0f && player.JumpVelocity <= 0f && player.AttackTime <= 0f;
            float moveSpeed = player.AttackTime > 0f ? RaidActionTempo.AttackMoveSpeed : player.Running ? RaidDashAttack.RunSpeed : RaidActionTempo.WalkSpeed;
            player.Position = ClampPosition(player.Position + move * (moveSpeed * player.MoveSpeedScale * FixedStep));
            // Lunging skills carry the caster forward during their opening (combat state, not presentation).
            if (player.LungeDelay > 0f) player.LungeDelay = Down(player.LungeDelay);
            else if (player.LungeTime > 0f)
            {
                player.Position = ClampPosition(player.Position + new Vector2(player.LungeSpeed * FixedStep, 0f));
                player.LungeTime = Down(player.LungeTime);
            }
            if (player.AttackTime > 0f) return;
            if (player.OverkillTime > 0f && player.OverkillAge >= 65f) {
                pending[(int)SkillId.ExtremeOverkill] = 0f;
                TrySkill(player, SkillId.ExtremeOverkill); return;
            }
            // Awakenings first, then the remaining skills in book order.
            for (int tier = 3; tier >= 0; tier--)
                for (int i = 1; i < SkillBook.Count; i++)
                {
                    SkillDef def = SkillBook.All[i];
                    if (def.Kind == SkillKind.Dodge || def.Tier != tier) continue;
                    if (TryBufferedSkill(player, def.Id, pending)) return;
                }
            if (command != null && command.Basic) TrySkill(player, SkillId.Basic);
        }

        private bool TryBufferedSkill(PlayerState player, SkillId skill, float[] pending)
        {
            int index = (int)skill;
            if (pending[index] <= 0f || pending[index] < Elapsed || !TrySkill(player, skill)) return false;
            pending[index] = 0f;
            return true;
        }

        // These two authored skill leaps are shared by collision and presentation. They do not
        // grant invulnerability: direct impacts still hit, while ground waves use this same height.
        public static float SkillElevation(PlayerState player, float extrapolation = 0f)
        {
            if (player.SkillLeapAge < 0f || player.Hp <= 0f || player.StunTime > 0f) return 0f;
            float from, until, peak;
            if (player.SkillLeapKind == SkillId.OutrageBreak) { from = .10f; until = .55f; peak = 64f / 65f; }
            else if (player.SkillLeapKind == SkillId.BloodSnatch) { from = .43f; until = 1.02f; peak = 56f / 65f; }
            else return 0f;
            float age = player.SkillLeapAge + extrapolation * player.ActionRate;
            if (age <= from || age >= until) return 0f;
            float progress = (age - from) / (until - from);
            return 4f * peak * progress * (1f - progress);
        }

        private static void UpdateJump(PlayerState player)
        {
            if (player.JumpHeight <= 0f && player.JumpVelocity <= 0f) return;
            player.JumpVelocity -= JumpGravity * FixedStep;
            player.JumpHeight += player.JumpVelocity * FixedStep;
            if (player.JumpHeight <= 0f)
            {
                player.JumpHeight = 0f; player.JumpVelocity = 0f;
                player.LandingTime = .10f;
                if (player.ActionAirborne) CancelPlayerAction(player);
            }
        }

        private bool TrySkill(PlayerState player, SkillId skill)
        {
            int index = (int)skill;
            SkillDef def = SkillBook.Get(skill);
            bool finishOverkill = skill == SkillId.ExtremeOverkill && player.OverkillTime > 0f;
            if (!finishOverkill && (player.Cooldowns[index] > 0f || player.Mp < def.Mp)) return false;
            if (skill == SkillId.Frenzy && player.FrenzyTime > 0f)
            {
                player.FrenzyTime = 0f;
                player.Cooldowns[index] = def.Cooldown;
                CancelPlayerAction(player);
                player.AttackTime = .1f;
                _lastBasicTimes[player.Id] = float.NegativeInfinity;
                _lastBasicFrenzy[player.Id] = false;
                Emit("BuffOff", player.Position, index, player.Id);
                return true;
            }
            player.SkillLeapKind = skill;
            player.SkillLeapAge = skill == SkillId.OutrageBreak || skill == SkillId.BloodSnatch ? 0f : -1f;
            BeginPlayerAction(player, skill);
            player.ActionOverkillFinish = finishOverkill;
            if (skill == SkillId.ExtremeOverkill && !finishOverkill) {
                player.Cooldowns[index] = def.Cooldown;
                player.Mp -= def.Mp;
                player.OverkillTime = 65f; player.OverkillAge = 0f;
                player.ActionDuration = .45f; player.AttackTime = .4f;
                player.LungeTime = 0f;
                Emit("Skill", player.Position, index, player.Id);
                return true;
            }
            if (finishOverkill) player.OverkillTime = player.OverkillAge = 0f;
            if (player.ActionDashAttack) def = RaidDashAttack.Attack;
            else if (player.ActionFrenzy) def = RaidFrenzy.Attack(player.BasicComboStep);
            if (!finishOverkill) player.Cooldowns[index] = def.Cooldown / (skill==SkillId.Basic?player.ActionRate:1f);
            player.AttackTime = def.Lock / player.ActionRate;
            if (!finishOverkill) player.Mp -= def.Mp;
            if (def.Invulnerable > 0f) player.Invulnerable = Mathf.Max(player.Invulnerable, def.Invulnerable / player.ActionRate);
            float multiplier = PlayerDamageScale * (player.PhysicalAttack / 100f) * (player.FrenzyTime > 0f ? FrenzyDamage : 1f) * (player.BerserkTime > 0f ? BerserkDamage : 1f);
            if (skill == SkillId.Frenzy) player.FrenzyTime = def.BuffSeconds;
            if (skill == SkillId.Berserk) player.BerserkTime = def.BuffSeconds;
            player.LungeTime = def.AdvanceUntil / player.ActionRate; player.LungeSpeed = def.Advance * player.Facing * player.ActionRate; player.LungeDelay = def.AdvanceDelay / player.ActionRate;
            int hitIndex = 0;
            foreach (HitSpec hit in def.Hits)
                for (int n = 0; n < Mathf.Max(1, hit.Repeat); n++)
                {
                    Vector2 offset = new Vector2(hit.Offset.x * player.Facing, hit.Offset.y);
                    AddZone(new AttackZone
                    {
                        Center = player.Position + offset, HalfSize = hit.Half, Radius = hit.Radius, Shape = hit.Shape,
                        Telegraph = hit.Telegraph, ActiveTime = hit.Active, Recovery = hit.Recovery, Damage = hit.Damage * multiplier,
                        Tag = def.Name, Velocity = new Vector2(hit.Velocity.x * player.Facing, hit.Velocity.y),
                        FromPlayer = true, RoomId = player.RoomId, OwnerId = player.Id, SourceAction = player.ActionSequence, HitIndex = hitIndex++, Skill = index, Facing = player.Facing, Follow = hit.Follow, Offset = offset,
                        PlaybackRate=player.ActionRate, Age = -(hit.Delay + n * hit.Interval)
                    });
                }
            Emit("Skill", player.Position, index, player.Id);
            return true;
        }

        private void BeginPlayerAction(PlayerState player, SkillId skill)
        {
            // Latch the stance for this attack so buff expiry cannot swap its motion halfway through.
            player.ActionDashAttack = skill == SkillId.Basic && player.Running;
            if (player.Running) _runBlocked[player.Id] = true;
            player.Running = false;
            player.ActionFrenzy = skill == SkillId.Basic && !player.ActionDashAttack && RaidFrenzy.GroundCombo(player);
            if (skill == SkillId.Basic)
            {
                bool reset = player.ActionDashAttack || Elapsed - _lastBasicTimes[player.Id] > .8f || _lastBasicFrenzy[player.Id] != player.ActionFrenzy;
                player.BasicComboStep = reset ? 0 : (player.BasicComboStep + 1) % (player.ActionFrenzy ? RaidFrenzy.ComboCount : 3);
                _lastBasicTimes[player.Id] = player.ActionDashAttack ? float.NegativeInfinity : Elapsed;
                _lastBasicFrenzy[player.Id] = player.ActionFrenzy;
            }
            // Awakening cinematics use their authored real-time schedule; other attacks share one latched rate.
            player.ActionRate=RaidActionTempo.Rate(skill,player.AttackSpeedScale);
            player.ActiveSkill = skill;
            player.ActionSequence++;
            player.ActionFacing = player.Facing;
            // Jump launch precedes the first height integration in this tick.
            player.ActionAirborne = player.JumpHeight > 0f || player.JumpVelocity > 0f;
            player.ActionElapsed = 0f;
            player.ActionDuration = player.ActionDashAttack ? RaidDashAttack.Attack.Duration : player.ActionFrenzy ? RaidFrenzy.Attack(player.BasicComboStep).Duration : SkillBook.Get(skill).Duration;
        }

        private static void CancelPlayerAction(PlayerState player)
        {
            player.ActionElapsed = player.ActionDuration = 0f;
            player.ActionOverkillFinish = false;
            player.SkillLeapAge = -1f;
        }

        private void CancelBossAction()
        {
            Boss.ActionElapsed = Boss.ActionDuration = 0f;
        }

        private void UpdateZones()
        {
            // Damage may clear enemy zones through stagger, so iterate a stable ID list.
            AttackZone[] current = Zones.ToArray();
            bool bossActionStarted = false;
            foreach (AttackZone zone in current)
            {
                if (!Zones.Contains(zone)) continue;
                float previousAge = zone.Age;
                zone.Age += FixedStep * zone.PlaybackRate;
                bool crossedActive=zone.FromPlayer && previousAge<zone.Telegraph+zone.ActiveTime && zone.Age>=zone.Telegraph;
                if (!crossedActive && zone.Age >= zone.Telegraph + zone.ActiveTime + zone.Recovery)
                {
                    RemoveZone(zone); continue;
                }
                bool active = crossedActive || zone.Age >= zone.Telegraph && zone.Age < zone.Telegraph + zone.ActiveTime;
                if (!active) continue;
                if (zone.Velocity.sqrMagnitude > 0f) zone.Center += zone.Velocity * FixedStep * zone.PlaybackRate;
                if (zone.Tag == "BossCharge") {
                    Boss.Position=zone.Center;
                    if(TickIndex%2==0 && zone.Velocity.sqrMagnitude>.001f) {
                        var trail=Circle(zone.Center,DawnPatternRules.ChargeTrailRadius,0,DawnPatternRules.ChargeTrailSeconds,0,DawnPatternRules.ChargeTrailDamage,"ChargeTrail");
                        trail.RoomId=zone.RoomId;trail.OwnerId=-1;trail.SourceAction=zone.Id;trail.GroundOnly=true;AddZone(trail);
                    }
                }
                if (zone.Follow) foreach (PlayerState owner in Players) if (owner.Id == zone.OwnerId && owner.RoomId == zone.RoomId) zone.Center = owner.Position + zone.Offset;
                if (!zone.FromPlayer && !IsRoomHazard(zone) && previousAge < zone.Telegraph)
                {
                    if (zone.Tag == "DawnMeteorImpact") Emit("MeteorImpact", zone.Center, zone.Id, -1);
                    if (zone.Tag == "DawnMeteorShockwave") Emit("MeteorShockwave", zone.Center, zone.Id, -1);
                    if (IsStompZone(zone)) Emit("StompImpact", Boss.Position, zone.Id, -1);
                    Boss.AttackTime = .25f;
                    if (!bossActionStarted)
                    {
                        Boss.ActionSequence++;
                        Boss.ActionElapsed = 0f;
                        Boss.ActionDuration = Boss.Phase == 1 ? 1f : .9f;
                        bossActionStarted = true;
                    }
                }
                if (zone.FromPlayer)
                {
                    if (zone.RoomId != BossRoom || Boss.TransitionTime > 0f || Boss.Hp <= 0f || HasHit(zone, -1)) continue;
                    if (Intersects(zone, Boss.Position, BossHurtboxHalfSize)) DamageBoss(zone);
                }
                else
                {
                    foreach (PlayerState player in Players)
                    {
                        if (player.RoomId != zone.RoomId || player.Hp <= 0f || player.Invulnerable > 0f || PracticeInvulnerable || (zone.Tag=="ChargeTrail"?TickIndex<player.NextChargeBurnTick:HasHit(zone, player.Id))) continue;
                        if (zone.GroundOnly && Mathf.Max(player.JumpHeight, SkillElevation(player)) >= JumpDodgeHeight) {
                            if (zone.Age <= zone.Telegraph + FixedStep + CollisionEpsilon && Intersects(zone,player.Position,PlayerHurtboxHalfSize)) Emit(zone.Tag=="DawnMeteorShockwave"?"MeteorJumpAvoid":"JumpAvoid",player.Position,zone.Id,player.Id);
                            continue;
                        }
                        if (Intersects(zone, player.Position, PlayerHurtboxHalfSize)) DamagePlayer(zone, player);
                    }
                }
            }
        }

        private bool HasHit(AttackZone zone, int targetId)
        {
            HashSet<int> targets;
            return _hitTargets.TryGetValue(zone.Id, out targets) && targets.Contains(targetId);
        }

        private void MarkHit(AttackZone zone, int targetId)
        {
            HashSet<int> targets;
            if (!_hitTargets.TryGetValue(zone.Id, out targets))
            {
                targets = new HashSet<int>(); _hitTargets[zone.Id] = targets;
            }
            targets.Add(targetId);
        }

        private void DamagePlayer(AttackZone zone, PlayerState player)
        {
            MarkHit(zone, player.Id);
            // Room mechanics consume a percentage of max HP independent of equipment defence.
            // Shared hit immunity still prevents simultaneous overlapping fire pulses stacking.
            float damage=zone.Tag=="RoomMeteorImpact"?player.MaxHp*RoomMeteorHpFraction:
                zone.Tag=="RoomBurn"?player.MaxHp*RoomBurnHpFraction:zone.Damage*(100f/(100f+player.EquipmentDefence));
            player.Hp = Mathf.Max(0f, player.Hp - damage);
            if(zone.Tag=="ChargeTrail") {
                player.NextChargeBurnTick=TickIndex+60;
                Emit("Damage",player.Position,damage,player.Id);
                return; // Small burns never juggle, stun or knock the player back.
            }
            player.StunTime = .2f;
            player.Running = false;
            _runBlocked[player.Id] = true;
            player.LungeTime = 0f;
            player.Invulnerable = .55f;
            player.AttackTime = 0f;
            CancelPlayerAction(player);
            Vector2 knock = player.Position - zone.Center;
            if (knock.sqrMagnitude < .001f) knock = player.Position - Boss.Position;
            if (knock.sqrMagnitude < .001f) knock = Vector2.left;
            player.Position = ClampPosition(player.Position + knock.normalized * .55f);
            Emit("Damage", player.Position, damage, player.Id);
        }

        private void DamageBoss(AttackZone zone)
        {
            MarkHit(zone, -1);
            float amount = zone.Damage * (Boss.StaggerTime > 0f ? 1.3f : 1f);
            // A large awakening cannot skip the phase-one transformation gate.
            float floor = IntroEnabled && Boss.Phase == 1 ? Boss.MaxHp * .5f : 0f;
            amount = Mathf.Min(amount, Mathf.Max(0f, Boss.Hp - floor));
            Boss.Hp = Mathf.Max(floor, Boss.Hp - amount);
            Emit("Damage", Boss.Position, amount, -1);
            Events[Events.Count-1].SourceId = zone.OwnerId; Events[Events.Count-1].FeedbackKey = $"hit/{zone.OwnerId}/{zone.SourceAction}/{zone.HitIndex}";
            var contact = Events[Events.Count-1];
            contact.Skill = zone.Skill; contact.SourceAction = zone.SourceAction;
            contact.HitIndex = zone.HitIndex; contact.Facing = zone.Facing;
            var owner = Players.Find(p => p.Id == zone.OwnerId);
            if (owner != null && owner.ActionSequence == zone.SourceAction) {
                contact.Combo = owner.BasicComboStep; contact.Frenzy = owner.ActionFrenzy;
                // A new zone advances in its creation tick; latch the contact boundary so
                // the cosmetic hold does not park the explosion just before its first frame.
                contact.ActionTime = Mathf.Max(owner.ActionElapsed, zone.Telegraph);
            }
            if (Boss.StaggerTime > 0f || Boss.Hp <= 0f) return;
            Boss.Stagger += zone.Damage >= 1000f ? 45f : zone.Damage >= 300f ? 22f : zone.Damage >= 200f ? 18f : 4f;
            if (Boss.Stagger >= 100f && !ProtectedVolleyActive) BeginStagger();
        }

        private bool ProtectedVolleyActive => (ProgramPattern==6 || ProgramPattern==DawnPatternRules.Charge) || Zones.Any(z => !z.FromPlayer && (z.Tag == "DawnMeteorImpact" || z.Tag == "DawnMeteorShockwave" || z.Tag == "BossCharge"));

        private void BeginStagger()
        {
            CancelBossProgram();
                Boss.Stagger = 0f; Boss.StaggerTime = 3.5f; Boss.AttackTime = 0f;
                CancelBossAction();
                Boss.PatternName = "무력화 · 공격 기회";
                ClearEnemyZones(); _patternRemaining = .7f; PendingPattern = -1; PendingPatternTick = 0;
                Emit("Stagger", Boss.Position, 3.5f, -1);
        }

        // Footprint collisions are original demo geometry. Sprite height does not expand
        // the depth lane; safe-zone edges require the complete hurtbox to fit inside.
        public static float ZoneHorizontalScale(AttackZone zone)
            => zone.FromPlayer && zone.Skill == (int)SkillId.ExtremeOverkill ? SkillBook.OverkillHorizontalScale : 1f;

        public static bool Intersects(AttackZone zone, Vector2 target, Vector2 halfSize)
        {
            if(zone.Tag=="DawnMeteorImpact")halfSize=Vector2.zero; // Preserve Dawn direct-impact ground-anchor targeting; geometry is the visible ellipse.
            Vector2 d = target - zone.Center;
            float horizontalScale=ZoneHorizontalScale(zone);
            // Map the ellipse and target AABB into the original circle's coordinate space.
            // Normalizing both values retains exact edge overlap without widening the Y lane.
            d.x/=horizontalScale;halfSize.x/=horizontalScale;
            if (zone.Shape == ZoneShape.Box)
                return Mathf.Abs(d.x) <= zone.HalfSize.x + halfSize.x + CollisionEpsilon && Mathf.Abs(d.y) <= zone.HalfSize.y + halfSize.y + CollisionEpsilon;
            float nearX = Mathf.Max(Mathf.Abs(d.x) - halfSize.x, 0f);
            float nearY = Mathf.Max(Mathf.Abs(d.y) - halfSize.y, 0f);
            if (zone.Shape == ZoneShape.Ellipse)
            {
                if (zone.HalfSize.x <= 0f || zone.HalfSize.y <= 0f) return false;
                float normalizedX = nearX / zone.HalfSize.x, normalizedY = nearY / zone.HalfSize.y;
                return normalizedX * normalizedX + normalizedY * normalizedY <= 1f + CollisionEpsilon;
            }
            if (nearX * nearX + nearY * nearY > zone.Radius * zone.Radius + CollisionEpsilon) return false;
            if (zone.Shape == ZoneShape.Circle) return true;
            float farX = Mathf.Abs(d.x) + halfSize.x;
            float farY = Mathf.Abs(d.y) + halfSize.y;
            return farX * farX + farY * farY > zone.InnerRadius * zone.InnerRadius + CollisionEpsilon;
        }

        private void BeginPhaseTwo()
        {
            CancelBossProgram();
            Boss.Phase = 2; Boss.TransitionTime = 1.2f; PendingPattern = -1; PendingPatternTick = 0;
            Boss.StaggerTime = Boss.Stagger = Boss.AttackTime = 0f;
            CancelBossAction();
            Boss.PatternName = "폭룡왕 각성 · 2페이즈";
            ClearEnemyZones(); _patternRemaining = .6f;
            Emit("Phase", Boss.Position, 2f, -1);
            if (IntroEnabled) StartPhaseTransition();
        }

        public void ForcePattern(int index)
        {
            CancelBossProgram();
            if (Mode != EncounterMode.Fighting) return;
            Boss.StaggerTime = Boss.TransitionTime = 0f;
            CancelBossAction();
            ClearEnemyZones();
            BeginPattern(Mathf.Clamp(index, 0, 6));
        }

        private int ChoosePattern()
        {
            int[] pool = Boss.Phase == 1 ? new[] { 0, 2, 3, 5 } : new[] { 0, 1, 2, 4, 5 };
            int next = pool[(int)(NextRandom() % (uint)pool.Length)];
            if (next == _lastPattern) next = pool[(Array.IndexOf(pool, next) + 1) % pool.Length];
            return next;
        }

        private void BeginPattern(int pattern,bool publish=true)
        {
            _lastPattern = pattern;
            float end = 0f;
            switch (pattern)
            {
                case 0:
                    Boss.PatternName = "전방 화염선";
                    PlayerState target = ClosestAlivePlayer();
                    int facing = target != null && target.Position.x > Boss.Position.x ? 1 : -1;
                    AddEnemy(Box(Boss.Position + new Vector2(facing * 2.1f, 0f), new Vector2(2.15f, .85f), .9f, .2f, .75f, 18f, Boss.PatternName), 0f, ref end);
                    break;
                case 1:
                    Boss.PatternName = "메테오 낙인";
                    foreach (PlayerState player in Players)
                    {
                        if (player.Hp <= 0f || player.RoomId!=BossRoom) continue;
                        Vector2 locked = player.Position;
                        AddEnemy(Circle(locked, 1.15f, 1.2f, .2f, .6f, 22f, Boss.PatternName), 0f, ref end);
                        float side = (NextRandom() & 1u) == 0 ? -1f : 1f;
                        Vector2 second = ClampPosition(locked + new Vector2(side * 2.1f, .7f));
                        AddEnemy(Circle(second, 1.1f, 1.2f, .2f, .6f, 22f, Boss.PatternName), .7f, ref end);
                    }
                    break;
                case 2:
                    Boss.PatternName = "용의 낙인 · 문양 안으로";
                    float safeY = (NextRandom() & 1u) == 0 ? -1.3f : 1.3f;
                    AttackZone brand = Circle(new Vector2(-1.5f, safeY), 30f, 2.4f, .2f, 1.5f, 32f, Boss.PatternName);
                    brand.Shape = ZoneShape.Donut; brand.InnerRadius = 1.55f;
                    AddEnemy(brand, 0f, ref end);
                    break;
                case 3:
                    Boss.PatternName = "용의 투기장 · 세 번의 칼날";
                    int firstLane = (int)(NextRandom() % 3u);
                    for (int wave = 0; wave < 3; wave++)
                    {
                        float y = ((firstLane + wave) % 3 - 1) * 2f;
                        AddEnemy(Box(new Vector2(0f, y), new Vector2(9f, .8f), .9f, .18f, .25f, 18f, Boss.PatternName), wave * 1.25f, ref end);
                    }
                    break;
                case 4:
                    Boss.PatternName = "불길 속에서 폭룡왕을 맞이해라";
                    int safeLane = (int)(NextRandom() % 3u);
                    for (int wave = 0; wave < 3; wave++)
                    {
                        int safe = (safeLane + wave) % 3;
                        for (int lane = 0; lane < 3; lane++)
                        {
                            if (lane == safe) continue;
                            AddEnemy(Box(new Vector2(0f, (lane - 1) * 2f), new Vector2(9f, 1f), 1.25f, .18f, .2f, 20f, Boss.PatternName), wave * 1.7f, ref end);
                        }
                    }
                    break;
                case 6:
                    Boss.PatternName = "Dawn 메테오 · 직격 이동 / 충격파 점프";
                    float fall = DawnPatternRules.StartHeight / DawnPatternRules.FallSpeed;
                    var targets=Players.Where(p=>p.Hp>0&&p.RoomId==BossRoom).OrderBy(p=>p.Id).ToArray();
                    for (int wave=0; wave<DawnPatternRules.MeteorCount && targets.Length>0; wave++)
                    {
                        var player=targets[wave%targets.Length];
                        float delay=wave*DawnPatternRules.MeteorInterval;
                        var impact=Ellipse(player.Position,new Vector2(DawnPatternRules.HalfWidth,DawnPatternRules.HalfDepth),fall,.12f,1.5f,DawnPatternRules.ImpactDamage,"DawnMeteorImpact");
                        AddEnemy(impact,delay,ref end);
                        var shock=Box(Vector2.zero,new Vector2(9f,3f),fall,.14f,.5f,16f,"DawnMeteorShockwave");shock.GroundOnly=true;
                        AddEnemy(shock,delay,ref end);
                    }
                    break;
                default:
                    Boss.PatternName = StompTag;
                    for (int wave = 0; wave < 3; wave++)
                    {
                        AttackZone ground = Box(Vector2.zero, new Vector2(9f, 3f), .9f, .14f, .3f, 16f, Boss.PatternName);
                        ground.GroundOnly = true;
                        AddEnemy(ground, wave * 1.3f, ref end);
                    }
                    break;
            }
            _patternRemaining = end + .6f;
            if(publish)Emit("Pattern", Boss.Position, pattern, -1);
        }

        private PlayerState ClosestAlivePlayer()
        {
            PlayerState best = null; float distance = float.MaxValue;
            foreach (PlayerState player in Players)
            {
                if (player.Hp <= 0f || player.RoomId != BossRoom) continue;
                float next = (player.Position - Boss.Position).sqrMagnitude;
                if (next < distance) { best = player; distance = next; }
            }
            return best;
        }

        private void AddEnemy(AttackZone zone, float delay, ref float end)
        {
            zone.Age = -delay; zone.OwnerId = -1;
            AddZone(zone);
            end = Mathf.Max(end, delay + zone.Telegraph + zone.ActiveTime + zone.Recovery);
        }

        private void AddZone(AttackZone zone)
        {
            zone.Id = _nextZoneId++;
            Zones.Add(zone);
        }

        private static AttackZone Box(Vector2 center, Vector2 half, float tell, float active, float recovery, float damage, string tag)
        {
            return new AttackZone { Center = center, HalfSize = half, Shape = ZoneShape.Box, Telegraph = tell, ActiveTime = active, Recovery = recovery, Damage = damage, Tag = tag };
        }

        private static AttackZone Ellipse(Vector2 center, Vector2 half, float tell, float active, float recovery, float damage, string tag)
        {
            return new AttackZone { Center = center, HalfSize = half, Shape = ZoneShape.Ellipse, Telegraph = tell, ActiveTime = active, Recovery = recovery, Damage = damage, Tag = tag };
        }

        private static AttackZone Circle(Vector2 center, float radius, float tell, float active, float recovery, float damage, string tag)
        {
            return new AttackZone { Center = center, Radius = radius, Shape = ZoneShape.Circle, Telegraph = tell, ActiveTime = active, Recovery = recovery, Damage = damage, Tag = tag };
        }

        private void ClearEnemyZones()
        {
            for (int i = Zones.Count - 1; i >= 0; i--)
                if (!Zones[i].FromPlayer && !IsRoomHazard(Zones[i])) { _hitTargets.Remove(Zones[i].Id); Zones.RemoveAt(i); }
        }

        private void RemoveZone(AttackZone zone)
        {
            _hitTargets.Remove(zone.Id); Zones.Remove(zone);
        }

        private bool CheckEnd()
        {
            if (Boss.Hp <= 0f) { End(EncounterMode.Victory); return true; }
            bool anyAlive = false;
            foreach (PlayerState player in Players) if (player.Hp > 0f) { anyAlive = true; break; }
            if(anyAlive)WipeDeadlineTick=0;
            else if(ReviveCharges>0&&Players.Any(p=>!p.Departed&&!p.Disconnected)) {
                if(WipeDeadlineTick==0)WipeDeadlineTick=TickIndex+600;
                if(TickIndex<WipeDeadlineTick&&Elapsed<TimeLimit)return false;
            }
            if (!anyAlive || Elapsed >= TimeLimit) { End(EncounterMode.Defeat); return true; }
            return false;
        }

        private void End(EncounterMode mode)
        {
            Mode = mode; Zones.Clear(); _hitTargets.Clear(); ResetRooms();
            Boss.AttackTime = 0f;
            CancelBossAction();
            foreach (PlayerState player in Players) CancelPlayerAction(player);
            Boss.PatternName = mode == EncounterMode.Victory ? "토벌 성공" : "도전 종료";
            Emit(mode == EncounterMode.Victory ? "Victory" : "Defeat", Boss.Position, Elapsed, -1);
        }

        private void Emit(string kind, Vector2 position, float value, int targetId)
        {
            Events.Add(new CombatEvent { Kind = kind, Position = position, Value = value, TargetId = targetId,
                RoomId = targetId >= 0 ? Players[targetId].RoomId : BossRoom,
                FeedbackKey = kind == "Skill" ? $"skill/{targetId}/{Players[targetId].ActionSequence}" : "" });
        }

        private void AgeEvents()
        {
            for (int i = Events.Count - 1; i >= 0; i--)
            {
                Events[i].Age += FixedStep;
                if (Events[i].Age > 1.6f) Events.RemoveAt(i);
            }
        }

        private uint NextRandom()
        {
            _random ^= _random << 13; _random ^= _random >> 17; _random ^= _random << 5;
            return _random;
        }

        private static float Down(float value) { return Mathf.Max(0f, value - FixedStep); }
        private static float AdvanceAction(float elapsed, float duration, float rate=1f) { return duration > 0f ? Mathf.Min(duration, elapsed + FixedStep * rate) : 0f; }
        private static Vector2 ClampPosition(Vector2 p) { return new Vector2(Mathf.Clamp(p.x, -9f, 9f), Mathf.Clamp(p.y, -3f, 3f)); }
    }
}
