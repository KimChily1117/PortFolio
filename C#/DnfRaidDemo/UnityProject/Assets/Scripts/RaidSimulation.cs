using System;
using System.Collections.Generic;
using UnityEngine;

namespace DnfRaidDemo
{
    public enum SkillId { Basic, ArcSlash, FlameWave, Dash, Awakening }
    public enum EncounterMode { Ready, Fighting, Victory, Defeat }
    public enum ZoneShape { Circle, Box, Donut }

    [Serializable]
    public class PlayerCommand
    {
        public int PlayerId;
        public Vector2 Move;
        public bool Basic, ArcSlash, FlameWave, Dash, Awakening, Jump;
    }

    [Serializable]
    public class PlayerState
    {
        public int Id;
        public Vector2 Position;
        public float Hp, MaxHp, Invulnerable, AttackTime, StunTime;
        public int Facing;
        public float[] Cooldowns = new float[5];
        public float DashTime;
        public Vector2 DashDirection;
        public float JumpHeight, JumpVelocity;
        // Presentation state only. Combat locks still use AttackTime and StunTime.
        public SkillId ActiveSkill;
        public int ActionSequence, BasicComboStep, ActionFacing = 1;
        public bool ActionAirborne;
        public float ActionElapsed, ActionDuration, LandingTime;
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
    }

    [Serializable]
    public class AttackZone
    {
        public int Id, OwnerId;
        public Vector2 Center, HalfSize;
        public float Radius, InnerRadius, Age, Telegraph, ActiveTime, Recovery, Damage;
        public bool FromPlayer;
        public ZoneShape Shape;
        public string Tag;
        public Vector2 Velocity;
        public bool GroundOnly;
    }

    [Serializable]
    public class CombatEvent
    {
        public string Kind;
        public Vector2 Position;
        public float Value, Age;
        public int TargetId;
    }

    // Independent demo mechanics and tuning, not recovered DNF game source or protocol.
    // The host alone owns this state machine. Remote clients display its public snapshots.
    public class RaidSimulation
    {
        public const float FixedStep = 1f / 60f;
        public const float CollisionEpsilon = .0001f;
        public const float InputBufferSeconds = .35f;
        public const float JumpDodgeHeight = .35f;
        public const float JumpLaunchVelocity = 5.8f;
        public const float JumpGravity = 18f;
        private const int JumpBufferSlot = 5;
        public List<PlayerState> Players = new List<PlayerState>();
        public BossState Boss = new BossState();
        public List<AttackZone> Zones = new List<AttackZone>();
        public List<CombatEvent> Events = new List<CombatEvent>();
        public EncounterMode Mode;
        public float Elapsed, TimeLimit = 180f;
        public int TickIndex;
        public bool PracticeInvulnerable;

        public static readonly Vector2 PlayerHurtboxHalfSize = new Vector2(.24f, .17f);
        public static readonly Vector2 BossHurtboxHalfSize = new Vector2(.75f, .45f);
        private readonly int _playerCount;
        private readonly uint _initialSeed;
        private uint _random;
        private float _accumulator, _patternRemaining;
        private int _nextZoneId, _lastPattern = -1;
        private readonly Dictionary<int, HashSet<int>> _hitTargets = new Dictionary<int, HashSet<int>>();
        private readonly Dictionary<int, float[]> _inputBuffers = new Dictionary<int, float[]>();
        private readonly Dictionary<int, float> _lastBasicTimes = new Dictionary<int, float>();

        public RaidSimulation(int playerCount = 1, int seed = 42)
        {
            _playerCount = Mathf.Clamp(playerCount, 1, 2);
            _initialSeed = seed == 0 ? 42u : unchecked((uint)seed);
            ResetState();
        }

