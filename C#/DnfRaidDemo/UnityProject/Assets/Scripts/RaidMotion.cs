using UnityEngine;

namespace DnfRaidDemo
{
    // Source PNG poses, with demo-authored timing. Original ANI timing is not available.
    public static class RaidMotion
    {
        public struct Pose
        {
            public string Clip;
            public float Time;
            public bool Once;
            public int Facing;
            public Pose(string clip, float time, bool once, int facing)
            { Clip = clip; Time = time; Once = once; Facing = facing; }
        }

        public static Pose Player(PlayerState p, bool walking, float loopTime, float extrapolation = 0)
        {
            if (p.Hp <= 0) return new Pose("death", loopTime, true, p.Facing);
            if (p.StunTime > 0) return new Pose("hurt", loopTime, true, p.Facing);
            float age = Mathf.Min(p.ActionDuration, p.ActionElapsed + extrapolation);
            bool action = p.ActionDuration > 0 && age < p.ActionDuration;
            if (action)
            {
                string clip;
                float time;
                if ((p.ActionAirborne || p.JumpHeight > .01f) &&
                    (p.ActiveSkill == SkillId.Basic || p.ActiveSkill == SkillId.ArcSlash))
                { clip = "air_attack"; time = age / p.ActionDuration * (6f / 18f); }
                else if (p.ActiveSkill == SkillId.Basic)
                { clip = p.BasicComboStep == 0 ? "attack" : p.BasicComboStep == 1 ? "attack2" : "attack3"; time = age; }
                else if (p.ActiveSkill == SkillId.ArcSlash)
                {
                    clip = "attack3";
                    // Keep preparation through Q's .14 s telegraph and the blade arc in its active window.
                    time = age < .14f ? age / .14f * .06f : age < .30f ? .06f + (age - .14f) / .16f * .12f : .18f + (age - .30f) / .22f * .15f;
                }
                else { clip = "cast"; time = age / p.ActionDuration * (4f / 9f); }
                return new Pose(clip, time, true, p.ActionFacing == 0 ? p.Facing : p.ActionFacing);
            }
            if (p.JumpHeight > .001f || p.JumpVelocity > 0)
            {
                float flight = (RaidSimulation.JumpLaunchVelocity - p.JumpVelocity) / RaidSimulation.JumpGravity;
                if (flight < .08f) return new Pose("jump_takeoff", flight / .08f * (2f / 14f), true, p.Facing);
                if (p.JumpVelocity > 0) return new Pose("jump_rise", (flight - .08f) * .68f, true, p.Facing);
                return new Pose("jump_fall", Mathf.Max(0, -p.JumpVelocity / RaidSimulation.JumpGravity), true, p.Facing);
            }
            if (p.LandingTime > 0) return new Pose("land", .10f - p.LandingTime, true, p.Facing);
            return new Pose(walking || p.DashTime > 0 ? "walk" : "idle", loopTime, false, p.Facing);
        }

        public static int Frame(int[] frames, float fps, float[] durations, float time, bool once)
        {
            time = Mathf.Max(0, time);
            if (durations != null && durations.Length == frames.Length)
            {
                float total = 0; foreach (float duration in durations) total += Mathf.Max(.001f, duration);
                if (once && time >= total) return frames[frames.Length - 1];
                time %= total;
                for (int i = 0; i < frames.Length; i++)
                { time -= Mathf.Max(.001f, durations[i]); if (time < 0) return frames[i]; }
                return frames[frames.Length - 1];
            }
            int frame = Mathf.FloorToInt(time * fps);
            return frames[once ? Mathf.Min(frame, frames.Length - 1) : frame % frames.Length];
        }
    }
}
