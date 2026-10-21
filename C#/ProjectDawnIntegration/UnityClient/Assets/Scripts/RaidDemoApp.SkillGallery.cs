using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DnfRaidDemo
{
    public sealed partial class RaidDemoApp
    {
        // Explicit offline presentation review. No gallery state enters network messages or SQL.
        bool skillGallery, galleryShowCutscenes, galleryIssued, galleryOverkillFinishIssued;
        bool SuppressGalleryCutscenes { get { return skillGallery && !galleryShowCutscenes; } }
        SkillDef gallerySkill;
        int galleryOrdinal;
        float galleryFps = 12f;
        string galleryRoot;
        readonly GalleryResult galleryResult = new GalleryResult();

        [Serializable] sealed class GalleryFrame
        {
            public string file, phase, activeSkill;
            public float seconds, actionSeconds, actionDuration, x, y, frenzySeconds;
            public int tick, actionSequence, combo;
        }
        [Serializable] sealed class GalleryClip
        {
            public string key, name, kind, look;
            public bool commandIssued, actionObserved, complete, secondCommandIssued, secondActionObserved;
            public float duration, observedDuration, damage;
            public List<GalleryFrame> frames = new List<GalleryFrame>();
        }
        [Serializable] sealed class GalleryResult
        {
            public string source = "Actual Unity end-of-frame render; isolated offline training simulation";
            public string scope = "Presentation and command acceptance only; not original-game accuracy or network validation";
            public string startedUtc;
            public int schema = 1, width, height, errors;
            public float requestedFps;
            public bool completed, cutscenesEnabled;
            public List<GalleryClip> skills = new List<GalleryClip>();
        }

        // Call after parsing --output and before --flow/--dawn dispatch in Awake.
        bool StartSkillGallery(string[] args)
        {
            int flag = Array.IndexOf(args, "--skill-gallery");
            if (flag < 0) return false;
            skillGallery = true;
            galleryShowCutscenes = Array.IndexOf(args, "--gallery-cutscenes") >= 0;
            string only = flag + 1 < args.Length && !args[flag + 1].StartsWith("--", StringComparison.Ordinal) ? args[flag + 1] : null;
            int fpsArg = Array.IndexOf(args, "--gallery-fps");
            int fps;
            if (fpsArg >= 0 && fpsArg + 1 < args.Length && int.TryParse(args[fpsArg + 1], out fps)) galleryFps = Mathf.Clamp(fps, 4, 24);
            var selected = new List<SkillDef>();
            foreach (var def in SkillBook.All) if (only == null || def.Key == only) selected.Add(def);
            if (selected.Count == 0) { Debug.LogError("[SKILL_GALLERY] Unknown skill key: " + only); Application.Quit(2); return true; }
            galleryRoot = Path.Combine(smokeDirectory, "skill-gallery");
            if (Directory.Exists(galleryRoot))
            { Debug.LogError("[SKILL_GALLERY] Use a new --output directory; refusing to replace " + galleryRoot); Application.Quit(2); return true; }
            Directory.CreateDirectory(galleryRoot);
            galleryResult.startedUtc = DateTime.UtcNow.ToString("o");
            galleryResult.requestedFps = galleryFps;
            galleryResult.cutscenesEnabled = galleryShowCutscenes;
            // Stable default layered avatar, independent of this PC's saved wardrobe. No preferences are written.
            localLook = bk.defaultLook ?? localLook;
            StartCoroutine(RunSkillGallery(selected));
            return true;
        }

        PlayerCommand SkillGalleryCommand(int id)
        {
            var command = new PlayerCommand { PlayerId = id };
            if (gallerySkill == null || sim.Elapsed < .5f) return command;
            if (gallerySkill.Kind == SkillKind.Basic)
            { command.Basic = sim.Elapsed < 2.1f; galleryIssued |= command.Basic; }
            else if (!galleryIssued)
            {
                galleryIssued = true;
                if (gallerySkill.Kind == SkillKind.Dodge) command.Dash = true;
                else command.Skills = PlayerCommand.Bit(gallerySkill.Id);
            }
            // Show the actual four-hit conversion after a normal Frenzy cast, not a forced sprite state.
            if (gallerySkill.Id == SkillId.Frenzy && sim.Elapsed >= 1.5f && sim.Elapsed < 3.1f) command.Basic = true;
            if (gallerySkill.Id == SkillId.ExtremeOverkill && galleryIssued && !galleryOverkillFinishIssued && sim.Elapsed >= 1.6f && sim.Players[id].AttackTime <= 0f)
            {
                // First press equips the blood sword; a later ordinary press releases its finisher.
                galleryOverkillFinishIssued = true;
                command.Skills = PlayerCommand.Bit(SkillId.ExtremeOverkill);
            }
            return command;
        }

        float GalleryDuration(SkillDef def)
        {
            float end = Mathf.Max(3.8f, .5f + def.Duration + 3.5f);
            foreach (var hit in def.Hits)
                end = Mathf.Max(end, .5f + hit.Delay + hit.Telegraph + hit.Active + hit.Recovery + (Mathf.Max(1, hit.Repeat) - 1) * hit.Interval + .5f);
            foreach (var cue in def.Fx)
            {
                BkEffect effect;
                float tail = fx.TryGetValue(cue.Effect, out effect) ? effect.frameCount / Mathf.Max(1f, effect.fps) : 1f;
                end = Mathf.Max(end, .5f + cue.Delay + Mathf.Max(cue.Loop, tail) + .3f);
            }
            if (def.Id == SkillId.Frenzy) end = Mathf.Max(end, 4f);
            if (def.Id == SkillId.ExtremeOverkill) end = Mathf.Max(end, 7f);
            // Bound artifact size; metadata records the requested and observed duration.
            return Mathf.Min(end, 12f);
        }

        IEnumerator RunSkillGallery(List<SkillDef> selected)
        {
            yield return null;
            foreach (SkillDef def in selected)
            {
                galleryOrdinal++;
                gallerySkill = def;
                galleryIssued = false;
                galleryOverkillFinishIssued = false;
                Begin(NetworkRole.Offline);
                if (sim == null || net == null) { Debug.LogError("[SKILL_GALLERY] Offline startup failed"); break; }
                // A fresh Start resets cooldowns, buffered commands, combos and all prior effect events.
                sim.AutoBuffs = false;
                sim.Start();
                sim.PracticeInvulnerable = true;
                sim.Boss.MaxHp = sim.Boss.Hp = 1000000f;
                sim.Boss.StaggerTime = 10000f;
                sim.Boss.PatternName = "스킬 검토용 고정 표적";
                sim.Boss.Position = new Vector2(0f, 0f);
                sim.Players[0].Position = new Vector2(-2f, 0f);
                sim.Players[0].Facing = 1;
                sim.Players[0].Look = localLook;
                var clip = new GalleryClip { key = def.Key, name = def.Name, kind = def.Kind.ToString(), look = localLook, duration = GalleryDuration(def) };
                galleryResult.skills.Add(clip);
                string directory = Path.Combine(galleryRoot, def.Key);
                Directory.CreateDirectory(directory);
                float next = 0f;
                while (sim.Elapsed < clip.duration)
                {
                    yield return new WaitForEndOfFrame();
                    var p = sim.Players[0];
                    clip.commandIssued = galleryIssued;
                    clip.secondCommandIssued = galleryOverkillFinishIssued;
                    if (def.Id == SkillId.ExtremeOverkill && galleryOverkillFinishIssued && p.ActionSequence >= 2 && p.ActiveSkill == def.Id) clip.secondActionObserved = true;
                    if ((p.ActionSequence > 0 && p.ActiveSkill == def.Id) || (def.Kind == SkillKind.Dodge && p.Cooldowns[(int)SkillId.Dash] > 0f)) clip.actionObserved = true;
                    if (sim.Elapsed + .0001f < next) continue;
                    string filename = "frame-" + clip.frames.Count.ToString("D4") + ".png";
                    Texture2D captured = ScreenCapture.CaptureScreenshotAsTexture();
                    try { File.WriteAllBytes(Path.Combine(directory, filename), captured.EncodeToPNG()); }
                    finally { Destroy(captured); }
                    clip.frames.Add(new GalleryFrame { file = def.Key + "/" + filename, seconds = sim.Elapsed, tick = sim.TickIndex,
                        activeSkill = p.ActiveSkill.ToString(), actionSeconds = p.ActionElapsed, actionDuration = p.ActionDuration,
                        actionSequence = p.ActionSequence, combo = p.BasicComboStep, x = p.Position.x, y = p.Position.y, frenzySeconds = p.FrenzyTime,
                        phase = sim.Elapsed < .5f ? "idle" : p.ActionDuration > 0 && p.ActionElapsed < p.ActionDuration ? "action" : "effect-tail" });
                    next = sim.Elapsed + 1f / galleryFps;
                }
                clip.observedDuration = sim.Elapsed;
                clip.damage = sim.Boss.MaxHp - sim.Boss.Hp;
                clip.complete = clip.actionObserved && clip.frames.Count > 0;
                if (def.Id == SkillId.ExtremeOverkill) clip.complete &= clip.secondActionObserved;
                SaveSkillGallery();
                Debug.Log("[SKILL_GALLERY] " + def.Key + " frames=" + clip.frames.Count + " cast=" + clip.actionObserved);
            }
            gallerySkill = null;
            pauseMenu = true;
            galleryResult.completed = galleryResult.skills.Count == selected.Count;
            foreach (var clip in galleryResult.skills) galleryResult.completed &= clip.complete;
            SaveSkillGallery();
            Application.Quit(galleryResult.completed && errors == 0 ? 0 : 1);
        }

        void SaveSkillGallery()
        {
            galleryResult.width = Screen.width; galleryResult.height = Screen.height; galleryResult.errors = errors;
            File.WriteAllText(Path.Combine(galleryRoot, "gallery.json"), JsonUtility.ToJson(galleryResult, true));
        }

        // Call at the end of DrawGame, under its existing 1280 x 800 GUI matrix.
        void DrawSkillGallery()
        {
            if (!skillGallery || gallerySkill == null) return;
            Fill(new Rect(18, 132, 850, 55), new Color(0f, 0f, 0f, .85f));
            Label(new Rect(30, 137, 820, 25), "스킬 검토 " + galleryOrdinal + " · " + gallerySkill.Name + " [" + gallerySkill.Key + "]", 18, Color.white, true);
            Label(new Rect(30, 164, 820, 18), "실제 렌더링 · 고정 표적 · " + sim.Elapsed.ToString("F2") + "s · 컷신 " + (galleryShowCutscenes ? "표시" : "숨김"), 12, cyan);
        }
    }
}
