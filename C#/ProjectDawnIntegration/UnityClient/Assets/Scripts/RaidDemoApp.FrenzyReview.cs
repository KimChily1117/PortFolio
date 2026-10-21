using System;
using System.IO;
using UnityEngine;

namespace DnfRaidDemo
{
    public sealed partial class RaidDemoApp
    {
        // Explicit opt-in visual regression scenario; normal play never reads this schedule.
        bool frenzyReview, frenzyReviewCast, frenzyReviewJump;
        int frenzyReviewShot;
        [Serializable] class FrenzyReviewResult { public int errors, ticks; public bool cast, jump; public float buffSeconds; }
        PlayerCommand FrenzyReviewCommand(int id)
        {
            float t = sim.Elapsed;
            var c = new PlayerCommand { PlayerId = id, Basic = (t >= 1f && t < 2.1f) || (t >= 4.6f && t < 6.1f) };
            if (t >= 2.8f && !frenzyReviewCast) { frenzyReviewCast = true; c.Skills = PlayerCommand.Bit(SkillId.Frenzy); }
            if (t >= 6.6f && t < 7f) c.Move = Vector2.left;
            if (t >= 7f && t < 7.4f) c.Move = Vector2.right;
            if (t >= 7.8f && !frenzyReviewJump) { frenzyReviewJump = true; c.Jump = true; }
            if (t >= 7.95f && t < 8.05f) c.Basic = true;
            return c;
        }
        void UpdateFrenzyReview()
        {
            if (sim == null) return;
            float[] at = { .6f, 4f, 4.78f, 5.11f, 5.5f, 5.88f, 6.8f, 7.15f, 8.02f };
            string[] names = { "normal-idle", "frenzy-idle", "frenzy-attack1", "frenzy-attack2", "frenzy-attack3", "frenzy-attack4", "frenzy-walk-left", "frenzy-walk-right", "frenzy-air-attack" };
            if (frenzyReviewShot < at.Length && sim.Elapsed >= at[frenzyReviewShot]) Capture(names[frenzyReviewShot++] + ".png");
            if (sim.Elapsed >= 10.5f)
            {
                Directory.CreateDirectory(smokeDirectory);
                File.WriteAllText(Path.Combine(smokeDirectory, "frenzy-review.json"), JsonUtility.ToJson(new FrenzyReviewResult {
                    errors = errors, ticks = sim.TickIndex, cast = frenzyReviewCast, jump = frenzyReviewJump, buffSeconds = sim.Players[0].FrenzyTime
                }, true));
                Application.Quit(errors == 0 ? 0 : 1);
            }
        }
    }
}
