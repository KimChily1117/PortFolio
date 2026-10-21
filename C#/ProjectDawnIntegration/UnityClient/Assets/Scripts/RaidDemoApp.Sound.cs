using System;
using System.Collections.Generic;
using UnityEngine;

namespace DnfRaidDemo
{
    // Original client sounds (SoundPacks / Music, read-only copies in Resources/Sound) driven by
    // simulation events. Which sound plays when is demo design; the game's own sound scripts are in the
    // encrypted Script.pvf. A name ending in '_' picks a random file with that prefix.
    public sealed partial class RaidDemoApp
    {
        [Serializable] class SoundList { public string[] names; }
        struct Cue { public float At, Volume; public string Name; }

        static readonly Dictionary<SkillId, string[]> SkillSounds = new Dictionary<SkillId, string[]>
        {
            { SkillId.BloodSword, new[] { "0:bloodsword_ready", "0:sm_blood_blastex", ".40:blood_sword_exp_" } },
            { SkillId.GoreCross, new[] { "0:gore_cross_01", ".12:gorecross_cross", ".33:gore_cross_02" } },
            { SkillId.RagingFury, new[] { "0:sm_blood", ".55:p_bloodexp", ".55:bloodblastex_loop" } },
            { SkillId.Rage, new[] { "0:rage_start", ".4:rage_hack", ".58:rage_hack", ".76:rage_hack", ".94:rage_hack", "1.3:rage_finish" } },
            { SkillId.BloodyRave, new[] { "0:sm_bloody_rave_01_1", "0:bloody_rave_cast", ".99:sm_bloody_rave_02", "1.03:bloody_rave_slash" } },
            { SkillId.OutrageBreak, new[] { "0:out_rage_break_cast", ".3:outrage_swing", ".55:outrage_crash", ".9:out_rage_break_exp", "1.08:out_rage_break_exp", "1.26:out_rage_break_exp", "1.44:out_rage_break_exp" } },
            { SkillId.BloodBoom, new[] { "0:sm_bloodboom_01", "0:bloodboom_cast_01", ".55:sm_bloodboom_02", ".6:bloodboom_exp" } },
            { SkillId.Bloodlust, new[] { "0:blood_lust_cast", ".25:blood_lust_shot" } },
            { SkillId.BrutalCrusher, new[] { "0:brutal_crusher_cast", ".6:brutal_crusher_swish", ".82:brutal_crusher_exp" } },
            // The ac01/ac02 assets are 17–18 second dialogue, not combat cast cues.
            { SkillId.Frenzy, Array.Empty<string>() },
            { SkillId.Berserk, new[] { "0:sm_ac_berserker_03" } },
            { SkillId.BloodScratch, new[] { "0:sm_blood_scratch_01", ".1:blood_scratch_01", ".36:blood_scratch_02" } },
            { SkillId.CruelEngage, new[] { ".16:cruel_engage_01", ".36:cruel_engage_02", ".65:cruel_engage_finish" } },
            { SkillId.ExtremeOverkill, new[] { "0:extreme_overkill_blood_rise" } },
            { SkillId.BloodSnatch, new[] { "0:bloodsnatch_dash", ".22:bloodsnatch_grab", ".5:bloodsnatch_jump", ".94:sm_bloodsnatch_02", "1.02:bloodsnatch_strike" } },
            { SkillId.FatalBlood, new[] { "0:sm_fatalblood_01", "0:fatalblood_appear", ".32:fatalblood_atk_01", ".54:fatalblood_atk_02", ".81:sm_fatalblood_03", ".86:fatalblood_atk_03" } },
            { SkillId.BloodRiven, new[] { "0:sm_bloodriven_01", "0:bloodriven_cast", ".4:bloodriven_change", ".9:sm_bloodriven_02", ".95:bloodriven_atk", "1.45:bloodriven_exp" } },
            { SkillId.RampantMadness, new[] { ".1:rampantmadness_01", ".3:rampantmadness_02", ".75:rampantmadness_03" } },
            { SkillId.Destruction, new[] { "0:sm_extreamblood_01_1", ".3:extreamblood_swish_01", ".65:extreamblood_swish_02", "1:extreamblood_swish_03", "1.35:extreamblood_swish_04",
                "2:extreamblood_put", "2.05:sm_extreamblood_02_1", "4.8:extreamblood_jump", "5.3:bloodriven_exp" } },
        };
        static readonly string[] OverkillFinisherSounds = { ".2:extreme_overkill_blood", ".45:extreme_overkill_blood_fin" };

