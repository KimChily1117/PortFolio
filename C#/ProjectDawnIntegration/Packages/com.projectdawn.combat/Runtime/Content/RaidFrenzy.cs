#nullable disable
using Vector2 = ProjectDawn.Combat.RaidVector;
using Mathf = ProjectDawn.Combat.RaidMath;
#if UNITY_5_3_OR_NEWER
using UnityEngine;
#endif

namespace DnfRaidDemo
{
    // Blood-sword IMG 0..21 aligns visually with body 106..127 on the original canvas.
    // Four attack boundaries, frame holds and hit timings are authored for this demo, not ANI data.
    public static class RaidFrenzy
    {
        public const int ComboCount = 4;
        public static readonly int[][] Frames = {
            new[] { 106, 107, 108, 109, 110, 111 },
            new[] { 112, 113, 114, 115, 116 },
            new[] { 117, 118, 119, 120, 121 },
            new[] { 122, 123, 124, 125, 126, 127 }
        };
        public static readonly float[][] FrameTimes = {
            new[] { .07f, .04f, .05f, .05f, .07f, .08f },
            new[] { .06f, .05f, .06f, .07f, .08f },
            new[] { .07f, .04f, .06f, .08f, .11f },
            new[] { .07f, .05f, .06f, .07f, .08f, .09f }
        };
        static readonly SkillDef[] attacks = Build();
        public static string Clip(int step) { return "frenzy_attack" + (Mathf.Clamp(step, 0, 3) + 1); }
        public static SkillDef Attack(int step) { return attacks[Mathf.Clamp(step, 0, 3)]; }
        public static bool GroundCombo(PlayerState p)
        { return p.FrenzyTime > 0f && p.JumpHeight <= 0f && p.JumpVelocity <= 0f; }
        public static bool SwordVisible(PlayerState p)
        {
            if (p.ActionDashAttack && p.ActionElapsed < p.ActionDuration) return false;
            // The existing airborne slash grips the equipped weapon with both hands. No proven
            // free-hand anchor exists for body69..74, so do not float a second sword over that pose.
            if (p.ActionElapsed < p.ActionDuration && (p.ActionAirborne || p.JumpHeight > .01f)
                && (p.ActiveSkill == SkillId.Basic || p.ActiveSkill == SkillId.BloodSword)) return false;
            return p.Hp > 0f && (p.FrenzyTime > 0f ||
                (p.ActionFrenzy && p.ActiveSkill == SkillId.Basic && p.ActionElapsed < p.ActionDuration));
        }
        static SkillDef[] Build()
        {
            var result = new SkillDef[ComboCount];
            float[][] hits = { new[] { .11f, .21f }, new[] { .11f, .18f }, new[] { .11f, .17f }, new[] { .12f, .18f } };
            for (int i = 0; i < ComboCount; i++)
            {
                float duration = 0f; foreach (float hold in FrameTimes[i]) duration += hold;
                result[i] = new SkillDef {
                    Id = SkillId.Basic, Key = "basic", Name = "프렌지 이도류", Kind = SkillKind.Basic,
                    Duration = duration, Cooldown = duration, Lock = duration - .04f,
                    Hits = new[] { Strike(hits[i][0]), Strike(hits[i][1]) }
                };
            }
            return result;
        }
        static HitSpec Strike(float delay)
        {
            // Split the original 45 damage between two blades; the existing buff multiplier applies once.
            return new HitSpec { Delay = delay, Offset = new Vector2(.9f, 0f), Half = new Vector2(.85f, .48f),
                Damage = 22.5f, Active = .06f, Recovery = .02f };
        }
    }
}
