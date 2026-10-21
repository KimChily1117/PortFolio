using System;
using System.Collections;
using System.IO;
using Google.Protobuf.Protocol;
using UnityEngine;
using UnityEngine.Video;

namespace DnfRaidDemo
{
    public sealed partial class RaidDemoApp
    {
        S_RaidCinematic clearCinema;
        S_RaidRewards deferredClearRewards;
        VideoPlayer clearVideo;
        AudioSource clearVideoAudio;
        RenderTexture clearVideoTexture;
        float clearAnchor,clearNextSeek,clearNextVote;
        bool clearVideoPlayed,clearVideoFailed,clearAutoVoted;
        public bool ClearCinematicActive=>clearCinema!=null&&!clearCinema.Complete;
        float ClearElapsed=>Time.realtimeSinceStartup-clearAnchor;
        void PrepareClearCinematic()
        {
            PreloadRewardMusic();
            PreloadCountdownArt();raidEntryStage=-1;
            ResetClearCinematic();
            var go=new GameObject("Clear cinematic · local playback");go.transform.SetParent(transform);
            clearVideoAudio=go.AddComponent<AudioSource>();clearVideoAudio.playOnAwake=false;clearVideoAudio.volume=.8f;
            clearVideo=go.AddComponent<VideoPlayer>();clearVideo.playOnAwake=false;clearVideo.isLooping=false;
            clearVideo.source=VideoSource.Url;clearVideo.url=new Uri(Path.Combine(Application.streamingAssetsPath,"Cinematics/bakal-clear.mp4")).AbsoluteUri;
            clearVideoTexture=new RenderTexture(1280,720,0);clearVideoTexture.Create();
            clearVideo.renderMode=VideoRenderMode.RenderTexture;clearVideo.targetTexture=clearVideoTexture;
            clearVideo.audioOutputMode=VideoAudioOutputMode.AudioSource;clearVideo.controlledAudioTrackCount=1;
            clearVideo.EnableAudioTrack(0,true);clearVideo.SetTargetAudioSource(0,clearVideoAudio);
            clearVideo.waitForFirstFrame=true;clearVideo.skipOnDrop=true;
            clearVideo.errorReceived+=(source,error)=>{clearVideoFailed=true;FlowRecord("ClearVideoUnavailable "+error);};
            clearVideo.Prepare();
        }
        void ResetClearCinematic()
        {
            clearCinema=null;deferredClearRewards=null;clearVideoPlayed=clearVideoFailed=clearAutoVoted=false;clearNextVote=clearNextSeek=0;
            if(clearVideo!=null){clearVideo.Stop();Destroy(clearVideo.gameObject);clearVideo=null;clearVideoAudio=null;}
            if(clearVideoTexture!=null){clearVideoTexture.Release();Destroy(clearVideoTexture);clearVideoTexture=null;}
        }
        void HandleClearCinematic(S_RaidCinematic packet)
        {
            if(net?.Dawn.Client.Start==null||packet.Encounter!=net.Dawn.Client.Start.Encounter)return;
            if(clearCinema!=null&&(packet.Revision<clearCinema.Revision||(clearCinema.Complete&&!packet.Complete)))return;
            bool first=clearCinema==null;bool finishing=packet.Complete&&(first||!clearCinema.Complete);
            if(first && clearVideo==null)PrepareClearCinematic();
            clearCinema=packet.Clone();
            // Periodic server elapsed time plus measured one-way transit; video never controls rewards.
            clearAnchor=Time.realtimeSinceStartup-(packet.ElapsedMs/1000f+Mathf.Clamp(net.Dawn.ClockRttUs/2000000f,0,.5f));
            if(first){
                HideRaidRewards();CloseChat();if(commandWindow)CloseSkillCommands();
                inventoryWindow=skillWindow=pauseMenu=showHelp=false;ResetHitFeedback();cues.Clear();audioSource.Stop();
                foreach(var voice in contactVoices)if(voice!=null)voice.Stop();
                if(flowMusic!=null)flowMusic.Stop();
                if(dawnHud!=null){dawnHud.Display(null,false);dawnHud.DisplayBoss(null,false);}
                FlowRecord("ClearCinematicBegin encounter="+packet.Encounter+" required="+packet.Required);
                if(flowAuto)StartCoroutine(CaptureClearCinematic());
            }
            if(finishing){
                if(clearVideo!=null)clearVideo.Stop();if(bgm!=null)bgm.Stop();
                FlowRecord("ClearCinematicEnd reason="+packet.Reason+" votes="+packet.VotedSlots.Count+" required="+packet.Required);
                if(deferredClearRewards!=null){var receipt=deferredClearRewards;deferredClearRewards=null;ShowRaidRewards(receipt);}
            }
        }
        bool UpdateClearCinematic()
        {
            if(!ClearCinematicActive)return false;
            float fade=clearCinema.FadeMs/1000f,elapsed=ClearElapsed;
            if(bgm!=null){bgm.volume=Mathf.Lerp(.32f,0,Mathf.Clamp01(elapsed/fade));if(elapsed>=fade)bgm.Stop();}
            if(elapsed>=fade && clearVideo!=null && clearVideo.isPrepared && !clearVideoFailed){
                double target=Math.Max(0,Math.Min(clearCinema.DurationMs/1000.0-.05,elapsed-fade));
                if(!clearVideoPlayed){
                    clearVideo.time=target;clearVideo.Play();clearVideoPlayed=true;clearNextSeek=Time.realtimeSinceStartup+2;
                    FlowRecord("ClearVideoPlaying seconds="+target.ToString("F2",System.Globalization.CultureInfo.InvariantCulture));
                }else if(clearVideo.isPlaying && Time.realtimeSinceStartup>=clearNextSeek){
                    if(Math.Abs(clearVideo.time-target)>.5)clearVideo.time=target;
                    clearNextSeek=Time.realtimeSinceStartup+2;
                }
            }
            if(Input.GetKeyDown(KeyCode.Escape))VoteClearSkip();
            if(flowAuto&&!clearAutoVoted&&Arg("--cinematic-auto","")=="skip"&&net.LocalPlayerId<3&&elapsed>=fade+3+net.LocalPlayerId*2){clearAutoVoted=true;VoteClearSkip();}
            return true;
        }
        void VoteClearSkip()
        {
            if(!ClearCinematicActive||ClearElapsed<clearCinema.FadeMs/1000f||clearCinema.VotedSlots.Contains(net.LocalPlayerId)||Time.realtimeSinceStartup<clearNextVote)return;
            clearNextVote=Time.realtimeSinceStartup+1;
            net.Dawn.SendFlow(new C_RaidCinematicSkip{Encounter=clearCinema.Encounter});
            FlowRecord("ClearSkipRequested slot="+net.LocalPlayerId);
        }
        void DrawClearCinematic()
        {
            float fade=clearCinema.FadeMs/1000f,elapsed=ClearElapsed;
            if(elapsed<fade && sim!=null){bool enabled=GUI.enabled;GUI.enabled=false;DrawGame();GUI.enabled=enabled;}
            var matrix=GUI.matrix;GUI.matrix=Matrix4x4.identity;
            Fill(new Rect(0,0,Screen.width,Screen.height),new Color(0,0,0,Mathf.Clamp01(elapsed/fade)));
            if(elapsed>=fade && clearVideoPlayed && clearVideo!=null && clearVideo.texture!=null)
                GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),clearVideo.texture,ScaleMode.ScaleToFit);
            GUI.matrix=matrix;
            if(elapsed>=fade){
                Fill(new Rect(280,746,720,34),new Color(0,0,0,.72f));
                bool voted=clearCinema.VotedSlots.Contains(net.LocalPlayerId);
                Label(new Rect(280,746,720,34),(voted?"스킵 투표 완료":"ESC · 스킵 투표")+"  "+clearCinema.VotedSlots.Count+" / "+clearCinema.Required+"표  ·  과반수 동의 시 보상으로 이동",16,Color.white,false,TextAnchor.MiddleCenter);
                if(clearVideoFailed||(!clearVideoPlayed&&elapsed>fade+3))
                    Label(new Rect(230,350,820,55),"영상을 불러오지 못했습니다.\nESC로 스킵에 투표하거나 종료까지 기다려 주세요.",19,Color.white,false,TextAnchor.MiddleCenter);
            }
        }
        IEnumerator CaptureClearCinematic()
        {
            foreach(float at in new[]{.9f,3.5f,6f,8f,10f}){
                while(ClearCinematicActive&&ClearElapsed<at)yield return null;
                if(!ClearCinematicActive)yield break;
                yield return new WaitForEndOfFrame();
                Capture("clear-cinematic-"+at.ToString("F1",System.Globalization.CultureInfo.InvariantCulture)+".png");
            }
        }
    }
}
