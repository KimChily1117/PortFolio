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

        public static bool BloodSwordWeapon(PlayerState p, float age)
        {
            return p.Hp > 0f && p.StunTime <= 0f && p.ActiveSkill == SkillId.BloodSword
                && p.ActionDuration > 0f && age >= 0f && age < p.ActionDuration
                && !p.ActionAirborne && p.JumpHeight <= .01f;
        }

        public static Pose Player(PlayerState p, bool walking, float loopTime, float extrapolation = 0)
        {
            if (p.Hp <= 0) return new Pose("death", loopTime, true, p.Facing);
            if (p.StunTime > 0) return new Pose("hurt", loopTime, true, p.Facing);
            float age = Mathf.Min(p.ActionDuration, p.ActionElapsed + extrapolation * p.ActionRate);
            bool action = p.ActionDuration > 0 && age < p.ActionDuration;
            if (action)
            {
                string clip;
                float time;
                if ((p.ActionAirborne || p.JumpHeight > .01f) &&
                    (p.ActiveSkill == SkillId.Basic || p.ActiveSkill == SkillId.BloodSword))
                { clip = "air_attack"; time = age / p.ActionDuration * (6f / 18f); }
                else if (p.ActiveSkill == SkillId.ExtremeOverkill && !p.ActionOverkillFinish)
                { clip = "sk_berserk"; time = age; }
                else if (p.ActiveSkill == SkillId.Basic)
                { clip = p.ActionDashAttack ? RaidDashAttack.Clip : p.ActionFrenzy ? RaidFrenzy.Clip(p.BasicComboStep) : p.BasicComboStep == 0 ? "attack" : p.BasicComboStep == 1 ? "attack2" : "attack3"; time = age; }
                else if (SkillBook.Valid(p.ActiveSkill) && SkillBook.Get(p.ActiveSkill).Clip != null)
                {
                    // Skill clips are stretched over the action (clip fps = frames / duration at load).
                    clip = SkillBook.Get(p.ActiveSkill).Clip; time = age;
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
            return new Pose(walking || p.DashTime > 0 ? "walk" : "idle", walking ? loopTime * p.MoveSpeedScale * (p.Running ? 1.5f : 1f) : loopTime, false, p.Facing);
        }

        // Atlas frame IDs retain transparent slots removed by packing. A missing atlas row is an
        // intentional empty frame, not permission to advance to the next visible sprite early.
        // Duration retimes a one-shot to its owning action window; a loop keeps its native cadence
        // but stops at Duration. Returning -1 means the playback window has ended.
        public static int AtlasFrame(int frameCount, float fps, float time, bool loop, float duration = 0f)
        {
            if (frameCount <= 0 || fps <= 0f || time < 0f || float.IsNaN(time) || float.IsInfinity(time)) return -1;
            if (duration > 0f && time >= duration) return -1;
            float step = !loop && duration > 0f ? time / duration * frameCount : time * fps;
            if (!loop && duration <= 0f && step >= frameCount) return -1;
            if (loop) step %= frameCount;
            return Mathf.Clamp(Mathf.FloorToInt(step), 0, frameCount - 1);
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
