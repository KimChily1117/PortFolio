using System;
using System.IO;
using UnityEngine;

namespace DnfRaidDemo {
 public sealed partial class RaidDemoApp {
  // User video 04-07-42: source 3.0s -> demo 0.0s, landing source14.8s ->11.8s.
  // Original sprite pixels; camera/ANI timing and leap frame mapping are reconstructed.
  // This presentation never writes HP, movement, hit boxes, inputs or server state.
  const float BakalEncounterSeconds=14f;
  bool encounterArtLoaded;Texture2D encounterThrone;RaidSimulation encounterSoundSimulation;
  float encounterSoundTime=-1;
  readonly float[] encounterCueTimes={4.9f,8.2f,10.8f,11.8f};
  readonly string[] encounterCueNames={"bakal_human_appear_on","bakal_human_appear_fire","bakal_dragon_breath_jump","bakal_human_warstomp_exp"};

  void LoadBakalEncounter(){
   if(encounterArtLoaded)return;encounterArtLoaded=true;
   var document=Resources.Load<TextAsset>("BakalEncounter/manifest");
   if(document!=null){var parsed=JsonUtility.FromJson<OriginalWorldDocument>(document.text);foreach(var effect in parsed.effects){fx[effect.id]=effect;GetSheet(effect.resource,effect.width,effect.height,effect.frames);}}
   encounterThrone=Resources.Load<Texture2D>("BakalEncounter/throne");
   foreach(var name in encounterCueNames)if(!clips.ContainsKey(name))clips[name]=Resources.Load<AudioClip>("Sound/"+name);
  }
  void EncounterFrame(string name,int frame,Vector2 foot,float scale,Color? tint=null){
   BkEffect effect;if(!fx.TryGetValue("enc_"+name,out effect))return;
   var sheet=GetSheet(effect.resource,effect.width,effect.height,effect.frames);int row;
   if(sheet.Texture!=null&&sheet.ByFrame.TryGetValue(Mathf.Clamp(frame,0,effect.frameCount-1),out row))DrawRow(sheet,row,foot,effect.anchor[0],effect.anchor[1],scale,tint??Color.white);
  }
  void EncounterEffect(string name,float time,Vector2 foot,float scale,float duration){DrawEffect("enc_"+name,time,foot,scale,1,Color.white,false,duration);}
  static float EncounterEase(float value){value=Mathf.Clamp01(value);return value*value*(3-2*value);}
  static float EncounterSnap(float value){value=1-Mathf.Clamp01(value);return 1-value*value*value*value;}
  void EncounterCamera(Vector2 focus,float zoom,Vector2 shake){GUI.matrix=GUI.matrix*Matrix4x4.TRS(new Vector3(640-focus.x*zoom+shake.x,350-focus.y*zoom+shake.y,0),Quaternion.identity,new Vector3(zoom,zoom,1));}

