#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectDawn.Combat
{
    public sealed class FighterState
    {
        public int Id, X, Hp, MaxHp, MoveDirection, JumpHeight;
        public long JumpStart = -1, StunnedUntil, MoveUntil;
        public Dictionary<int, long> Cooldowns = new Dictionary<int, long>();
        public bool IsBoss;
        public FighterState Clone() => new FighterState { Id = Id, X = X, Hp = Hp, MaxHp = MaxHp,
            MoveDirection = MoveDirection, JumpHeight = JumpHeight, JumpStart = JumpStart,
            StunnedUntil = StunnedUntil, MoveUntil = MoveUntil, IsBoss = IsBoss,
            Cooldowns = new Dictionary<int, long>(Cooldowns) };
    }

    internal sealed class ActiveAttack
    {
        public HostProposal Proposal;
        public int Center;
        public ActiveAttack Clone() => new ActiveAttack { Proposal = Proposal, Center = Center };
    }

    public sealed class CombatState
    {
        public long Tick;
        public RaidContentState? Content;
        public Dictionary<int, FighterState> Fighters = new Dictionary<int, FighterState>();
        internal List<ActiveAttack> Attacks = new List<ActiveAttack>();
        internal HashSet<string> HitHistory = new HashSet<string>();
        public CombatState Clone() => new CombatState { Tick = Tick, Content = Content?.Clone(),
            Fighters = Fighters.ToDictionary(x => x.Key, x => x.Value.Clone()),
            Attacks = Attacks.Select(x => x.Clone()).ToList(), HitHistory = new HashSet<string>(HitHistory) };
        public static CombatState FourPlayers(int bossX = 0)
        {
            var state = new CombatState();
            for (int id = 1; id <= 4; id++) state.Fighters.Add(id, new FighterState { Id = id, Hp = 100, MaxHp = 100 });
            state.Fighters.Add(100, new FighterState { Id = 100, IsBoss = true, X = bossX, Hp = 1000, MaxHp = 1000 });
            return state;
        }
    }

    public sealed class CombatEvent
    {
        public long Sequence { get; }
        public long EvaluatedTick { get; }
        public string Kind { get; }
        public string Key { get; }
        public InputKey? Origin { get; }
        public int Target { get; }
        public int Damage { get; }
        public int HpAfter { get; }
        internal CombatEvent(long sequence, long tick, string kind, string key, InputKey? origin,
            int target = 0, int damage = 0, int hp = 0)
        { Sequence = sequence; EvaluatedTick = tick; Kind = kind; Key = key; Origin = origin;
            Target = target; Damage = damage; HpAfter = hp; }
        internal CombatEvent Commit(long seq) => new CombatEvent(seq, EvaluatedTick, Kind, Key, Origin, Target, Damage, HpAfter);
    }

    // Pure deterministic replay step. No Lua, network, clock, RNG, audio or database callbacks.
    internal static class CombatStep
    {
        public static List<CombatEvent> Run(CombatState state, long tick, IReadOnlyList<InputReceipt> inputs,
            IReadOnlyList<HostProposal> proposals, CombatRules rules)
        {
            var events = new List<CombatEvent>();
            state.Tick = tick;
            if (state.Content != null) return RaidContentStep.Run(state, tick, inputs, proposals);
            foreach (var receipt in inputs.OrderBy(x => x.Input.Key))
            {
                var input = receipt.Input;
                var player = state.Fighters[input.Key.Player];
                bool usable = player.Hp > 0 && tick >= player.StunnedUntil;
                if (input.Kind == InputKind.Move)
                {
                    player.MoveDirection = usable ? input.Value : 0;
                    player.MoveUntil = tick + rules.MoveLeaseTicks;
                    events.Add(new CombatEvent(0, tick, usable ? "InputCommitted" : "InputRejected", input.Key.ToString(), input.Key));
                }
                else if (input.Kind == InputKind.Jump)
                {
                    usable &= player.JumpStart < 0 || tick - player.JumpStart >= rules.JumpDurationTicks;
                    if (usable) player.JumpStart = tick;
                    events.Add(new CombatEvent(0, tick, usable ? "InputCommitted" : "InputRejected", input.Key.ToString(), input.Key));
                }
                // Attacks are only executed through a matching host proposal below.
            }

            foreach (var fighter in state.Fighters.Values.OrderBy(x => x.Id))
            {
                long age = fighter.JumpStart < 0 ? rules.JumpDurationTicks : tick - fighter.JumpStart;
                fighter.JumpHeight = age >= rules.JumpDurationTicks || fighter.Hp <= 0 ? 0 :
                    checked((int)Math.Min(age, rules.JumpDurationTicks - age) * rules.JumpRisePerTick);
                if (fighter.Hp > 0 && tick >= fighter.StunnedUntil && tick < fighter.MoveUntil)
                    fighter.X = (int)Math.Max(-rules.WorldLimit, Math.Min(rules.WorldLimit,
                        (long)fighter.X + fighter.MoveDirection * (long)rules.MovePerTick));
            }

            var claimedInputs = new HashSet<InputKey>();
            foreach (var proposal in proposals.OrderBy(x => x.Origin.HasValue ? 1 : 0)
                .ThenBy(x => x.Origin ?? new InputKey(x.Encounter, x.Actor, x.HostEpoch, x.Sequence)).ThenBy(x => x.Sequence))
            {
                var actor = state.Fighters[proposal.Actor];
                rules.TryAttack(proposal.Definition, out var definition);
                bool valid = actor.Hp > 0 && tick >= actor.StunnedUntil && actor.IsBoss == definition.FromBoss;
                if (proposal.Origin.HasValue)
                {
                    var origin = proposal.Origin.Value;
                    valid &= claimedInputs.Add(origin) && inputs.Any(x => x.Input.Key.Equals(origin) &&
                        x.Input.Kind == InputKind.Attack && x.Input.Value == proposal.Definition);
                }
                else valid &= actor.IsBoss;
                valid &= !actor.Cooldowns.TryGetValue(definition.Id, out long ready) || tick >= ready;
                if (valid)
                {
                    actor.Cooldowns[definition.Id] = tick + definition.CooldownTicks;
                    state.Attacks.Add(new ActiveAttack { Proposal = proposal, Center = actor.X });
                }
                events.Add(new CombatEvent(0, tick, valid ? "ActionCommitted" : "ActionRejected",
                    proposal.Id, proposal.Origin));
            }
            foreach (var input in inputs.Where(x => x.Input.Kind == InputKind.Attack))
                if (!claimedInputs.Contains(input.Input.Key))
                    events.Add(new CombatEvent(0, tick, "InputRejected", input.Input.Key.ToString(), input.Input.Key));

            foreach (var attack in state.Attacks.OrderBy(x => x.Proposal.StartTick)
                .ThenBy(x => x.Proposal.Actor).ThenBy(x => x.Proposal.Origin?.Sequence ?? x.Proposal.Sequence).ToArray())
            {
                var p = attack.Proposal;
                rules.TryAttack(p.Definition, out var def);
                long elapsed = tick - p.StartTick - def.DelayTicks;
                var owner = state.Fighters[p.Actor];
                if (owner.Hp <= 0 && !def.SurvivesOwnerDeath)
                {
                    state.Attacks.Remove(attack);
                    events.Add(new CombatEvent(0, tick, "ActionCancelled", p.Id, p.Origin));
                    continue;
                }
                if (elapsed < 0 || elapsed % def.IntervalTicks != 0) continue;
                long pulse = elapsed / def.IntervalTicks;
                if (pulse >= def.Repeat) continue;
                foreach (var target in state.Fighters.Values.Where(x => x.IsBoss != owner.IsBoss).OrderBy(x => x.Id))
                {
                    if (target.Hp <= 0 || Math.Abs((long)target.X - attack.Center) > def.Range) continue;
                    string hitKey = $"{p.Id}/s0/t{target.Id}/r{pulse}";
                    if (!state.HitHistory.Add(hitKey)) continue;
                    bool dodge = def.GroundOnly && target.JumpHeight >= rules.DodgeHeight;
                    int damage = dodge ? 0 : Math.Min(target.Hp, def.Damage);
                    target.Hp -= damage;
                    if (!dodge)
                    {
                        target.StunnedUntil = Math.Max(target.StunnedUntil, tick + def.StunTicks);
                        target.X = (int)Math.Max(-rules.WorldLimit, Math.Min(rules.WorldLimit, (long)target.X + def.Knockback));
                    }
                    events.Add(new CombatEvent(0, tick, dodge ? "Evaded" : "HitResolved", hitKey,
                        p.Origin, target.Id, damage, target.Hp));
                }
                if (pulse == def.Repeat - 1)
                {
                    state.Attacks.Remove(attack);
                    events.Add(new CombatEvent(0, tick, "AttackWindowClosed", p.Id, p.Origin));
                }
            }
            // Closed attacks cannot hit again; retain only active attack history in checkpoints.
            var activeIds = state.Attacks.Select(x => x.Proposal.Id + "/").ToArray();
            state.HitHistory.RemoveWhere(x => !activeIds.Any(prefix => x.StartsWith(prefix, StringComparison.Ordinal)));
            return events;
        }
    }
}
