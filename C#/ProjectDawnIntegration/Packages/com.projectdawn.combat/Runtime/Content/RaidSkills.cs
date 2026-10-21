#nullable disable
using Vector2 = ProjectDawn.Combat.RaidVector;
using Mathf = ProjectDawn.Combat.RaidMath;
#if UNITY_5_3_OR_NEWER
using UnityEngine;
#endif

namespace DnfRaidDemo
{
    public enum SkillKind { Basic, Dodge, Active, Buff, Awakening }

    // One damage window. Delay counts from the cast; Offset/Velocity x follow the caster's facing.
    public sealed class HitSpec
    {
        public float Delay, Telegraph, Active = .12f, Recovery = .12f, Damage, Radius;
        public ZoneShape Shape = ZoneShape.Box;
        public Vector2 Offset, Half, Velocity;
        public bool Follow;
        public int Repeat = 1;
        public float Interval;
    }

    // One visual cue. Presentation only: it never feeds combat.
    public sealed class FxCue
    {
        public string Effect;
        public float Delay, Scale = 1f, Alpha = 1f, Loop;
        // Optional one-shot playback length. Zero uses the atlas' authored playback rate.
        public float Duration;
        public Vector2 Offset;
        // Retain the world position sampled at cue activation, including after the body recovers.
        public bool Behind, Grounded;
    }

    public sealed class SkillDef
    {
        public SkillId Id;
        public string Key, Name, Group, Clip, Icon, Slot, Description, ZoneFx;
        public SkillKind Kind;
        public int Tier;
        public float Cooldown, Lock, Duration, Mp, Invulnerable, BuffSeconds;
        // Forward lunge (world units/s) for AdvanceUntil seconds, starting AdvanceDelay after the cast.
        public float Advance, AdvanceUntil, AdvanceDelay;
        // Transformation art drawn instead of the avatar between BodyFrom and BodyUntil.
        public string BodyFx;
        public float BodyFrom, BodyUntil;
        public HitSpec[] Hits = new HitSpec[0];
        public FxCue[] Fx = new FxCue[0];
    }

    // Berserker kit for this demo. Skill names, groups (전직/헬벤터/블러드 이블/진 버서커) and roles follow
    // the 나무위키 버서커 skill article; effect packs follow the local client's file names
    // (e.g. extreamblood = 혈귀극도 : 파멸). Numbers, timings, hit shapes and body-frame sequences are
    // demo design, not recovered SKL/ANI data (Script.pvf is encrypted). The 2026-10-18 reference
    // pass separates observed windup/strike/aftermath phases while preserving each cast's total damage.
    public static class SkillBook
    {
        // User tuning: widen only Extreme Overkill's horizontal effect and footprint.
        public const float OverkillHorizontalScale = 1.5f;
        public static readonly SkillDef[] All = Build();
        public static int Count { get { return All.Length; } }
        public static SkillDef Get(SkillId id) { return All[(int)id]; }
        public static bool Valid(SkillId id) { return (int)id >= 0 && (int)id < All.Length; }

        public static readonly string[] SlotKeys = { "Q", "W", "E", "R", "T", "Y", "U", "A", "S", "D", "F", "G", "H", "J" };
#if UNITY_5_3_OR_NEWER
        public static readonly KeyCode[] SlotKeyCodes = { KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R, KeyCode.T, KeyCode.Y, KeyCode.U,
            KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.F, KeyCode.G, KeyCode.H, KeyCode.J };
#endif

        static HitSpec Box(float delay, float x, Vector2 half, float damage, float active = .12f)
        { return new HitSpec { Delay = delay, Offset = new Vector2(x, 0f), Half = half, Damage = damage, Active = active }; }
        static HitSpec Circle(float delay, float x, float radius, float damage, float active = .14f)
        { return new HitSpec { Delay = delay, Shape = ZoneShape.Circle, Offset = new Vector2(x, 0f), Radius = radius, Damage = damage, Active = active }; }
        static HitSpec Multi(HitSpec hit, int repeat, float interval) { hit.Repeat = repeat; hit.Interval = interval; hit.Recovery = .02f; return hit; }
        static FxCue Fx(string effect, float delay, float x = 0f, float scale = 1f, bool behind = false, float loop = 0f, bool grounded = false, float duration = 0f)
        { return new FxCue { Effect = effect, Delay = delay, Offset = new Vector2(x, 0f), Scale = scale, Behind = behind, Loop = loop, Grounded = grounded, Duration = duration }; }

