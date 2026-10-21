using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DnfRaidDemo
{
    public static class FrenzyChecks
    {
        [Serializable] class Layer { public string resource; public int[] frames; }
        [Serializable] class Frenzy { public Layer back, front; public int[] poses; }
        [Serializable] class Doc { public Frenzy frenzy; }
        [Serializable] class Check { public string Name, Detail; public bool Passed; }
        [Serializable] class Report { public bool Passed = true; public List<Check> Checks = new List<Check>(); }
        static void Require(bool yes, string detail) { if (!yes) throw new Exception(detail); }
        static void Tick(RaidSimulation s, bool basic = false, bool jump = false, int skills = 0)
        { s.Tick(RaidSimulation.FixedStep, new[] { new PlayerCommand { Basic = basic, Jump = jump, Skills = skills } }); }
        static RaidSimulation New()
        {
            var s = new RaidSimulation(); s.Start(); s.PracticeInvulnerable = true;
            s.Players[0].Position = s.Boss.Position + new Vector2(-1f, 0); s.Boss.AttackTime = 100f;
            return s;
        }
        static void CheckCase(Report r, string name, Action a)
        {
            var c = new Check { Name = name }; r.Checks.Add(c);
            try { a(); c.Passed = true; c.Detail = "passed"; }
            catch (Exception e) { r.Passed = false; c.Detail = e.Message; }
        }
        [MenuItem("DNF Raid Demo/Run Frenzy Checks")]
        public static void Run()
        {
            var r = new Report();
            CheckCase(r, "original_22_sword_slots_and_pose_alignment", Art);
            CheckCase(r, "four_step_combo_and_all_22_body_frames", Combo);
            CheckCase(r, "two_independent_hits_without_doubling_total_damage", Damage);
            CheckCase(r, "expiry_preserves_current_attack_then_resets_stance", Expiry);
            CheckCase(r, "air_attack_and_landing_keep_valid_pose", Air);
            CheckCase(r, "manual_cast_and_auto_buff_equip_sword", Buff);
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Logs", "animation-review-20261018"));
            Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, "frenzy-checks.json"), JsonUtility.ToJson(r, true));
            Debug.Log(r.Passed ? "[DNF_FRENZY_CHECKS_PASSED]" : "[DNF_FRENZY_CHECKS_FAILED]");
            if (!r.Passed) throw new Exception("Frenzy checks failed; see frenzy-checks.json");
        }
        static void Art()
        {
            var f = JsonUtility.FromJson<Doc>(Resources.Load<TextAsset>("Art/berserker").text).frenzy;
            Require(f != null && f.poses.Length % 4 == 0, "missing Frenzy pose map");
            var visible = new HashSet<int>();
            foreach (var layer in new[] { f.back, f.front })
            {
                Require(layer != null && layer.frames.Length % 7 == 0, "invalid sword layer");
                var data = Resources.Load<TextAsset>(layer.resource); Require(data != null, "missing sword atlas");
                var tex = new Texture2D(2, 2); Require(tex.LoadImage(data.bytes), "invalid sword PNG");
                for (int k = 0; k < layer.frames.Length; k += 7)
                {
                    int[] x = layer.frames; Require(x[k] >= 0 && x[k] < 22, "sword index outside source");
                    Require(x[k+1] >= 0 && x[k+2] >= 0 && x[k+1]+x[k+3] <= tex.width && x[k+2]+x[k+4] <= tex.height, "atlas crop outside image");
                    visible.Add(x[k]);
                }
                UnityEngine.Object.DestroyImmediate(tex);
            }
            Require(visible.Count == 22, "under/upper union must preserve every original sword frame");
            var body = new HashSet<int>();
            for (int k = 0; k < f.poses.Length; k += 4)
            {
                Require(body.Add(f.poses[k]), "duplicate body pose");
                Require(visible.Contains(f.poses[k+1]), "pose selects invisible sword");
                if (f.poses[k] >= 106 && f.poses[k] <= 127)
                    Require(f.poses[k+1] == f.poses[k]-106 && f.poses[k+2] == 0 && f.poses[k+3] == 0, "attack canvas alignment changed");
            }
            foreach (int frame in new[] {39,40,41,51,52,53,54,55,56,57,58,61,62,63,64,65,66,67,68})
                Require(body.Contains(frame), "missing locomotion pose " + frame);
        }
        static void Combo()
        {
            var s = New(); var p = s.Players[0]; p.FrenzyTime = 600f;
            var sequence = new List<int>(); var frames = new HashSet<int>(); int last = 0;
            for (int t = 0; t < 180 && sequence.Count < 5; t++)
            {
                Tick(s, true);
                if (p.ActionSequence != last) { sequence.Add(p.BasicComboStep); last = p.ActionSequence; }
                Require(p.ActionFrenzy, "ground combo lost its stance");
                var pose = RaidMotion.Player(p, false, 0);
                if (p.ActionElapsed < p.ActionDuration)
                {
                    Require(pose.Clip == RaidFrenzy.Clip(p.BasicComboStep), "wrong dual-wield clip");
                    frames.Add(RaidMotion.Frame(RaidFrenzy.Frames[p.BasicComboStep], 20, RaidFrenzy.FrameTimes[p.BasicComboStep], pose.Time, true));
                }
            }
            Require(string.Join(",", sequence) == "0,1,2,3,0", "four-step cycle not completed");
            Require(frames.Count == 22, "some attack frames unreachable at 60 Hz");
        }
        static void Damage()
        {
            var s = New(); var p = s.Players[0]; p.FrenzyTime = 600f;
            float hp = s.Boss.Hp; Tick(s, true);
            Require(s.Zones.Count == 2, "expected one independent zone per blade");
            Require(Mathf.Abs(s.Zones[0].Damage+s.Zones[1].Damage-67.5f*RaidSimulation.FrenzyDamage) < .001f, "v19 buff multiplier or damage split incorrect");
            for (int i = 0; i < 30; i++) Tick(s);
            Require(Mathf.Abs(hp-s.Boss.Hp-84.375f) < .01f, "actual total damage differs or hits repeated");
        }
        static void Expiry()
        {
            var s = New(); var p = s.Players[0]; p.FrenzyTime = .09f; Tick(s, true);
            for (int i = 0; i < 8; i++) Tick(s);
            Require(p.FrenzyTime == .09f && p.ActionFrenzy && RaidFrenzy.SwordVisible(p), "toggle must not expire or cut off a live attack");
            Require(RaidMotion.Player(p, false, 0).Clip == RaidFrenzy.Clip(0), "toggle stance swapped the action clip");
            for (int i = 0; i < 30; i++) Tick(s);
            Tick(s, skills:PlayerCommand.Bit(SkillId.Frenzy));
            Require(p.FrenzyTime==0 && !RaidFrenzy.SwordVisible(p), "manual toggle OFF leaves second sword visible");
            for(int i=0;i<10;i++)Tick(s);
            Tick(s, true);
            Require(!p.ActionFrenzy && p.BasicComboStep == 0 && RaidMotion.Player(p,false,0).Clip == "attack", "normal stance did not restart at first attack");
        }
        static void Air()
        {
            var s = New(); var p = s.Players[0]; p.FrenzyTime = 600f; Tick(s, true, true);
            Require(p.ActionAirborne && !p.ActionFrenzy && RaidMotion.Player(p,false,0).Clip == "air_attack", "ground combo leaked into airborne attack");
            Require(!RaidFrenzy.SwordVisible(p) && p.FrenzyTime > 0, "two-handed air slash must hide the auxiliary sword without clearing the buff");
            for (int i = 0; i < 60; i++) Tick(s);
            Require(p.JumpHeight == 0, "did not land"); Tick(s, true);
            Require(p.ActionFrenzy && p.BasicComboStep == 0, "landing failed to begin dual combo");
        }
        static void Buff()
        {
            var s = New(); var p = s.Players[0]; Require(!RaidFrenzy.SwordVisible(p), "normal stance has blood sword");
            Tick(s, skills: PlayerCommand.Bit(SkillId.Frenzy));
            Require(p.FrenzyTime > 599f && RaidFrenzy.SwordVisible(p), "manual Frenzy failed");
            var auto = new RaidSimulation(2) { AutoBuffs = true }; auto.Start();
            Require(RaidFrenzy.SwordVisible(auto.Players[0]) && RaidFrenzy.SwordVisible(auto.Players[1]), "auto buff not applied to both players");
        }
    }
}