        private void ResetState()
        {
            Players.Clear();
            _inputBuffers.Clear();
            _lastBasicTimes.Clear();
            for (int i = 0; i < _playerCount; i++)
            {
                _inputBuffers[i] = new float[6];
                _lastBasicTimes[i] = float.NegativeInfinity;
                Players.Add(new PlayerState
                {
                    Id = i, Position = new Vector2(-4f, _playerCount == 1 ? 0f : (i == 0 ? -.8f : .8f)),
                    Hp = 100f, MaxHp = 100f, Facing = 1
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
            Elapsed = 0f; TickIndex = 0; Mode = EncounterMode.Ready;
        }

        public void Start()
        {
            ResetState();
            Mode = EncounterMode.Fighting;
            Boss.PatternName = "전투 시작";
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
            Elapsed += FixedStep;
            if (CheckEnd()) return;

            Boss.AttackTime = Down(Boss.AttackTime);
            Boss.StaggerTime = Down(Boss.StaggerTime);
            Boss.TransitionTime = Down(Boss.TransitionTime);
            Boss.ActionElapsed = AdvanceAction(Boss.ActionElapsed, Boss.ActionDuration);
            foreach (PlayerState player in Players)
            {
                player.Invulnerable = Down(player.Invulnerable);
                player.AttackTime = Down(player.AttackTime);
                player.StunTime = Down(player.StunTime);
                player.ActionElapsed = AdvanceAction(player.ActionElapsed, player.ActionDuration);
                player.LandingTime = Down(player.LandingTime);
                for (int i = 0; i < player.Cooldowns.Length; i++) player.Cooldowns[i] = Down(player.Cooldowns[i]);
                if (player.Hp <= 0f)
                {
                    player.DashTime = player.JumpHeight = player.JumpVelocity = 0f;
                    player.LandingTime = 0f;
                    CancelPlayerAction(player);
                    Array.Clear(_inputBuffers[player.Id], 0, _inputBuffers[player.Id].Length);
                    continue;
                }
                PlayerCommand command = FindCommand(commands, player.Id);
                UpdatePlayer(player, command);
                UpdateJump(player);
            }

            if (Boss.Phase == 1 && Boss.Hp <= Boss.MaxHp * .5f) BeginPhaseTwo();
            if (Boss.StaggerTime <= 0f && Boss.TransitionTime <= 0f)
            {
                _patternRemaining -= FixedStep;
                if (_patternRemaining <= 0f) BeginPattern(ChoosePattern());
            }
            UpdateZones();
            if (!CheckEnd() && Boss.Phase == 1 && Boss.Hp <= Boss.MaxHp * .5f) BeginPhaseTwo();
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
                if (command.ArcSlash) pending[(int)SkillId.ArcSlash] = Elapsed + InputBufferSeconds;
                if (command.FlameWave) pending[(int)SkillId.FlameWave] = Elapsed + InputBufferSeconds;
                if (command.Awakening) pending[(int)SkillId.Awakening] = Elapsed + InputBufferSeconds;
                if (command.Dash) pending[(int)SkillId.Dash] = Elapsed + InputBufferSeconds;
                if (command.Jump) pending[JumpBufferSlot] = Elapsed + InputBufferSeconds;
            }
            Vector2 move = command == null ? Vector2.zero : command.Move;
            if (float.IsNaN(move.x) || float.IsInfinity(move.x) || float.IsNaN(move.y) || float.IsInfinity(move.y)) move = Vector2.zero;
            if (move.sqrMagnitude > 1f) move.Normalize();
            if (Mathf.Abs(move.x) > .01f) player.Facing = move.x < 0f ? -1 : 1;

            if (pending[JumpBufferSlot] > 0f && pending[JumpBufferSlot] >= Elapsed && player.JumpHeight <= 0f && player.JumpVelocity <= 0f && player.StunTime <= 0f)
            {
                pending[JumpBufferSlot] = 0f;
                player.JumpVelocity = JumpLaunchVelocity;
                player.LandingTime = 0f;
                Emit("Jump", player.Position, JumpLaunchVelocity, player.Id);
            }

            if (pending[(int)SkillId.Dash] > 0f && pending[(int)SkillId.Dash] >= Elapsed && player.Cooldowns[(int)SkillId.Dash] <= 0f && player.StunTime <= 0f)
            {
                pending[(int)SkillId.Dash] = 0f;
                player.DashDirection = move.sqrMagnitude > .01f ? move.normalized : new Vector2(player.Facing, 0f);
                player.DashTime = .2f;
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
            if (player.StunTime > 0f) return;
            float moveSpeed = player.AttackTime > 0f ? 1.8f : 4.8f;
            player.Position = ClampPosition(player.Position + move * (moveSpeed * FixedStep));
            if (player.AttackTime > 0f) return;
            if (TryBufferedSkill(player, SkillId.Awakening, pending)) return;
            if (TryBufferedSkill(player, SkillId.FlameWave, pending)) return;
            if (TryBufferedSkill(player, SkillId.ArcSlash, pending)) return;
            if (command != null && command.Basic) TrySkill(player, SkillId.Basic);
        }

        private bool TryBufferedSkill(PlayerState player, SkillId skill, float[] pending)
        {
            int index = (int)skill;
            if (pending[index] <= 0f || pending[index] < Elapsed || !TrySkill(player, skill)) return false;
            pending[index] = 0f;
            return true;
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
            if (player.Cooldowns[index] > 0f) return false;
            AttackZone zone;
            Vector2 forward = new Vector2(player.Facing, 0f);
            switch (skill)
            {
                case SkillId.Basic:
                    player.Cooldowns[index] = .33f; player.AttackTime = .23f;
                    zone = Box(player.Position + forward * .9f, new Vector2(.85f, .48f), .06f, .12f, .12f, 45f, "기본 베기");
                    break;
                case SkillId.ArcSlash:
                    player.Cooldowns[index] = 3f; player.AttackTime = .42f;
                    zone = Box(player.Position + forward * 1.15f, new Vector2(1.2f, .95f), .14f, .16f, .22f, 220f, "반월 베기");
                    break;
                case SkillId.FlameWave:
                    player.Cooldowns[index] = 5.5f; player.AttackTime = .35f;
                    zone = Box(player.Position + forward * .8f, new Vector2(.6f, .55f), .12f, .7f, .12f, 350f, "화염 검기");
                    zone.Velocity = forward * 7f;
                    break;
                case SkillId.Awakening:
                    player.Cooldowns[index] = 18f; player.AttackTime = .95f;
                    zone = Circle(player.Position, 3.4f, .45f, .2f, .4f, 1100f, "각성 · 천공참");
                    player.Invulnerable = Mathf.Max(player.Invulnerable, .65f);
                    break;
                default: return false;
            }
            zone.FromPlayer = true; zone.OwnerId = player.Id;
            AddZone(zone);
            BeginPlayerAction(player, skill);
            Emit("Skill", player.Position, index, player.Id);
            return true;
        }

        private void BeginPlayerAction(PlayerState player, SkillId skill)
        {
            if (skill == SkillId.Basic)
            {
                player.BasicComboStep = Elapsed - _lastBasicTimes[player.Id] > .8f ? 0 : (player.BasicComboStep + 1) % 3;
                _lastBasicTimes[player.Id] = Elapsed;
            }
            player.ActiveSkill = skill;
            player.ActionSequence++;
            player.ActionFacing = player.Facing;
            // Jump launch precedes the first height integration in this tick.
            player.ActionAirborne = player.JumpHeight > 0f || player.JumpVelocity > 0f;
            player.ActionElapsed = 0f;
            player.ActionDuration = skill == SkillId.Basic ? .33f : skill == SkillId.ArcSlash ? .52f : skill == SkillId.FlameWave ? .45f : 1.05f;
        }

        private static void CancelPlayerAction(PlayerState player)
        {
            player.ActionElapsed = player.ActionDuration = 0f;
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
                zone.Age += FixedStep;
                if (zone.Age >= zone.Telegraph + zone.ActiveTime + zone.Recovery)
                {
                    RemoveZone(zone); continue;
                }
                bool active = zone.Age >= zone.Telegraph && zone.Age < zone.Telegraph + zone.ActiveTime;
                if (!active) continue;
                if (zone.Velocity.sqrMagnitude > 0f) zone.Center += zone.Velocity * FixedStep;
                if (!zone.FromPlayer && previousAge < zone.Telegraph)
                {
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
                    if (Boss.TransitionTime > 0f || Boss.Hp <= 0f || HasHit(zone, -1)) continue;
                    if (Intersects(zone, Boss.Position, BossHurtboxHalfSize)) DamageBoss(zone);
                }
                else
                {
                    foreach (PlayerState player in Players)
                    {
                        if (player.Hp <= 0f || player.Invulnerable > 0f || PracticeInvulnerable || HasHit(zone, player.Id)) continue;
                        if (zone.GroundOnly && player.JumpHeight >= JumpDodgeHeight) continue;
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
            player.Hp = Mathf.Max(0f, player.Hp - zone.Damage);
            player.StunTime = .2f;
            player.Invulnerable = .55f;
            player.AttackTime = 0f;
            CancelPlayerAction(player);
            Vector2 knock = player.Position - zone.Center;
            if (knock.sqrMagnitude < .001f) knock = player.Position - Boss.Position;
            if (knock.sqrMagnitude < .001f) knock = Vector2.left;
            player.Position = ClampPosition(player.Position + knock.normalized * .55f);
            Emit("Damage", player.Position, zone.Damage, player.Id);
        }

        private void DamageBoss(AttackZone zone)
        {
            MarkHit(zone, -1);
            float amount = zone.Damage * (Boss.StaggerTime > 0f ? 1.3f : 1f);
            Boss.Hp = Mathf.Max(0f, Boss.Hp - amount);
            Emit("Damage", Boss.Position, amount, -1);
            if (Boss.StaggerTime > 0f || Boss.Hp <= 0f) return;
            Boss.Stagger += zone.Damage >= 1000f ? 45f : zone.Damage >= 300f ? 22f : zone.Damage >= 200f ? 18f : 4f;
            if (Boss.Stagger >= 100f)
            {
                Boss.Stagger = 0f; Boss.StaggerTime = 3.5f; Boss.AttackTime = 0f;
                CancelBossAction();
                Boss.PatternName = "무력화 · 공격 기회";
                ClearEnemyZones(); _patternRemaining = .7f;
                Emit("Stagger", Boss.Position, 3.5f, -1);
            }
        }

        // Footprint collisions are original demo geometry. Sprite height does not expand
        // the depth lane; safe-zone edges require the complete hurtbox to fit inside.
        public static bool Intersects(AttackZone zone, Vector2 target, Vector2 halfSize)
        {
            Vector2 d = target - zone.Center;
            if (zone.Shape == ZoneShape.Box)
                return Mathf.Abs(d.x) <= zone.HalfSize.x + halfSize.x + CollisionEpsilon && Mathf.Abs(d.y) <= zone.HalfSize.y + halfSize.y + CollisionEpsilon;
            float nearX = Mathf.Max(Mathf.Abs(d.x) - halfSize.x, 0f);
            float nearY = Mathf.Max(Mathf.Abs(d.y) - halfSize.y, 0f);
            if (nearX * nearX + nearY * nearY > zone.Radius * zone.Radius + CollisionEpsilon) return false;
            if (zone.Shape == ZoneShape.Circle) return true;
            float farX = Mathf.Abs(d.x) + halfSize.x;
            float farY = Mathf.Abs(d.y) + halfSize.y;
            return farX * farX + farY * farY > zone.InnerRadius * zone.InnerRadius + CollisionEpsilon;
        }

        private void BeginPhaseTwo()
        {
            Boss.Phase = 2; Boss.TransitionTime = 1.2f;
            Boss.StaggerTime = Boss.Stagger = Boss.AttackTime = 0f;
            CancelBossAction();
            Boss.PatternName = "폭룡왕 각성 · 2페이즈";
            ClearEnemyZones(); _patternRemaining = .6f;
            Emit("Phase", Boss.Position, 2f, -1);
        }

        public void ForcePattern(int index)
        {
            if (Mode != EncounterMode.Fighting) return;
            Boss.StaggerTime = Boss.TransitionTime = 0f;
            CancelBossAction();
            ClearEnemyZones();
            BeginPattern(Mathf.Clamp(index, 0, 5));
        }

        private int ChoosePattern()
        {
            int[] pool = Boss.Phase == 1 ? new[] { 0, 2, 3, 5 } : new[] { 0, 1, 2, 4, 5 };
            int next = pool[(int)(NextRandom() % (uint)pool.Length)];
            if (next == _lastPattern) next = pool[(Array.IndexOf(pool, next) + 1) % pool.Length];
            return next;
        }

        private void BeginPattern(int pattern)
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
                        if (player.Hp <= 0f) continue;
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
                default:
                    Boss.PatternName = "지면 충격파 · 점프로 회피";
                    for (int wave = 0; wave < 3; wave++)
                    {
                        AttackZone ground = Box(Vector2.zero, new Vector2(9f, 3f), .9f, .14f, .3f, 16f, Boss.PatternName);
                        ground.GroundOnly = true;
                        AddEnemy(ground, wave * 1.3f, ref end);
                    }
                    break;
            }
            _patternRemaining = end + .6f;
            Emit("Pattern", Boss.Position, pattern, -1);
        }

        private PlayerState ClosestAlivePlayer()
        {
            PlayerState best = null; float distance = float.MaxValue;
            foreach (PlayerState player in Players)
            {
                if (player.Hp <= 0f) continue;
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

        private static AttackZone Circle(Vector2 center, float radius, float tell, float active, float recovery, float damage, string tag)
        {
            return new AttackZone { Center = center, Radius = radius, Shape = ZoneShape.Circle, Telegraph = tell, ActiveTime = active, Recovery = recovery, Damage = damage, Tag = tag };
        }

        private void ClearEnemyZones()
        {
            for (int i = Zones.Count - 1; i >= 0; i--)
                if (!Zones[i].FromPlayer) { _hitTargets.Remove(Zones[i].Id); Zones.RemoveAt(i); }
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
            if (!anyAlive || Elapsed >= TimeLimit) { End(EncounterMode.Defeat); return true; }
            return false;
        }

        private void End(EncounterMode mode)
        {
            Mode = mode; Zones.Clear(); _hitTargets.Clear();
            Boss.AttackTime = 0f;
            CancelBossAction();
            foreach (PlayerState player in Players) CancelPlayerAction(player);
            Boss.PatternName = mode == EncounterMode.Victory ? "토벌 성공" : "도전 종료";
            Emit(mode == EncounterMode.Victory ? "Victory" : "Defeat", Boss.Position, Elapsed, -1);
        }

        private void Emit(string kind, Vector2 position, float value, int targetId)
        {
            Events.Add(new CombatEvent { Kind = kind, Position = position, Value = value, TargetId = targetId });
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
        private static float AdvanceAction(float elapsed, float duration) { return duration > 0f ? Mathf.Min(duration, elapsed + FixedStep) : 0f; }
        private static Vector2 ClampPosition(Vector2 p) { return new Vector2(Mathf.Clamp(p.x, -9f, 9f), Mathf.Clamp(p.y, -3f, 3f)); }
    }
}