        static SkillDef[] Build()
        {
            var all = new SkillDef[21];
            all[(int)SkillId.Basic] = new SkillDef { Id = SkillId.Basic, Key = "basic", Name = "기본 공격", Kind = SkillKind.Basic, Cooldown = .33f, Lock = .23f, Duration = .33f,
                Hits = new[] { new HitSpec { Telegraph = .06f, Offset = new Vector2(.9f, 0f), Half = new Vector2(.85f, .48f), Active = .12f, Recovery = .12f, Damage = 45f } } };
            all[(int)SkillId.Dash] = new SkillDef { Id = SkillId.Dash, Key = "dodge", Name = "회피", Kind = SkillKind.Dodge, Cooldown = 1.6f };

            // ----- 전직 전 (귀검사 공용) -----
            all[(int)SkillId.GoreCross] = new SkillDef { Id = SkillId.GoreCross, Key = "gorecross", Name = "고어 크로스", Group = "전직 전", Slot = "A", Clip = "sk_gorecross", Icon = "gorecross",
                Kind = SkillKind.Active, Cooldown = 5.5f, Lock = .35f, Duration = .45f, Mp = 12f, ZoneFx = "bk_gorecross",
                Description = "교차베기로 붉은 십자 검기를 전방으로 날려 보냅니다.",
                Hits = new[] { new HitSpec { Telegraph = .12f, Offset = new Vector2(.8f, 0f), Half = new Vector2(.6f, .55f), Active = .7f, Recovery = .12f, Damage = 350f, Velocity = new Vector2(7f, 0f) } } };
            all[(int)SkillId.Bloodlust] = new SkillDef { Id = SkillId.Bloodlust, Key = "bloodlust", Name = "블러드러스트", Group = "전직 전", Clip = "sk_bloodlust", Icon = "bloodlust",
                Kind = SkillKind.Active, Cooldown = 6f, Lock = .55f, Duration = .7f, Mp = 16f,
                Description = "귀수로 적을 움켜쥐고 혈기를 터뜨립니다.",
                Hits = new[] { Box(.25f, .9f, new Vector2(.8f, .6f), 300f) },
                Fx = new[] { Fx("bk_bloodlust", .22f, 1.05f, 1f) } };

            // ----- 전직 : 보조 -----
            all[(int)SkillId.Frenzy] = new SkillDef { Id = SkillId.Frenzy, Key = "frenzy", Name = "프렌지", Group = "전직 · 보조", Clip = "sk_frenzy", Icon = "frenzy",
                Kind = SkillKind.Buff, Cooldown = .6f, Lock = .4f, Duration = .5f, Mp = 0f, BuffSeconds = 600f,
                Description = "ON/OFF 전환 버프. 켜면 혈검과 이도류 4단 평타, 공격력 25% 증가. 끄면 기본 한손검 모션으로 돌아갑니다. 기본 커맨드 ↓ ↓ Space. 전투 시작 시 ON.",
                Fx = new[] { Fx("bk_frenzy_start", .02f, 0f, 1.1f) } };
            all[(int)SkillId.Berserk] = new SkillDef { Id = SkillId.Berserk, Key = "berserk", Name = "폭주", Group = "전직 · 보조", Clip = "sk_berserk", Icon = "berserk",
                Kind = SkillKind.Buff, Cooldown = 5f, Lock = .4f, Duration = .5f, Mp = 0f, BuffSeconds = 600f,
                Description = "우렁찬 기합과 함께 폭주 상태에 돌입해 스킬 공격력이 15% 증가합니다. 전투 시작 시 자동으로 시전됩니다.",
                Fx = new[] { Fx("bk_berserk", .02f, 0f, 1.2f) } };

            // ----- 전직 : 전투 -----
            all[(int)SkillId.BloodScratch] = new SkillDef { Id = SkillId.BloodScratch, Key = "scratch", Name = "블러드 스크래치", Group = "전직 · 전투", Clip = "sk_scratch", Icon = "scratch",
                Kind = SkillKind.Active, Cooldown = 4f, Lock = .55f, Duration = .65f, Mp = 10f, Advance = 3f, AdvanceUntil = .25f,
                Description = "양손의 검으로 미끄러지듯 전진하며 적을 두 번 벱니다.",
                Hits = new[] { Box(.1f, 1.2f, new Vector2(1f, .7f), 180f), Box(.36f, 1.75f, new Vector2(1f, .7f), 180f) },
                Fx = new[] { Fx("bk_scratch", .06f, .9f, 1.1f, duration: .22f), Fx("bk_scratch_b", .32f, 1f, 1.1f, duration: .24f) } };
            all[(int)SkillId.RagingFury] = new SkillDef { Id = SkillId.RagingFury, Key = "ragingfury", Name = "레이징 퓨리", Group = "전직 · 전투", Slot = "W", Clip = "sk_ragingfury", Icon = "ragingfury",
                Kind = SkillKind.Active, Cooldown = 7f, Lock = .6f, Duration = 1.1f, Mp = 18f,
                Description = "바닥에 원형 진을 그린 뒤 분노를 폭발시켜 충격파와 피기둥으로 주변을 연속 타격합니다. (옛 이름: 피폭발)",
                Hits = new[] { Multi(Circle(.55f, 0f, 1.6f, 60f, .06f), 10, .06f) },
                Fx = new[] { Fx("bk_rf_ring", 0f, 0f, 1.2f, true, grounded: true, duration: .55f), Fx("bk_rf_pillar", .55f, 0f, 1.25f, grounded: true, duration: .8f), Fx("bk_rf_front", .55f, 0f, 1.2f, grounded: true, duration: .8f) } };
            all[(int)SkillId.CruelEngage] = new SkillDef { Id = SkillId.CruelEngage, Key = "cruel", Name = "크루얼 인게이지", Group = "전직 · 전투", Clip = "sk_cruel", Icon = "cruel",
                Kind = SkillKind.Active, Cooldown = 8f, Lock = .8f, Duration = .85f, Mp = 18f,
                Description = "검을 휘둘러 양옆의 적을 앞으로 모은 뒤 회전하며 올려베어 날려버립니다.",
                Hits = new[] { Box(.16f, 0f, new Vector2(2.2f, .9f), 160f), Box(.36f, 0f, new Vector2(2.2f, .9f), 160f), Box(.65f, 1f, new Vector2(1.2f, .8f), 260f) },
                Fx = new[] { Fx("bk_cruel_a", .12f, .5f, 1.2f, duration: .2f), Fx("bk_cruel_b", .32f, -.3f, 1.2f, duration: .2f), Fx("bk_cruel_c", .6f, 1f, 1.2f, duration: .25f) } };
            all[(int)SkillId.Rage] = new SkillDef { Id = SkillId.Rage, Key = "rage", Name = "격노", Group = "전직 · 전투", Slot = "E", Clip = "sk_rage", Icon = "rage",
                Kind = SkillKind.Active, Cooldown = 12f, Lock = 1.55f, Duration = 1.65f, Mp = 25f,
                Description = "무기를 내려쳐 적을 띄운 뒤 이도류로 난도질하고, 남겨진 검흔과 함께 적을 찢어발깁니다.",
                Hits = new[] { Box(.15f, 1f, new Vector2(1.2f, .8f), 150f), Multi(Box(.4f, 1f, new Vector2(1.2f, .8f), 110f), 4, .18f), Box(1.3f, 1.1f, new Vector2(1.4f, .9f), 380f) },
                Fx = new[] { Fx("bk_rage_down", .1f, 1f, 1.1f, duration: .25f), Fx("bk_rage_slash", .36f, 1f, 1.1f, duration: .94f), Fx("bk_rage", 1.3f, 1.2f, 1.25f, duration: .35f) } };
            all[(int)SkillId.BloodyRave] = new SkillDef { Id = SkillId.BloodyRave, Key = "bloodyrave", Name = "블러디 레이브", Group = "전직 · 전투", Slot = "R", Clip = "sk_bloodyrave", Icon = "bloodyrave",
                Kind = SkillKind.Active, Cooldown = 9f, Lock = 1.2f, Duration = 1.35f, Mp = 22f,
                Description = "귀수에서 혈기의 회오리를 일으켜 전방의 적을 끌어온 뒤 혈기검으로 강하게 후려칩니다.",
                Hits = new[] { Multi(Circle(.12f, 2f, 1.8f, 40f, .08f), 6, .12f), Box(1.03f, 1.2f, new Vector2(1.3f, .9f), 420f) },
                Fx = new[] { new FxCue { Effect = "bk_rave_loop", Delay = .08f, Offset = new Vector2(2.8f, 2.5f), Scale = 3.3f, Loop = .68f }, Fx("bk_rave_finish", .98f, 1f, 1.15f, duration: .38f) } };
            all[(int)SkillId.BloodSword] = new SkillDef { Id = SkillId.BloodSword, Key = "bloodsword", Name = "블러드 소드", Group = "전직 · 전투", Slot = "Q", Clip = "sk_bloodsword", Icon = "bloodsword",
                Kind = SkillKind.Active, Cooldown = 3f, Lock = .42f, Duration = .52f, Mp = 8f,
                Description = "전방에 혈기를 내뿜어 검을 생성한 뒤 강하게 찔러 폭발시킵니다.",
                Hits = new[] { new HitSpec { Telegraph = .40f, Offset = new Vector2(1.15f, 0f), Half = new Vector2(1.2f, .95f), Active = .08f, Recovery = .04f, Damage = 220f } },
                Fx = new[] { new FxCue { Effect = "bk_bloodsword_exp", Delay = .40f, Offset = new Vector2(3f, 2.5f), Scale = 1.1f } } };
            all[(int)SkillId.OutrageBreak] = new SkillDef { Id = SkillId.OutrageBreak, Key = "outrage", Name = "아웃레이지 브레이크", Group = "전직 · 전투", Slot = "T", Clip = "sk_outrage", Icon = "outrage",
                Kind = SkillKind.Active, Cooldown = 8f, Lock = 1.5f, Duration = 1.6f, Mp = 20f, Advance = 3.6f, AdvanceUntil = .45f,
                Description = "혈기의 검을 소환해 뛰어오른 뒤 땅을 내리쳐 넓은 충격파와 솟구치는 파편으로 공격합니다.",
                Hits = new[] { Circle(.55f, 1.62f, 1.6f, 280f), Multi(Circle(.9f, 1.62f, 1.6f, 50f, .08f), 4, .18f) },
                Fx = new[] { Fx("bk_outrage_sword", .18f, 0f, 1f, duration: .4f), Fx("bk_outrage_ground", .55f, 0f, 1.3f, true, grounded: true, duration: 1.3f), Fx("bk_outrage_exp", .55f, 0f, 1.3f, grounded: true, duration: .35f), Fx("bk_outrage_pillar", .9f, 0f, 1.3f, grounded: true, duration: .8f) } };

            // ----- 1차 각성 : 헬벤터 -----
            all[(int)SkillId.ExtremeOverkill] = new SkillDef { Id = SkillId.ExtremeOverkill, Key = "awaken1", Name = "익스트림 오버킬", Group = "헬벤터 (1차 각성)", Slot = "U", Clip = "sk_awaken1", Icon = "awaken1",
                Kind = SkillKind.Awakening, Tier = 1, Cooldown = 18f, Lock = 1.05f, Duration = 1.1f, Mp = 40f, Invulnerable = .65f,
                Description = "1차 각성기. 첫 입력으로 등 뒤에 혈검을 65초간 유지합니다. 다시 입력하거나 시간이 끝나면 검을 내려쳐 지속 혈기 폭풍을 일으킵니다.",
                // This definition is the finisher. The authoritative simulation handles arming separately.
                Hits = new[] { Multi(Circle(.45f, .8f, 3.4f, 110f, .16f), 10, .3f) },
                Fx = new[] { Fx("bk_eo_ring", 0f, .6f, 1.4f, true, grounded: true, duration: .6f), Fx("bk_eo_main_red", .45f, .8f, .9f, loop: 2.45f, grounded: true), Fx("bk_eo_main_light", .45f, .8f, .9f, loop: 2.45f, grounded: true), Fx("bk_eo_pillar", .45f, .8f, .9f, grounded: true, duration: 3f), Fx("bk_eo_burst", .45f, .8f, 1.6f, grounded: true, duration: .65f), Fx("bk_eo_smoke", .65f, .8f, 1.5f, grounded: true, duration: 3f) } };
            all[(int)SkillId.BloodSnatch] = new SkillDef { Id = SkillId.BloodSnatch, Key = "bloodsnatch", Name = "블러드 스내치", Group = "헬벤터 (1차 각성)", Slot = "Y", Clip = "sk_bloodsnatch", Icon = "bloodsnatch",
                Kind = SkillKind.Active, Cooldown = 10f, Lock = 1.3f, Duration = 1.4f, Mp = 28f, Advance = 2.5f, AdvanceUntil = .2f,
                Description = "전방의 적을 잡아챈 뒤 살짝 뛰어올라 바닥에 패대기쳐 강력한 혈기 폭발을 일으킵니다.",
                Hits = new[] { Box(.22f, 1.4f, new Vector2(.9f, .7f), 100f), Circle(1.02f, 1.5f, 1.8f, 750f) },
                Fx = new[] { Fx("bk_snatch_boom", 1.02f, 1f, 1.3f, grounded: true, duration: .65f), Fx("bk_snatch_burst", 1.02f, 1f, 1.3f, grounded: true, duration: .8f) } };
            all[(int)SkillId.BrutalCrusher] = new SkillDef { Id = SkillId.BrutalCrusher, Key = "brutal", Name = "브루탈 크러셔", Group = "헬벤터 (1차 각성)", Slot = "S", Clip = "sk_brutal", Icon = "brutal",
                Kind = SkillKind.Active, Cooldown = 12f, Lock = 1.12f, Duration = 1.25f, Mp = 28f,
                Description = "검으로 손을 베어 뿜어진 혈기로 혈검을 만든 뒤, 크게 들어 올려 지면을 내려쳐 거대한 폭발을 일으킵니다.",
                Hits = new[] { Circle(.82f, 1.5f, 1.8f, 700f) },
                Fx = new[] { Fx("bk_brutal_swing", .72f, 1f, 1.2f, duration: .35f),
                    Fx("bk_brutal_ground", .82f, 1.5f, 1.1f, true, grounded: true, duration: 1f),
                    Fx("bk_brutal_shock", .82f, 1.5f, 1.1f, grounded: true, duration: .6f),
                    Fx("bk_brutal", .82f, 1.5f, 1.3f, grounded: true, duration: .9f) } };

            // ----- 2차 각성 : 블러드 이블 -----
            all[(int)SkillId.BloodBoom] = new SkillDef { Id = SkillId.BloodBoom, Key = "bloodboom", Name = "블러드 붐", Group = "블러드 이블 (2차 각성)", Slot = "D", Clip = "sk_bloodboom", Icon = "bloodboom",
                Kind = SkillKind.Active, Cooldown = 14f, Lock = .8f, Duration = 1.1f, Mp = 32f,
                Description = "피의 분노를 일으켜 자신의 몸을 기점으로 대규모 피 폭발을 일으킵니다.",
                Hits = new[] { Circle(.6f, 0f, 2.4f, 800f, .16f) },
                Fx = new[] { Fx("bk_bloodboom_cast", 0f, 0f, 1.1f, true, duration: .6f), Fx("bk_bloodboom", .6f, 0f, 1.8f, grounded: true, duration: .95f) } };
            all[(int)SkillId.FatalBlood] = new SkillDef { Id = SkillId.FatalBlood, Key = "fatalblood", Name = "페이탈 블러드", Group = "블러드 이블 (2차 각성)", Slot = "F", Clip = "sk_fatalblood", Icon = "fatalblood",
                Kind = SkillKind.Active, Cooldown = 12f, Lock = 1.15f, Duration = 1.3f, Mp = 30f, Advance = 2.5f, AdvanceUntil = .3f,
                Description = "혈기로 검을 만들어 적을 빠르게 두 번 베어낸 뒤 강력한 교차베기로 치명적인 피해를 입힙니다.",
                Hits = new[] { Box(.32f, 1.75f, new Vector2(1.3f, .8f), 300f), Box(.54f, 1.75f, new Vector2(1.3f, .8f), 300f), Box(.86f, 1.95f, new Vector2(1.6f, 1f), 700f) },
                Fx = new[] { Fx("bk_fatal_slash1", .27f, 1f, 1.1f, duration: .22f), Fx("bk_fatal_slash2", .49f, 1f, 1.1f, duration: .25f), Fx("bk_fatal_cross", .81f, 1.2f, 1.35f, duration: .4f), Fx("bk_fatal_hit", .81f, 1.2f, 1.35f, duration: .4f) } };
            all[(int)SkillId.BloodRiven] = new SkillDef { Id = SkillId.BloodRiven, Key = "awaken2", Name = "블러드 리븐", Group = "블러드 이블 (2차 각성)", Slot = "H", Clip = "sk_awaken2", Icon = "awaken2",
                Kind = SkillKind.Awakening, Tier = 2, Cooldown = 30f, Lock = 1.8f, Duration = 2.25f, Mp = 60f, Invulnerable = 1.9f,
                Advance = 4f, AdvanceDelay = .9f, AdvanceUntil = .45f, BodyFx = "bk_riven_body", BodyFrom = .4f, BodyUntil = 1.45f,
                Description = "2차 각성기. 피구름에 휩싸여 혈마인으로 변신한 뒤 전방으로 돌격해 피의 폭발로 적을 초토화합니다.",
                Hits = new[] { new HitSpec { Delay = .95f, Shape = ZoneShape.Circle, Radius = 1.3f, Active = .1f, Recovery = .02f, Damage = 300f, Follow = true, Repeat = 2, Interval = .2f },
                    Circle(1.45f, 2.6f, 3f, 1800f, .18f) },
                Fx = new[] { Fx("bk_riven_cast", 0f, 0f, 1.35f, duration: .9f), Fx("bk_riven_bottom", 1.45f, .8f, 1.7f, true, grounded: true, duration: 1.3f), Fx("bk_riven", 1.45f, .8f, 1.8f, grounded: true, duration: 1f) } };

            // ----- 3차 각성 : 진(眞) 버서커 -----
            all[(int)SkillId.RampantMadness] = new SkillDef { Id = SkillId.RampantMadness, Key = "rampant", Name = "램펀트 매드니스", Group = "진(眞) 버서커 (3차 각성)", Slot = "G", Clip = "sk_rampant", Icon = "rampant",
                Kind = SkillKind.Active, Cooldown = 16f, Lock = 1.3f, Duration = 1.5f, Mp = 40f,
                Description = "거대한 혈기 덩어리를 바닥에 내려쳐 부순 뒤, 부서진 덩어리를 전방으로 후려쳐 혈기 파편을 흩뿌립니다.",
                Hits = new[] { Circle(.3f, 1.2f, 2f, 600f), Box(.75f, 3f, new Vector2(3.5f, 1.4f), 900f) },
                Fx = new[] { Fx("bk_rampant_swing", .12f, 1.2f, 1.4f, duration: .35f), Fx("bk_rampant_stone", .3f, 1.2f, 1.4f, grounded: true, duration: .45f), Fx("bk_rampant_end", .75f, 3f, 1.5f, grounded: true, duration: .9f) } };
            all[(int)SkillId.Destruction] = new SkillDef { Id = SkillId.Destruction, Key = "awaken3", Name = "혈귀극도 : 파멸", Group = "진(眞) 버서커 (3차 각성)", Slot = "J", Clip = "sk_awaken3", Icon = "awaken3",
                Kind = SkillKind.Awakening, Tier = 3, Cooldown = 45f, Lock = 6.6f, Duration = 6.7f, Mp = 80f, Invulnerable = 6.6f,
                BodyFx = "bk_destroy_body", BodyFrom = 0f, BodyUntil = 6.6f,
                Description = "3차(진) 각성기. 혈마인이 되어 적을 연속으로 베고 바닥에 처박은 뒤, 혈검을 뽑아 가르며 지면의 혈기를 폭발시켜 주위의 적을 파멸시킵니다.",
                Hits = new[] { Multi(Box(.3f, 1.4f, new Vector2(1.8f, 1.1f), 400f, .08f), 4, .35f), Circle(2f, 1.4f, 2f, 800f), Circle(5.3f, 1f, 4.2f, 3000f, .2f) },
                Fx = new[] { Fx("bk_destroy_line", .25f, 1.4f, 1.1f, duration: .3f), Fx("bk_destroy_line", .6f, 1.4f, 1.1f, duration: .3f), Fx("bk_destroy_line", .95f, 1.4f, 1.1f, duration: .3f), Fx("bk_destroy_line", 1.3f, 1.4f, 1.1f, duration: .3f),
                    Fx("bk_destroy_dash", .25f, 0f, 1.2f, duration: .3f), Fx("bk_destroy_dash", .6f, 0f, 1.2f, duration: .3f), Fx("bk_destroy_dash", .95f, 0f, 1.2f, duration: .3f), Fx("bk_destroy_dash", 1.3f, 0f, 1.2f, duration: .3f),
                    Fx("bk_destroy_ground", 2f, 1f, .7f, true, loop: 2.6f, grounded: true), Fx("bk_destroy_trace", 4.1f, 1.4f, 1.3f, duration: 1.1f),
                    Fx("bk_extreme_pole", 5.2f, -1.2f, 1.3f, grounded: true, duration: 1.1f), Fx("bk_extreme_pole", 5.25f, 1.2f, 1.3f, grounded: true, duration: 1.1f), Fx("bk_extreme_pole", 5.3f, 0f, 1.5f, grounded: true, duration: 1.1f), Fx("bk_extreme_wave", 5.3f, 1f, 2.3f, grounded: true, duration: 1.4f) } };
            return all;
        }
    }
}
