using System;
using System.Collections.Generic;
using System.Linq;
using DnfRaidDemo;
using ProjectDawn.Combat;

internal static partial class Program
{
    static RaidSimulation ReferenceTraining()
    {
        var simulation = new RaidSimulation(4) { AutoBuffs = false };
        simulation.Start();
        simulation.Boss.StaggerTime = 1000f;
        simulation.Boss.Hp = simulation.Boss.MaxHp = 1000000f;
        simulation.Players[0].Position = simulation.Boss.Position + new RaidVector(-1f, 0f);
        simulation.Players[0].Mp = simulation.Players[0].MaxMp = 1000f;
        return simulation;
    }

    static void ReferenceIdle(RaidSimulation simulation, int ticks)
    {
        for (int i = 0; i < ticks; i++) simulation.Tick(RaidSimulation.FixedStep, Array.Empty<PlayerCommand>());
    }

    static void ReferenceCast(RaidSimulation simulation, SkillId skill)
    {
        simulation.Tick(RaidSimulation.FixedStep, new[] { new PlayerCommand { PlayerId = 0, Skills = PlayerCommand.Bit(skill) } });
    }

    static void TestSkillReference()
    {
        var armed = ReferenceTraining();
        var actor = armed.Players[0];
        ReferenceCast(armed, SkillId.ExtremeOverkill);
        Check(actor.OverkillTime > 64f && actor.ActionSequence == 1 && !actor.ActionOverkillFinish
            && !armed.Zones.Any(z => z.FromPlayer), "Overkill first input arms sword without premature attack zones");
        Check(Math.Abs(actor.Mp - 960f) < .001f, "Overkill pays 40 MP once when arming");
        ReferenceIdle(armed, 65);
        actor.Cooldowns[(int)SkillId.BloodSword] = 5f;
        int beforeOtherSkill = actor.ActionSequence;
        ReferenceCast(armed, SkillId.BloodSword);
        Check(actor.ActionSequence == beforeOtherSkill && actor.OverkillTime > 0f,
            "armed Overkill does not bypass unrelated skill cooldowns");
        ReferenceIdle(armed, 24); // Expire the rejected skill's normal input buffer.
        float beforeMana = actor.Mp;
        Check(actor.Cooldowns[(int)SkillId.ExtremeOverkill] > 0f, "Overkill finisher is requested during original cooldown");
        ReferenceCast(armed, SkillId.ExtremeOverkill);
        Check(actor.ActionOverkillFinish && actor.ActionSequence == 2 && actor.OverkillTime == 0f,
            "Overkill second input releases armed finisher despite its own cooldown");
        Check(Math.Abs(actor.Mp - Math.Min(actor.MaxMp, beforeMana + RaidSimulation.MpRegen * RaidSimulation.FixedStep)) < .001f,
            "Overkill finisher does not charge another MP cost");
        Check(armed.Zones.Count(z => z.FromPlayer && z.Skill == (int)SkillId.ExtremeOverkill) == 10,
            "Overkill finisher produces one deterministic ten-hit sequence");
        var confirmedHits = new HashSet<string>();
        for (int tick = 0; tick < 240; tick++)
        {
            ReferenceIdle(armed, 1);
            foreach (var ev in armed.Events)
                if (ev.Kind == "Damage" && ev.TargetId == -1 && ev.SourceId == 0 && ev.FeedbackKey.StartsWith("hit/0/2/")) confirmedHits.Add(ev.FeedbackKey);
        }
        Check(confirmedHits.Count == 10, "all ten lingering Overkill hits resolve once against the target");

        var source = ReferenceTraining();
        ReferenceCast(source, SkillId.ExtremeOverkill);
        ReferenceIdle(source, 72);
        var restored = RaidSnapshotCodec.Decode(RaidSnapshotCodec.Encode(source));
        Check(restored.Players[0].OverkillTime == source.Players[0].OverkillTime
            && restored.Players[0].OverkillAge == source.Players[0].OverkillAge,
            "armed sword duration and age survive the network checkpoint");
        ReferenceCast(source, SkillId.ExtremeOverkill);
        ReferenceCast(restored, SkillId.ExtremeOverkill);
        ReferenceIdle(source, 300);
        ReferenceIdle(restored, 300);
        Check(RaidSnapshotCodec.Encode(source).SequenceEqual(RaidSnapshotCodec.Encode(restored)),
            "restored host and server replay the same Overkill finisher and lingering hits");

        var expired = ReferenceTraining();
        ReferenceCast(expired, SkillId.ExtremeOverkill);
        ReferenceIdle(expired, 64 * 60);
        Check(expired.Players[0].OverkillTime > 0f && expired.Players[0].ActionSequence == 1,
            "Overkill retains its armed state before the 65-second deadline");
        ReferenceIdle(expired, 120);
        Check(expired.Players[0].OverkillTime == 0f && expired.Players[0].ActionSequence == 2
            && expired.Players[0].ActionOverkillFinish,
            "65-second armed expiry automatically releases one finisher");
        ReferenceIdle(expired, 360);
        Check(expired.Players[0].ActionSequence == 2, "expired Overkill cannot repeatedly auto-cast");

        var dead = ReferenceTraining();
        ReferenceCast(dead, SkillId.ExtremeOverkill);
        dead.Players[0].Hp = 0f;
        ReferenceIdle(dead, 1);
        Check(dead.Players[0].OverkillTime == 0f && dead.Players[0].OverkillAge == 0f
            && !dead.Players[0].ActionOverkillFinish && !dead.Zones.Any(z => z.FromPlayer),
            "death clears the armed sword without releasing its attack");
        TestSkillElevation();
        TestOverkillWidth();
    }

