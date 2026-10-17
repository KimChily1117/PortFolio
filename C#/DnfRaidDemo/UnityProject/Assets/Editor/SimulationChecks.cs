using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DnfRaidDemo
{
    public static class SimulationChecks
    {
        [Serializable] private class Check { public string Name; public bool Passed; public string Detail; }
        [Serializable] private class BattleMetric { public int Players; public bool PracticeInvulnerable; public string Result; public float Seconds, BossHp, PlayerHpTotal; }
        [Serializable] private class Report { public string Utc; public bool Passed; public List<Check> Checks = new List<Check>(); public List<BattleMetric> BalanceMetrics = new List<BattleMetric>(); }

        [MenuItem("DNF Raid Demo/Run Simulation Checks")]
        public static void Run()
        {
            Report report = new Report { Utc = DateTime.UtcNow.ToString("o"), Passed = true };
            RunCheck(report, "geometry_boundaries_and_depth", Geometry);
            RunCheck(report, "movement_bounds_and_diagonal_normalization", Movement);
            RunCheck(report, "dodge_invulnerability_and_cooldown", Dodge);
            RunCheck(report, "skill_range_depth_and_cooldown", Skills);
            RunCheck(report, "pressed_skill_buffer_during_basic_hold_and_expiry", InputBuffer);
            RunCheck(report, "jump_avoids_ground_but_not_air_explosion", JumpCombat);
            RunCheck(report, "jump_landing_independent_players_and_air_attack", JumpState);
            RunCheck(report, "attack_id_prevents_duplicate_damage", SingleHit);
            RunCheck(report, "seed_and_fixed_step_reproducibility", Reproducibility);
            RunCheck(report, "independent_two_player_commands_and_death", TwoPlayers);
            RunCheck(report, "phase_threshold_stagger_and_transition", PhaseAndStagger);
            RunCheck(report, "victory_defeat_timeout_restart", EndStates);
            RunCheck(report, "all_six_patterns_telegraph_then_attack", Patterns);
            RunCheck(report, "presentation_basic_combo_cycle_and_expiry", ActionCombo);
            RunCheck(report, "presentation_skill_identity_lifetime_and_facing", ActionIdentity);
            RunCheck(report, "presentation_landing_and_action_interrupts", ActionInterrupts);
            RunCheck(report, "presentation_boss_activation_and_cancellation", BossActions);
            RunCheck(report, "presentation_metadata_does_not_change_combat", PresentationIsolation);
            for (int count = 1; count <= 2; count++)
            {
                report.BalanceMetrics.Add(MeasureBalance(count, true));
                report.BalanceMetrics.Add(MeasureBalance(count, false));
            }
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Logs"));
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "simulation-checks.json");
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            Debug.Log("Simulation checks: " + (report.Passed ? "PASS" : "FAIL") + " — " + path);
            if (!report.Passed) throw new Exception("Simulation checks failed; see " + path);
        }

        private static void RunCheck(Report report, string name, Action action)
        {
            Check check = new Check { Name = name };
            try { action(); check.Passed = true; check.Detail = "passed"; }
            catch (Exception ex) { check.Passed = false; check.Detail = ex.Message; report.Passed = false; }
            report.Checks.Add(check);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private static bool Near(float a, float b, float epsilon = .001f) { return Mathf.Abs(a - b) < epsilon; }
        private static RaidSimulation New(int count = 1, int seed = 42)
        {
            RaidSimulation simulation = new RaidSimulation(count, seed); simulation.Start(); return simulation;
        }

        private static void Advance(RaidSimulation simulation, int frames, params PlayerCommand[] commands)
        {
            for (int i = 0; i < frames; i++) simulation.Tick(RaidSimulation.FixedStep, commands);
        }

        private static void Geometry()
        {
            Vector2 half = RaidSimulation.PlayerHurtboxHalfSize;
            AttackZone box = new AttackZone { Shape = ZoneShape.Box, HalfSize = new Vector2(1f, .5f) };
            Require(RaidSimulation.Intersects(box, new Vector2(1f + half.x, 0f), half), "touching box edge should count");
            Require(!RaidSimulation.Intersects(box, new Vector2(0f, .5f + half.y + .01f), half), "depth lane outside should miss");
            AttackZone circle = new AttackZone { Shape = ZoneShape.Circle, Radius = 1f };
            Require(RaidSimulation.Intersects(circle, new Vector2(1.2f, 0f), half), "circle should intersect near face");
            Require(!RaidSimulation.Intersects(circle, new Vector2(1.4f, 1.4f), half), "circle corner outside should miss");
            AttackZone donut = new AttackZone { Shape = ZoneShape.Donut, Radius = 20f, InnerRadius = 1.55f };
            Require(!RaidSimulation.Intersects(donut, Vector2.zero, half), "complete hurtbox in safe circle should be safe");
            Require(RaidSimulation.Intersects(donut, new Vector2(1.5f, 0f), half), "partial overlap of safe boundary should still be dangerous");
        }

        private static void Movement()
        {
            RaidSimulation simulation = New(); simulation.PracticeInvulnerable = true;
            Vector2 before = simulation.Players[0].Position;
            Advance(simulation, 1, new PlayerCommand { PlayerId = 0, Move = Vector2.one });
            Require(Near((simulation.Players[0].Position - before).magnitude, 4.8f / 60f), "diagonal movement is normalized");
            Advance(simulation, 300, new PlayerCommand { PlayerId = 0, Move = new Vector2(1f, 1f) });
            Require(Near(simulation.Players[0].Position.x, 9f) && Near(simulation.Players[0].Position.y, 3f), "world maximum clamp");
            Advance(simulation, 600, new PlayerCommand { PlayerId = 0, Move = new Vector2(-1f, -1f) });
            Require(Near(simulation.Players[0].Position.x, -9f) && Near(simulation.Players[0].Position.y, -3f), "world minimum clamp");
        }

        private static void Dodge()
        {
            RaidSimulation simulation = New();
            simulation.ForcePattern(0);
            simulation.Players[0].Position = new Vector2(2f, 0f);
            simulation.Players[0].Invulnerable = 2f;
            Advance(simulation, 70);
            Require(Near(simulation.Players[0].Hp, 100f), "invulnerability must reject active attack");
            RaidSimulation dash = New();
            Advance(dash, 1, new PlayerCommand { PlayerId = 0, Dash = true, Move = Vector2.right });
            Require(dash.Players[0].Invulnerable > .25f && dash.Players[0].DashTime > 0f, "dodge grants invulnerability and movement");
            float cooldown = dash.Players[0].Cooldowns[3];
            Advance(dash, 1, new PlayerCommand { PlayerId = 0, Dash = true });
            Require(dash.Players[0].Cooldowns[3] < cooldown, "press during cooldown cannot refresh dodge");
        }

        private static void Skills()
        {
            RaidSimulation simulation = New();
            simulation.Players[0].Position = new Vector2(2.6f, 2f);
            float bossHp = simulation.Boss.Hp;
            Advance(simulation, 1, new PlayerCommand { PlayerId = 0, Basic = true });
            Advance(simulation, 15);
            Require(Near(simulation.Boss.Hp, bossHp), "basic attack must miss mismatched depth lane");
            simulation.Players[0].Position = new Vector2(2.6f, 0f);
            Advance(simulation, 20);
            Advance(simulation, 1, new PlayerCommand { PlayerId = 0, Basic = true });
            Advance(simulation, 15);
            Require(Near(simulation.Boss.Hp, bossHp - 45f), "in-range basic hits exactly once");
            RaidSimulation cooldown = New();
            Advance(cooldown, 1, new PlayerCommand { PlayerId = 0, ArcSlash = true });
            float initialCooldown = cooldown.Players[0].Cooldowns[1];
            Advance(cooldown, 40, new PlayerCommand { PlayerId = 0, ArcSlash = true });
            Require(cooldown.Players[0].Cooldowns[1] < initialCooldown - .5f, "skill does not reset cooldown when spammed");
            RaidSimulation wave = New(); wave.Players[0].Position = new Vector2(0f, 0f);
            Advance(wave, 1, new PlayerCommand { PlayerId = 0, FlameWave = true });
            Advance(wave, 55);
            Require(wave.Boss.Hp < wave.Boss.MaxHp, "moving flame wave reaches distant boss");
        }

        private static void SingleHit()
        {
            RaidSimulation simulation = New(); simulation.ForcePattern(0);
            PlayerState player = simulation.Players[0];
            for (int i = 0; i < 70; i++)
            {
                player.Position = new Vector2(2f, 0f);
                player.Invulnerable = 0f; player.StunTime = 0f;
                Advance(simulation, 1);
            }
            Require(Near(player.Hp, 82f), "same attack ID must not hit again even if invulnerability is cleared");
            Require(player.Hp > 0f, "one short active window cannot multiply damage per rendered frame");
        }

        private static void InputBuffer()
        {
            RaidSimulation simulation = New();
            Advance(simulation, 1, new PlayerCommand { PlayerId = 0, Basic = true });
            Advance(simulation, 1, new PlayerCommand { PlayerId = 0, Basic = true, ArcSlash = true });
            Require(Near(simulation.Players[0].Cooldowns[1], 0f), "buffered skill must wait for basic recovery");
            Advance(simulation, 20, new PlayerCommand { PlayerId = 0, Basic = true });
            Require(simulation.Players[0].Cooldowns[1] > 2.7f, "one-tick Q press should survive held basic recovery");
            RaidSimulation expired = New(); expired.Players[0].AttackTime = 1f;
            Advance(expired, 1, new PlayerCommand { PlayerId = 0, ArcSlash = true });
            Advance(expired, 80);
            Require(Near(expired.Players[0].Cooldowns[1], 0f), "expired buffer must not trigger a surprise skill");
        }

        private static BattleMetric MeasureBalance(int playerCount, bool practice)
        {
            RaidSimulation simulation = New(playerCount);
            simulation.PracticeInvulnerable = practice;
            for (int i = 0; i < 60 * 181 && simulation.Mode == EncounterMode.Fighting; i++)
            {
                List<PlayerCommand> commands = new List<PlayerCommand>();
                foreach (PlayerState player in simulation.Players)
                {
                    Vector2 goal = simulation.Boss.Position + new Vector2(-1.4f, player.Id == 0 ? -.1f : .1f);
                    Vector2 difference = goal - player.Position;
                    bool near = difference.magnitude < .25f;
                    commands.Add(new PlayerCommand
                    {
                        PlayerId = player.Id, Move = near ? Vector2.zero : difference.normalized,
                        Basic = near, ArcSlash = near && player.Cooldowns[1] <= 0f,
                        FlameWave = near && player.Cooldowns[2] <= 0f, Awakening = near && player.Cooldowns[4] <= 0f
                    });
                }
                simulation.Tick(RaidSimulation.FixedStep, commands);
            }
            float hp = 0f; foreach (PlayerState player in simulation.Players) hp += player.Hp;
            return new BattleMetric { Players = playerCount, PracticeInvulnerable = practice, Result = simulation.Mode.ToString(), Seconds = simulation.Elapsed, BossHp = simulation.Boss.Hp, PlayerHpTotal = hp };
        }

        private static void JumpCombat()
        {
            RaidSimulation grounded = New(); grounded.ForcePattern(5);
            Advance(grounded, 66);
            Require(Near(grounded.Players[0].Hp, 84f), "ground shockwave must hit a grounded player");
            RaidSimulation jump = New(); jump.ForcePattern(5);
            Advance(jump, 47);
            Advance(jump, 1, new PlayerCommand { PlayerId = 0, Jump = true });
            Advance(jump, 20);
            Require(Near(jump.Players[0].Hp, 100f), "jumping above ground threshold must avoid shockwave");
            RaidSimulation meteor = New(); meteor.ForcePattern(1);
            Advance(meteor, 62);
            Advance(meteor, 1, new PlayerCommand { PlayerId = 0, Jump = true });
            Advance(meteor, 12);
            Require(meteor.Players[0].JumpHeight > RaidSimulation.JumpDodgeHeight, "meteor check should occur while airborne");
            Require(Near(meteor.Players[0].Hp, 78f), "ordinary meteor explosion must hit airborne player");
        }

        private static void JumpState()
        {
            RaidSimulation simulation = New(2);
            Advance(simulation, 1, new PlayerCommand { PlayerId = 0, Jump = true, Basic = true });
            Require(simulation.Players[0].JumpHeight > 0f && Near(simulation.Players[1].JumpHeight, 0f), "jump state belongs to only the commanding player");
            Require(simulation.Players[0].Cooldowns[0] > 0f, "attacking while jumping remains allowed");
            Advance(simulation, 45);
            Require(Near(simulation.Players[0].JumpHeight, 0f) && Near(simulation.Players[0].JumpVelocity, 0f), "jump lands exactly on floor without residual velocity");
            RaidSimulation buffered = New(); buffered.Players[0].StunTime = .2f;
            Advance(buffered, 1, new PlayerCommand { PlayerId = 0, Jump = true });
            Require(Near(buffered.Players[0].JumpHeight, 0f), "jump waits through short stun");
            Advance(buffered, 15);
            Require(buffered.Players[0].JumpHeight > 0f, "jump input buffer executes after short stun");
        }

        private static void Reproducibility()
        {
            RaidSimulation a = New(2, 1234); RaidSimulation b = New(2, 1234);
            a.PracticeInvulnerable = b.PracticeInvulnerable = true;
            for (int i = 0; i < 900; i++)
            {
                PlayerCommand[] command = { new PlayerCommand { PlayerId = 0, Move = new Vector2(i % 120 < 60 ? 1f : -1f, 0f), Basic = i % 20 == 0 }, new PlayerCommand { PlayerId = 1, Move = new Vector2(0f, i % 90 < 45 ? 1f : -1f) } };
                a.Tick(RaidSimulation.FixedStep, command); b.Tick(RaidSimulation.FixedStep, command);
                Require(a.TickIndex == b.TickIndex && a.Boss.PatternName == b.Boss.PatternName && a.Zones.Count == b.Zones.Count, "seed timeline diverged at tick " + i);
                Require(a.Players[0].Position == b.Players[0].Position && Near(a.Boss.Hp, b.Boss.Hp), "state diverged at tick " + i);
                for (int z = 0; z < a.Zones.Count; z++) Require(a.Zones[z].Center == b.Zones[z].Center && a.Zones[z].Id == b.Zones[z].Id, "zone order/geometry diverged");
            }
            RaidSimulation c = New(); RaidSimulation d = New();
            PlayerCommand[] move = { new PlayerCommand { PlayerId = 0, Move = Vector2.right } };
            c.Tick(1f / 30f, move); d.Tick(1f / 60f, move); d.Tick(1f / 60f, move);
            Require(c.TickIndex == 2 && c.Players[0].Position == d.Players[0].Position, "fixed step partition mismatch");
        }

        private static void TwoPlayers()
        {
            RaidSimulation simulation = New(2);
            Vector2 a = simulation.Players[0].Position, b = simulation.Players[1].Position;
            Advance(simulation, 1, new PlayerCommand { PlayerId = 0, Move = Vector2.right }, new PlayerCommand { PlayerId = 1, Move = Vector2.up, ArcSlash = true });
            Require(simulation.Players[0].Position.x > a.x && Near(simulation.Players[0].Position.y, a.y), "player 0 input independent");
            Require(simulation.Players[1].Position.y > b.y && Near(simulation.Players[1].Position.x, b.x), "player 1 input independent");
            Require(simulation.Players[1].Cooldowns[1] > 0f && Near(simulation.Players[0].Cooldowns[1], 0f), "skill cooldowns independent");
            simulation.Players[0].Hp = 0f; a = simulation.Players[0].Position;
            Advance(simulation, 5, new PlayerCommand { PlayerId = 0, Move = Vector2.right, Basic = true, Dash = true });
            Require(simulation.Mode == EncounterMode.Fighting && simulation.Players[0].Position == a, "one dead player cannot move and does not end two-player fight");
            Require(Near(simulation.Players[0].Cooldowns[0], 0f), "dead player cannot cast");
            simulation.Players[1].Hp = 0f; Advance(simulation, 1);
            Require(simulation.Mode == EncounterMode.Defeat, "all players dead ends encounter");
        }

        private static void PhaseAndStagger()
        {
            RaidSimulation phase = New(); phase.ForcePattern(0); phase.Boss.Hp = phase.Boss.MaxHp * .5f;
            Advance(phase, 1);
            Require(phase.Boss.Phase == 2 && phase.Boss.TransitionTime > 0f && phase.Zones.Count == 0, "50 percent transitions once and removes old hazards");
            Advance(phase, 80);
            Require(phase.Boss.Phase == 2 && phase.Boss.TransitionTime <= 0f, "transition cannot repeat");
            RaidSimulation stagger = New(); stagger.Boss.Stagger = 98f; stagger.Players[0].Position = new Vector2(2.6f, 0f); stagger.ForcePattern(0);
            Advance(stagger, 1, new PlayerCommand { PlayerId = 0, Basic = true }); Advance(stagger, 8);
            Require(stagger.Boss.StaggerTime > 3f && Near(stagger.Boss.Stagger, 0f), "hits trigger stagger threshold");
            foreach (AttackZone zone in stagger.Zones) Require(zone.FromPlayer, "stagger cancels enemy hazards");
        }

        private static void EndStates()
        {
            RaidSimulation simulation = New(); simulation.Boss.Hp = 0f; Advance(simulation, 1);
            Require(simulation.Mode == EncounterMode.Victory && simulation.Zones.Count == 0, "zero boss hp is victory");
            simulation.Start();
            Require(simulation.Mode == EncounterMode.Fighting && simulation.TickIndex == 0 && Near(simulation.Elapsed, 0f) && simulation.Players[0].Hp == simulation.Players[0].MaxHp, "restart resets encounter state");
            simulation.Players[0].Hp = 0f; Advance(simulation, 1);
            Require(simulation.Mode == EncounterMode.Defeat, "single player death is defeat");
            simulation.Start(); simulation.TimeLimit = .1f; Advance(simulation, 10);
            Require(simulation.Mode == EncounterMode.Defeat, "time limit is enforced");
        }

        private static void Patterns()
        {
            for (int index = 0; index < 6; index++)
            {
                RaidSimulation simulation = New(2); simulation.ForcePattern(index);
                Require(simulation.Zones.Count > 0, "pattern creates zones " + index);
                foreach (AttackZone zone in simulation.Zones)
                    Require(zone.Telegraph >= .8f && zone.ActiveTime > 0f && zone.Recovery > 0f && zone.Age <= 0f, "telegraph / attack / recovery data invalid " + index);
                float hp0 = simulation.Players[0].Hp, hp1 = simulation.Players[1].Hp;
                Advance(simulation, 30);
                Require(Near(simulation.Players[0].Hp, hp0) && Near(simulation.Players[1].Hp, hp1), "damage occurred before telegraph " + index);
            }
        }

        private static void ActionCombo()
        {
            RaidSimulation simulation = New(); simulation.PracticeInvulnerable = true;
            PlayerState player = simulation.Players[0]; player.Position = new Vector2(2.6f, 0f);
            PlayerCommand held = new PlayerCommand { PlayerId = 0, Basic = true };
            Advance(simulation, 1, held);
            Require(player.ActionSequence == 1 && player.BasicComboStep == 0 && Near(player.ActionDuration, .33f), "first basic starts combo 0");
            for (int step = 1; step <= 3; step++)
            {
                Advance(simulation, 20, held);
                Require(player.ActionSequence == step + 1 && player.BasicComboStep == step % 3, "held basic should cycle 0,1,2 without altering cooldown");
            }
            Advance(simulation, 8);
            Require(Near(simulation.Boss.Hp, simulation.Boss.MaxHp - 4f * 45f), "visual combo must preserve all four original basic damage values");
            Advance(simulation, 50);
            Advance(simulation, 1, held);
            Require(player.ActionSequence == 5 && player.BasicComboStep == 0, "gap beyond 0.8 seconds resets the visual combo");
            simulation.Start();
            Require(simulation.Players[0].ActionSequence == 0 && simulation.Players[0].ActionFacing == 1, "restart resets presentation state");
        }

        private static void ActionIdentity()
        {
            SkillId[] skills = { SkillId.Basic, SkillId.ArcSlash, SkillId.FlameWave, SkillId.Awakening };
            float[] durations = { .33f, .52f, .45f, 1.05f };
            int[] afterCombatLock = { 15, 26, 22, 58 };
            for (int i = 0; i < skills.Length; i++)
            {
                RaidSimulation simulation = New(); simulation.PracticeInvulnerable = true;
                PlayerState player = simulation.Players[0];
                Advance(simulation, 1, new PlayerCommand
                {
                    PlayerId = 0, Basic = i == 0, ArcSlash = i == 1, FlameWave = i == 2, Awakening = i == 3
                });
                Require(player.ActiveSkill == skills[i] && player.ActionSequence == 1 && Near(player.ActionElapsed, 0f), "successful skill records identity and sequence");
                Advance(simulation, afterCombatLock[i], new PlayerCommand { PlayerId = 0, Move = Vector2.left });
                Require(player.AttackTime <= 0f && player.ActionElapsed < player.ActionDuration, "visual recovery must outlast the original combat lock");
                Require(player.ActiveSkill == skills[i] && Near(player.ActionDuration, durations[i]), "late skill frames must retain their actual skill identity");
                Require(player.Facing == -1 && player.ActionFacing == 1, "movement facing must not flip an action already started");
                Advance(simulation, 70);
                Require(Near(player.ActionElapsed, durations[i]) && player.ActiveSkill == skills[i], "completed action clock clamps without changing skill identity");
            }
        }

        private static void ActionInterrupts()
        {
            RaidSimulation jump = New(2); jump.PracticeInvulnerable = true;
            PlayerState player = jump.Players[0];
            Advance(jump, 1, new PlayerCommand { PlayerId = 0, Jump = true, Basic = true });
            Require(player.ActionAirborne && player.JumpHeight > 0f, "same-tick launch and attack should record an airborne action");
            int flightTicks = 1;
            while (player.JumpHeight > 0f && flightTicks < 60) { Advance(jump, 1); flightTicks++; }
            Require(flightTicks == 38 && Near(player.LandingTime, .10f), "unchanged jump physics should land at tick 38 and start only a visual landing window");
            Require(Near(jump.Players[1].LandingTime, 0f), "landing is independent for each player");
            Vector2 before = player.Position;
            Advance(jump, 3, new PlayerCommand { PlayerId = 0, Move = Vector2.right });
            Require(Near(player.LandingTime, .05f) && Near(player.Position.x - before.x, 3f * 4.8f / 60f), "landing timer must not add movement lock");
            Advance(jump, 1, new PlayerCommand { PlayerId = 0, Jump = true });
            Require(Near(player.LandingTime, 0f) && player.JumpHeight > 0f, "jump immediately clears landing presentation");

            RaidSimulation airSkill = New();
            Advance(airSkill, 1, new PlayerCommand { PlayerId = 0, Jump = true, Awakening = true });
            Advance(airSkill, 37);
            Require(airSkill.Players[0].ActionAirborne && Near(airSkill.Players[0].ActionDuration, 0f) && Near(airSkill.Players[0].LandingTime, .10f), "landing cancels a long airborne action before the landing pose ends");
            Require(airSkill.Players[0].AttackTime > .3f && airSkill.Zones.Count == 1, "landing presentation cannot shorten combat recovery or remove the live skill zone");

            RaidSimulation hit = New(); player = hit.Players[0];
            Advance(hit, 1, new PlayerCommand { PlayerId = 0, ArcSlash = true });
            int sequence = player.ActionSequence;
            hit.Zones.Add(new AttackZone { Id = 100000, OwnerId = -1, Shape = ZoneShape.Circle, Center = player.Position, Radius = 2f, ActiveTime = .3f, Recovery = .1f, Damage = 10f });
            Advance(hit, 1);
            Require(Near(player.Hp, 90f) && player.StunTime > 0f && Near(player.ActionDuration, 0f) && Near(player.ActionElapsed, 0f), "actual hit interrupts visual attack without suppressing damage or stun");
            Require(player.ActionSequence == sequence && player.Cooldowns[1] > 2.9f, "interruption does not fake a new action or refund cooldown");

            RaidSimulation dash = New(); player = dash.Players[0];
            Advance(dash, 1, new PlayerCommand { PlayerId = 0, Basic = true });
            Advance(dash, 1, new PlayerCommand { PlayerId = 0, Dash = true });
            Require(player.DashTime > 0f && Near(player.ActionDuration, 0f) && dash.Zones.Count == 1, "dash cancels presentation and retains existing attack-zone behavior");
            RaidSimulation death = New(2);
            Advance(death, 1, new PlayerCommand { PlayerId = 0, ArcSlash = true });
            death.Players[0].Hp = 0f; Advance(death, 1);
            Require(death.Mode == EncounterMode.Fighting && Near(death.Players[0].ActionDuration, 0f), "dead member's action stops without ending a surviving partner's fight");
        }

        private static void BossActions()
        {
            RaidSimulation simulation = New(2); simulation.PracticeInvulnerable = true; simulation.ForcePattern(1);
            Advance(simulation, 73);
            Require(simulation.Boss.ActionSequence == 1 && Near(simulation.Boss.ActionDuration, 1f), "simultaneous player-targeted meteors start only one boss action");
            Advance(simulation, 20);
            Require(simulation.Boss.AttackTime <= 0f && simulation.Boss.ActionElapsed < simulation.Boss.ActionDuration, "boss visual action survives the original 0.25 second attack timer");
            simulation.Boss.Hp = simulation.Boss.MaxHp * .5f; Advance(simulation, 1);
            Require(simulation.Boss.Phase == 2 && Near(simulation.Boss.ActionDuration, 0f), "phase transition cancels the old boss action");
            simulation.ForcePattern(0); Advance(simulation, 55);
            Require(simulation.Boss.ActionSequence == 2 && Near(simulation.Boss.ActionDuration, .9f), "phase two activation uses its own visual duration");
            simulation.Boss.Hp = 0f; Advance(simulation, 1);
            Require(simulation.Mode == EncounterMode.Victory && Near(simulation.Boss.ActionDuration, 0f), "encounter end cancels boss presentation");

            RaidSimulation stagger = New(); stagger.PracticeInvulnerable = true; stagger.ForcePattern(0); Advance(stagger, 55);
            stagger.Boss.Stagger = 98f; stagger.Players[0].Position = new Vector2(2.6f, 0f);
            Advance(stagger, 1, new PlayerCommand { PlayerId = 0, Basic = true }); Advance(stagger, 8);
            Require(stagger.Boss.StaggerTime > 3f && Near(stagger.Boss.ActionDuration, 0f), "stagger interrupts boss presentation");
        }

        private static void PresentationIsolation()
        {
            RaidSimulation normal = New(2, 314); RaidSimulation altered = New(2, 314);
            for (int tick = 0; tick < 600; tick++)
            {
                // Deliberately alter only new snapshot fields. None may feed combat gates,
                // movement, jump integration, damage, pattern scheduling or random state.
                foreach (PlayerState player in altered.Players)
                {
                    player.ActiveSkill = SkillId.Dash; player.ActionSequence = tick;
                    player.BasicComboStep = 2; player.ActionFacing = -1; player.ActionAirborne = true;
                    player.ActionDuration = 100f; player.ActionElapsed = 50f; player.LandingTime = 99f;
                }
                altered.Boss.ActionSequence = tick; altered.Boss.ActionElapsed = 50f; altered.Boss.ActionDuration = 100f;
                PlayerCommand[] commands = {
                    new PlayerCommand { PlayerId = 0, Move = new Vector2(tick % 160 < 80 ? 1f : -1f, 0f), Basic = true, ArcSlash = tick % 190 == 0, Jump = tick % 75 == 0 },
                    new PlayerCommand { PlayerId = 1, Move = new Vector2(1f, tick % 120 < 60 ? -.3f : .3f), Basic = true, FlameWave = tick % 340 == 0, Awakening = tick == 0, Dash = tick % 110 == 30 }
                };
                normal.Tick(RaidSimulation.FixedStep, commands); altered.Tick(RaidSimulation.FixedStep, commands);
                Require(normal.Mode == altered.Mode && normal.TickIndex == altered.TickIndex && Near(normal.Elapsed, altered.Elapsed), "metadata changed encounter progression");
                BossState a = normal.Boss, b = altered.Boss;
                Require(a.Position == b.Position && Near(a.Hp, b.Hp) && Near(a.Stagger, b.Stagger) && Near(a.StaggerTime, b.StaggerTime) && Near(a.AttackTime, b.AttackTime) && Near(a.TransitionTime, b.TransitionTime) && a.Phase == b.Phase && a.PatternName == b.PatternName, "metadata changed boss combat state");
                for (int p = 0; p < normal.Players.Count; p++)
                {
                    PlayerState x = normal.Players[p], y = altered.Players[p];
                    Require(x.Position == y.Position && Near(x.Hp, y.Hp) && Near(x.Invulnerable, y.Invulnerable) && Near(x.AttackTime, y.AttackTime) && Near(x.StunTime, y.StunTime) && x.Facing == y.Facing && Near(x.JumpHeight, y.JumpHeight) && Near(x.JumpVelocity, y.JumpVelocity) && Near(x.DashTime, y.DashTime) && x.DashDirection == y.DashDirection, "metadata changed player movement, jump or damage");
                    for (int c = 0; c < x.Cooldowns.Length; c++) Require(Near(x.Cooldowns[c], y.Cooldowns[c]), "metadata changed cooldown");
                }
                Require(normal.Zones.Count == altered.Zones.Count && normal.Events.Count == altered.Events.Count, "metadata changed attacks or events");
                for (int z = 0; z < normal.Zones.Count; z++) Require(JsonUtility.ToJson(normal.Zones[z]) == JsonUtility.ToJson(altered.Zones[z]), "metadata changed an attack zone");
                for (int e = 0; e < normal.Events.Count; e++) Require(JsonUtility.ToJson(normal.Events[e]) == JsonUtility.ToJson(altered.Events[e]), "metadata changed an event");
            }
        }
    }
}
