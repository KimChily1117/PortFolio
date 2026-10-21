using System.Linq;
using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace DnfRaidDemo
{
    public sealed partial class RaidDemoApp
    {
        // Opt-in evidence recorder. Ordinary play never enters this path.
        bool recordFour, recordEarlyAwakeningObserved;
        RaidAudioRecorder fourAudio;
        bool fourAudioStarted;
        int recordCutActor = -1, recordCutSkill = -1;
        float recordCutAge;
        [Serializable] sealed class RecordedActor {
            public int id, skill, sequence, room; public string name; public float age, duration, hp, x, y, effectAge;public bool rendered;
        }
        [Serializable] sealed class RecordedScreen {
            public bool clearCinematic,rewards,videoPlaying;public float clearElapsed;public int clearVotes,clearRequired;public long videoFrame;
            public int phase,phaseStart;public bool phaseTransition,battlefield;public float combatBossHp;
            public string file; public long utcTicks; public int local, tick, cutActor, cutSkill;
            public int[] markTargets;public int markDeadline,nextRoomMarkTick;public RoomFire[] fires; public float bossHp,bossDamage;
            public int chargeTarget,chargeWave,chargeTrailCount;public float chargeAge,chargeTell;
            public float seconds, cutAge, bossX,bossY; public int room,markTarget,fireCount; public RecordedActor[] actors;
        }
        void StartFourRecording(string[] args)
        {
            portfolioDemo=Array.IndexOf(args,"--portfolio-demo")>=0;
            int.TryParse(Arg("--portfolio-slot","1"),out portfolioSlot);
            long.TryParse(Arg("--portfolio-start-utc","0"),out portfolioEpoch);
            recordFour = Array.IndexOf(args, "--record-four") >= 0;
            if(!recordFour && Array.IndexOf(args,"--record-audio")>=0 && fourAudio!=null)
                fourAudio.Begin(Path.Combine(smokeDirectory,"recording"));
            if (recordFour) StartCoroutine(portfolioDemo ? RecordPortfolioScreens() : RecordFourScreens());
        }
        void RecordCutDraw(int actor, int skill, float age)
        {
            if (!recordFour || Event.current.type != EventType.Repaint) return;
            recordCutActor = actor; recordCutSkill = skill; recordCutAge = age;
        }
        PlayerCommand RecordingCommand(PlayerCommand command)
        {
            // Reserve the first player's MP and send an ordinary skill input early enough
            // to compare all four views. Never alter simulation state or bypass validation.
            if (!recordFour || net.LocalPlayerId != 0) return command;
            var p = sim.Players[0];
            if (p.ActiveSkill == SkillId.Destruction && p.ActionDuration > 0)
                recordEarlyAwakeningObserved = true;
            if (recordEarlyAwakeningObserved) return command;
            command.Skills = 0;
            if (p.RoomId==RaidSimulation.BossRoom && Array.IndexOf(sim.MarkTargets,p.Id)<0 && sim.Elapsed >= 8 && p.Hp > 0 && p.AttackTime <= 0 && p.JumpHeight <= 0 && p.Mp >= SkillBook.Get(SkillId.Destruction).Mp) {
                command.Basic = command.Dash = command.Jump = false;
                command.Skills = PlayerCommand.Bit(SkillId.Destruction);
            }
            return command;
        }
        float RecordedEffectAge(PlayerState player)
        {
            if(player.RoomId!=ViewedRoom)return -1;
            var track=skillTracks.FindLast(t=>t.Actor==player.Id&&t.Sequence==player.ActionSequence&&t.RoomId==player.RoomId);
            return track==null?-1:(sim.Elapsed-track.Start+PresentationExtrapolation())*track.Rate;
        }
        IEnumerator RecordFourScreens()
        {
            string directory = Path.Combine(smokeDirectory, "recording");
            Directory.CreateDirectory(directory);
            string index = Path.Combine(directory, "frames.jsonl");
            if (File.Exists(index)) { Debug.LogError("Recording requires a fresh output directory"); yield break; }
            float next = 0; int number = 0;
            while (true) {
                yield return new WaitForEndOfFrame();
                if (!inGame || sim == null || net == null || !net.IsDawn || (!net.Connected&&!ClearCinematicActive&&!RaidRewardsVisible) || flowStage != "Raid") {
                    if(fourAudioStarted && fourAudio!=null)fourAudio.Finish();
                    continue;
                }
                if(!fourAudioStarted && net.LocalPlayerId==0){
                    fourAudioStarted=true;
                    fourAudio.Begin(directory);
                }
                if (Time.realtimeSinceStartup < next) continue;
                var row = new RecordedScreen { file = "frame-" + number++.ToString("D5") + ".jpg",
                    clearCinematic=ClearCinematicActive,rewards=RaidRewardsVisible,clearElapsed=clearCinema==null?-1:ClearElapsed,
                    clearVotes=clearCinema?.VotedSlots.Count??0,clearRequired=clearCinema?.Required??0,
                    videoPlaying=clearVideo!=null&&clearVideo.isPlaying,videoFrame=clearVideo!=null?clearVideo.frame:-1,
                    phase=sim.Boss.Phase,phaseStart=sim.PhaseTransitionStartTick,phaseTransition=sim.PhaseTransitionActive,battlefield=sim.PhaseTwoBattlefieldActive,combatBossHp=sim.Boss.Hp,
                    utcTicks = DateTime.UtcNow.Ticks, local = net.LocalPlayerId, tick = sim.TickIndex,
                    chargeTarget=sim.Zones.FirstOrDefault(z=>z.Tag=="BossCharge"&&z.Age<z.Telegraph+z.ActiveTime)?.TargetId??-1,chargeWave=sim.Zones.FirstOrDefault(z=>z.Tag=="BossCharge"&&z.Age<z.Telegraph+z.ActiveTime)?.HitIndex??0,chargeAge=sim.Zones.FirstOrDefault(z=>z.Tag=="BossCharge"&&z.Age<z.Telegraph+z.ActiveTime)?.Age??0,chargeTell=DawnPatternRules.ChargeTell,chargeTrailCount=sim.Zones.Count(z=>z.Tag=="ChargeTrail"&&z.RoomId==ViewedRoom),
                    nextRoomMarkTick=sim.NextRoomMarkTick,markTargets=sim.MarkTargets,markDeadline=sim.MarkDeadline,fires=sim.RoomFires.ToArray(),bossHp=dawnHud==null?1:dawnHud.BossHpFraction,bossDamage=dawnHud==null?1:dawnHud.BossDamageFraction,bossX=sim.Boss.Position.x,bossY=sim.Boss.Position.y,room=ViewedRoom,markTarget=sim.MarkTarget,fireCount=sim.RoomFires.Count,seconds = sim.Elapsed, cutActor = recordCutActor, cutSkill = recordCutSkill, cutAge = recordCutAge,
                    actors = new RecordedActor[sim.Players.Count] };
                for (int i = 0; i < sim.Players.Count; i++) {
                    var p = sim.Players[i]; row.actors[i] = new RecordedActor { id=p.Id, room=p.RoomId,name=p.Name,skill=(int)p.ActiveSkill,
                        rendered=p.RoomId==ViewedRoom,effectAge=RecordedEffectAge(p),sequence=p.ActionSequence, age=p.ActionElapsed, duration=p.ActionDuration, hp=p.Hp, x=p.Position.x, y=p.Position.y };
                }
                var texture = ScreenCapture.CaptureScreenshotAsTexture();
                try { File.WriteAllBytes(Path.Combine(directory, row.file), texture.EncodeToJPG(90)); }
                finally { Destroy(texture); }
                File.AppendAllText(index, JsonUtility.ToJson(row) + Environment.NewLine);
                next = Time.realtimeSinceStartup + 1f / 12f;
            }
        }
    }
}