  bool DrawBakalEncounter(float elapsed){
   if(encounterReview)elapsed=encounterReviewTime;
   if(elapsed<0||elapsed>=BakalEncounterSeconds||sim==null)return false;
   LoadBakalEncounter();
   if(Event.current.type==EventType.Repaint){
    if(encounterSoundSimulation!=sim||elapsed<encounterSoundTime){encounterSoundSimulation=sim;encounterSoundTime=elapsed-.001f;}
    for(int i=0;i<encounterCueTimes.Length;i++)if(encounterSoundTime<encounterCueTimes[i]&&elapsed>=encounterCueTimes[i]&&elapsed-encounterCueTimes[i]<.25f)PlaySound(encounterCueNames[i],i==3?.85f:.64f);
    encounterSoundTime=elapsed;
   }
   var matrix=GUI.matrix;var originalColor=GUI.color;GUI.color=Color.white;
   float blast=Mathf.Max(0,1-(elapsed-8.2f)/.6f);if(elapsed<8.2f)blast=0;
   float landing=Mathf.Max(0,1-(elapsed-11.8f)/.8f);if(elapsed<11.8f)landing=0;
   Vector2 shake=new Vector2(Mathf.Sin(elapsed*93),Mathf.Sin(elapsed*127))*(blast*10+landing*16);
   bool battle=elapsed>=11.65f;
   if(!battle){
    Vector2 focus=new Vector2(640,350);float zoom=1;
    if(elapsed<4.35f){zoom=Mathf.Lerp(1,1.15f,EncounterSnap(elapsed/.18f));focus=Vector2.Lerp(focus,new Vector2(640,300),EncounterSnap(elapsed/.18f));}
    else if(elapsed<6.45f){float snap=EncounterSnap((elapsed-4.35f)/.10f);zoom=Mathf.Lerp(1.15f,4.45f,snap);if(elapsed>=4.45f)zoom=Mathf.Lerp(4.45f,4.25f,Mathf.Clamp01((elapsed-4.45f)/.12f));focus=Vector2.Lerp(new Vector2(640,300),new Vector2(699,117),snap);}
    else if(elapsed<7.35f){zoom=3.8f;focus=new Vector2(640,140);}
    else if(elapsed<7.8f){zoom=Mathf.Lerp(3.8f,1,EncounterSnap((elapsed-7.35f)/.12f));focus=Vector2.Lerp(new Vector2(640,140),new Vector2(640,350),EncounterSnap((elapsed-7.35f)/.12f));}
    EncounterCamera(focus,zoom,shake);
    Fill(new Rect(-300,-300,1880,1600),Color.black);
    if(encounterThrone!=null)GUI.DrawTexture(new Rect(0,0,1280,720),encounterThrone);
    DrawEffect("enc_torch1",elapsed,new Vector2(456,210),1,1,Color.white,true);
    DrawEffect("enc_torch2",elapsed,new Vector2(817,210),1,1,Color.white,true);
    Vector2 seatedFoot=new Vector2(640,338),wine=new Vector2(699,117);
    // The high-resolution seated frame0 already contains the wineglass. Frame1 has the closed fist.
    if(elapsed<7.8f){EncounterFrame("seated",elapsed<4.9f?0:1,seatedFoot,.22f);
     if(elapsed>=4.9f&&elapsed<6.45f){
      float a=elapsed-4.9f;Vector2 glassOrigin=wine+new Vector2(58,0);
      EncounterEffect("wine",a,wine,.7f,.65f);
      EncounterEffect("glass",a,glassOrigin,.45f,1.5f);
      EncounterEffect("glass_b",a,glassOrigin,.45f,1.55f);
      EncounterEffect("splash",a,wine,.55f,.95f);
      EncounterEffect("particle",a,wine,.65f,1.1f);
      // Source video 8.2–9.4s: an actual dragon face moves inside glassc's original pixels.
      // Its native offsets carry the shard from the wine hand across the foreground.
      EncounterEffect("glass_reflection",a-.18f,glassOrigin,.45f,1.35f);
     }
    }else if(elapsed<8.2f)EncounterFrame("stand",Mathf.FloorToInt((elapsed-7.8f)/.08f),seatedFoot,1.15f);
    else if(elapsed<10.2f){EncounterFrame("reveal",Mathf.FloorToInt((elapsed-8.2f)/.15f),seatedFoot,1.15f);
     OriginalWorld("world_large_flame",seatedFoot,1.3f,elapsed-8.2f,Color.white,false,.9f);
     OriginalWorld("world_impact_wave",seatedFoot,1.1f,elapsed-8.2f,Color.white,false,.65f);
    }else if(elapsed<10.8f)EncounterFrame("leap",Mathf.FloorToInt((elapsed-10.2f)/.2f),seatedFoot,1.15f);
    else if(elapsed<11.25f){float u=(elapsed-10.8f)/.45f;EncounterFrame("leap",3+Mathf.FloorToInt(u*4),seatedFoot+new Vector2(40*u,-720*u*u),1.15f);OriginalWorld("world_large_flame",seatedFoot,1.2f,elapsed-10.8f,Color.white,false,.55f);}
    if(elapsed<4.35f||elapsed>=7.8f)foreach(var player in sim.Players)if(!player.Departed&&player.RoomId==RaidSimulation.BossRoom)DrawPlayer(player);
   }else{
    // Match the source's sharp change from throne framing to the normal combat view.
    GUI.matrix=matrix*Matrix4x4.Translate(new Vector3(shake.x,shake.y,0));
    if(arena!=null)GUI.DrawTexture(new Rect(0,0,1280,720),arena);
    DrawRoomFloor();
    foreach(var player in sim.Players)if(!player.Departed&&player.RoomId==RaidSimulation.BossRoom)DrawPlayer(player);
    Vector2 at=ScreenPoint(sim.Boss.Position);
    if(elapsed<11.8f){float u=(elapsed-11.65f)/.15f;EncounterFrame("leap",7,at+new Vector2(60*(1-u),-500*(1-u)*(1-u)),1.508f);}
    else{
     float a=elapsed-11.8f;EncounterFrame("leap",a<.42f?9+Mathf.FloorToInt(a/.105f):13,at,1.508f);
     EncounterEffect("landing_a",a,at,4.8f,.85f);EncounterEffect("landing_b",a,at,4.2f,1.1f);
     OriginalWorld("world_impact_wave",at,2.8f,a,Color.white,false,.65f);
     OriginalWorld("world_large_flame",at,2.2f,a,Color.white,false,.7f);
    }
   }
   GUI.matrix=matrix;
   if(elapsed>=4.9f&&elapsed<5.02f)Fill(new Rect(0,0,1280,720),new Color(1,.7f,.6f,(1-(elapsed-4.9f)/.12f)*.45f));
   if(blast>0||landing>0)Fill(new Rect(0,0,1280,720),new Color(1,.65f,.3f,Mathf.Max(blast,landing)*.1f));
   float bars=elapsed<12.5f?36:36*(1-EncounterEase((elapsed-12.5f)/1.5f));Fill(new Rect(0,0,1280,bars),Color.black);Fill(new Rect(0,720-bars,1280,bars),Color.black);
   if(elapsed>12.2f){float alpha=Mathf.Clamp01((elapsed-12.2f)*3)*Mathf.Clamp01((14-elapsed)*2);Label(new Rect(0,150,1280,45),"폭룡왕 바칼",30,new Color(1,.76f,.36f,alpha),true,TextAnchor.MiddleCenter);}
   GUI.color=originalColor;return true;
  }

