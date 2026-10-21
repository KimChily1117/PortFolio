using System;
using System.IO;
using UnityEngine;

namespace DnfRaidDemo {
 public sealed partial class RaidDemoApp {
  // Recorded image sequence, not recovered original ANI or camera scripts.
  Texture2D phaseTwoBattlefield; bool phaseTwoArtLoaded;
  RaidSimulation phaseTwoSoundSimulation; int phaseTwoSoundStart=-1; float phaseTwoSoundTime=-1;
  readonly float[] phaseTwoCueTimes={0,2,4.4f,8.35f,9.5f,10.6f};
  readonly string[] phaseTwoCueNames={"bakal_dragon_incoming_start","bakal_dragon_incoming_appear","bakal_dragon_roar_01","bakal_human_warstomp_exp","bakal_dragon_incoming_fall","bakal_dragon_pattern1_landing"};
  void LoadBakalPhaseTwo(){
   if(phaseTwoArtLoaded)return;phaseTwoArtLoaded=true;
   var text=Resources.Load<TextAsset>("BakalPhase2/manifest");
   if(text!=null)foreach(var effect in JsonUtility.FromJson<OriginalWorldDocument>(text.text).effects){fx[effect.id]=effect;GetSheet(effect.resource,effect.width,effect.height,effect.frames);}
   phaseTwoBattlefield=Resources.Load<Texture2D>("BakalPhase2/battlefield");
   foreach(var cue in phaseTwoCueNames)if(!clips.ContainsKey(cue))clips[cue]=Resources.Load<AudioClip>("Sound/"+cue);
  }
  void PhaseTwoFrame(string key,int frame,Vector2 anchor,float scale,Color tint){
   BkEffect effect;if(!fx.TryGetValue("p2_"+key,out effect))return;
   var sheet=GetSheet(effect.resource,effect.width,effect.height,effect.frames);int row;
   if(sheet.ByFrame.TryGetValue(Mathf.Clamp(frame,0,effect.frameCount-1),out row))DrawRow(sheet,row,anchor,effect.anchor[0],effect.anchor[1],scale,tint);
  }
  void DrawPhaseTwoBattlefield(){
   if(sim==null || !sim.PhaseTwoBattlefieldActive)return;
   LoadBakalPhaseTwo();if(phaseTwoBattlefield!=null)GUI.DrawTexture(new Rect(0,0,1280,720),phaseTwoBattlefield);
  }
  bool DrawBakalPhaseTwo(){
   if(!sim.PhaseTransitionActive)return false;
   LoadBakalPhaseTwo();float time=(sim.TickIndex-sim.PhaseTransitionStartTick)/60f;
   if(Event.current.type==EventType.Repaint){
    if(phaseTwoSoundSimulation!=sim || phaseTwoSoundStart!=sim.PhaseTransitionStartTick){phaseTwoSoundSimulation=sim;phaseTwoSoundStart=sim.PhaseTransitionStartTick;phaseTwoSoundTime=time-.001f;cues.Clear();}
    // Never replay old cues after reconciliation or a mid-cutscene reconnect.
    for(int i=0;i<phaseTwoCueTimes.Length;i++)if(phaseTwoSoundTime<phaseTwoCueTimes[i]&&time>=phaseTwoCueTimes[i]&&time-phaseTwoCueTimes[i]<.25f)PlaySound(phaseTwoCueNames[i],i==4?.9f:.7f);
    phaseTwoSoundTime=Mathf.Max(phaseTwoSoundTime,time);
   }
   var matrix=GUI.matrix;var tint=GUI.color;GUI.color=Color.white;
   Fill(new Rect(0,0,1280,720),Color.black);
   if(time<2.25f){
    if(arena!=null)GUI.DrawTexture(new Rect(0,0,1280,720),arena);
    if(ViewedRoom!=RaidSimulation.BossRoom&&originalCorridor!=null)GUI.DrawTexture(new Rect(0,0,1280,720),originalCorridor);
    foreach(var player in sim.Players)if(!player.Departed&&player.RoomId==ViewedRoom)DrawPlayer(player);
    if(ViewedRoom==RaidSimulation.BossRoom)Sprite("boss1","cast",ScreenPoint(sim.Boss.Position),1.508f,-1,time,Color.white,true);
    Fill(new Rect(0,0,1280,720),new Color(0,0,0,Mathf.Lerp(.1f,.85f,Mathf.Clamp01(time/2))));
   }
   if(time>=2.0f&&time<2.4f)Fill(new Rect(0,0,1280,720),new Color(1,1,1,1-Mathf.Clamp01((time-2.1f)/.3f)));
   if(time>=2.3f&&time<5.35f){
    float age=time-2.3f;
    if(time<3.9f)PhaseTwoFrame("rush_background",Mathf.FloorToInt(age/1.6f*22),Vector2.zero,1280f/854,Color.white);
    int frame=time<3.9f?Mathf.FloorToInt(age/1.6f*10):time<4.3f?10+Mathf.FloorToInt((time-3.9f)/.4f*5):15+Mathf.FloorToInt((time-4.3f)/1.05f*19);
    float shake=time>=4.3f?5f:0;
    GUI.matrix=matrix*Matrix4x4.Translate(new Vector3(Mathf.Sin(time*83)*shake,Mathf.Sin(time*107)*shake,0));
    PhaseTwoFrame("incoming",frame,Vector2.zero,1,Color.white);GUI.matrix=matrix;
   }
   if(time>=4.4f&&time<6.05f){
    float age=time-4.4f;Vector2 at=new Vector2(640,330);
    float alpha=Mathf.Clamp01((6.05f-time)/.35f);
    Color fire=new Color(1,1,1,alpha);
    if(age<.85f)DrawEffect("p2_fire_face_a",age,at,1.8f,1,fire,false,.85f);
    else DrawEffect("p2_fire_face_b",age-.85f,at,1.65f,1,fire,false,.8f);
    DrawEffect("p2_face_particle",age,at,1.65f,1,fire,false,1.65f);
    DrawEffect("p2_eye_light",age,new Vector2(640,340),1.6f,1,fire,false,1.65f);
   }
   if(time>=PlayerFallStart&&time<PlayerFallReveal)DrawPhaseTwoPlayerFall(time);
   if(time>=8){
    float landing=Mathf.Clamp01(1-(time-10.6f)/.65f);if(time<10.6f)landing=0;
    float partyImpact=time>=PlayerFallLand?Mathf.Clamp01(1-(time-PlayerFallLand)/.28f):0;
    GUI.matrix=matrix*Matrix4x4.Translate(new Vector3(Mathf.Sin(time*103)*(landing*15+partyImpact*5),Mathf.Sin(time*79)*(landing*12+partyImpact*8),0));
    // Follow the downward travel, then settle into the regular combat camera.
    float fallZoom=time<9?Mathf.Lerp(1.12f,.86f,EncounterSnap((time-8)/.16f)):Mathf.Lerp(.86f,1,EncounterSnap((time-9)/.18f));
    GUIUtility.ScaleAroundPivot(new Vector2(fallZoom,fallZoom),new Vector2(640,420));
    float overscan=Mathf.Max(0,1/fallZoom-1)*680;
    if(phaseTwoBattlefield!=null)GUI.DrawTexture(new Rect(-overscan,-overscan*.58f,1280+overscan*2,720+overscan*1.16f),phaseTwoBattlefield);
    DrawPhaseTwoPlayerFall(time);
    if(time>=9f&&ViewedRoom==RaidSimulation.BossRoom){
     Vector2 at=ScreenPoint(sim.Boss.Position);float progress=Mathf.Clamp01((time-9f)/1.6f);
     int frame=time<10.6f?Mathf.FloorToInt(progress*10):Mathf.Min(13,10+Mathf.FloorToInt((time-10.6f)/.12f));
     var bodyMatrix=GUI.matrix;if(bossFacing<0)GUIUtility.ScaleAroundPivot(new Vector2(-1,1),at);
     PhaseTwoFrame("descent",frame,at+new Vector2(0,-380*(1-progress*progress)),1.32f,Color.white);GUI.matrix=bodyMatrix;
     if(time>=10.6f){float age=time-10.6f;EncounterEffect("landing_a",age,at,5.2f,.75f);EncounterEffect("landing_b",age,at,4.8f,.95f);OriginalWorld("world_impact_wave",at,3,age,Color.white,false,.7f);}
    }
    GUI.matrix=matrix;
    if(time>=9&&time<10.6f){float speed=(time-9)/1.6f;for(int n=0;n<12;n++){float x=(n*113+37)%1280;float y=(time*(300+speed*650)+n*97)%650;Fill(new Rect(x,y,1+n%2,25+speed*90),new Color(1,.65f,.45f,.08f+speed*.12f));}}
    if(time<8.12f)Fill(new Rect(0,0,1280,720),new Color(0,0,0,.25f*(1-(time-8)/.12f)));
    if(time>=10.6f)Fill(new Rect(0,0,1280,720),new Color(1,.7f,.4f,landing*.13f));
   }
   GUI.matrix=matrix;
   float bars=time<11?34:34*(1-EncounterEase(time-11));Fill(new Rect(0,0,1280,bars),Color.black);Fill(new Rect(0,720-bars,1280,bars),Color.black);
   if(time>=10.8f)Label(new Rect(0,120,1280,46),"폭룡왕 바칼 · 진정한 모습",29,new Color(1,.73f,.3f,Mathf.Clamp01((time-10.8f)*4)),true,TextAnchor.MiddleCenter);
   GUI.color=tint;return true;
  }