        // Weapon swing/hit file prefixes per weapon kind (SoundPacks/sounds_char_swordman_weapon).
        static readonly Dictionary<string, string> WeaponSound = new Dictionary<string, string>
        { { "lswd", "sqrswd" }, { "katana", "kat" }, { "sswd", "minswd" }, { "club", "stick" }, { "beamswd", "beamswd" } };

        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, List<string>> prefixCache = new Dictionary<string, List<string>>();
        readonly List<Cue> cues = new List<Cue>();
        string[] soundNames = new string[0];
        AudioSource bgm;
        string bgmName;
        int bgmStarts;
        float lastBossVoice;
        readonly System.Random soundRandom = new System.Random(7);

        void LoadSounds()
        {
            TextAsset list = Resources.Load<TextAsset>("Sound/sounds");
            if (list != null) soundNames = JsonUtility.FromJson<SoundList>(list.text).names ?? new string[0];
            // Avoid loading/decoding the contact bank on the first successful hit.
            foreach(string name in soundNames)
                if(name.Contains("_hit_") || name.StartsWith("blood_sword_exp_") || name=="extreme_overkill_blood_fin")
                    clips[name]=Resources.Load<AudioClip>("Sound/"+name);
            for(int i=0;i<contactVoices.Length;i++) {
                var voice=gameObject.AddComponent<AudioSource>();voice.playOnAwake=false;voice.priority=16;contactVoices[i]=voice;
            }
            bgm = gameObject.AddComponent<AudioSource>();
            bgm.playOnAwake=false;bgm.loop = true; bgm.volume = .32f;
            // Decode before encounter admission so the opening image does not wait on disk I/O.
            foreach(string name in new[]{"bgm_bakal_dragon_king_1phase","bgm_bakal_dragon_king_2phase"}){
                var music=Resources.Load<AudioClip>("Sound/"+name);clips[name]=music;if(music!=null)music.LoadAudioData();
            }
        }

        string Pick(string name)
        {
            if (!name.EndsWith("_")) return name;
            List<string> matches;
            if (!prefixCache.TryGetValue(name, out matches))
            {
                matches = new List<string>();
                foreach (string candidate in soundNames) if (candidate.StartsWith(name)) matches.Add(candidate);
                prefixCache[name] = matches;
            }
            return matches.Count == 0 ? null : matches[soundRandom.Next(matches.Count)];
        }

        // Swing files are "<kind><a|b|c>[_NN].ogg"; hit files contain "_hit".
        string Swing(string stem)
        {
            var matches = new List<string>();
            foreach (string candidate in soundNames)
                if (candidate.StartsWith(stem) && !candidate.Contains("hit") && !candidate.Contains("touch")
                    && (candidate.Length == stem.Length || candidate[stem.Length] == '_')) matches.Add(candidate);
            return matches.Count == 0 ? null : matches[soundRandom.Next(matches.Count)];
        }

        bool PlaySound(string name, float volume = .7f)
        {
            name = name == null ? null : Pick(name);
            if (name == null) return false;
            AudioClip clip;
            if (!clips.TryGetValue(name, out clip)) { clip = Resources.Load<AudioClip>("Sound/" + name); clips[name] = clip; }
            if (clip == null) return false;
            audioSource.PlayOneShot(clip, volume);
            return true;
        }