  [Serializable] sealed class EncounterCapturedFrame {public string filename;public float timestamp;public float encounterSeconds;public int simulationTick;}
  [Serializable] sealed class EncounterCaptureManifest {public int nominalFps=12;public string source="Unity end-of-frame JPG capture; offline visual fixture";public System.Collections.Generic.List<EncounterCapturedFrame> frames=new System.Collections.Generic.List<EncounterCapturedFrame>();}
  readonly EncounterCaptureManifest encounterFrames=new EncounterCaptureManifest();
  bool encounterCaptureBusy;float encounterNextCapture;
  bool encounterReview;float encounterReviewTime,encounterReviewAt;int encounterReviewStage=-1;
  System.Collections.IEnumerator CaptureEncounterFrame(){
   encounterCaptureBusy=true;float timestamp=Time.unscaledTime-encounterReviewAt,phaseTime=encounterReviewTime;int tick=sim.TickIndex;
   yield return new WaitForEndOfFrame();
   Texture2D frame=ScreenCapture.CaptureScreenshotAsTexture();
   try {string folder=Path.Combine(smokeDirectory,"frames");Directory.CreateDirectory(folder);string filename="frame"+encounterFrames.frames.Count.ToString("D5")+".jpg";File.WriteAllBytes(Path.Combine(folder,filename),frame.EncodeToJPG(88));encounterFrames.frames.Add(new EncounterCapturedFrame{filename=filename,timestamp=timestamp,encounterSeconds=phaseTime,simulationTick=tick});}
   finally {Destroy(frame);encounterCaptureBusy=false;}
  }
  readonly float[] encounterReviewTimes={.8f,4.65f,5.15f,5.6f,6.8f,7.98f,8.6f,10.5f,10.95f,11.74f,11.95f,13.3f};
  readonly string[] encounterReviewNames={"01-seated","02-wine-close","03-wine-break","03b-dragon-reflection","04-face","05-stand","06-reveal","07-prepare-jump","08-jump","09-descent","10-landing","11-combat"};
  bool StartEncounterReview(string[] args){
   if(Array.IndexOf(args,"--encounter-review")<0)return false;
   encounterReview=true;Begin(NetworkRole.Offline);LoadBakalEncounter();encounterReviewAt=-1;return true;
  }
  void UpdateEncounterReview(){
   if(encounterReviewAt<0){encounterReviewAt=Time.unscaledTime;encounterReviewStage=0;}
   encounterReviewTime=Mathf.Max(0,Time.unscaledTime-encounterReviewAt-1);
   sim.TickIndex=Mathf.RoundToInt(encounterReviewTime*60);lastHudTick=sim.TickIndex;sim.Boss.Phase=1;sim.Boss.Position=new Vector2(2.8f,0);sim.Players[0].Position=new Vector2(-2,0);sim.Players[0].RoomId=RaidSimulation.BossRoom;
   UpdateSounds();
   if(encounterReviewTime<14.5f&&Time.unscaledTime>=encounterNextCapture&&!encounterCaptureBusy){encounterNextCapture=Time.unscaledTime+1f/24;StartCoroutine(CaptureEncounterFrame());}
   if(encounterReviewStage<encounterReviewTimes.Length&&encounterReviewTime>=encounterReviewTimes[encounterReviewStage]){Capture(encounterReviewNames[encounterReviewStage]+".png");encounterReviewStage++;}
   if(encounterReviewTime>=14.6f){Directory.CreateDirectory(smokeDirectory);File.WriteAllText(Path.Combine(smokeDirectory,"frames.json"),JsonUtility.ToJson(encounterFrames,true));File.WriteAllText(Path.Combine(smokeDirectory,"encounter-review.json"),"{\"offlineVisualFixture\":true,\"continuousPlayback\":true,\"errors\":"+errors+",\"scenes\":"+encounterReviewStage+",\"duration\":14,\"landingTime\":11.8}");Application.Quit(errors==0?0:1);}
  }
  void FinishEncounterReview(){Application.Quit(errors==0?0:1);}
 }
}
