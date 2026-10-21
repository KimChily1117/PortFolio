#nullable disable
using Vector2 = ProjectDawn.Combat.RaidVector;
using Mathf = ProjectDawn.Combat.RaidMath;
#if UNITY_5_3_OR_NEWER
using UnityEngine;
#endif

namespace DnfRaidDemo
{
    // The equipped weapon's original body32..38 poses share Blood Sword's forward thrust.
    // Double-tap running, timings and damage are authored demo behavior, not recovered ANI.
    public static class RaidDashAttack
    {
        public const string Clip = "dash_attack";
        public const float RunSpeed = RaidActionTempo.RunSpeed;
        public static readonly int[] Frames = { 32, 33, 34, 35, 36, 37, 38 };
        public static readonly float[] FrameTimes = { .04f, .02f, .03f, .06f, .07f, .06f, .06f };
        public static readonly SkillDef Attack = new SkillDef {
            Id = SkillId.Basic, Key = "basic", Name = "대시 찌르기", Kind = SkillKind.Basic,
            Cooldown = .34f, Lock = .30f, Duration = .34f,
            Advance = 4.5f, AdvanceDelay = .06f, AdvanceUntil = .12f,
            Hits = new[] { new HitSpec { Delay = .10f, Offset = new Vector2(1.15f, 0f),
                Half = new Vector2(.85f, .48f), Damage = 45f, Active = .10f, Recovery = .12f, Follow = true } }
        };
    }
}
