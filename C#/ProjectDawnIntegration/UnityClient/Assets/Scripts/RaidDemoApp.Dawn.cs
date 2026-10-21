using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DnfRaidDemo
{
    public sealed partial class RaidDemoApp
    {
        bool dawnAuto,dawnVerified;
        float dawnStartAt=-1,dawnVerifySeconds,dawnNextCapture=2,dawnShakeUntil,dawnShakePower;
        readonly HashSet<string> dawnFeedback=new HashSet<string>();
        void BeginDawn()
        {
            try
            {
                net?.Dispose();sim=new RaidSimulation(4);net=new RaidNetwork();
                PreloadCutscenes();PrepareDawnHud();
                net.StartDawn(hostAddress,int.Parse(portText));
                net.Dawn.PersonalFeedback+=e=>{if(AcceptFeedback(e))PlayOriginal(e);};
                inGame=true;pauseMenu=showHelp=false;accumulator=0;dash=jump=false;pressedSkills=0;
                playedEvents.Clear();dawnFeedback.Clear();ResetHitFeedback();displayPositions.Clear();displayHeights.Clear();animations.Clear();
                string[] args=Environment.GetCommandLineArgs();dawnAuto=Array.IndexOf(args,"--dawn-auto")>=0;
                int verify=Array.IndexOf(args,"--verify-seconds");if(verify>=0 && verify+1<args.Length)float.TryParse(args[verify+1],out dawnVerifySeconds);
                dawnStartAt=-1;dawnVerified=false;dawnNextCapture=2;
            }
            catch(Exception e){message="서버 접속 실패: "+e.Message;net?.Dispose();net=null;sim=null;inGame=false;Debug.LogException(e);}
        }
        bool AcceptFeedback(CombatEvent e)
        {
            if(sim!=null && e.RoomId!=ViewedRoom)return false;
            if(!string.IsNullOrEmpty(e.FeedbackKey) && !dawnFeedback.Add(e.FeedbackKey))return false;
            PersonalContact(e);
            // Meteor kick is driven by the displayed zone activation, never a second delayed confirmation.
            if(e.Kind=="Phase" || e.Kind=="Stagger" || e.Kind=="Skill" && SkillBook.Get((SkillId)(int)e.Value).Tier>0)
            {dawnShakeUntil=Time.unscaledTime+.32f;dawnShakePower=6;}
            return true;
        }
        Vector2 DawnShake()
        {
            if(net==null)return Vector2.zero;
            Vector2 personal=ContactShake()+MeteorCameraKick();
            if(!net.IsDawn || Time.unscaledTime>=dawnShakeUntil)return personal;
            float fade=Mathf.Clamp01((dawnShakeUntil-Time.unscaledTime)*8);
            return Vector2.ClampMagnitude(personal+new Vector2(Mathf.Sin(Time.unscaledTime*97),Mathf.Cos(Time.unscaledTime*113))*dawnShakePower*fade,18f);
        }
        void DrawDawnParty()
        {
            int row=0;
            foreach(var p in sim.Players)
            {
                if(p.Id==net.LocalPlayerId)continue;
                float y=746+row++*16;
                Label(new Rect(980,y-3,50,16),(p.Id+1)+"P",11,Color.white);
                Bar(new Rect(1018,y,175,5),p.Hp/p.MaxHp,p.Hp>0?cyan:Color.gray);
                Label(new Rect(1200,y-3,55,16),Mathf.CeilToInt(p.Hp).ToString(),10,Color.gray);
            }
        }
        PlayerCommand DawnAutoCommand()
        {
            int id=net.LocalPlayerId;
            var command=RaidShowcase.Command(sim,id);
            var p=sim.Players[id];command.Skills=0;command.Basic=sim.Elapsed<6;
            if(p.Invulnerable>0)command.Jump=command.Dash=false;
            bool groundSoon=false;bool danger=false;
            foreach(var zone in sim.Zones)
            {
                if(zone.Tag=="ChargeTrail" || zone.RoomId!=p.RoomId || zone.FromPlayer || zone.Age<0 || zone.Age>zone.Telegraph+zone.ActiveTime)continue;
                if(zone.GroundOnly && zone.Telegraph-zone.Age<.65f)groundSoon=true;
                // React only to the same current-time telegraph shown on screen; no hidden finalization-delay offset.
                if(zone.GroundOnly && zone.Telegraph-zone.Age<.26f && zone.Telegraph-zone.Age>.15f)
                    command.Jump|=p.JumpHeight<=.001f && p.Invulnerable<=0;
                if(!zone.GroundOnly && RaidSimulation.Intersects(zone,p.Position,RaidSimulation.PlayerHurtboxHalfSize)) {
                    danger=true;
                    if(zone.Telegraph-zone.Age<.35f && zone.Telegraph-zone.Age>.1f && p.Cooldowns[(int)SkillId.Dash]<=0 && p.Invulnerable<=0)command.Dash=true;
                }
            }
            command.Basic=sim.Elapsed<6 && !danger && !groundSoon;
            if(!danger && !groundSoon && p.AttackTime<=0 && p.JumpHeight<=0 && Mathf.Abs(p.Position.y-sim.Boss.Position.y)<.9f)
            {
                if (p.OverkillTime > 0 && p.OverkillAge > 1.2f) { command.Skills = PlayerCommand.Bit(SkillId.ExtremeOverkill); return command; }
                for(int i=1;i<SkillBook.Count;i++)
                {
                    if(i==3 || i%sim.Players.Count!=id || net.Dawn.Skills.Contains(i) || sim.Elapsed<4+i*2.2f)continue;
                    var skill=SkillBook.All[i];
                    if(p.Cooldowns[i]<=0 && p.Mp>=skill.Mp){command.Skills=PlayerCommand.Bit(skill.Id);break;}
                }
            }
            if(sim.Elapsed>7 && sim.Elapsed<7.2f && id==0)command.Dash=true;
            return EvadeCharge(RoomAutoCommand(command));
        }
        void UpdateDawnVerification()
        {
            if(net.Dawn.Connected && dawnStartAt<0)dawnStartAt=Time.realtimeSinceStartup;
            if(dawnStartAt<0 || dawnVerifySeconds<=0)return;
            float elapsed=Time.realtimeSinceStartup-dawnStartAt;
            if(elapsed>=dawnNextCapture && elapsed<dawnVerifySeconds)
            {
                Capture("play-"+((int)dawnNextCapture).ToString("D3")+".png");dawnNextCapture+=8;
            }
            if(elapsed>=dawnVerifySeconds && !dawnVerified)
            {
                dawnVerified=true;net.Dawn.WriteAudit(smokeDirectory);Capture("final.png");
                File.WriteAllText(Path.Combine(smokeDirectory,"render-result.json"),JsonUtility.ToJson(new DawnRenderResult {
                    errors=errors,frames=frameCount,repaints=guiRepaints,width=Screen.width,height=Screen.height,
                    localSlot=net.LocalPlayerId,players=sim.Players.Count,autoInput=dawnAuto},true));
                Debug.Log("[DAWN_UNITY_VERIFIED] slot="+net.LocalPlayerId+" frames="+frameCount+" errors="+errors);
            }
            if(dawnVerified && elapsed>dawnVerifySeconds+2)Application.Quit(errors==0?0:1);
        }
        [Serializable] class DawnRenderResult {public int errors,frames,repaints,width,height,localSlot,players;public bool autoInput;}
    }
}
