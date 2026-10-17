using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DnfRaidDemo
{
    // Tests the shipped manifest and the same selector used by the renderer. This does
    // not recover original ANI timing or replace visual review of the authored clips.
    public static class MotionChecks
    {
        [Serializable] private class Clip { public string name; public float fps; public int[] frames; public float[] durations; }
        [Serializable] private class Art { public string id, resourcePrefix; public Clip[] clips; }
        [Serializable] private class Manifest { public Art[] actors; }
        [Serializable] private class Check { public string Name; public bool Passed; public string Detail; }
        [Serializable] private class Sample { public string Check, Clip; public float Time; public int Frame, Tick; }
        [Serializable] private class Report
        {
            public string Utc, Scope;
            public bool Passed = true;
            public List<Check> Checks = new List<Check>();
            public List<Sample> Samples = new List<Sample>();
        }
        [Serializable] private class PlayerSummary
        {
            public int Id, Actions, JumpStarts, GroundJumpAvoids;
            public int[] SkillCounts = new int[5];
            public float Hp, MinimumHp;
        }
        [Serializable] private class ScheduleEvent { public int Index, PatternId, Tick; public float Seconds; }
        [Serializable] private class GroundAvoid { public int ZoneId, PlayerId, Tick; public float Seconds, Height, Hp; public string Pattern; }
        [Serializable] private class BattleSample { public int Tick, Phase; public float Seconds, BossHp, Player0Hp, Player1Hp; public string Pattern, Mode; }
        [Serializable] private class ShowcaseReport
        {
            public string Utc, Scope, Mode, Failure;
            public bool Passed, PracticeInvulnerable, EarlyVictory;
            public int RequestedTicks = 2400, ExecutedTicks, AppliedPatterns;
            public float RequestedSeconds = 40f, Elapsed, BossHp;
            public PlayerSummary[] Players;
            public List<ScheduleEvent> Schedule = new List<ScheduleEvent>();
            public List<GroundAvoid> GroundAvoids = new List<GroundAvoid>();
            public List<BattleSample> Timeline = new List<BattleSample>();
        }

        [MenuItem("DNF Raid Demo/Run Motion Checks")]
        public static void Run()
        {
            Report report = new Report
            {
                Utc = DateTime.UtcNow.ToString("o"),
                Scope = "Actual Resources/Art manifest and RaidMotion selector; fixed 60 Hz simulation. No Unity rendering, network, wall-clock pause or original ANI equivalence claim."
            };
            Art player = null;
            RunCheck(report, "manifest_idle_reference_and_frame_resources", delegate { player = LoadAndCheckManifest(); });
            RunCheck(report, "basic_three_combo_start_middle_end_and_033_duration", delegate { BasicCombos(player, report); });
            RunCheck(report, "completed_attack_returns_to_looping_idle", delegate { AttackCompletion(player, report); });
            RunCheck(report, "q_e_r_late_recovery_retains_skill_clip", delegate { LateSkills(player, report); });
            RunCheck(report, "takeoff_velocity_002_and_006_reach_both_frames", delegate { TakeoffReferences(player, report); });
            RunCheck(report, "fixed_tick_jump_reaches_takeoff_rise_fall_and_land", delegate { JumpReachability(player, report); });
            RunCheck(report, "air_attack_all_six_frames_are_reachable", delegate { AirAttackReachability(player, report); });
            RunCheck(report, "action_pose_is_deterministic_from_snapshot_elapsed", delegate { DeterministicPose(player); });
            ShowcaseReport showcase = null;
            RunCheck(report, "showcase_two_players_without_practice_invulnerability", delegate
            {
                showcase = RunShowcase();
                Require(showcase.Passed, showcase.Failure);
            });
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Logs"));
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "motion-checks.json"), JsonUtility.ToJson(report, true));
            if (showcase != null) File.WriteAllText(Path.Combine(folder, "showcase-simulation.json"), JsonUtility.ToJson(showcase, true));
            Debug.Log(report.Passed ? "[DNF_MOTION_CHECKS_PASSED]" : "[DNF_MOTION_CHECKS_FAILED]");
            if (!report.Passed) throw new Exception("Motion/showcase checks failed; see Logs/motion-checks.json and showcase-simulation.json.");
        }

        private static void RunCheck(Report report, string name, Action action)
        {
            Check check = new Check { Name = name };
            try { action(); check.Passed = true; check.Detail = "passed"; }
            catch (Exception error) { check.Passed = false; check.Detail = error.Message; report.Passed = false; }
            report.Checks.Add(check);
        }
        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static bool Near(float a, float b) { return Mathf.Abs(a - b) < .0001f; }
        private static RaidSimulation New(int count = 1) { RaidSimulation sim = new RaidSimulation(count, 42); sim.Start(); return sim; }
        private static void Tick(RaidSimulation sim, params PlayerCommand[] commands) { sim.Tick(RaidSimulation.FixedStep, commands); }
        private static Clip FindClip(Art player, string name)
        {
            Require(player != null && player.clips != null, "player manifest was not loaded");
            foreach (Clip clip in player.clips) if (clip.name == name) return clip;
            throw new Exception("Missing clip: " + name);
        }
        private static int Resolve(Art player, RaidMotion.Pose pose)
        {
            Clip clip = FindClip(player, pose.Clip);
            return RaidMotion.Frame(clip.frames, clip.fps, clip.durations, pose.Time, pose.Once);
        }
        private static int Observe(Art player, Report report, string check, PlayerState state, float loopTime = 0f, int tick = 0)
        {
            RaidMotion.Pose pose = RaidMotion.Player(state, false, loopTime);
            int frame = Resolve(player, pose);
            report.Samples.Add(new Sample { Check = check, Clip = pose.Clip, Time = pose.Time, Frame = frame, Tick = tick });
            return frame;
        }
        private static Art LoadAndCheckManifest()
        {
            TextAsset text = Resources.Load<TextAsset>("Art/manifest");
            Require(text != null, "Art/manifest is not a Unity resource");
            Manifest manifest = JsonUtility.FromJson<Manifest>(text.text);
            Art player = null;
            foreach (Art actor in manifest.actors) if (actor.id == "player") player = actor;
            Clip idle = FindClip(player, "idle");
            Require(idle.frames.Length == 4 && idle.frames[0] == 39 && idle.frames[1] == 39 && idle.frames[2] == 40 && idle.frames[3] == 41 && Near(idle.fps, 6f), "confirmed idle sequence [39,39,40,41] at 6 fps changed");
            HashSet<int> loaded = new HashSet<int>();
            foreach (Clip clip in player.clips)
            {
                Require(clip.frames != null && clip.frames.Length > 0 && clip.fps > 0f, "empty or invalid clip: " + clip.name);
                foreach (int frame in clip.frames)
                    if (loaded.Add(frame)) Require(Resources.Load<Texture2D>(player.resourcePrefix + frame.ToString("D4")) != null, "missing imported PNG frame " + frame);
            }
            return player;
        }
        private static void BasicCombos(Art art, Report report)
        {
            string[] names = { "attack", "attack2", "attack3" };
            int[,] expected = { { 0, 4, 7 }, { 8, 10, 13 }, { 14, 18, 20 } };
            float[] ages = { 0f, .165f, .329f };
            for (int combo = 0; combo < 3; combo++)
            {
                Clip clip = FindClip(art, names[combo]);
                Require(clip.durations != null && clip.durations.Length == clip.frames.Length, "combo needs per-frame durations");
                float total = 0f; foreach (float duration in clip.durations) { Require(duration > 0f, "duration must be positive"); total += duration; }
                Require(Near(total, .33f), "combo clip must last 0.33 seconds: " + names[combo]);
                PlayerState state = new PlayerState { Hp = 100f, Facing = 1, ActionFacing = 1, ActiveSkill = SkillId.Basic, BasicComboStep = combo, ActionDuration = .33f };
                for (int point = 0; point < ages.Length; point++)
                {
                    state.ActionElapsed = ages[point];
                    RaidMotion.Pose pose = RaidMotion.Player(state, false, 9f);
                    Require(pose.Clip == names[combo] && pose.Once, "wrong basic combo clip");
                    Require(Observe(art, report, "combo" + combo, state) == expected[combo, point], "wrong basic start/middle/end frame");
                }
            }
        }
        private static void AttackCompletion(Art art, Report report)
        {
            PlayerState state = new PlayerState { Hp = 100f, Facing = 1, ActiveSkill = SkillId.Basic, ActionDuration = .33f, ActionElapsed = .33f };
            RaidMotion.Pose pose = RaidMotion.Player(state, false, .35f);
            Require(pose.Clip == "idle" && !pose.Once, "completed attack must return to looping idle");
            Require(Observe(art, report, "idle_after_attack", state, .35f) == 40, "idle time must advance after an attack");
            Require(Observe(art, report, "idle_after_attack_loop", state, .85f) == 39, "idle must wrap instead of clamping a one-shot pose");
            Require(RaidMotion.Player(state, true, 0f).Clip == "walk", "finished action must allow walking animation");
        }
        private static void LateSkills(Art art, Report report)
        {
            int[] steps = { 26, 22, 58 };
            string[] clips = { "attack3", "cast", "cast" };
            int[] frames = { 20, 93, 93 };
            for (int i = 0; i < 3; i++)
            {
                RaidSimulation sim = New();
                Tick(sim, new PlayerCommand { PlayerId = 0, ArcSlash = i == 0, FlameWave = i == 1, Awakening = i == 2 });
                for (int n = 0; n < steps[i]; n++) Tick(sim);
                PlayerState state = sim.Players[0];
                Require(state.AttackTime <= 0f && state.ActionElapsed < state.ActionDuration, "sample must be after combat lock but before visual recovery ends");
                Require(RaidMotion.Player(state, false, 0f).Clip == clips[i], "late Q/E/R switched to a different skill clip");
                Require(Observe(art, report, "late_" + state.ActiveSkill, state, 0f, sim.TickIndex) == frames[i], "late Q/E/R did not reach recovery pose");
            }
        }
        private static void TakeoffReferences(Art art, Report report)
        {
            float[] flights = { .02f, .06f };
            for (int i = 0; i < flights.Length; i++)
            {
                PlayerState state = new PlayerState { Hp = 100f, Facing = 1, JumpHeight = .1f, JumpVelocity = RaidSimulation.JumpLaunchVelocity - RaidSimulation.JumpGravity * flights[i] };
                Require(RaidMotion.Player(state, false, 0f).Clip == "jump_takeoff", "velocity sample must select takeoff");
                Require(Observe(art, report, "takeoff_" + flights[i], state) == 61 + i, "both takeoff PNGs must have a reachable time interval");
            }
        }
        private static void JumpReachability(Art art, Report report)
        {
            RaidSimulation sim = New(); HashSet<int> seen = new HashSet<int>();
            for (int tick = 0; tick < 50; tick++)
            {
                Tick(sim, new PlayerCommand { PlayerId = 0, Jump = tick == 0 });
                RaidMotion.Pose pose = RaidMotion.Player(sim.Players[0], false, 0f);
                int frame = Resolve(art, pose);
                if (seen.Add(frame)) Observe(art, report, "physical_jump", sim.Players[0], 0f, sim.TickIndex);
                if (frame == 61 || frame == 62) Require(pose.Clip == "jump_takeoff", "takeoff frame clip mismatch");
                if (frame == 63 || frame == 64) Require(pose.Clip == "jump_rise", "rise frame clip mismatch");
                if (frame >= 65 && frame <= 67) Require(pose.Clip == "jump_fall", "fall frame clip mismatch");
                if (frame == 68) Require(pose.Clip == "land", "landing frame clip mismatch");
            }
            for (int frame = 61; frame <= 68; frame++) Require(seen.Contains(frame), "normal fixed-tick jump cannot reach frame " + frame);
            Require(RaidMotion.Player(sim.Players[0], false, 0f).Clip == "idle", "landing should settle back to idle");
        }
        private static void AirAttackReachability(Art art, Report report)
        {
            RaidSimulation sim = New(); HashSet<int> seen = new HashSet<int>();
            for (int tick = 0; tick < 24; tick++)
            {
                Tick(sim, new PlayerCommand { PlayerId = 0, Jump = tick == 0, Basic = tick == 0 });
                RaidMotion.Pose pose = RaidMotion.Player(sim.Players[0], false, 0f);
                if (pose.Clip != "air_attack") continue;
                int frame = Resolve(art, pose);
                if (seen.Add(frame)) Observe(art, report, "physical_air_attack", sim.Players[0], 0f, sim.TickIndex);
            }
            for (int frame = 69; frame <= 74; frame++) Require(seen.Contains(frame), "actual jumping basic attack cannot reach frame " + frame);
        }
        private static void DeterministicPose(Art art)
        {
            PlayerState state = new PlayerState { Hp = 100f, Facing = -1, ActionFacing = 1, ActiveSkill = SkillId.ArcSlash, ActionDuration = .52f, ActionElapsed = .25f };
            string before = JsonUtility.ToJson(state);
            RaidMotion.Pose expected = RaidMotion.Player(state, false, 0f);
            for (int i = 0; i < 100; i++)
            {
                // Arbitrarily changing the loop clock emulates paused/unpaused render calls.
                // Action selection must depend only on the unchanged snapshot action age.
                RaidMotion.Pose actual = RaidMotion.Player(state, false, i * 17f);
                Require(actual.Clip == expected.Clip && actual.Once == expected.Once && actual.Facing == expected.Facing && Near(actual.Time, expected.Time) && Resolve(art, actual) == Resolve(art, expected), "render calls advanced or changed an unchanged action");
            }
            Require(JsonUtility.ToJson(state) == before, "pose selection mutated simulation state");
            state.ActionElapsed = .51f;
            Require(RaidMotion.Player(state, false, 0f, .02f).Clip == "idle", "snapshot extrapolation must clamp at the action end");
        }
        private static ShowcaseReport RunShowcase()
        {
            RaidSimulation sim = New(2);
            sim.PracticeInvulnerable = false;
            ShowcaseReport report = new ShowcaseReport
            {
                Utc = DateTime.UtcNow.ToString("o"),
                Scope = "Two local simulated players, seed 42, normal RaidShowcase commands and disclosed PatternTimes/Ids schedule. No network, no HP edits, no practice invulnerability, no forced 23-second boss HP/phase change. Normal dash/skill invulnerability remains part of gameplay.",
                Players = new[] { new PlayerSummary { Id = 0, MinimumHp = 100f }, new PlayerSummary { Id = 1, MinimumHp = 100f } }
            };
            HashSet<string> avoids = new HashSet<string>();
            int[] previousActions = new int[2];
            for (int tick = 0; tick < report.RequestedTicks && sim.Mode == EncounterMode.Fighting; tick++)
            {
                while (report.AppliedPatterns < RaidShowcase.PatternTimes.Length && sim.Elapsed >= RaidShowcase.PatternTimes[report.AppliedPatterns])
                {
                    int index = report.AppliedPatterns++;
                    sim.ForcePattern(RaidShowcase.PatternIds[index]);
                    report.Schedule.Add(new ScheduleEvent { Index = index, PatternId = RaidShowcase.PatternIds[index], Tick = sim.TickIndex, Seconds = sim.Elapsed });
                }
                PlayerCommand[] commands = { RaidShowcase.Command(sim, 0), RaidShowcase.Command(sim, 1) };
                sim.Tick(RaidSimulation.FixedStep, commands);
                for (int id = 0; id < 2; id++)
                {
                    PlayerState player = sim.Players[id]; PlayerSummary summary = report.Players[id];
                    summary.MinimumHp = Mathf.Min(summary.MinimumHp, player.Hp);
                    if (player.ActionSequence != previousActions[id])
                    {
                        summary.Actions++; summary.SkillCounts[(int)player.ActiveSkill]++;
                        previousActions[id] = player.ActionSequence;
                    }
                    foreach (CombatEvent ev in sim.Events) if (ev.Kind == "Jump" && ev.TargetId == id && ev.Age == 0f) summary.JumpStarts++;
                    foreach (AttackZone zone in sim.Zones)
                    {
                        if (zone.FromPlayer || !zone.GroundOnly || zone.Age < zone.Telegraph || zone.Age >= zone.Telegraph + zone.ActiveTime || player.Hp <= 0f || player.Invulnerable > 0f || player.JumpHeight < RaidSimulation.JumpDodgeHeight) continue;
                        if (!RaidSimulation.Intersects(zone, player.Position, RaidSimulation.PlayerHurtboxHalfSize) || !avoids.Add(zone.Id + ":" + id)) continue;
                        summary.GroundJumpAvoids++;
                        report.GroundAvoids.Add(new GroundAvoid { ZoneId = zone.Id, PlayerId = id, Tick = sim.TickIndex, Seconds = sim.Elapsed, Height = player.JumpHeight, Hp = player.Hp, Pattern = zone.Tag });
                    }
                }
                if (tick % 60 == 59 || sim.Mode != EncounterMode.Fighting)
                    report.Timeline.Add(new BattleSample { Tick = sim.TickIndex, Seconds = sim.Elapsed, Phase = sim.Boss.Phase, BossHp = sim.Boss.Hp, Player0Hp = sim.Players[0].Hp, Player1Hp = sim.Players[1].Hp, Pattern = sim.Boss.PatternName, Mode = sim.Mode.ToString() });
            }
            report.Mode = sim.Mode.ToString(); report.ExecutedTicks = sim.TickIndex; report.Elapsed = sim.Elapsed; report.BossHp = sim.Boss.Hp;
            report.PracticeInvulnerable = sim.PracticeInvulnerable;
            report.EarlyVictory = sim.Mode == EncounterMode.Victory && sim.Elapsed < report.RequestedSeconds;
            List<string> failures = new List<string>();
            if (sim.PracticeInvulnerable) failures.Add("Practice invulnerability was enabled");
            if (sim.Mode == EncounterMode.Defeat) failures.Add("Encounter ended in defeat");
            for (int id = 0; id < 2; id++)
            {
                PlayerSummary player = report.Players[id]; player.Hp = sim.Players[id].Hp;
                if (player.Hp <= 0f) failures.Add("Player " + id + " died");
                if (player.SkillCounts[0] == 0 || player.SkillCounts[1] + player.SkillCounts[2] + player.SkillCounts[4] == 0) failures.Add("Player " + id + " did not use both basic and an active skill");
            }
            if (report.GroundAvoids.Count == 0) failures.Add("No ground attack was avoided by jump height without invulnerability");
            if (report.AppliedPatterns != RaidShowcase.PatternTimes.Length && !report.EarlyVictory) failures.Add("The full demonstration pattern schedule did not run");
            report.Failure = string.Join("; ", failures.ToArray()); report.Passed = failures.Count == 0;
            return report;
        }
    }
}
