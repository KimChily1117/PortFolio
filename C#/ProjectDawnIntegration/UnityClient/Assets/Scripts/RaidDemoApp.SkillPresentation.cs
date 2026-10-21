using System;
using System.Collections.Generic;
using UnityEngine;

namespace DnfRaidDemo
{
    public sealed partial class RaidDemoApp
    {
        // User-directed palette override. Keep source atlas bytes/alpha intact; lift opaque black
        // ink to blood red at decode time rather than a multiply tint (which cannot recolor black).
        static bool UsesRedBloodPalette(string resource)
        {
            return resource == "Art/fx/bk_rave_loop" || resource == "Art/fx/bk_rave_finish"
                || resource == "Art/fx/bk_bloodsword" || resource == "Art/fx/bk_bloodsword_exp";
        }
        static void ApplyRedBloodPalette(Texture2D texture)
        {
            var pixels = texture.GetPixels32();
            for (int i = 0; i < pixels.Length; i++) {
                var p = pixels[i];
                if (p.a == 0) continue;
                byte brightness = (byte)Mathf.Max(p.r, Mathf.Max(p.g, p.b));
                pixels[i] = new Color32((byte)Mathf.Max(160, brightness), (byte)(p.g * .22f), (byte)(p.b * .18f), p.a);
            }
            texture.SetPixels32(pixels); texture.Apply(false, true);
        }

        // Visual tracks outlive the actor's recovery. They never create hits; skill elevation is shared simulation state.
        sealed class SkillTrack
        {
            public int Actor, Sequence, Facing, RoomId;
            public SkillDef Def;
            public float Rate=1f;
            public float Start, CancelAt = float.PositiveInfinity;
            public Vector2 Origin;
        }
        readonly List<SkillTrack> skillTracks = new List<SkillTrack>();
        float trackClock = -1;
        RaidSimulation trackSimulation;

        void DrawSkillTracks(PlayerState p, Vector2 foot, bool behind)
        {
            if (!ReferenceEquals(trackSimulation, sim) && sim.Elapsed < trackClock || sim.Elapsed < trackClock - .1f)
                skillTracks.Clear();
            trackSimulation = sim; trackClock = sim.Elapsed;
            float now = sim.Elapsed + PresentationExtrapolation();
            skillTracks.RemoveAll(x => now - x.Start > 14f);
            SkillTrack current = null;
            foreach (var track in skillTracks)
                if (track.Actor == p.Id && track.Sequence == p.ActionSequence) current = track;
            bool armedCast = p.ActiveSkill == SkillId.ExtremeOverkill && !p.ActionOverkillFinish;
            if (current == null && p.ActionDuration > 0 && SkillBook.Valid(p.ActiveSkill) && !armedCast)
            {
                var def = SkillBook.Get(p.ActiveSkill);
                // Recover the cast origin if the first observed snapshot is already mid-lunge.
                float actionAge = p.Id==net.LocalPlayerId ? PlayerById(p.Id).ActionElapsed : p.ActionElapsed;
                float advanced = Mathf.Clamp(actionAge - p.ActionPresentationLead - def.AdvanceDelay, 0f, def.AdvanceUntil) * def.Advance;
                current = new SkillTrack { Actor = p.Id, Sequence = p.ActionSequence, Facing = p.ActionFacing,
                    RoomId=p.RoomId, Def = def, Rate=p.ActionRate, Start = sim.Elapsed - actionAge / p.ActionRate,
                    Origin = p.Position - new Vector2(advanced * p.ActionFacing, 0f) };
                skillTracks.Add(current);
            }
            foreach (var track in skillTracks)
            {
                if (track.Actor != p.Id || track.RoomId != p.RoomId) continue;
                bool ownsAction = track.Sequence == p.ActionSequence && p.ActionDuration > 0;
                float age = PersonalEffectAge(track.Actor,track.Sequence,(now - track.Start)*track.Rate);
                if (!ownsAction && age < track.Def.Lock - .025f) track.CancelAt = Mathf.Min(track.CancelAt, age);
                foreach (var cue in track.Def.Fx)
                {
                    if (cue.Behind != behind || cue.Delay > track.CancelAt || (!ownsAction && !cue.Grounded)) continue;
                    float time = age - cue.Delay;
                    if (time < 0) continue;
                    float travel = Mathf.Clamp(cue.Delay - track.Def.AdvanceDelay, 0f, track.Def.AdvanceUntil) * track.Def.Advance;
                    var anchor = cue.Grounded ? ScreenPoint(track.Origin + new Vector2(travel * track.Facing, 0f)) : foot;
                    var at = anchor + new Vector2(cue.Offset.x * track.Facing * 56, -cue.Offset.y * 32);
                    float envelope = cue.Loop > 0 ? Mathf.Clamp01(Mathf.Min(time / .05f, (cue.Loop - time) / .12f)) : 1f;
                    var savedMatrix=GUI.matrix;
                    if(track.Def.Id==SkillId.ExtremeOverkill)GUIUtility.ScaleAroundPivot(new Vector2(SkillBook.OverkillHorizontalScale,1),at);
                    DrawEffect(cue.Effect, time, at, 1.15f * cue.Scale, track.Facing,
                        new Color(1, 1, 1, cue.Alpha * envelope), cue.Loop > 0, cue.Loop > 0 ? cue.Loop : cue.Duration);
                    GUI.matrix=savedMatrix;
                }
            }
        }

        void DrawOverkillSword(PlayerState p, Vector2 foot, Color tint)
        {
            if (p.OverkillTime <= 0 || p.Hp <= 0) return;
            if (p.OverkillTime < 10) {
                float frequency = Mathf.Lerp(3f, 10f, 1f - p.OverkillTime / 10f);
                tint = Color.Lerp(tint, Color.white, .5f + .5f * Mathf.Sin(p.OverkillAge * frequency * Mathf.PI));
                tint.a *= .65f + .35f * Mathf.Abs(Mathf.Sin(p.OverkillAge * frequency * Mathf.PI));
            }
            // Original sword-normal IMG frame 2: fully charged back sword, not an invented shape.
            var savedMatrix=GUI.matrix;
            GUIUtility.ScaleAroundPivot(new Vector2(SkillBook.OverkillHorizontalScale,1),foot);
            DrawEffect("rv_hellbenter_sword_normal", 2f, foot, 1.15f, p.Facing, tint, false);
            GUI.matrix=savedMatrix;
        }

        // Observed body phases, not original ANI durations. Avoid playing the 48 poses uniformly:
        // rapid four-cut opening, jump/slam, held embedded blade, pull-out, recovery.
        static float DestructionBodyFrame(float age)
        {
            if (age < 1.4f) return Mathf.Min(21, Mathf.Floor(age / 1.4f * 22));
            if (age < 2f) return 22 + Mathf.Min(9, Mathf.Floor((age - 1.4f) / .6f * 10));
            if (age < 3.8f) return 32 + Mathf.Min(5, Mathf.Floor((age - 2f) / 1.8f * 6));
            if (age < 5.3f) return 38 + Mathf.Min(6, Mathf.Floor((age - 3.8f) / 1.5f * 7));
            return 45 + Mathf.Min(2, Mathf.Floor((age - 5.3f) / 1.3f * 3));
        }

    }
}
