using System;
using System.Collections;
using System.IO;
using ProjectDawn.Network;
using UnityEngine;
namespace DnfRaidDemo
{
    public sealed partial class RaidDemoApp
    {
        bool commandsReview,commandsReviewBasic;
        [Serializable] sealed class CommandsReviewResult
        {
            public bool offlineInputFixture=true,physicalKeyboardInjected=false,commandEditorCaptured;
            public bool initialFrenzyOn,frenzyOff,frenzyOffBasic,frenzyOn,frenzyOnBasic,berserkCommand,spaceConsumed;
            public int errors;
        }
        bool StartCommandsReview(string[] args)
        {
            if(Array.IndexOf(args,"--commands-review")<0)return false;
            commandsReview=true;Begin(NetworkRole.Offline);sim.PracticeInvulnerable=true;
            sim.Boss.AttackTime=10000;sim.Boss.StaggerTime=10000;
            sim.Players[0].Position=new Vector2(-3,0);
            StartCoroutine(CommandsReviewRoutine());return true;
        }
        PlayerCommand CommandsReviewCommand(int playerId)
        { return new PlayerCommand{PlayerId=playerId,Skills=pressedSkills,Basic=commandsReviewBasic}; }
        bool InjectCommandReview(SkillCommandKeys direction)
        {
            // Same recognizer and request latch as live input. Deterministic samples,
            // not OS keystrokes; this fixture makes no physical keyboard QA claim.
            double now=Time.unscaledTime;skillCommands.Reset();
            skillCommands.Sample(direction,sim.Players[0].Facing,now-.24,commandBindings);
            skillCommands.Sample(SkillCommandKeys.None,sim.Players[0].Facing,now-.18,commandBindings);
            skillCommands.Sample(direction,sim.Players[0].Facing,now-.12,commandBindings);
            skillCommands.Sample(SkillCommandKeys.None,sim.Players[0].Facing,now-.06,commandBindings);
            var result=skillCommands.Sample(SkillCommandKeys.Space,sim.Players[0].Facing,now,commandBindings);
            ApplySkillCommandMatches(result);
            return result.Count==1 && result[0].Terminal==SkillCommandKeys.Space && !dash;
        }
        IEnumerator CommandsReviewRoutine()
        {
            var report=new CommandsReviewResult();
            yield return new WaitForSecondsRealtime(1.5f);
            var saved=commandBindings.ToArray();DefaultSkillCommands(false);OpenSkillCommands();
            yield return new WaitForSecondsRealtime(.7f);Capture("commands-editor.png");report.commandEditorCaptured=true;
            yield return new WaitForSecondsRealtime(.4f);CloseSkillCommands();
            float deadline=Time.unscaledTime+30;
            while(sim.IntroActive && Time.unscaledTime<deadline)yield return null;
            yield return new WaitForSecondsRealtime(.8f);
            var player=sim.Players[0];report.initialFrenzyOn=player.FrenzyTime>0;
            report.spaceConsumed=InjectCommandReview(SkillCommandKeys.Down);
            yield return new WaitForSecondsRealtime(.8f);
            report.frenzyOff=player.FrenzyTime<=0;Capture("frenzy-off-idle.png");
            yield return new WaitForSecondsRealtime(.4f);commandsReviewBasic=true;
            yield return new WaitForSecondsRealtime(.18f);
            report.frenzyOffBasic=player.ActiveSkill==SkillId.Basic && !player.ActionFrenzy;Capture("frenzy-off-basic.png");
            commandsReviewBasic=false;yield return new WaitForSecondsRealtime(.8f);
            report.spaceConsumed&=InjectCommandReview(SkillCommandKeys.Down);
            yield return new WaitForSecondsRealtime(.8f);
            report.frenzyOn=player.FrenzyTime>0;Capture("frenzy-on-idle.png");
            yield return new WaitForSecondsRealtime(.4f);commandsReviewBasic=true;
            yield return new WaitForSecondsRealtime(.18f);
            report.frenzyOnBasic=player.ActiveSkill==SkillId.Basic && player.ActionFrenzy;Capture("frenzy-on-basic.png");
            commandsReviewBasic=false;yield return new WaitForSecondsRealtime(.8f);
            int sequence=player.ActionSequence;
            report.spaceConsumed&=InjectCommandReview(player.Facing<0?SkillCommandKeys.Left:SkillCommandKeys.Right);
            yield return new WaitForSecondsRealtime(.15f);
            report.berserkCommand=player.ActionSequence>sequence && player.ActiveSkill==SkillId.Berserk;Capture("berserk-command.png");
            yield return new WaitForSecondsRealtime(.6f);
            report.errors=errors;commandBindings.Clear();commandBindings.AddRange(saved);
            Directory.CreateDirectory(smokeDirectory);File.WriteAllText(Path.Combine(smokeDirectory,"commands-review.json"),JsonUtility.ToJson(report,true));
            Application.Quit(report.errors==0 && report.commandEditorCaptured && report.initialFrenzyOn && report.frenzyOff && report.frenzyOffBasic && report.frenzyOn && report.frenzyOnBasic && report.berserkCommand && report.spaceConsumed?0:1);
        }
    }
}
