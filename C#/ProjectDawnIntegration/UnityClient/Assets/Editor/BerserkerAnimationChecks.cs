using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DnfRaidDemo
{
    // Checks actual packed atlas metadata against the production frame selector. These checks prove
    // playback consistency in this demo, not agreement with an external video or recovered ANI.
    public static class BerserkerAnimationChecks
    {
        [Serializable] class Effect { public string id, resource; public int width, height, frameCount; public float fps; public int[] frames; }
        [Serializable] class Clip { public string name; public int[] frames; public float[] durations; }
        [Serializable] class Doc { public Effect[] effects; public Clip[] skillClips; }
        [Serializable] class Check { public string Name, Detail; public bool Passed; }
        [Serializable] class SkillAudit
        {
            public string Skill, Name, Clip, BodyEffect;
            public int[] BodyFrames;
            public float ActionDuration, CombatLock, UniformBodyFps, TransformationFrom, TransformationUntil, OldTransformationEnd;
            public float[] HitTimes, BodyFrameTimes;
        }
        [Serializable] class Report
        {
            public bool Passed = true;
            public string Scope = "Actual berserker atlas metadata and production playback selector; no original ANI or external GIF equivalence claim.";
            public List<Check> Checks = new List<Check>();
            public List<SkillAudit> Skills = new List<SkillAudit>();
        }

        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        static Effect Find(Doc doc, string id)
        {
            foreach (Effect effect in doc.effects) if (effect.id == id) return effect;
            throw new Exception("Missing effect: " + id);
        }
        static void RunCheck(Report report, string name, Action body)
        {
            var check = new Check { Name = name };
            try { body(); check.Passed = true; check.Detail = "passed"; }
            catch (Exception ex) { check.Detail = ex.Message; report.Passed = false; }
            report.Checks.Add(check);
        }

        [MenuItem("DNF Raid Demo/Run Berserker Animation Checks")]
        public static void Run()
        {
            var report = new Report();
            Doc doc = JsonUtility.FromJson<Doc>(Resources.Load<TextAsset>("Art/berserker").text);
            RunCheck(report, "all_41_atlases_keep_source_frame_ids_and_valid_rectangles", () =>
            {
                Require(doc.effects.Length == 41, "unexpected effect inventory");
                foreach (Effect effect in doc.effects)
                {
                    Require(effect.frameCount > 0 && effect.fps > 0 && effect.frames.Length % 7 == 0, "invalid metadata " + effect.id);
                    var ids = new HashSet<int>();
                    TextAsset packed = Resources.Load<TextAsset>(effect.resource);
                    Require(packed != null && packed.bytes.Length > 24, "missing atlas " + effect.id);
                    byte[] bytes = packed.bytes;
                    Require(bytes[0] == 137 && bytes[1] == 80 && bytes[2] == 78 && bytes[3] == 71, "not PNG " + effect.id);
                    int width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
                    int height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
                    Require(width == effect.width && height == effect.height, "atlas dimensions mismatch " + effect.id);
                    for (int i = 0; i < effect.frames.Length; i += 7)
                    {
                        int[] r = effect.frames;
                        Require(r[i] >= 0 && r[i] < effect.frameCount && ids.Add(r[i]), "duplicate/out-of-range frame " + effect.id);
                        Require(r[i + 1] >= 0 && r[i + 2] >= 0 && r[i + 3] > 0 && r[i + 4] > 0 && r[i + 1] + r[i + 3] <= width && r[i + 2] + r[i + 4] <= height, "crop outside atlas " + effect.id);
                    }
                    Resources.UnloadAsset(packed);
                }
            });
            RunCheck(report, "all_19_skill_clips_and_declared_effects_are_resolvable", () =>
            {
                var clips = new Dictionary<string, Clip>();
                foreach (Clip clip in doc.skillClips)
                {
                    Require(!clips.ContainsKey(clip.name) && clip.frames.Length > 0, "duplicate/empty clip");
                    foreach (int frame in clip.frames) Require(frame >= 0 && frame < 179, "body frame out of range");
                    clips.Add(clip.name, clip);
                }
                foreach (SkillDef def in SkillBook.All)
                {
                    if (def.Clip == null) continue;
                    Require(clips.ContainsKey(def.Clip) && def.Duration > 0, "skill clip missing " + def.Key);
                    foreach (FxCue cue in def.Fx) Find(doc, cue.Effect);
                    if (def.ZoneFx != null) Find(doc, def.ZoneFx);
                    Effect body = def.BodyFx == null ? null : Find(doc, def.BodyFx);
                    var hits = new List<float>();
                    foreach (HitSpec hit in def.Hits)
                        for (int n = 0; n < Mathf.Max(1, hit.Repeat); n++) hits.Add(hit.Delay + n * hit.Interval + hit.Telegraph);
                    report.Skills.Add(new SkillAudit { Skill = def.Key, Name = def.Name, Clip = def.Clip,
                        BodyFrames = clips[def.Clip].frames, ActionDuration = def.Duration, CombatLock = def.Lock,
                        UniformBodyFps = clips[def.Clip].durations == null ? clips[def.Clip].frames.Length / def.Duration : 0f, BodyFrameTimes = clips[def.Clip].durations, BodyEffect = def.BodyFx,
                        TransformationFrom = def.BodyFrom, TransformationUntil = def.BodyUntil,
                        OldTransformationEnd = body == null ? 0 : def.BodyFrom + body.frameCount / body.fps, HitTimes = hits.ToArray() });
                }
                Require(report.Skills.Count == 19, "not all skills were checked");
            });
            RunCheck(report, "awakening_transformations_cover_every_tick_without_avatar_fallback", () =>
            {
                foreach (SkillDef def in SkillBook.All)
                {
                    if (def.BodyFx == null) continue;
                    Effect effect = Find(doc, def.BodyFx);
                    float window = Mathf.Min(def.BodyUntil, def.Duration) - def.BodyFrom;
                    var seen = new HashSet<int>();
                    var rows = new HashSet<int>();
                    for (int row = 0; row < effect.frames.Length; row += 7) rows.Add(effect.frames[row]);
                    for (int tick = 0; tick / 60f < window; tick++)
                    {
                        int frame = RaidMotion.AtlasFrame(effect.frameCount, effect.fps, tick / 60f, false, window);
                        Require(frame >= 0 && rows.Contains(frame), "transformation ended within action window");
                        seen.Add(frame);
                    }
                    Require(seen.Count == effect.frameCount, "transformation skipped a frame at 60Hz: " + def.Key);
                    Require(RaidMotion.AtlasFrame(effect.frameCount, effect.fps, window - .00001f, false, window) == effect.frameCount - 1, "last transformation frame missing");
                    Require(RaidMotion.AtlasFrame(effect.frameCount, effect.fps, window, false, window) == -1, "transformation outlives action window");
                }
            });
            RunCheck(report, "transparent_atlas_frames_preserve_leading_internal_and_trailing_time", () =>
            {
                Effect frenzy = Find(doc, "bk_frenzy_sword"), ring = Find(doc, "bk_rf_ring");
                Require(frenzy.frameCount == 22 && frenzy.frames.Length / 7 == 13, "frenzy source count changed");
                Require(ring.frameCount == 13 && ring.frames.Length / 7 == 12, "ring source count changed");
                foreach (Effect effect in new[] { frenzy, ring })
                {
                    var visible = new HashSet<int>();
                    for (int i = 0; i < effect.frames.Length; i += 7) visible.Add(effect.frames[i]);
                    for (int i = 0; i < effect.frameCount; i++)
                        Require(RaidMotion.AtlasFrame(effect.frameCount, effect.fps, (i + .25f) / effect.fps, false) == i, "packed rows collapsed empty frame time");
                    Require(visible.Count < effect.frameCount, "fixture needs a transparent slot");
                }
                Require(RaidMotion.AtlasFrame(5, 10, .425f, false) == 4, "trailing empty source slot must remain addressable");
            });
            RunCheck(report, "rave_loop_stops_at_deadline_without_one_shot_tail", () =>
            {
                Effect effect = Find(doc, "bk_rave_loop");
                foreach (FxCue cue in SkillBook.Get(SkillId.BloodyRave).Fx)
                {
                    if (cue.Loop <= 0) continue;
                    Require(RaidMotion.AtlasFrame(effect.frameCount, effect.fps, cue.Loop - .001f, true, cue.Loop) >= 0, "loop stopped early");
                    Require(RaidMotion.AtlasFrame(effect.frameCount, effect.fps, cue.Loop, true, cue.Loop) == -1, "loop deadline leaked a frame");
                    Require(RaidMotion.AtlasFrame(effect.frameCount, effect.fps, cue.Loop + .02f, true, cue.Loop) == -1, "loop resumed as a one-shot");
                }
            });
            RunCheck(report, "one_shot_and_loop_boundaries_use_the_same_source_clock", () =>
            {
                foreach (Effect effect in doc.effects)
                {
                    float end = effect.frameCount / effect.fps;
                    Require(RaidMotion.AtlasFrame(effect.frameCount, effect.fps, -.01f, false) == -1, "negative cue time visible");
                    Require(RaidMotion.AtlasFrame(effect.frameCount, effect.fps, end - .0001f, false) == effect.frameCount - 1, "last frame not reachable");
                    Require(RaidMotion.AtlasFrame(effect.frameCount, effect.fps, end + .0001f, false) == -1, "one-shot not finished");
                    Require(RaidMotion.AtlasFrame(effect.frameCount, effect.fps, end + .001f, true) == 0, "loop did not wrap");
                }
            });
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Logs", "animation-review-20261018"));
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "berserker-animation-checks.json"), JsonUtility.ToJson(report, true));
            Debug.Log(report.Passed ? "[DNF_BERSERKER_ANIMATION_CHECKS_PASSED]" : "[DNF_BERSERKER_ANIMATION_CHECKS_FAILED]");
            if (!report.Passed) throw new Exception("Berserker animation playback checks failed; see report.");
        }
    }
}