  bool phaseTwoReview;float phaseTwoReviewAt=-1;int phaseTwoReviewStage;
  readonly float[] phaseTwoReviewTimes={.8f,2.05f,2.8f,3.7f,4.15f,4.8f,5.6f,7.5f,8.15f,8.45f,8.8f,10.15f,10.75f,12.25f};
  readonly string[] phaseTwoReviewNames={"01-darken","02-flash","03-rush","04-closeup","05-silhouette","06-transform","07-fire","08-party-fall","08b-party-descent","08c-party-impact","09-battlefield","10-descent","11-landing","12-resumed"};
  bool StartPhaseTwoReview(string[] args){
   if(Array.IndexOf(args,"--phase2-review")<0)return false;
   phaseTwoReview=true;Begin(NetworkRole.Offline);sim.IntroEnabled=true;sim.TickIndex=RaidSimulation.EncounterIntroTicks;
   sim.Boss.Hp=sim.Boss.MaxHp*.5f;sim.Boss.Position=new Vector2(2.8f,0);sim.Players[0].Position=new Vector2(-2,0);
   sim.Tick(RaidSimulation.FixedStep,Array.Empty<PlayerCommand>());LoadBakalPhaseTwo();return true;
  }
  void UpdatePhaseTwoReview(){
   if(phaseTwoReviewAt<0){phaseTwoReviewAt=Time.unscaledTime;encounterReviewAt=phaseTwoReviewAt;}
   float age=Mathf.Max(0,Time.unscaledTime-phaseTwoReviewAt-1);
   int desired=sim.PhaseTransitionStartTick+Mathf.FloorToInt(age*60);
   while(sim.TickIndex<desired)sim.Tick(RaidSimulation.FixedStep,Array.Empty<PlayerCommand>());
   foreach(var p in sim.Players){displayPositions[p.Id]=p.Position;displayHeights[p.Id]=p.JumpHeight;}
   encounterReviewTime=age;
   UpdateSounds();
   if(age<12.6f&&Time.unscaledTime>=encounterNextCapture&&!encounterCaptureBusy){encounterNextCapture=Time.unscaledTime+1f/24;StartCoroutine(CaptureEncounterFrame());}
   if(phaseTwoReviewStage<phaseTwoReviewTimes.Length&&age>=phaseTwoReviewTimes[phaseTwoReviewStage]){Capture(phaseTwoReviewNames[phaseTwoReviewStage]+".png");phaseTwoReviewStage++;}
   if(age>=12.7f){Directory.CreateDirectory(smokeDirectory);File.WriteAllText(Path.Combine(smokeDirectory,"frames.json"),JsonUtility.ToJson(encounterFrames,true));File.WriteAllText(Path.Combine(smokeDirectory,"phase2-review.json"),"{\"offlineVisualFixture\":true,\"simulationDriven\":true,\"errors\":"+errors+",\"scenes\":"+phaseTwoReviewStage+",\"phase\":"+sim.Boss.Phase+",\"resumed\":"+(!sim.PhaseTransitionActive).ToString().ToLowerInvariant()+"}");Application.Quit(errors==0?0:1);}
  }
 }
}