    static void TestSkillElevation()
    {
        foreach (var skill in new[] { SkillId.OutrageBreak, SkillId.BloodSnatch })
        {
            float launch = skill == SkillId.OutrageBreak ? .1f : .43f;
            float landing = skill == SkillId.OutrageBreak ? .55f : 1.02f;
            float apex = (launch + landing) / 2f;
            var simulation = ReferenceTraining();
            ReferenceCast(simulation, skill);
            var actor = simulation.Players[0];
            for (int tick = 0; actor.SkillLeapAge < apex && tick < 120; tick++) ReferenceIdle(simulation, 1);
            Check(actor.JumpHeight == 0f && RaidSimulation.SkillElevation(actor) >= RaidSimulation.JumpDodgeHeight,
                skill + " actual cast reaches authoritative skill-hop dodge height");

            var boundary = actor.Clone();
            boundary.SkillLeapAge = launch - .001f;
            bool beforeLaunchGrounded = RaidSimulation.SkillElevation(boundary) == 0f;
            boundary.SkillLeapAge = landing + .001f;
            Check(beforeLaunchGrounded && RaidSimulation.SkillElevation(boundary) == 0f,
                skill + " skill elevation is zero before launch and after landing");
            boundary = actor.Clone(); boundary.StunTime = .1f;
            Check(RaidSimulation.SkillElevation(boundary) == 0f, skill + " stunned actor cannot retain skill-hop evasion");
            var canceled = simulation.Clone();
            canceled.Tick(RaidSimulation.FixedStep, new[] { new PlayerCommand { PlayerId = 0, Dash = true } });
            Check(canceled.Players[0].DashTime > 0f && RaidSimulation.SkillElevation(canceled.Players[0]) == 0f,
                skill + " real dash cancellation clears authoritative skill-hop height");
            boundary = actor.Clone(); boundary.Hp = 0f;
            Check(RaidSimulation.SkillElevation(boundary) == 0f, skill + " dead actor has no skill-hop height");
            boundary = actor.Clone(); boundary.SkillLeapAge = launch + .025f;
            var advanced = boundary.Clone(); advanced.SkillLeapAge += .05f * advanced.ActionRate;
            Check(Math.Abs(RaidSimulation.SkillElevation(boundary, .05f) - RaidSimulation.SkillElevation(advanced)) < .00001f,
                skill + " visual extrapolation and server leap time use the same height curve");
            boundary = actor.Clone();
            boundary.ActiveSkill = SkillId.Basic; boundary.ActionDuration = 0f; boundary.ActionElapsed = 99f;
            boundary.ActionAirborne = false; boundary.ActionFacing = -1; boundary.ActionSequence = 987;
            Check(RaidSimulation.SkillElevation(boundary) == RaidSimulation.SkillElevation(actor),
                skill + " presentation identity and clocks cannot alter authoritative hop height");

            var direct = simulation.Clone();
            foreach (var current in new[] { simulation, direct })
                current.Zones.Add(new AttackZone { Id = 9000, Center = current.Players[0].Position, HalfSize = new RaidVector(1, 1),
                    Shape = ZoneShape.Box, Damage = 20f, Telegraph = 0f, ActiveTime = .08f, Recovery = 0f,
                    GroundOnly = ReferenceEquals(current, simulation), Tag = "SkillElevationTest" });
            float hp = actor.Hp;
            ReferenceIdle(simulation, 1);
            ReferenceIdle(direct, 1);
            Check(actor.Hp == hp && simulation.Events.Any(ev => ev.Kind == "JumpAvoid" && ev.TargetId == 0 && ev.Value == 9000),
                skill + " real ground shock resolves as an avoid during its skill hop");
            Check(direct.Players[0].Hp == hp - 20f && RaidSimulation.SkillElevation(direct.Players[0]) == 0f,
                skill + " direct hit still damages and cancels the airborne skill action");
        }
    }
}
