using System;
using System.IO;
using UnityEngine;

namespace DnfRaidDemo {
 public sealed partial class RaidDemoApp {
  bool stompArtLoaded;
  void LoadBakalStomp(){
   if(stompArtLoaded)return;stompArtLoaded=true;
   var text=Resources.Load<TextAsset>("BakalStomp/manifest");
   if(text!=null)foreach(var effect in JsonUtility.FromJson<OriginalWorldDocument>(text.text).effects){fx[effect.id]=effect;GetSheet(effect.resource,effect.width,effect.height,effect.frames);}
  }
  AttackZone VisibleStomp(){
   AttackZone selected=null;
   foreach(var zone in sim.Zones)if(RaidSimulation.IsStompZone(zone)&&zone.Age>=0&&zone.Age<zone.Telegraph+zone.ActiveTime+zone.Recovery&&(selected==null||zone.Age<selected.Age))selected=zone;
   return selected;
  }
  bool DrawStompBoss(Vector2 foot){
   var zone=VisibleStomp();if(zone==null)return false;LoadBakalStomp();
   string key=sim.Boss.Phase==2?"stomp_p2":"stomp_p1";BkEffect effect;if(!fx.TryGetValue(key,out effect))return false;
   int plant=sim.Boss.Phase==2?7:8;float progress=Mathf.Clamp01(zone.Age/zone.Telegraph);
   int frame=zone.Age<zone.Telegraph?Mathf.Min(plant-1,Mathf.FloorToInt(progress/.85f*plant)):Mathf.Min(effect.frameCount-1,plant+Mathf.FloorToInt((zone.Age-zone.Telegraph)/.12f));
   var sheet=GetSheet(effect.resource,effect.width,effect.height,effect.frames);int row;
   var matrix=GUI.matrix;float scale=sim.Boss.Phase==2?1.32f:1.508f;
   if(bossFacing<0)GUIUtility.ScaleAroundPivot(new Vector2(-1,1),foot);
   if(sheet.ByFrame.TryGetValue(frame,out row))DrawRow(sheet,row,foot,effect.anchor[0],effect.anchor[1],scale,Color.white);
   GUI.matrix=matrix;return true;
  }
  void DrawStompWarning(AttackZone zone){
   float scale=sim.Boss.Phase==2?1.32f:1.508f;
   Vector2 contact=sim.Boss.Phase==2?new Vector2(125,-11):new Vector2(110,-25);
   Vector2 foot=ScreenPoint(sim.Boss.Position)+new Vector2(contact.x*scale*bossFacing,contact.y*scale);float progress=Mathf.Clamp01(zone.Age/zone.Telegraph),age=zone.Age-zone.Telegraph;
   const float width=350,height=142;
   if(age<0){
    if(meteorMarkOutline==null)meteorMarkOutline=Radial(256,.975f,false);
    TintTexture(meteorMarkOutline,new Rect(foot.x-width*.5f,foot.y-height*.5f,width,height),new Color(1,.12f,.05f,.95f));
    TintTexture(disc,new Rect(foot.x-width*progress*.5f,foot.y-height*progress*.5f,width*progress,height*progress),new Color(1,.09f,.025f,.38f));
    if(progress>=.85f)TintTexture(meteorMarkOutline,new Rect(foot.x-width*.5f,foot.y-height*.5f,width,height),new Color(1,.95f,.65f,.9f));
    DrawJumpWarningGuide(foot+new Vector2(0,-8),progress);
    Label(new Rect(foot.x-145,foot.y+76,290,24),progress>=.85f?"지금 점프!":"전장 전체 충격파 · 점프 준비",17,progress>=.85f?Color.white:gold,true,TextAnchor.MiddleCenter);
   }else{
    float alpha=Mathf.Clamp01(1-age/.44f),spread=Mathf.Clamp01(age/.14f);
    TintTexture(ring,new Rect(foot.x-700*spread,foot.y-285*spread,1400*spread,570*spread),new Color(1,.74f,.44f,alpha));
    // The wave expands visually only. Collision activates across the whole room at plant time.
    DrawEffect("stomp_wave",age,foot,1.8f,1,new Color(1,1,1,alpha),false,.32f);
    DrawEffect("stomp_glow",age,foot,1.2f,1,new Color(1,1,1,alpha),false,.24f);
   }
   if(showBoxes)DrawHurtbox(ScreenPoint(zone.Center),zone.HalfSize,gold);
  }

