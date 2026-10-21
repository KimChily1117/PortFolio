using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DnfRaidDemo
{
    public static class ThrustChecks
    {
        [Serializable] class Clip { public string name; public int[] frames; public float[] durations; }
        [Serializable] class Weapon { public int[] frames; public float[] durations; }
        [Serializable] class Doc { public Clip[] skillClips; public Weapon bloodSword; }
        [Serializable] class Report { public bool Passed = true; public List<string> Checks = new List<string>(); }
        static void Require(bool ok, string why) { if (!ok) throw new Exception(why); }
        static void Tick(RaidSimulation sim, PlayerCommand c = null) { sim.Tick(RaidSimulation.FixedStep, c == null ? null : new[] { c }); }
        static RaidSimulation Setup(bool frenzy = false)
        {
            var s = new RaidSimulation(); s.Start(); s.PracticeInvulnerable = true;
            s.Players[0].Position = Vector2.zero;
            s.Boss.Position = new Vector2(1.8f, 0f); s.Players[0].FrenzyTime = frenzy ? 10f : 0f;
            return s;
        }
        public static void RunValidation()
        {
            Run(); SimulationChecks.Run(); MotionChecks.Run(); BerserkerAnimationChecks.Run();
            FrenzyChecks.Run(); NetworkChecks.Run(); FlickerProbe.Run();
            Debug.Log("[DNF_V022_VALIDATION_PASSED]");
        }
        public static void Run()
        {
            var report = new Report();
            foreach (int direction in new[] { -1, 1 })
            {
                var input = new RaidRunInput();
                input.Sample(direction, direction, 0f); Require(!input.Running, "single tap must walk");
                input.Sample(direction, 0, .1f); Require(!input.Running, "holding cannot create second tap");
                input.Sample(0, 0, .12f); input.Sample(direction, direction, .2f);
                Require(input.Running, "same-direction double tap must run");
                input.Sample(direction, 0, 2f); Require(input.Running, "run lasts while held");
                input.Sample(-direction, -direction, 2.1f); Require(!input.Running, "opposite direction cancels run");
                input.Sample(0, 0, 2.2f); input.Sample(-direction, -direction, 3f);
                Require(!input.Running, "slow second tap must walk");
                input.Reset(); input.Sample(direction, direction, 3.05f);
                Require(!input.Running, "menu/focus reset must clear tap history");
            }
            report.Checks.Add("double tap, held direction, opposite turn, timeout and reset in both directions");
            foreach (bool frenzy in new[] { false, true })
            foreach (int direction in new[] { -1, 1 })
            {
                var s = Setup(frenzy); var p = s.Players[0];
                s.Boss.Position = new Vector2(1.8f * direction, 0f);
                var c = new PlayerCommand { Move = new Vector2(direction, 0), Run = true };
                Tick(s, c); Require(p.Running && Mathf.Abs(p.Position.x) > .1f, "running speed/state");
                c.Basic = true; Tick(s, c);
                Require(p.ActionDashAttack && !p.ActionFrenzy && !p.Running, "running basic must latch thrust");
                Require(RaidMotion.Player(p, true, 0f).Clip == RaidDashAttack.Clip && p.ActionFacing == direction, "thrust pose/direction");
                Require(!RaidFrenzy.SwordVisible(p), "two-handed thrust cannot float an offhand sword");
                Require(p.Invulnerable == 0f && p.Cooldowns[(int)SkillId.Dash] == 0f, "running is not an invulnerable Space dodge");
                c.Basic = false;
                for (int i = 0; i < 23; i++) Tick(s, c);
                Require(Mathf.Abs(s.Boss.MaxHp - s.Boss.Hp - 67.5f * (frenzy ? RaidSimulation.FrenzyDamage : 1f)) < .001f, "running thrust damages once with v19 tuning and one buff multiplier");
                Require(!p.Running, "holding direction after thrust resumes walking");
                c.Basic = true; Tick(s, c);
                Require(!p.ActionDashAttack && p.BasicComboStep == 0 && p.ActionFrenzy == frenzy, "normal/Frenzy combo restarts at first strike");
            }
            report.Checks.Add("left/right running thrust, one hit, buff multiplier, no dodge invulnerability, normal/Frenzy combo recovery");
            var air = Setup(); Tick(air, new PlayerCommand { Run = true, Move = Vector2.right, Jump = true, Basic = true });
            Require(air.Players[0].ActionAirborne && !air.Players[0].ActionDashAttack, "air attack stays separate");
            var dodge = Setup(); Tick(dodge, new PlayerCommand { Dash = true, Basic = true, Move = Vector2.right });
            Require(dodge.Players[0].DashTime > 0f && !dodge.Players[0].ActionDashAttack, "Space remains dodge");
            report.Checks.Add("jump and Space do not trigger running thrust");
            var doc = JsonUtility.FromJson<Doc>(Resources.Load<TextAsset>("Art/berserker").text);
            Clip blood = Array.Find(doc.skillClips, c => c.name == "sk_bloodsword");
            Require(blood != null && blood.frames.Length == doc.bloodSword.frames.Length, "paired body/weapon timeline");
            float end = 0;
            for (int i = 0; i < blood.frames.Length; i++)
            {
                Require(blood.durations[i] == doc.bloodSword.durations[i] && blood.durations[i] > 0, "paired frame holds");
                end += blood.durations[i];
            }
            Require(Mathf.Abs(end - SkillBook.Get(SkillId.BloodSword).Duration) < .00001f, "timeline fits action");
            Require(RaidMotion.Frame(blood.frames, 1, blood.durations, .36f, true) == 35, "low forward thrust before hit");
            Require(RaidMotion.Frame(doc.bloodSword.frames, 1, doc.bloodSword.durations, .36f, true) == 17, "blood blade follows thrust");
            var bs = Setup(); Tick(bs, new PlayerCommand { Skills = PlayerCommand.Bit(SkillId.BloodSword) });
            for (int i = 0; i < 20; i++) Tick(bs);
            Require(bs.Boss.Hp == bs.Boss.MaxHp, "Blood Sword cannot damage during creation/turn");
            for (int i = 0; i < 6; i++) Tick(bs);
            Require(Mathf.Abs(bs.Boss.MaxHp - bs.Boss.Hp - 330f) < .001f, "Blood Sword hits after forward thrust with v19 tuning");
            Require(RaidMotion.BloodSwordWeapon(bs.Players[0], .4f) && !RaidMotion.BloodSwordWeapon(bs.Players[0], .52f), "attached sword ends with action");
            report.Checks.Add("Blood Sword body/blade clock, creation before damage, forward thrust and attached-weapon expiry");
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Logs/bloodsword-review-20261018"));
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "thrust-checks.json"), JsonUtility.ToJson(report, true));
            Debug.Log("[DNF_THRUST_CHECKS_PASSED] " + string.Join("; ", report.Checks));
        }
    }
}
