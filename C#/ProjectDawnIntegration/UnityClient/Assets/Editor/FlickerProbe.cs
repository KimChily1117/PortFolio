using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DnfRaidDemo
{
    // Replays a fixed 60 Hz fight and records which body frame the renderer would select every tick.
    // A single-tick blip (A, B, A) or a one-tick idle pose between two actions is a visible flicker.
    public static class FlickerProbe
    {
        [Serializable] class Clip { public string name; public float fps; public int[] frames; public float[] durations; }
        [Serializable] class Art { public string id; public Clip[] clips; }
        [Serializable] class Manifest { public Art[] actors; }
        [Serializable] class BkClip { public string name; public int[] frames; }
        [Serializable] class BkDoc { public BkClip[] skillClips; }

        [MenuItem("DNF Raid Demo/Run Flicker Probe")]
        public static void Run()
        {
            Art player = null;
            foreach (Art actor in JsonUtility.FromJson<Manifest>(Resources.Load<TextAsset>("Art/manifest").text).actors) if (actor.id == "player") player = actor;
            var clips = new List<Clip>(player.clips);
            foreach (BkClip clip in JsonUtility.FromJson<BkDoc>(Resources.Load<TextAsset>("Art/berserker").text).skillClips)
            {
                float duration = 1f; foreach (SkillDef def in SkillBook.All) if (def.Clip == clip.name) duration = def.Duration;
                clips.Add(new Clip { name = clip.name, frames = clip.frames, fps = clip.frames.Length / duration });
            }
            var sim = new RaidSimulation(1, 42) { PracticeInvulnerable = true };
            sim.Start();
            var log = new StringBuilder();
            var history = new List<string>();
            var walkUntil = 0f; Vector2 last = sim.Players[0].Position;
            float idleStarted = 0f; string lastLoopClip = null;
            SkillId[] cycle = { SkillId.BloodSword, SkillId.GoreCross, SkillId.RagingFury, SkillId.Rage, SkillId.BloodyRave, SkillId.ExtremeOverkill };
            for (int tick = 0; tick < 1500; tick++)
            {
                PlayerState p = sim.Players[0];
                var command = new PlayerCommand { PlayerId = 0 };
                Vector2 goal = sim.Boss.Position + new Vector2(-1.5f, 0f);
                if ((goal - p.Position).magnitude > .2f && tick % 300 < 120) command.Move = (goal - p.Position).normalized;
                else command.Basic = tick % 300 < 220;
                if (tick % 90 == 45) command.Skills = PlayerCommand.Bit(cycle[(tick / 90) % cycle.Length]);
                if (tick % 200 == 150) command.Jump = true;
                if (tick % 260 == 30) command.Dash = true;
                sim.Tick(RaidSimulation.FixedStep, new[] { command });
                if ((last - p.Position).sqrMagnitude > .00001f) walkUntil = sim.Elapsed + .12f;
                last = p.Position;
                RaidMotion.Pose pose = RaidMotion.Player(p, walkUntil > sim.Elapsed, 0);
                // Loop clips use the app's per-clip clock (AnimationTime): restart when the clip changes.
                if (pose.Clip != lastLoopClip) { idleStarted = sim.Elapsed; lastLoopClip = pose.Clip; }
                if (pose.Clip == "idle" || pose.Clip == "walk") pose.Time = sim.Elapsed - idleStarted;
                Clip clip = clips[0]; foreach (Clip c in clips) if (c.name == pose.Clip) clip = c;
                int frame = RaidMotion.Frame(clip.frames, clip.fps, clip.durations, pose.Time, pose.Once);
                history.Add(pose.Clip + ":" + frame + "|" + p.ActiveSkill + "#" + p.ActionSequence + "|inv=" + (p.Invulnerable > 0 ? 1 : 0));
            }
            int blips = 0, idleGaps = 0, blinkTicks = 0;
            for (int i = 1; i + 1 < history.Count; i++)
            {
                string a = history[i - 1].Split('|')[0], b = history[i].Split('|')[0], c = history[i + 1].Split('|')[0];
                if (a == c && a != b) { blips++; log.AppendLine("BLIP tick " + i + ": " + history[i - 1] + " -> " + history[i] + " -> " + history[i + 1]); }
                if (b.StartsWith("idle") && !a.StartsWith("idle") && !c.StartsWith("idle") && !a.StartsWith("walk") && !c.StartsWith("walk"))
                { idleGaps++; log.AppendLine("IDLE-GAP tick " + i + ": " + history[i - 1] + " -> " + history[i] + " -> " + history[i + 1]); }
                if (history[i].EndsWith("inv=1")) blinkTicks++;
            }
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Logs"));
            File.WriteAllText(Path.Combine(folder, "flicker-probe.txt"), "blips=" + blips + " idleGaps=" + idleGaps + " invulnerableTicks=" + blinkTicks + "\n" + log + "\n---\n" + string.Join("\n", history.ToArray()));
            Debug.Log("[DNF_FLICKER_PROBE] blips=" + blips + " idleGaps=" + idleGaps + " invulnerableTicks=" + blinkTicks);
        }
    }
}