        void Schedule(string name, float delay, float volume = .7f)
        { cues.Add(new Cue { At = Time.unscaledTime + Mathf.Max(0, delay), Name = name, Volume = volume }); }

        void UpdateSounds()
        {
            for (int i = cues.Count - 1; i >= 0; i--)
                if (Time.unscaledTime >= cues[i].At) { PlaySound(cues[i].Name, cues[i].Volume); cues.RemoveAt(i); }
            UpdateBakalMusic();
        }

        void UpdateBakalMusic()
        {
            if(bgm==null)return;
            string wanted = !inGame || sim == null || sim.Mode != EncounterMode.Fighting ? null
                : sim.PhaseTwoBattlefieldActive ? "bgm_bakal_dragon_king_2phase" : "bgm_bakal_dragon_king_1phase";
            float volume=sim!=null&&sim.IntroActive?.44f:Time.unscaledTime<duckUntil?.23f:.32f;
            bgm.volume=Mathf.MoveTowards(bgm.volume,volume,Time.unscaledDeltaTime*1.2f);
            if (wanted != bgmName)
            {
                bgmName = wanted;
                bgm.Stop();
                if (wanted != null)
                {
                    AudioClip clip;clips.TryGetValue(wanted,out clip);
                    if (clip != null) {
                        if(flowMusic!=null)flowMusic.Stop();
                        bgm.clip = clip;bgm.volume=volume;bgm.Play();bgmStarts++;
                        Debug.Log("[BAKAL_BGM_START] track="+wanted+" tick="+sim.TickIndex+" intro="+sim.IntroActive+" starts="+bgmStarts+" volume="+volume);
                    }
                }
            }
        }

        string WeaponKind(PlayerState p)
        {
            string weapon; if (!ParseLook(string.IsNullOrEmpty(p.Look) ? bk.defaultLook : p.Look).TryGetValue("weapon", out weapon)) return "lswd";
            int colon = weapon.IndexOf(':');
            return colon > 0 ? weapon.Substring(0, colon) : weapon;
        }

        PlayerState PlayerById(int id)
        {
            foreach (PlayerState p in sim.Players) if (p.Id == id) return p;
            return null;
        }