  bool stompReview;float stompReviewAt=-1;int stompReviewStage;bool stompReviewSecond;readonly System.Collections.Generic.HashSet<int> stompJumped=new System.Collections.Generic.HashSet<int>();
  readonly float[] stompReviewTimes={.45f,.79f,.94f,1.15f,4.7f,5.04f,5.2f,5.4f};
  readonly string[] stompReviewNames={"01-fill","02-jump85","03-plant","04-wave","05-dragon-fill","06-dragon-jump85","07-dragon-plant","08-dragon-wave"};
  bool StartStompReview(string[] args){
   if(Array.IndexOf(args,"--stomp-review")<0)return false;
   stompReview=true;Begin(NetworkRole.Offline);sim=new RaidSimulation(2);sim.Start();sim.Boss.Position=new Vector2(2,0);sim.Players[0].Position=new Vector2(-2,0);sim.Players[1].Position=new Vector2(-4,0);sim.ForcePattern(5);LoadBakalStomp();return true;
  }
  void UpdateStompReview(){
   if(stompReviewAt<0){stompReviewAt=Time.unscaledTime;encounterReviewAt=stompReviewAt;}
   float age=Mathf.Max(0,Time.unscaledTime-stompReviewAt-1);
   int desired=Mathf.FloorToInt(age*60);
   while(sim.TickIndex<desired){
    if(!stompReviewSecond&&sim.TickIndex>=255){stompReviewSecond=true;sim.Boss.Phase=2;sim.ForcePattern(5);}
    var cmd=new PlayerCommand{PlayerId=0};
    foreach(var zone in sim.Zones)if(RaidSimulation.IsStompZone(zone)&&zone.Age>=zone.Telegraph*.85f&&zone.Age<zone.Telegraph&&stompJumped.Add(zone.Id))cmd.Jump=true;
    sim.Tick(RaidSimulation.FixedStep,new[]{cmd});
   }
   foreach(var p in sim.Players){displayPositions[p.Id]=p.Position;displayHeights[p.Id]=p.JumpHeight;}
   encounterReviewTime=age;PlayEvents();UpdateSounds();UpdateCombatHud();
   if(age<8.3f&&Time.unscaledTime>=encounterNextCapture&&!encounterCaptureBusy){encounterNextCapture=Time.unscaledTime+1f/12;StartCoroutine(CaptureEncounterFrame());}
   if(stompReviewStage<stompReviewTimes.Length&&age>=stompReviewTimes[stompReviewStage]){Capture(stompReviewNames[stompReviewStage]+".png");stompReviewStage++;}
   if(age>=8.4f){bool safe=sim.Players[0].Hp==sim.Players[0].MaxHp&&sim.Players[1].Hp<sim.Players[1].MaxHp;File.WriteAllText(Path.Combine(smokeDirectory,"frames.json"),JsonUtility.ToJson(encounterFrames,true));File.WriteAllText(Path.Combine(smokeDirectory,"stomp-review.json"),"{\"offlineInputFixture\":true,\"errors\":"+errors+",\"scenes\":"+stompReviewStage+",\"jump85Safe\":"+safe.ToString().ToLowerInvariant()+",\"jumpCount\":"+stompJumped.Count+",\"jumpingHp\":"+sim.Players[0].Hp+",\"standingHp\":"+sim.Players[1].Hp+"}");Application.Quit(errors==0&&safe?0:1);}
  }
 }
}
