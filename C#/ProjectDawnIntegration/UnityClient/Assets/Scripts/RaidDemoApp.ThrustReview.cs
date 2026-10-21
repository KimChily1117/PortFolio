using System;
using System.IO;
using UnityEngine;

namespace DnfRaidDemo
{
    public sealed partial class RaidDemoApp
    {
        bool thrustReview;
        readonly RaidRunInput reviewRunInput = new RaidRunInput();
        int reviewLastDirection, reviewSeenSequence;
        [Serializable] class ThrustReviewResult { public int errors, ticks, bloodSwordCasts, runningAttacks; public bool frenzyAfter; }
        readonly ThrustReviewResult thrustResult = new ThrustReviewResult();
        bool reviewBloodRight, reviewBloodLeft, reviewFrenzy;
        PlayerCommand ThrustReviewCommand(int id)
        {
            float t = sim.Elapsed;
            var c = new PlayerCommand { PlayerId = id };
            if (t >= 1f && !reviewBloodRight) { reviewBloodRight = true; c.Skills = PlayerCommand.Bit(SkillId.BloodSword); }
            if ((t >= 3f && t < 3.08f) || (t >= 3.18f && t < 3.8f)) c.Move = Vector2.right;
            if (t >= 3.52f && t < 3.55f) c.Basic = true;
            if (t >= 5f && !reviewFrenzy) { reviewFrenzy = true; c.Skills = PlayerCommand.Bit(SkillId.Frenzy); }
            if ((t >= 7f && t < 7.08f) || (t >= 7.18f && t < 7.8f)) c.Move = Vector2.left;
            if (t >= 7.52f && t < 7.55f) c.Basic = true;
            if (t >= 9f && !reviewBloodLeft) { reviewBloodLeft = true; c.Skills = PlayerCommand.Bit(SkillId.BloodSword); }
            int held = (int)c.Move.x;
            reviewRunInput.Sample(held, held != reviewLastDirection ? held : 0, t);
            reviewLastDirection = held;
            c.Run = reviewRunInput.Running;
            return c;
        }
        void UpdateThrustReview()
        {
            if (sim == null) return;
            PlayerState p = sim.Players[0];
            if (p.ActionSequence != reviewSeenSequence)
            {
                reviewSeenSequence = p.ActionSequence;
                if (p.ActionDashAttack) thrustResult.runningAttacks++;
                if (p.ActiveSkill == SkillId.BloodSword) thrustResult.bloodSwordCasts++;
            }
            if (sim.Elapsed < 11.5f) return;
            thrustResult.errors = errors; thrustResult.ticks = sim.TickIndex; thrustResult.frenzyAfter = p.FrenzyTime > 0f;
            Directory.CreateDirectory(smokeDirectory);
            File.WriteAllText(Path.Combine(smokeDirectory, "thrust-review.json"), JsonUtility.ToJson(thrustResult, true));
            Application.Quit(errors == 0 && thrustResult.runningAttacks == 2 && thrustResult.bloodSwordCasts == 2 ? 0 : 1);
        }
    }
}