        // Returns true when an original sound covered the event (otherwise the synthesized cue plays).
        bool PlayOriginal(CombatEvent ev)
        {
            float back = -ev.Age;
            bool phase2 = sim.Boss.Phase == 2;
            switch (ev.Kind)
            {
                case "Skill":
                {
                    PlayerState p = PlayerById(ev.TargetId);
                    var skill = (SkillId)Mathf.RoundToInt(ev.Value);
                    if (skill == SkillId.Basic && p != null)
                    {
                        string weapon; WeaponSound.TryGetValue(WeaponKind(p), out weapon);
                        string letter = p.BasicComboStep == 0 ? "a" : p.BasicComboStep == 1 ? "b" : "c";
                        PlaySound(Swing((weapon ?? "sqrswd") + letter) ?? Swing((weapon ?? "sqrswd") + "a"), .5f);
                        if (soundRandom.NextDouble() < .35) PlaySound("sm_" + (Mathf.Min(p.BasicComboStep, 2) + 1) + "atk_berserker_", .6f);
                        return true;
                    }
                    string[] list;
                    if (!SkillSounds.TryGetValue(skill, out list)) return false;
                    if (skill == SkillId.ExtremeOverkill && p != null && p.ActionOverkillFinish) list = OverkillFinisherSounds;
                    foreach (string item in list)
                    {
                        int colon = item.IndexOf(':');
                        string sound=item.Substring(colon+1);
                        // Personal impact accents fire at contact, not at a fixed cast timer or on a miss.
                        if(ev.TargetId==net.LocalPlayerId && (skill==SkillId.BloodSword && sound=="blood_sword_exp_" || skill==SkillId.ExtremeOverkill && sound=="extreme_overkill_blood_fin"))continue;
                        float rate=p!=null?Mathf.Max(.1f,p.ActionRate):1f;
                        Schedule(sound, float.Parse(item.Substring(0, colon), System.Globalization.CultureInfo.InvariantCulture)/rate + back,ev.TargetId==net.LocalPlayerId?.7f:.42f);
                    }
                    return true;
                }
                case "Damage":
                    if (ev.TargetId >= 0) return PlaySound("sm_dmg_", .65f);
                    float previous;hitSoundAt.TryGetValue(ev.SourceId,out previous);
                    bool own=ev.SourceId==net.LocalPlayerId;
                    if (!hitSoundAt.ContainsKey(ev.SourceId) || Time.unscaledTime-previous>(own?.035f:.09f))
                    {
                        hitSoundAt[ev.SourceId]=Time.unscaledTime;
                        PlayerState attacker = PlayerById(ev.SourceId);
                        string weapon; WeaponSound.TryGetValue(attacker != null ? WeaponKind(attacker) : "lswd", out weapon);
                        if(!own || !PlayPersonalContactSound(ev,weapon))PlaySound((weapon ?? "sqrswd") + "a_hit_",own?.65f:.25f);
                    }
                    if (Time.unscaledTime - lastBossVoice > 2.6f && soundRandom.NextDouble() < .3)
                    { lastBossVoice = Time.unscaledTime; PlaySound(phase2 ? "bakal_dragon_dmg_" : "bakal_human_dmg_", .55f); }
                    return true;
                case "MeteorImpact": return PlaySound("bakal_dragon_meteor_exp_", .85f);
                case "Jump": return PlaySound("sm_jump", .45f);
                case "Start": return PlaySound("bakal_human_meet_", .8f);
                case "Stagger": PlaySound(phase2 ? "bakal_dragon_shield_break" : "bakal_human_groggy", .8f); return false;
                case "Phase":
                    if(sim.PhaseTransitionStartTick>=0)return true; // Shared cinematic clock owns these cues.
                    PlaySound("bakal_dragon_incoming_appear", .8f); Schedule("bakal_dragon_roar_01", .5f + back, .9f);
                    return true;
                case "StompImpact": return PlaySound(phase2?"bakal_dragon_fire_stomp_impact":"bakal_human_warstomp_exp",.85f);
                case "Pattern":
                {
                    if (Time.unscaledTime - lastBossVoice > 1.2f)
                    { lastBossVoice = Time.unscaledTime; PlaySound(phase2 ? "bakal_dragon_skill_" : "bakal_human_skill_", .75f); }
                    string[] phase1 = { "bakal_human_firebreath_", "bakal_dragon_meteor_cast", "bakal_human_pattern_ready", "bakal_human_attack_wind_", "bakal_fever_start", "bakal_human_earth_attack" };
                    string[] phase2Sfx = { "bakal_dragon_breath_shout", "bakal_dragon_meteor_cast", "bakal_dragon_pattern2_ready", "bakal_dragon_pattern1_wing", "bakal_fever_start", "bakal_dragon_fire_stomp_attack" };
                    if(Mathf.RoundToInt(ev.Value)==DawnPatternRules.Meteor) return PlaySound("bakal_dragon_meteor_cast",.6f);
                    int index = Mathf.Clamp(Mathf.RoundToInt(ev.Value), 0, 5);
                    if(index==5)return true; // Foot-contact cue comes from StompImpact, not pattern preparation.
                    PlaySound((phase2 ? phase2Sfx : phase1)[index], .6f);
                    return true;
                }
                case "Victory": return PlaySound(phase2 ? "bakal_dragon_incoming_fall" : "bakal_human_groggy", .9f);
                case "Defeat": return PlaySound("sm_die", .8f);
            }
            return false;
        }
    }
}
